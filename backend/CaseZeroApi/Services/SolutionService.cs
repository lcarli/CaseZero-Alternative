using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

public class SolutionService : ISolutionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICaseV2StorageService _storage;
    private readonly ILogger<SolutionService> _logger;

    public SolutionService(
        ApplicationDbContext context,
        ICaseV2StorageService storage,
        ILogger<SolutionService> logger)
    {
        _context = context;
        _storage = storage;
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

        var isRookieCase = IsRookieDifficulty(caseData.Metadata?.Difficulty)
                        || IsRookieDifficulty(caseData.Metadata?.RequiredRank);

        // Culprit
        var culpritCorrect = string.Equals(request.SuspectId, sol.CulpritId, StringComparison.Ordinal);
        var culpritScore = culpritCorrect ? rules.CulpritWeight : 0.0;

        // Evidence
        double evidenceScore;
        if (sol.RequiredEvidenceIds.Count == 0)
        {
            evidenceScore = rules.EvidenceWeight;
        }
        else
        {
            var hits = request.EvidenceIds.Intersect(sol.RequiredEvidenceIds, StringComparer.Ordinal).Count();
            evidenceScore = rules.EvidenceWeight * ((double)hits / sol.RequiredEvidenceIds.Count);
        }

        // Analysis
        // For Rookie cases, the Forensics Lab is intentionally unavailable —
        // documents and analyses are pre-revealed, so the player can't (and
        // shouldn't have to) submit analysisIds. Always award full credit for
        // that category, regardless of whether the case author leaked
        // requiredAnalysisIds into a Rookie case by mistake.
        double analysisScore;
        if (isRookieCase || sol.RequiredAnalysisIds.Count == 0)
        {
            analysisScore = rules.AnalysisWeight;
        }
        else
        {
            var hits = request.AnalysisIds.Intersect(sol.RequiredAnalysisIds, StringComparer.Ordinal).Count();
            analysisScore = rules.AnalysisWeight * ((double)hits / sol.RequiredAnalysisIds.Count);
        }

        // Questions
        double questionsScore = 0.0;
        if (sol.Questions.Count > 0)
        {
            double totalWeight = 0, gained = 0;
            foreach (var q in sol.Questions)
            {
                var w = q.Weight ?? 1.0;
                totalWeight += w;
                var answer = request.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                if (answer is not null && string.Equals(answer.OptionId, q.CorrectOptionId, StringComparison.Ordinal))
                    gained += w;
            }
            if (totalWeight > 0)
                questionsScore = rules.QuestionsWeight * (gained / totalWeight);
        }

        var total = culpritScore + evidenceScore + analysisScore + questionsScore;
        var correct = total >= sol.MinimumScore;

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
            EvaluatedAt = DateTime.UtcNow,
            RequestPayloadJson = JsonSerializer.Serialize(request),
            AttemptNumber = attemptNumber,
            Graded = graded
        };
        _context.CaseSubmissions.Add(submission);
        await _context.SaveChangesAsync(ct);

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
            Math.Round(total, 4),
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
            showExplanation ? sol.Explanation : null);
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
