namespace CaseZero.Scoring;

public sealed record SubmissionWeights(double Culprit = .4, double Evidence = .2, double Analysis = .2, double Questions = .2);
public sealed record ScoredQuestion(string Id, string? CorrectOptionId, double Weight = 1);
public sealed record SubmittedAnswer(string QuestionId, string OptionId);
public sealed record SubmissionScore(double RawTotal, double Culprit, double Evidence, double Analysis, double Questions, bool Correct)
{
    public double RoundedTotal => Math.Round(RawTotal, 4);
}

public static class SubmissionScoring
{
    public static SubmissionScore Evaluate(
        string expectedSuspectId,
        string submittedSuspectId,
        IReadOnlyList<string> requiredEvidenceIds,
        IEnumerable<string> authorizedEvidenceIds,
        IReadOnlyList<string> requiredAnalysisIds,
        IEnumerable<string> completedAnalysisIds,
        IReadOnlyList<ScoredQuestion> questions,
        IEnumerable<SubmittedAnswer> submittedAnswers,
        SubmissionWeights weights,
        double minimumScore,
        bool rookieAnalysisCredit)
    {
        var culprit = expectedSuspectId == submittedSuspectId ? weights.Culprit : 0;
        var evidence = Credit(requiredEvidenceIds, authorizedEvidenceIds, weights.Evidence);
        var analysis = rookieAnalysisCredit ? weights.Analysis
            : Credit(requiredAnalysisIds, completedAnalysisIds, weights.Analysis);
        var answers = submittedAnswers.ToList();
        var totalQuestionWeight = questions.Sum(question => question.Weight);
        var gained = questions.Where(question =>
            answers.FirstOrDefault(answer => answer.QuestionId == question.Id) is { } answer
                && answer.OptionId == question.CorrectOptionId)
            .Sum(question => question.Weight);
        var questionCredit = questions.Count > 0 && totalQuestionWeight > 0
            ? weights.Questions * (gained / totalQuestionWeight) : 0;
        var total = culprit + evidence + analysis + questionCredit;
        return new(total, culprit, evidence, analysis, questionCredit, total >= minimumScore);
    }

    private static double Credit(IReadOnlyList<string> required, IEnumerable<string> submitted, double weight) =>
        required.Count == 0 ? weight
            : weight * ((double)submitted.Intersect(required, StringComparer.Ordinal).Count() / required.Count);
}
