namespace CaseGen.Functions.Services.CaseV2;

public static class DifficultyTopologyValidator
{
    public static StageValidationReport Validate(CaseDraft draft)
    {
        var report = new StageValidationReport();
        var profile = DifficultyProfileCatalog.Get(draft);
        var topology = profile.Topology;
        var graph = draft.CaseGraph;
        var reachability = ReachabilityClosureEngine.Calculate(graph);
        var closure = DerivationClosureEngine.Calculate(graph, reachability.FinalObservationIds);
        var culpritPaths = CulpritUniquenessGate.PathsForSuspect(
            CulpritUniquenessGate.FindCulpritConclusions(graph),
            draft.CulpritId,
            closure);

        if (culpritPaths.Count == 0)
        {
            report.Errors.Add($"difficulty node '{draft.CulpritId}' has no player-reachable culprit proof for {profile.Name}");
        }
        else
        {
            var deepest = culpritPaths.Max(path => path.Depth);
            if (deepest < topology.MinDerivationDepth)
                report.Errors.Add($"difficulty node '{draft.CulpritId}' proof depth {deepest} is below {profile.Name} minimum {topology.MinDerivationDepth}");
            if (culpritPaths.All(path => path.Depth > topology.MaxDerivationDepth))
                report.Errors.Add($"difficulty node '{draft.CulpritId}' has no proof within {profile.Name} maximum depth {topology.MaxDerivationDepth}");
        }

        var independent = IndependentProofPathGate.Validate(
            graph,
            draft.CulpritId,
            profile.Name,
            reachability.FinalObservationIds);
        if (independent.Paths.Count < topology.MinIndependentProofPaths)
            report.Errors.Add($"difficulty node '{draft.CulpritId}' has {independent.Paths.Count} independent paths; {profile.Name} requires {topology.MinIndependentProofPaths}");

        if (graph.DecoyArcs.Count < topology.RequiredDecoyArcs)
            report.Errors.Add($"difficulty graph has {graph.DecoyArcs.Count} decoy arcs; {profile.Name} requires {topology.RequiredDecoyArcs}");

        var forensicObservationIds = graph.ForensicTransforms
            .SelectMany(transform => transform.ProducedObservationIds)
            .ToHashSet(StringComparer.Ordinal);
        var maximumForensicHops = culpritPaths.Count == 0
            ? 0
            : culpritPaths.Max(path => path.ObservationIds.Count(forensicObservationIds.Contains));
        if (maximumForensicHops < topology.MinForensicHops)
            report.Errors.Add($"difficulty culprit proof has {maximumForensicHops} forensic hops; {profile.Name} requires {topology.MinForensicHops}");

        var crossSourceCount = graph.Derivations.Count(derivation =>
            derivation.Rule == DerivationRule.CrossSourceCorroboration);
        if (crossSourceCount < topology.RequiredCrossSourceCorrelations)
            report.Errors.Add($"difficulty graph has {crossSourceCount} cross-source correlations; {profile.Name} requires {topology.RequiredCrossSourceCorrelations}");

        ValidateDecoyDepth(graph, closure, topology, profile.Name, report);
        ValidateInitialContract(draft, profile, reachability, report);

        if (topology.RequiresConflictingObservation
            && !graph.Observations.Any(observation => observation.Reliability == ObservationReliability.Disputed)
            && !graph.Facts.Any(fact => fact.TruthStatus == FactTruthStatus.Disputed))
        {
            report.Errors.Add($"difficulty graph for {profile.Name} requires at least one conflicting but explainable observation");
        }

        if (topology.RequiresPartiallyOverlappingProofPaths && !HasPartiallyOverlappingPaths(culpritPaths))
            report.Errors.Add($"difficulty graph for {profile.Name} requires partially overlapping proof paths");

        var optionalAnalyses = graph.Actions.Count(action =>
            action.Kind == InvestigationActionKind.RequestAnalysis && !action.Required);
        if (optionalAnalyses < topology.MinOptionalAnalyses)
            report.Errors.Add($"difficulty graph has {optionalAnalyses} optional analyses; {profile.Name} requires {topology.MinOptionalAnalyses}");

        var reliabilityLevels = graph.Observations.Select(observation => observation.Reliability).Distinct().Count();
        if (reliabilityLevels < topology.MinReliabilityLevels)
            report.Errors.Add($"difficulty graph uses {reliabilityLevels} reliability levels; {profile.Name} requires {topology.MinReliabilityLevels}");

        if (topology.RequiresMeaningfulInvestigationOrder
            && !graph.Actions.Any(action =>
                action.PrerequisiteActionIds.Count > 0
                || action.PrerequisiteSourceIds.Count > 0))
        {
            report.Errors.Add($"difficulty graph for {profile.Name} has no meaningful investigation order");
        }

        return report;
    }

    private static bool HasPartiallyOverlappingPaths(IReadOnlyList<DerivationPath> paths)
    {
        for (var left = 0; left < paths.Count; left++)
        for (var right = left + 1; right < paths.Count; right++)
        {
            var leftOrigins = paths[left].EvidentiaryOriginIds.ToHashSet(StringComparer.Ordinal);
            var rightOrigins = paths[right].EvidentiaryOriginIds.ToHashSet(StringComparer.Ordinal);
            var overlap = leftOrigins.Intersect(rightOrigins, StringComparer.Ordinal).Any();
            var distinct = !leftOrigins.SetEquals(rightOrigins);
            if (overlap && distinct)
                return true;
        }
        return false;
    }

    private static void ValidateDecoyDepth(
        CaseGraph graph,
        DerivationClosureResult closure,
        DifficultyTopology topology,
        string difficulty,
        StageValidationReport report)
    {
        var derivations = graph.Derivations.ToDictionary(derivation => derivation.Id, StringComparer.Ordinal);
        foreach (var arc in graph.DecoyArcs)
        {
            if (!derivations.TryGetValue(arc.ResolutionDerivationId, out var resolution))
                continue;
            var depth = closure.GetPaths(resolution.ConclusionFactId)
                .Where(path => path.FinalDerivationId == resolution.Id)
                .Select(path => path.Depth)
                .DefaultIfEmpty(0)
                .Max();
            if (depth < topology.RedHerringResolutionDepth)
                report.Errors.Add($"difficulty decoy '{arc.SuspectId}' resolution depth {depth} is below {difficulty} minimum {topology.RedHerringResolutionDepth}");
        }
    }

    private static void ValidateInitialContract(
        CaseDraft draft,
        DifficultyProfile profile,
        ReachabilityResult reachability,
        StageValidationReport report)
    {
        var leakage = PrematureSolutionLeakageGate.Validate(draft.CaseGraph, draft.CulpritId, profile.Name);
        if (profile.Topology.InitialSolutionAllowed)
        {
            if (!leakage.IsValid)
                report.Errors.AddRange(leakage.Failures.Select(failure => $"difficulty {failure.Message}"));
            if (draft.CaseGraph.ForensicTransforms.Count > 0
                || draft.CaseGraph.Actions.Any(action => action.Kind == InvestigationActionKind.RequestAnalysis)
                || draft.AnalysisTypes.Count > 0)
            {
                report.Errors.Add("difficulty Rookie topology requires zero forensic hops and analyses");
            }
            if (draft.CaseGraph.Sources.Any(source => source.AvailableAt != ReachabilityState.Initial))
                report.Errors.Add("difficulty Rookie topology requires all evidence to be initial");
        }
        else if (!leakage.IsValid)
        {
            report.Errors.AddRange(leakage.Failures.Select(failure => $"difficulty {failure.Message}"));
        }

        if (reachability.UnreachableRequiredSourceIds.Count > 0)
            report.Errors.Add($"difficulty graph has unreachable required sources [{string.Join(", ", reachability.UnreachableRequiredSourceIds)}]");
    }
}
