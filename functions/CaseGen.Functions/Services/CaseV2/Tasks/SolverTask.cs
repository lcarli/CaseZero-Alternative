using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using CaseZero.Scoring;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>
/// Player-visible model investigation plus a separately evaluated private proof
/// and shared runtime grade. The graph witness remains diagnostic, not a blind solver.
/// </summary>
public class SolverTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public SolverTask(ILLMProvider llm, ILogger logger)
    {
        _llm = llm;
        _logger = logger;
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
        public string Mode { get; set; } = "graph-witness";
        public bool RenderedTextVerified { get; set; }
        public string? ErrorCode { get; set; }
        public List<BlindEvidenceCitation> Citations { get; set; } = [];
    }

    public async Task<SolverResult> RunAsync(CaseDraft draft, CancellationToken ct,
        IReadOnlyDictionary<string, string>? readableTexts = null)
    {
        var logical = LogicalSolvabilityGate.Validate(draft);
        if (readableTexts is null)
            return new()
            {
                Mode = "player-visible", LogicalProofPassed = logical.IsValid,
                ErrorCode = "solver_rendered_materials_missing",
                Notes = "Blind investigation requires text verified from actual rendered documents."
            };
        var blind = await new BlindPlayerInvestigator(_llm, _logger).RunAsync(draft, readableTexts, ct);
        var attempt = blind.Attempt ?? new BlindInvestigationAttempt();
        var supportedReferences = new HashSet<(string SourceId, string ReferenceId)>();
        foreach (var citation in attempt.Citations)
        {
            var asset = draft.AssetFull.Concat(draft.ResultAssets).FirstOrDefault(asset => asset.Id == citation.SourceId);
            CaseAuthoredContent? source = asset;
            source ??= draft.FollowUpEmails.Concat(draft.ResultEmails).FirstOrDefault(email => email.Id == citation.SourceId);
            if (citation.SourceId == "email.briefing") source = draft.Briefing;
            foreach (var support in source?.Support ?? [])
            {
                var quote = RenderedEvidenceText.Normalize(citation.Quote);
                var excerpt = RenderedEvidenceText.Normalize(support.Excerpt);
                if (excerpt.Length > 0 && quote.Contains(excerpt, StringComparison.Ordinal))
                    supportedReferences.Add((citation.SourceId, support.ReferenceId));
            }
        }
        attempt.ObservationIds = draft.CaseGraph.Observations
            .Where(observation => supportedReferences.Contains((observation.SourceAssetId,
                CaseBibleAuthorship.ResolveObservation(draft, observation).Id)))
            .Select(observation => observation.Id).Distinct(StringComparer.Ordinal).ToList();
        var closure = DerivationClosureEngine.Calculate(draft.CaseGraph, attempt.ObservationIds, ProofKnowledgeScope.PlayerReachable);
        var paths = CulpritUniquenessGate.PathsForSuspect(
            CulpritUniquenessGate.FindCulpritConclusions(draft.CaseGraph), attempt.SuspectId, closure);
        var narrativePassed = blind.Attempt is not null && attempt.SuspectId == draft.CulpritId
            && paths.Any(path => path.ObservationIds.Count > 0 && path.ObservationIds.All(attempt.ObservationIds.Contains));
        blind.Trace.FinalReachableObservationIds = attempt.ObservationIds.ToList();
        blind.Trace.LogicalProofPassed = logical.IsValid;
        blind.Trace.NarrativeSimulationPassed = narrativePassed;
        if (blind.Attempt is not null)
            blind.Trace.Entries.Add(new(blind.Trace.Entries.Count + 1, PlayerActionKind.AccuseSuspect,
                $"accuse:{attempt.SuspectId}", attempt.SuspectId, narrativePassed,
                narrativePassed ? "supported_submission" : "unsupported_submission",
                attempt.Rationale ?? string.Empty, [], attempt.ObservationIds));
        draft.SolverTrace = blind.Trace;
        var result = Grade(draft, attempt, logical, narrativePassed, blind.Trace);
        result.Mode = "player-visible";
        result.RenderedTextVerified = true;
        result.ErrorCode = blind.ErrorCode;
        result.Citations = attempt.Citations;
        result.Notes = blind.Error ?? (!narrativePassed
            ? "Player quotations do not establish the unique culprit through supported reachable evidence."
            : result.Notes);
        return result;
    }

    public Task<SolverResult> RunWitnessAsync(CaseDraft draft, CancellationToken ct)
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

    private SolverResult Grade(
        CaseDraft d,
        Attempt attempt,
        LogicalSolvabilityResult logical,
        bool narrativePassed,
        SolverDiagnosticTrace trace)
    {
        var score = SubmissionScoring.Evaluate(d.CulpritId, attempt.SuspectId, d.RequiredEvidenceIds,
            attempt.EvidenceIds, d.RequiredAnalysisIds, attempt.AnalysisIds,
            d.Questions.Select(question => new ScoredQuestion(question.Id, question.CorrectOptionId, question.Weight)).ToList(),
            attempt.Answers.Select(answer => new SubmittedAnswer(answer.QuestionId, answer.OptionId)),
            new SubmissionWeights(), .7,
            string.Equals(d.Metadata.Difficulty, "Rookie", StringComparison.OrdinalIgnoreCase)
                || string.Equals(d.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase));
        return new SolverResult
        {
            Attempt = attempt,
            Score = score.RoundedTotal,
            CulpritScore = Math.Round(score.Culprit, 4),
            EvidenceScore = Math.Round(score.Evidence, 4),
            AnalysisScore = Math.Round(score.Analysis, 4),
            QuestionsScore = Math.Round(score.Questions, 4),
            Correct = SolvabilityDecision.BothPass(logical.IsValid, narrativePassed, score.RawTotal),
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
