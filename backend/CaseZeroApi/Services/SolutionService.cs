using System.Text.Json;
using CaseZero.Scoring;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

public class SolutionService : ISolutionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICaseV2StorageService _storage;
    private readonly IPromotionService _promotion;
    private readonly ILogger<SolutionService> _logger;

    public SolutionService(
        ApplicationDbContext context,
        ICaseV2StorageService storage,
        IPromotionService promotion,
        ILogger<SolutionService> logger)
    {
        _context = context;
        _storage = storage;
        _promotion = promotion;
        _logger = logger;
    }

    public async Task<SubmitCaseResult> SubmitAsync(string caseId, string userId, SubmitCaseRequest request, CancellationToken ct = default)
    {
        var caseData = await _storage.GetRawAsync(caseId, ct);
        if (caseData?.Solution is null)
            throw new InvalidOperationException($"Case {caseId} has no solution defined");

        var sol = caseData.Solution;
        var rules = sol.PartialCreditRules ?? new CaseV2PartialCreditRules();

        // A previously-shipped throw on previousAttempts >= MaxAttempts was removed:
        // the case is no longer "locked" once N graded attempts have been used. The
        // player can keep submitting to try and reveal the culprit, but those extra
        // submissions are saved with Graded = false and do not affect promotion.
        var gradedAttemptsUsed = await _context.CaseSubmissions
            .CountAsync(s => s.CaseId == caseId && s.SubmittedByUserId == userId && s.Graded, ct);
        var totalPreviousAttempts = await _context.CaseSubmissions
            .CountAsync(s => s.CaseId == caseId && s.SubmittedByUserId == userId, ct);

        // This submission is graded only if the player hasn't already used all
        // graded attempts before this one.
        var graded = gradedAttemptsUsed < sol.MaxAttempts;

        var isRookieCase = IsRookieDifficulty(caseData.Metadata.Difficulty)
                        || IsRookieDifficulty(caseData.Metadata.RequiredRank);

        var evidenceIds = request.EvidenceIds;
        var analysisIds = request.AnalysisIds;
        // Cases declaring progression cannot earn credit for undiscovered or merely claimed results.
        if (caseData.Metadata.UnlockMode is "gated" or "all_initial")
        {
            var revealed = await _context.CaseSessionVisibleAssets
                .Where(v => v.UserId == userId && v.CaseId == caseId).Select(v => v.AssetId).ToListAsync(ct);
            var visible = caseData.Assets.Where(a => a.Visibility == "initial" ||
                caseData.Metadata.IsAllInitial || revealed.Contains(a.Id)).Select(a => a.Id).ToHashSet();
            var completed = await _context.ForensicRequests
                .Where(r => r.UserId == userId && r.CaseId == caseId && r.Status == "completed")
                .Select(r => new { r.InputAssetId, r.AnalysisType }).ToListAsync(ct);
            var performed = completed.Select(r => $"{r.InputAssetId}:{r.AnalysisType}").ToHashSet();
            evidenceIds = request.EvidenceIds.Where(visible.Contains).ToArray();
            analysisIds = request.AnalysisIds.Where(performed.Contains).ToArray();
        }

        var score = SubmissionScoring.Evaluate(
            sol.CulpritId,
            request.SuspectId,
            sol.RequiredEvidenceIds,
            evidenceIds,
            sol.RequiredAnalysisIds,
            analysisIds,
            sol.Questions.Select(q => new ScoredQuestion(q.Id, q.CorrectOptionId, q.Weight ?? 1.0)).ToArray(),
            request.Answers.Select(a => new SubmittedAnswer(a.QuestionId, a.OptionId)),
            new SubmissionWeights(rules.CulpritWeight, rules.EvidenceWeight, rules.AnalysisWeight, rules.QuestionsWeight),
            sol.MinimumScore,
            rookieAnalysisCredit: isRookieCase);

        var culpritCorrect = string.Equals(request.SuspectId, sol.CulpritId, StringComparison.Ordinal);
        var culpritScore = score.Culprit;
        var evidenceScore = score.Evidence;
        var analysisScore = score.Analysis;
        var questionsScore = score.Questions;
        var total = score.RawTotal;
        var correct = score.Correct;

        var attemptNumber = totalPreviousAttempts + 1;
        // attemptsRemaining counts only graded attempts left, post-submission.
        var gradedAttemptsAfter = gradedAttemptsUsed + (graded ? 1 : 0);
        var attemptsRemaining = Math.Max(0, sol.MaxAttempts - gradedAttemptsAfter);

        // Ensure Case row exists for FK constraint
        await EnsureCaseRowAsync(caseId, caseData, ct);

        var submission = new CaseSubmission
        {
            CaseId = caseId,
            SubmittedByUserId = userId,
            SuspectName = request.SuspectId,
            SuspectId = request.SuspectId,
            KeyEvidenceDescription = string.Join(",", request.EvidenceIds),
            SupportingEvidenceIds = JsonSerializer.Serialize(request.EvidenceIds),
            Reasoning = string.Join(",", request.AnalysisIds),
            SubmittedAt = DateTime.UtcNow,
            Status = correct ? SubmissionStatus.Approved : SubmissionStatus.Rejected,
            IsCorrectSuspect = culpritCorrect,
            IsValidEvidence = evidenceScore > 0,
            Score = total * 100.0,
            CulpritScore = culpritScore,
            EvidenceScore = evidenceScore,
            AnalysisScore = analysisScore,
            QuestionsScore = questionsScore,
            EvaluatedAt = DateTime.UtcNow,
            RequestPayloadJson = JsonSerializer.Serialize(request),
            AttemptNumber = attemptNumber,
            Graded = graded
        };
        _context.CaseSubmissions.Add(submission);
        await _context.SaveChangesAsync(ct);

        // Career progression: graded submissions update the player's stats and may
        // trigger a promotion. The sync is idempotent (rank derives purely from the
        // resolved-case count), so re-submissions never double-count.
        PromotionInfo? promotionInfo = null;
        if (graded)
        {
            try
            {
                var outcome = await _promotion.SyncAsync(userId, ct);
                if (outcome.Promoted)
                    promotionInfo = new PromotionInfo(
                        true, outcome.PreviousRank.ToString(), outcome.NewRank.ToString());
            }
            catch (Exception ex)
            {
                // A promotion failure must not fail the submission itself.
                _logger.LogError(ex, "Promotion sync failed for user {UserId} on case {CaseId}", userId, caseId);
            }
        }

        // The player chose "unlimited_ungraded": the explanation is only revealed
        // when the case is actually solved. Running out of graded attempts no
        // longer auto-spoils the answer — the player can keep trying ungraded.
        var showExplanation = correct;

        var feedbackCode = (correct, graded) switch
        {
            (true, true)   => "correct",
            (true, false)  => "correct_ungraded",
            (false, true)  => attemptsRemaining == 0 ? "incorrect_last_graded" : "incorrect_attempts_remaining",
            (false, false) => "incorrect_ungraded",
        };

        return new SubmitCaseResult(
            correct,
            score.RoundedTotal,
            new ScoreBreakdown(
                Math.Round(culpritScore, 4),
                Math.Round(evidenceScore, 4),
                Math.Round(analysisScore, 4),
                Math.Round(questionsScore, 4)),
            new ScoreWeights(
                Math.Round(rules.CulpritWeight, 4),
                Math.Round(rules.EvidenceWeight, 4),
                Math.Round(rules.AnalysisWeight, 4),
                Math.Round(rules.QuestionsWeight, 4)),
            attemptsRemaining,
            graded,
            feedbackCode,
            showExplanation ? sol.Explanation : null,
            promotionInfo);
    }

    private static bool IsRookieDifficulty(string? value)
        => !string.IsNullOrEmpty(value)
            && string.Equals(value, "Rookie", StringComparison.OrdinalIgnoreCase);

    private async Task EnsureCaseRowAsync(string caseId, CaseV2 caseData, CancellationToken ct)
    {
        var exists = await _context.Cases.AnyAsync(c => c.Id == caseId, ct);
        if (exists) return;
        _context.Cases.Add(new Case
        {
            Id = caseId,
            Title = caseData.Metadata.Title,
            Description = caseData.Metadata.Description,
            CreatedAt = DateTime.UtcNow,
            Status = CaseStatus.Open
        });
    }
}
