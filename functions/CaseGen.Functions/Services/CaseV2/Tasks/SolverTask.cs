using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>
/// Stage 15 — deterministic graph witness. Replays a valid sequence of player
/// actions from the typed graph and proves that its culprit path, required
/// evidence, analyses, and questions are reachable through the public workflow.
/// </summary>
public class SolverTask
{
    public SolverTask(ILLMProvider llm, ILogger logger)
    {
        _ = llm;
        _ = logger;
    }

    public class Attempt
    {
        [JsonPropertyName("suspectId")] public string SuspectId { get; set; } = string.Empty;
        [JsonPropertyName("evidenceIds")] public List<string> EvidenceIds { get; set; } = new();
        [JsonPropertyName("analysisIds")] public List<string> AnalysisIds { get; set; } = new();
        [JsonPropertyName("observationIds")] public List<string> ObservationIds { get; set; } = new();
        [JsonPropertyName("answers")] public List<SolverAnswer> Answers { get; set; } = new();
        [JsonPropertyName("rationale")] public string? Rationale { get; set; }
    }

    public class SolverAnswer
    {
        [JsonPropertyName("questionId")] public string QuestionId { get; set; } = string.Empty;
        [JsonPropertyName("optionId")] public string OptionId { get; set; } = string.Empty;
    }

    public class SolverResult
    {
        public bool Correct { get; set; }
        public double Score { get; set; }
        public double CulpritScore { get; set; }
        public double EvidenceScore { get; set; }
        public double AnalysisScore { get; set; }
        public double QuestionsScore { get; set; }
        public Attempt Attempt { get; set; } = new();
        public bool LogicalProofPassed { get; set; }
        public bool NarrativeSimulationPassed { get; set; }
        public SolverDiagnosticTrace Trace { get; set; } = new();
        public string? Notes { get; set; }
    }

    public Task<SolverResult> RunAsync(CaseDraft draft, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var logical = LogicalSolvabilityGate.Validate(draft);
        var simulator = new SequentialPlayerSimulator(draft);
        if (logical.IsValid)
        {
            var maximumActions = Math.Max(12, (draft.CaseGraph.Sources.Count + draft.CaseGraph.Actions.Count) * 2 + 4);
            for (var step = 0; step < maximumActions; step++)
            {
                ct.ThrowIfCancellationRequested();
                var next = simulator.AvailableActions()
                    .Where(action => action.Kind is PlayerActionKind.InspectAsset
                        or PlayerActionKind.CompareRecords
                        or PlayerActionKind.RequestAnalysis
                        or PlayerActionKind.ReviewResult)
                    .OrderBy(action => action.Kind == PlayerActionKind.InspectAsset ? 0 : 1)
                    .ThenBy(action => action.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (next is null)
                    break;

                var trace = simulator.Apply(new PlayerActionRequest
                {
                    Kind = next.Kind,
                    ActionId = next.Id,
                    TargetId = next.TargetId,
                    AnalysisId = next.AnalysisId
                });
                if (!trace.Accepted)
                    break;
            }

            var closure = DerivationClosureEngine.Calculate(
                draft.CaseGraph,
                simulator.ReachableObservationIds,
                ProofKnowledgeScope.PlayerReachable);
            var proofPath = CulpritUniquenessGate.PathsForSuspect(
                    CulpritUniquenessGate.FindCulpritConclusions(draft.CaseGraph),
                    draft.CulpritId,
                    closure)
                .OrderBy(path => path.Depth)
                .ThenBy(path => path.Signature, StringComparer.Ordinal)
                .FirstOrDefault();
            if (proofPath is not null)
            {
                var requiredSourceIds = draft.RequiredEvidenceIds
                    .Concat(draft.CaseGraph.RequiredSourceIds)
                    .Concat(draft.CaseGraph.Sources.Where(source => source.Required).Select(source => source.Id))
                    .ToHashSet(StringComparer.Ordinal);
                var witnessObservationIds = proofPath.ObservationIds
                    .Concat(draft.CaseGraph.Observations
                        .Where(observation => requiredSourceIds.Contains(observation.SourceAssetId))
                        .Select(observation => observation.Id))
                    .Where(simulator.ReachableObservationIds.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList();
                simulator.Apply(new PlayerActionRequest
                {
                    Kind = PlayerActionKind.AccuseSuspect,
                    ActionId = $"accuse:{draft.CulpritId}",
                    TargetId = draft.CulpritId,
                    SuspectId = draft.CulpritId,
                    ObservationIds = witnessObservationIds,
                    Rationale = "Deterministic CaseGraph proof witness"
                });
            }

            foreach (var question in draft.Questions.OrderBy(question => question.Id, StringComparer.Ordinal))
            {
                simulator.Apply(new PlayerActionRequest
                {
                    Kind = PlayerActionKind.AnswerQuestion,
                    ActionId = $"answer:{question.Id}",
                    TargetId = question.Id,
                    QuestionId = question.Id,
                    OptionId = question.CorrectOptionId
                });
            }
        }

        var citedObservationIds = simulator.Trace
            .Where(entry => entry.Accepted && entry.Kind == PlayerActionKind.AccuseSuspect)
            .SelectMany(entry => entry.CitedObservationIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var observations = draft.CaseGraph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        var analysisIds = simulator.Trace
            .Where(entry => entry.Accepted && entry.Kind == PlayerActionKind.RequestAnalysis)
            .Select(entry => draft.CaseGraph.Actions.FirstOrDefault(action => action.Id == entry.ActionId)?.AnalysisId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var attempt = new Attempt
        {
            SuspectId = simulator.AccusedSuspectId ?? string.Empty,
            ObservationIds = citedObservationIds,
            EvidenceIds = citedObservationIds
                .Where(observations.ContainsKey)
                .Select(id => observations[id].SourceAssetId)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            AnalysisIds = analysisIds,
            Answers = simulator.Answers.Select(pair => new SolverAnswer
            {
                QuestionId = pair.Key,
                OptionId = pair.Value
            }).ToList(),
            Rationale = simulator.AccusedSuspectId is null ? null : "Deterministic CaseGraph proof witness"
        };
        var narrativePassed = NarrativeSucceeded(draft, simulator, attempt);
        draft.SolverTrace = simulator.Snapshot(logical.IsValid, narrativePassed);
        return Task.FromResult(Grade(draft, attempt, logical, narrativePassed, draft.SolverTrace));
    }

    /// <summary>Apply the exact same scoring formula used by the live runtime
    /// (mirrors <c>backend/CaseZeroApi/Services/SolutionService.cs</c>) so the solver
    /// score is an honest preview of what a player would get.</summary>
    private SolverResult Grade(
        CaseDraft d,
        Attempt attempt,
        LogicalSolvabilityResult logical,
        bool narrativePassed,
        SolverDiagnosticTrace trace)
    {
        var pcr = new SolutionPartialCredit(); // defaults 0.4/0.2/0.2/0.2

        var culpritCorrect = attempt.SuspectId == d.CulpritId;
        var culpritScore = culpritCorrect ? pcr.CulpritWeight : 0;

        double evidenceScore;
        if (d.RequiredEvidenceIds.Count == 0) evidenceScore = pcr.EvidenceWeight;
        else
        {
            var hit = attempt.EvidenceIds.Intersect(d.RequiredEvidenceIds, StringComparer.Ordinal).Count();
            evidenceScore = pcr.EvidenceWeight * ((double)hit / d.RequiredEvidenceIds.Count);
        }

        double analysisScore;
        if (d.RequiredAnalysisIds.Count == 0) analysisScore = pcr.AnalysisWeight;
        else
        {
            var hit = attempt.AnalysisIds.Intersect(d.RequiredAnalysisIds, StringComparer.Ordinal).Count();
            analysisScore = pcr.AnalysisWeight * ((double)hit / d.RequiredAnalysisIds.Count);
        }

        double questionsScore = 0;
        if (d.Questions.Count > 0)
        {
            double tw = 0, gained = 0;
            foreach (var q in d.Questions)
            {
                var w = q.Weight; tw += w;
                var ans = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                if (ans is not null && ans.OptionId == q.CorrectOptionId) gained += w;
            }
            if (tw > 0) questionsScore = pcr.QuestionsWeight * (gained / tw);
        }

        var total = culpritScore + evidenceScore + analysisScore + questionsScore;
        return new SolverResult
        {
            Attempt = attempt,
            Score = Math.Round(total, 4),
            CulpritScore = Math.Round(culpritScore, 4),
            EvidenceScore = Math.Round(evidenceScore, 4),
            AnalysisScore = Math.Round(analysisScore, 4),
            QuestionsScore = Math.Round(questionsScore, 4),
            Correct = SolvabilityDecision.BothPass(logical.IsValid, narrativePassed, total),
            LogicalProofPassed = logical.IsValid,
            NarrativeSimulationPassed = narrativePassed,
            Trace = trace,
            Notes = logical.IsValid ? null : string.Join(" · ", logical.Errors.Take(8))
        };
    }

    private static bool NarrativeSucceeded(
        CaseDraft draft,
        SequentialPlayerSimulator simulator,
        Attempt attempt)
    {
        if (attempt.SuspectId != draft.CulpritId || attempt.ObservationIds.Count == 0)
            return false;
        var closure = DerivationClosureEngine.Calculate(
            draft.CaseGraph,
            simulator.ReachableObservationIds,
            ProofKnowledgeScope.PlayerReachable);
        var paths = CulpritUniquenessGate.PathsForSuspect(
            CulpritUniquenessGate.FindCulpritConclusions(draft.CaseGraph),
            draft.CulpritId,
            closure);
        var cited = attempt.ObservationIds.ToHashSet(StringComparer.Ordinal);
        return paths.Any(path => path.ObservationIds.Count > 0 && path.ObservationIds.All(cited.Contains));
    }
}
