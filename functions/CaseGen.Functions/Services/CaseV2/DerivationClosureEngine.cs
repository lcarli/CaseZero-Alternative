namespace CaseGen.Functions.Services.CaseV2;

public enum ProofKnowledgeScope
{
    PlayerReachable,
    FullKnowledge
}

public sealed class DerivationPath
{
    public string ConclusionFactId { get; init; } = string.Empty;
    public string? FinalDerivationId { get; init; }
    public int Depth { get; init; }
    public IReadOnlyList<string> PremiseIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ObservationIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EvidentiaryOriginIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DerivationIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DerivationRule> Rules { get; init; } = Array.Empty<DerivationRule>();

    public string Signature =>
        $"{ConclusionFactId}|{FinalDerivationId}|{string.Join(",", ObservationIds)}|{string.Join(",", DerivationIds)}";
}

public sealed class DerivationClosureResult
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<DerivationPath>> _pathsByNodeId;

    internal DerivationClosureResult(IReadOnlyDictionary<string, IReadOnlyList<DerivationPath>> pathsByNodeId)
    {
        _pathsByNodeId = pathsByNodeId;
        KnownFactIds = pathsByNodeId.Keys
            .Where(id => id.StartsWith("fact.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> KnownFactIds { get; }

    public IReadOnlyList<DerivationPath> GetPaths(string conclusionFactId) =>
        _pathsByNodeId.GetValueOrDefault(conclusionFactId) ?? Array.Empty<DerivationPath>();

    public bool CanDerive(string conclusionFactId) => GetPaths(conclusionFactId).Count > 0;
}

public static class DerivationClosureEngine
{
    private const int MaxPathsPerNode = 512;

    public static DerivationClosureResult Calculate(
        CaseGraph graph,
        IEnumerable<string>? observationIds = null,
        ProofKnowledgeScope scope = ProofKnowledgeScope.PlayerReachable)
    {
        var observations = graph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        var sources = graph.Sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
        var selectedIds = observationIds?.ToHashSet(StringComparer.Ordinal)
            ?? graph.Observations.Select(observation => observation.Id).ToHashSet(StringComparer.Ordinal);
        var paths = new Dictionary<string, List<DerivationPath>>(StringComparer.Ordinal);

        foreach (var observation in graph.Observations
                     .Where(observation => selectedIds.Contains(observation.Id))
                     .Where(observation => scope == ProofKnowledgeScope.FullKnowledge
                                           || observation.Visibility == FactVisibility.Public)
                     .OrderBy(observation => observation.Id, StringComparer.Ordinal))
        {
            var originId = observation.EvidentiaryOriginId;
            if (string.IsNullOrWhiteSpace(originId)
                && sources.TryGetValue(observation.SourceAssetId, out var source))
            {
                originId = source.EvidentiaryOriginId;
            }

            var path = new DerivationPath
            {
                ConclusionFactId = observation.FactId,
                Depth = 0,
                PremiseIds = new[] { observation.Id },
                ObservationIds = new[] { observation.Id },
                EvidentiaryOriginIds = new[] { originId ?? observation.SourceAssetId }
            };
            AddPath(paths, observation.Id, path);
            AddPath(paths, observation.FactId, path);
        }

        if (scope == ProofKnowledgeScope.FullKnowledge)
        {
            foreach (var fact in graph.Facts.OrderBy(fact => fact.Id, StringComparer.Ordinal))
            {
                AddPath(paths, fact.Id, new DerivationPath
                {
                    ConclusionFactId = fact.Id,
                    Depth = 0
                });
            }
        }

        var orderedDerivations = graph.Derivations.OrderBy(derivation => derivation.Id, StringComparer.Ordinal).ToList();
        for (var iteration = 0; iteration <= orderedDerivations.Count; iteration++)
        {
            var changed = false;
            foreach (var derivation in orderedDerivations)
            {
                if (derivation.PremiseIds.Count == 0)
                    continue;
                var premiseOptions = derivation.PremiseIds
                    .Select(premiseId => paths.GetValueOrDefault(premiseId))
                    .ToList();
                if (premiseOptions.Any(options => options is null || options.Count == 0))
                    continue;

                foreach (var combination in Cartesian(premiseOptions!))
                {
                    var path = Combine(graph, derivation, combination);
                    changed |= AddPath(paths, derivation.Id, path);
                    changed |= AddPath(paths, derivation.ConclusionFactId, path);
                }
            }

            if (!changed)
                break;
        }

        var frozen = paths.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<DerivationPath>)pair.Value
                .OrderBy(path => path.Depth)
                .ThenBy(path => path.Signature, StringComparer.Ordinal)
                .ToList(),
            StringComparer.Ordinal);
        return new DerivationClosureResult(frozen);
    }

    private static DerivationPath Combine(
        CaseGraph graph,
        ProofDerivation derivation,
        IReadOnlyList<DerivationPath> premises)
    {
        var observationIds = premises.SelectMany(path => path.ObservationIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var origins = premises.SelectMany(path => path.EvidentiaryOriginIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var derivationIds = premises.SelectMany(path => path.DerivationIds)
            .Append(derivation.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var derivationsById = graph.Derivations.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var rules = derivationIds
            .Where(derivationsById.ContainsKey)
            .Select(id => derivationsById[id].Rule)
            .Distinct()
            .OrderBy(rule => rule)
            .ToArray();

        return new DerivationPath
        {
            ConclusionFactId = derivation.ConclusionFactId,
            FinalDerivationId = derivation.Id,
            Depth = premises.Max(path => path.Depth) + 1,
            PremiseIds = derivation.PremiseIds.Order(StringComparer.Ordinal).ToArray(),
            ObservationIds = observationIds,
            EvidentiaryOriginIds = origins,
            DerivationIds = derivationIds,
            Rules = rules
        };
    }

    private static bool AddPath(
        IDictionary<string, List<DerivationPath>> paths,
        string nodeId,
        DerivationPath path)
    {
        if (!paths.TryGetValue(nodeId, out var existing))
        {
            existing = new List<DerivationPath>();
            paths[nodeId] = existing;
        }

        if (existing.Count >= MaxPathsPerNode || existing.Any(candidate => candidate.Signature == path.Signature))
            return false;
        existing.Add(path);
        return true;
    }

    private static IEnumerable<IReadOnlyList<DerivationPath>> Cartesian(
        IReadOnlyList<List<DerivationPath>?> options)
    {
        IEnumerable<IReadOnlyList<DerivationPath>> combinations =
            new[] { (IReadOnlyList<DerivationPath>)Array.Empty<DerivationPath>() };
        foreach (var option in options)
        {
            combinations = combinations.SelectMany(
                prefix => option!.Take(MaxPathsPerNode).Select(path => (IReadOnlyList<DerivationPath>)prefix.Append(path).ToArray()));
        }

        return combinations.Take(MaxPathsPerNode);
    }
}
