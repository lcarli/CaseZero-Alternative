namespace CaseZeroApi.Services;

public interface ISolutionService
{
    Task<SubmitCaseResult> SubmitAsync(string caseId, string userId, SubmitCaseRequest request, CancellationToken ct = default);
}

public record SubmitCaseRequest(
    string SuspectId,
    string[] EvidenceIds,
    string[] AnalysisIds,
    AnswerDto[] Answers);

public record AnswerDto(string QuestionId, string OptionId);

public record SubmitCaseResult(
    bool Correct,
    double Score,
    ScoreBreakdown Breakdown,
    int AttemptsRemaining,
    string FeedbackText,
    string? ExplanationMarkdown);

public record ScoreBreakdown(
    double CulpritScore,
    double EvidenceScore,
    double AnalysisScore,
    double QuestionsScore);

public class MaxAttemptsExceededException : Exception
{
    public int MaxAttempts { get; }
    public MaxAttemptsExceededException(int max) : base($"Max attempts ({max}) exceeded") { MaxAttempts = max; }
}
