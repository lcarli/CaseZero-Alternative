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

        var previousAttempts = await _context.CaseSubmissions
            .CountAsync(s => s.CaseId == caseId && s.SubmittedByUserId == userId, ct);

        if (previousAttempts >= sol.MaxAttempts)
            throw new MaxAttemptsExceededException(sol.MaxAttempts);

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
        double analysisScore;
        if (sol.RequiredAnalysisIds.Count == 0)
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

        var attemptNumber = previousAttempts + 1;
        var attemptsRemaining = Math.Max(0, sol.MaxAttempts - attemptNumber);

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
            AttemptNumber = attemptNumber
        };
        _context.CaseSubmissions.Add(submission);
        await _context.SaveChangesAsync(ct);

        var showExplanation = correct || attemptsRemaining == 0;

        var feedbackCode = correct
            ? "correct"
            : attemptsRemaining == 0
                ? "incorrect_no_attempts"
                : "incorrect_attempts_remaining";

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
            feedbackCode,
            showExplanation ? sol.Explanation : null);
    }

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
