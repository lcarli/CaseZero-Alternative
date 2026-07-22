namespace CaseGen.Functions.Services.CaseV2;

public sealed record DifficultyProfile(
    string Name,
    int MinAssets,
    int MaxAssets,
    int MinFamilies,
    int MaxRarity,
    int MinSuspects,
    int MaxSuspects,
    int MinAnalyses,
    int MaxAnalyses,
    int MinIndependentCulpritSources,
    int MaxRepairIterations,
    bool AllEvidenceInitial)
{
    public DifficultyTopology Topology { get; init; } = new();
}

public sealed class DifficultyTopology
{
    public int MinDerivationDepth { get; init; }
    public int MaxDerivationDepth { get; init; } = int.MaxValue;
    public int MinIndependentProofPaths { get; init; } = 1;
    public int RequiredDecoyArcs { get; init; }
    public int MinForensicHops { get; init; }
    public int RedHerringResolutionDepth { get; init; } = 1;
    public int RequiredCrossSourceCorrelations { get; init; }
    public bool InitialSolutionAllowed { get; init; }
    public bool RequiresConflictingObservation { get; init; }
    public bool RequiresPartiallyOverlappingProofPaths { get; init; }
    public int MinOptionalAnalyses { get; init; }
    public int MinReliabilityLevels { get; init; } = 1;
    public bool RequiresMeaningfulInvestigationOrder { get; init; }
}

public static class DifficultyProfileCatalog
{
    public static DifficultyProfile Get(CaseDraft draft) =>
        Get(draft.Metadata.Difficulty
            ?? draft.Metadata.RequiredRank
            ?? draft.Request.Difficulty
            ?? draft.Request.RequiredRank);

    public static DifficultyProfile Get(string? difficulty) =>
        difficulty?.Trim().ToLowerInvariant() switch
        {
            "rookie" => Profile("Rookie", 3, 5, 3, 1, 3, 4, 0, 0, 2, 3, true,
                new DifficultyTopology
                {
                    MinDerivationDepth = 1, MaxDerivationDepth = 2,
                    MinIndependentProofPaths = 2, RequiredDecoyArcs = 2,
                    MinForensicHops = 0, RedHerringResolutionDepth = 1,
                    InitialSolutionAllowed = true
                }),
            "detective" => Profile("Detective", 6, 8, 5, 2, 3, 4, 1, 2, 2, 3, false,
                new DifficultyTopology
                {
                    MinDerivationDepth = 3, MaxDerivationDepth = 4,
                    MinIndependentProofPaths = 2, RequiredDecoyArcs = 2,
                    MinForensicHops = 1, RedHerringResolutionDepth = 1,
                    RequiredCrossSourceCorrelations = 1,
                    RequiresMeaningfulInvestigationOrder = true
                }),
            "detective2" => Profile("Detective2", 7, 9, 5, 2, 4, 5, 1, 2, 2, 3, false,
                Advanced(4, 5, 2, 3, 1)),
            "sergeant" => Profile("Sergeant", 8, 10, 6, 3, 4, 5, 1, 3, 2, 3, false,
                Advanced(4, 6, 2, 3, 1)),
            "lieutenant" => Profile("Lieutenant", 8, 10, 6, 3, 4, 6, 2, 3, 3, 3, false,
                Senior(5, 7, 3, 3, 2)),
            "captain" => Profile("Captain", 9, 10, 6, 3, 5, 6, 2, 4, 3, 3, false,
                Senior(5, 8, 3, 4, 2)),
            "commander" => Profile("Commander", 9, 10, 6, 3, 5, 6, 2, 4, 3, 3, false,
                Senior(6, 9, 3, 4, 3)),
            _ => Profile("Detective", 6, 8, 5, 2, 3, 4, 1, 2, 2, 3, false,
                new DifficultyTopology
                {
                    MinDerivationDepth = 3, MaxDerivationDepth = 4,
                    MinIndependentProofPaths = 2, RequiredDecoyArcs = 2,
                    MinForensicHops = 1, RedHerringResolutionDepth = 1,
                    RequiredCrossSourceCorrelations = 1,
                    RequiresMeaningfulInvestigationOrder = true
                })
        };

    private static DifficultyProfile Profile(
        string name,
        int minAssets,
        int maxAssets,
        int minFamilies,
        int maxRarity,
        int minSuspects,
        int maxSuspects,
        int minAnalyses,
        int maxAnalyses,
        int minSources,
        int maxRepairs,
        bool allInitial,
        DifficultyTopology topology) =>
        new(name, minAssets, maxAssets, minFamilies, maxRarity, minSuspects, maxSuspects,
            minAnalyses, maxAnalyses, minSources, maxRepairs, allInitial)
        {
            Topology = topology
        };

    private static DifficultyTopology Advanced(
        int minDepth,
        int maxDepth,
        int paths,
        int decoys,
        int optionalAnalyses) =>
        new()
        {
            MinDerivationDepth = minDepth,
            MaxDerivationDepth = maxDepth,
            MinIndependentProofPaths = paths,
            RequiredDecoyArcs = decoys,
            MinForensicHops = 1,
            RedHerringResolutionDepth = 2,
            RequiredCrossSourceCorrelations = 1,
            RequiresConflictingObservation = true,
            RequiresPartiallyOverlappingProofPaths = true,
            MinOptionalAnalyses = optionalAnalyses,
            MinReliabilityLevels = 2,
            RequiresMeaningfulInvestigationOrder = true
        };

    private static DifficultyTopology Senior(
        int minDepth,
        int maxDepth,
        int paths,
        int decoys,
        int forensicHops) =>
        new()
        {
            MinDerivationDepth = minDepth,
            MaxDerivationDepth = maxDepth,
            MinIndependentProofPaths = paths,
            RequiredDecoyArcs = decoys,
            MinForensicHops = forensicHops,
            RedHerringResolutionDepth = 2,
            RequiredCrossSourceCorrelations = 2,
            RequiresConflictingObservation = true,
            MinOptionalAnalyses = 1,
            MinReliabilityLevels = 3,
            RequiresMeaningfulInvestigationOrder = true
        };
}
