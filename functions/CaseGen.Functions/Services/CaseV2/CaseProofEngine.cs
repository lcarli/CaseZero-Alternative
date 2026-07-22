namespace CaseGen.Functions.Services.CaseV2;

public sealed class ReachabilitySnapshot
{
    public ReachabilityState State { get; init; }
    public IReadOnlyList<string> SourceIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ObservationIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ActionIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AnalysisIds { get; init; } = Array.Empty<string>();
}

public sealed class ReachabilityResult
{
    public IReadOnlyList<ReachabilitySnapshot> Snapshots { get; init; } = Array.Empty<ReachabilitySnapshot>();
    public IReadOnlyList<string> InitialObservationIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FinalSourceIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FinalObservationIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExecutedActionIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ReachableAnalysisIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnreachableRequiredSourceIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnreachableRequiredAnalysisIds { get; init; } = Array.Empty<string>();
}

public static class ReachabilityClosureEngine
{
    public static ReachabilityResult Calculate(CaseGraph graph)
    {
        var sources = graph.Sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
        var observations = graph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        var reachableSources = new HashSet<string>(StringComparer.Ordinal);
        var reachableObservations = new HashSet<string>(StringComparer.Ordinal);
        var executedActions = new HashSet<string>(StringComparer.Ordinal);
        var analyses = new HashSet<string>(StringComparer.Ordinal);
        var explicitlyRevealedSources = new HashSet<string>(StringComparer.Ordinal);
        var snapshots = new Dictionary<ReachabilityState, ReachabilitySnapshot>();
        var state = ReachabilityState.Initial;

        AddEligibleSources();
        Capture(ReachabilityState.Initial);
        var initialObservations = reachableObservations.Order(StringComparer.Ordinal).ToArray();

        var maximumPasses = graph.Sources.Count + graph.Actions.Count + 1;
        for (var pass = 0; pass < maximumPasses; pass++)
        {
            var changed = AddEligibleSources();
            foreach (var action in graph.Actions.OrderBy(action => action.Id, StringComparer.Ordinal))
            {
                if (executedActions.Contains(action.Id)
                    || action.PrerequisiteSourceIds.Any(id => !reachableSources.Contains(id))
                    || action.PrerequisiteActionIds.Any(id => !executedActions.Contains(id)))
                {
                    continue;
                }

                executedActions.Add(action.Id);
                explicitlyRevealedSources.UnionWith(action.RevealsSourceIds);
                if (!string.IsNullOrWhiteSpace(action.AnalysisId))
                    analyses.Add(action.AnalysisId);
                if (action.CompletesAt > state)
                    state = action.CompletesAt;
                changed = true;
                AddEligibleSources();
                Capture(state);
            }

            if (!changed)
                break;
        }

        AddEligibleSources();
        Capture(state);

        return new ReachabilityResult
        {
            Snapshots = snapshots.Values.OrderBy(snapshot => snapshot.State).ToArray(),
            InitialObservationIds = initialObservations,
            FinalSourceIds = reachableSources.Order(StringComparer.Ordinal).ToArray(),
            FinalObservationIds = reachableObservations.Order(StringComparer.Ordinal).ToArray(),
            ExecutedActionIds = executedActions.Order(StringComparer.Ordinal).ToArray(),
            ReachableAnalysisIds = analyses.Order(StringComparer.Ordinal).ToArray(),
            UnreachableRequiredSourceIds = graph.RequiredSourceIds
                .Concat(graph.Sources.Where(source => source.Required).Select(source => source.Id))
                .Distinct(StringComparer.Ordinal)
                .Where(id => !reachableSources.Contains(id))
                .Order(StringComparer.Ordinal)
                .ToArray(),
            UnreachableRequiredAnalysisIds = graph.RequiredAnalysisIds
                .Where(id => !analyses.Contains(id))
                .Order(StringComparer.Ordinal)
                .ToArray()
        };

        bool AddEligibleSources()
        {
            var changed = false;
            foreach (var source in graph.Sources.OrderBy(source => source.Id, StringComparer.Ordinal))
            {
                if (reachableSources.Contains(source.Id))
                    continue;
                var stageAllows = source.AvailableAt <= state || explicitlyRevealedSources.Contains(source.Id);
                var prerequisitesAllow = source.RevealPrerequisiteIds.All(
                    id => reachableSources.Contains(id) || executedActions.Contains(id));
                if (!stageAllows || !prerequisitesAllow)
                    continue;

                reachableSources.Add(source.Id);
                foreach (var observationId in source.ObservationIds.Where(observations.ContainsKey))
                    reachableObservations.Add(observationId);
                changed = true;
            }

            return changed;
        }

        void Capture(ReachabilityState snapshotState)
        {
            snapshots[snapshotState] = new ReachabilitySnapshot
            {
                State = snapshotState,
                SourceIds = reachableSources.Order(StringComparer.Ordinal).ToArray(),
                ObservationIds = reachableObservations.Order(StringComparer.Ordinal).ToArray(),
                ActionIds = executedActions.Order(StringComparer.Ordinal).ToArray(),
                AnalysisIds = analyses.Order(StringComparer.Ordinal).ToArray()
            };
        }
    }
}

public sealed record ProofGateFailure(
    string Code,
    string NodeId,
    string OwningStage,
    string Message,
    GraphRepairTarget RepairTarget);

public class ProofGateResult
{
    public List<ProofGateFailure> Failures { get; } = new();
    public List<DerivationPath> Paths { get; } = new();
    public bool IsValid => Failures.Count == 0;
}

public static class CulpritUniquenessGate
{
    public static ProofGateResult Validate(
        CaseGraph graph,
        string culpritId,
        string? difficulty,
        IEnumerable<string>? reachableObservationIds = null)
    {
        var result = new ProofGateResult();
        var observations = reachableObservationIds?.ToArray()
            ?? ReachabilityClosureEngine.Calculate(graph).FinalObservationIds.ToArray();
        var playerClosure = DerivationClosureEngine.Calculate(
            graph,
            observations,
            ProofKnowledgeScope.PlayerReachable);
        var fullClosure = DerivationClosureEngine.Calculate(
            graph,
            observationIds: null,
            ProofKnowledgeScope.FullKnowledge);
        var conclusions = FindCulpritConclusions(graph);
        var playerPaths = PathsForSuspect(conclusions, culpritId, playerClosure);
        var fullPaths = PathsForSuspect(conclusions, culpritId, fullClosure);

        if (playerPaths.Count == 0)
        {
            var privateOnly = fullPaths.Count > 0;
            Add(
                result,
                privateOnly ? "private_only_culprit" : "culprit_not_derivable",
                culpritId,
                privateOnly
                    ? $"culprit '{culpritId}' is derivable only from private truth"
                    : $"culprit '{culpritId}' is not derivable from player-reachable observations",
                "derivations",
                privateOnly ? "replace private premises with player evidence" : "add a player-reachable culprit proof");
        }
        else
        {
            result.Paths.AddRange(playerPaths);
        }

        foreach (var alternative in conclusions
                     .Select(item => item.SuspectId)
                     .Where(id => !string.IsNullOrWhiteSpace(id) && id != culpritId)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            if (PathsForSuspect(conclusions, alternative!, playerClosure).Count == 0)
                continue;
            Add(
                result,
                "multiple_valid_culprits",
                alternative!,
                $"suspect '{alternative}' is also derivable as a culprit",
                "derivations",
                "remove or refute the alternative culprit proof");
        }

        if (playerPaths.Count > 0 && playerPaths.All(path => IsMotiveOnly(graph, path)))
        {
            Add(
                result,
                "motive_only_culprit",
                culpritId,
                $"culprit '{culpritId}' is identified only by motive or benefit",
                "derivations",
                "add identity, action, access, or timeline attribution evidence");
        }

        var minimumDepth = MinimumProofDepth(difficulty);
        if (playerPaths.Count > 0 && playerPaths.All(path => path.Depth < minimumDepth))
        {
            Add(
                result,
                "insufficient_proof_depth",
                culpritId,
                $"{difficulty ?? "Detective"} requires culprit proof depth {minimumDepth}, but the deepest path is {playerPaths.Max(path => path.Depth)}",
                "derivations",
                "add a meaningful intermediate derivation");
        }

        return result;
    }

    internal static IReadOnlyList<(string SuspectId, ProofDerivation Derivation)> FindCulpritConclusions(CaseGraph graph)
    {
        var facts = graph.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        var suspectByPerson = graph.SuspectReferences
            .GroupBy(reference => reference.PersonEntityId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SuspectId, StringComparer.Ordinal);

        return graph.Derivations
            .Where(derivation =>
                derivation.IsCulpritConclusion
                || (facts.TryGetValue(derivation.ConclusionFactId, out var fact)
                    && IsCulpritPredicate(fact.Predicate)))
            .Select(derivation =>
            {
                facts.TryGetValue(derivation.ConclusionFactId, out var fact);
                var suspectId = derivation.SupportsSuspectId
                    ?? (fact is not null && suspectByPerson.TryGetValue(fact.SubjectId, out var mapped)
                        ? mapped
                        : fact?.SubjectId ?? string.Empty);
                return (suspectId, derivation);
            })
            .OrderBy(item => item.suspectId, StringComparer.Ordinal)
            .ThenBy(item => item.derivation.Id, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<DerivationPath> PathsForSuspect(
        IReadOnlyList<(string SuspectId, ProofDerivation Derivation)> conclusions,
        string suspectId,
        DerivationClosureResult closure) =>
        conclusions
            .Where(item => item.SuspectId == suspectId)
            .SelectMany(item => closure.GetPaths(item.Derivation.ConclusionFactId)
                .Where(path => path.FinalDerivationId == item.Derivation.Id))
            .OrderBy(path => path.Depth)
            .ThenBy(path => path.Signature, StringComparer.Ordinal)
            .ToArray();

    internal static bool IsMotiveOnly(CaseGraph graph, DerivationPath path)
    {
        var meaningfulRules = new HashSet<DerivationRule>
        {
            DerivationRule.IdentityMatch,
            DerivationRule.AccountOwnership,
            DerivationRule.DeviceAssignment,
            DerivationRule.SessionOrigin,
            DerivationRule.AccessOpportunity,
            DerivationRule.ActionAttribution,
            DerivationRule.TimelineCorrelation,
            DerivationRule.AlibiContradiction
        };
        if (path.Rules.Any(meaningfulRules.Contains))
            return false;

        var facts = graph.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        var observationFacts = graph.Observations
            .Where(observation => path.ObservationIds.Contains(observation.Id, StringComparer.Ordinal))
            .Select(observation => facts.GetValueOrDefault(observation.FactId))
            .Where(fact => fact is not null)
            .ToList();
        return path.Rules.All(rule => rule is DerivationRule.BenefitAttribution or DerivationRule.CrossSourceCorroboration)
               && observationFacts.All(fact =>
                   fact!.Predicate.Contains("motive", StringComparison.OrdinalIgnoreCase)
                   || fact.Predicate.Contains("benefit", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCulpritPredicate(string predicate) =>
        predicate.Equals("isCulprit", StringComparison.OrdinalIgnoreCase)
        || predicate.Equals("culprit", StringComparison.OrdinalIgnoreCase)
        || predicate.Equals("committedCrime", StringComparison.OrdinalIgnoreCase)
        || predicate.Equals("culpritIdentity", StringComparison.OrdinalIgnoreCase);

    private static int MinimumProofDepth(string? difficulty) =>
        difficulty?.Trim().ToLowerInvariant() switch
        {
            "detective2" or "sergeant" => 2,
            "lieutenant" or "captain" or "commander" => 3,
            _ => 1
        };

    private static void Add(
        ProofGateResult result,
        string code,
        string nodeId,
        string message,
        string property,
        string operation) =>
        result.Failures.Add(new ProofGateFailure(
            code,
            nodeId,
            "proof",
            message,
            new GraphRepairTarget("proof", nodeId, property, operation)));
}

public static class IndependentProofPathGate
{
    public static ProofGateResult Validate(
        CaseGraph graph,
        string culpritId,
        string? difficulty,
        IEnumerable<string>? reachableObservationIds = null)
    {
        var result = new ProofGateResult();
        var observations = reachableObservationIds?.ToArray()
            ?? ReachabilityClosureEngine.Calculate(graph).FinalObservationIds.ToArray();
        var closure = DerivationClosureEngine.Calculate(graph, observations);
        var conclusions = CulpritUniquenessGate.FindCulpritConclusions(graph);
        var sharedForensicOrigins = graph.ForensicTransforms
            .SelectMany(transform => transform.ProducedObservationIds)
            .Select(observationId => graph.Observations.FirstOrDefault(observation => observation.Id == observationId))
            .Where(observation => observation is not null)
            .Select(observation => graph.Sources.FirstOrDefault(source => source.Id == observation!.SourceAssetId)?.EvidentiaryOriginId
                                   ?? observation!.SourceAssetId)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = CulpritUniquenessGate.PathsForSuspect(conclusions, culpritId, closure)
            .Where(path => path.EvidentiaryOriginIds.Count > 0)
            .Where(path => path.EvidentiaryOriginIds.Any(origin => !sharedForensicOrigins.Contains(origin)))
            .Where(path => !CulpritUniquenessGate.IsMotiveOnly(graph, path))
            .OrderBy(path => path.EvidentiaryOriginIds.Count)
            .ThenBy(path => path.Signature, StringComparer.Ordinal)
            .ToArray();
        var selected = MaximumIndependentSet(candidates, sharedForensicOrigins);
        result.Paths.AddRange(selected);

        var required = DifficultyProfileCatalog.Get(difficulty).MinIndependentCulpritSources;
        if (selected.Count < required)
        {
            result.Failures.Add(new ProofGateFailure(
                "insufficient_independent_paths",
                culpritId,
                "proof",
                $"{difficulty ?? "Detective"} requires {required} independently originated culprit paths, but only {selected.Count} were found",
                new GraphRepairTarget(
                    "proof",
                    culpritId,
                    "derivations/evidentiaryOriginId",
                    "add a player-reachable proof from an independent evidentiary origin")));
        }

        return result;
    }

    private static IReadOnlyList<DerivationPath> MaximumIndependentSet(
        IReadOnlyList<DerivationPath> paths,
        IReadOnlySet<string> sharedOrigins)
    {
        var best = new List<DerivationPath>();

        void Search(int index, HashSet<string> usedOrigins, List<DerivationPath> selected)
        {
            if (selected.Count + paths.Count - index <= best.Count)
                return;
            if (index == paths.Count)
            {
                if (selected.Count > best.Count)
                    best = selected.ToList();
                return;
            }

            var path = paths[index];
            var independentOrigins = path.EvidentiaryOriginIds
                .Where(origin => !sharedOrigins.Contains(origin))
                .ToArray();
            if (independentOrigins.Length > 0 && !independentOrigins.Any(usedOrigins.Contains))
            {
                var nextOrigins = new HashSet<string>(usedOrigins, StringComparer.Ordinal);
                nextOrigins.UnionWith(independentOrigins);
                selected.Add(path);
                Search(index + 1, nextOrigins, selected);
                selected.RemoveAt(selected.Count - 1);
            }

            Search(index + 1, usedOrigins, selected);
        }

        Search(0, new HashSet<string>(StringComparer.Ordinal), new List<DerivationPath>());
        return best;
    }
}

public static class ReachabilityGate
{
    public static ProofGateResult Validate(CaseGraph graph, string culpritId)
    {
        var result = new ProofGateResult();
        var validation = CaseGraphValidator.Validate(graph);
        foreach (var error in validation.Errors.Where(error => error.Code == "circular_reveal_dependency"))
        {
            result.Failures.Add(new ProofGateFailure(
                error.Code,
                error.NodeId,
                error.OwningStage,
                error.Message,
                error.RepairTarget));
        }

        var reachability = ReachabilityClosureEngine.Calculate(graph);
        foreach (var sourceId in reachability.UnreachableRequiredSourceIds)
        {
            result.Failures.Add(new ProofGateFailure(
                "unreachable_required_source",
                sourceId,
                "rules",
                $"required evidence source '{sourceId}' is unreachable",
                new GraphRepairTarget("rules", sourceId, "revealPrerequisiteIds", "repair the reveal chain")));
        }

        foreach (var analysisId in reachability.UnreachableRequiredAnalysisIds)
        {
            result.Failures.Add(new ProofGateFailure(
                "unreachable_required_analysis",
                analysisId,
                "forensics",
                $"required analysis '{analysisId}' is unreachable",
                new GraphRepairTarget("forensics", analysisId, "actions", "add a reachable analysis request action")));
        }

        var closure = DerivationClosureEngine.Calculate(graph, reachability.FinalObservationIds);
        var culpritPaths = CulpritUniquenessGate.PathsForSuspect(
            CulpritUniquenessGate.FindCulpritConclusions(graph),
            culpritId,
            closure);
        if (culpritPaths.Count == 0)
        {
            result.Failures.Add(new ProofGateFailure(
                "reachable_evidence_cannot_derive_culprit",
                culpritId,
                "proof",
                $"final reachable observations cannot derive culprit '{culpritId}'",
                new GraphRepairTarget("proof", culpritId, "derivations", "connect reachable evidence to the culprit proof")));
        }
        else
        {
            result.Paths.AddRange(culpritPaths);
        }

        return result;
    }
}

public sealed class LeakageGateResult : ProofGateResult
{
    public DerivationPath? LeakingPath { get; internal set; }
}

public static class PrematureSolutionLeakageGate
{
    public static LeakageGateResult Validate(CaseGraph graph, string culpritId, string? difficulty)
    {
        var result = new LeakageGateResult();
        var reachability = ReachabilityClosureEngine.Calculate(graph);
        var closure = DerivationClosureEngine.Calculate(graph, reachability.InitialObservationIds);
        var paths = CulpritUniquenessGate.PathsForSuspect(
            CulpritUniquenessGate.FindCulpritConclusions(graph),
            culpritId,
            closure);
        var rookie = string.Equals(difficulty, "Rookie", StringComparison.OrdinalIgnoreCase);

        if (rookie && paths.Count == 0)
        {
            result.Failures.Add(new ProofGateFailure(
                "rookie_not_initially_solvable",
                culpritId,
                "proof",
                $"Rookie culprit '{culpritId}' cannot be derived from initial observations",
                new GraphRepairTarget("evidencePlan", culpritId, "sources", "make the complete Rookie proof initially reachable")));
        }
        else if (!rookie && paths.Count > 0)
        {
            result.LeakingPath = paths.OrderBy(path => path.Depth).ThenBy(path => path.Signature, StringComparer.Ordinal).First();
            result.Paths.Add(result.LeakingPath);
            result.Failures.Add(new ProofGateFailure(
                "premature_culprit_leak",
                result.LeakingPath.FinalDerivationId ?? culpritId,
                "proof",
                $"initial observations derive culprit '{culpritId}' through '{result.LeakingPath.FinalDerivationId}'",
                new GraphRepairTarget(
                    "evidencePlan",
                    result.LeakingPath.FinalDerivationId ?? culpritId,
                    "source availability",
                    "gate at least one decisive premise behind an action or forensic result")));
        }
        else
        {
            result.Paths.AddRange(paths);
        }

        return result;
    }
}
