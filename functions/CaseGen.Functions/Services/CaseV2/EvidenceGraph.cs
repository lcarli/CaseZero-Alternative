namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Transitional clue-ladder projection retained while the generator migrates to
/// <see cref="CaseGraph"/>. It is never the canonical source when a hand-authored
/// CaseGraph is present.
/// </summary>
public sealed class EvidenceGraph
{
    public List<EvidenceClaimNode> Claims { get; set; } = new();
    public List<RedHerringNode> RedHerrings { get; set; } = new();
}

public sealed class EvidenceClaimNode
{
    public string ClueId { get; set; } = string.Empty;
    public string Discovery { get; set; } = string.Empty;
    public string Inference { get; set; } = string.Empty;
    public string? SupportsSuspectId { get; set; }
    public string Strength { get; set; } = "supporting";
    public string SourceType { get; set; } = "document";
    public string Role { get; set; } = "context";
    public List<string> AssetIds { get; set; } = new();
}

public sealed class RedHerringNode
{
    public string SuspectId { get; set; } = string.Empty;
    public string Suspicion { get; set; } = string.Empty;
    public string Verification { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public List<string> AssetIds { get; set; } = new();
}

public static class EvidenceGraphCompiler
{
    public static EvidenceGraph Compile(CaseDraft draft)
    {
        var graph = new EvidenceGraph
        {
            Claims = draft.Blueprint.ClueLadder.Select(clue => new EvidenceClaimNode
            {
                ClueId = clue.Id,
                Discovery = clue.Discovery,
                Inference = clue.Inference,
                SupportsSuspectId = clue.SupportsSuspectId,
                Strength = clue.Strength,
                SourceType = clue.SourceType,
                Role = clue.Role,
                AssetIds = draft.AssetStubs
                    .Where(asset => asset.SupportsClueIds.Contains(clue.Id, StringComparer.Ordinal))
                    .Select(asset => asset.Id)
                    .Concat(draft.ForensicStubs
                        .Where(outcome => outcome.SupportsClueIds.Contains(clue.Id, StringComparer.Ordinal))
                        .Select(outcome => draft.ForensicFull.FirstOrDefault(full =>
                            full.InputAssetId == outcome.InputAssetId
                            && full.AnalysisType == outcome.AnalysisType)?.ResultAssetId)
                        .Where(assetId => !string.IsNullOrWhiteSpace(assetId))
                        .Select(assetId => assetId!))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            }).ToList(),
            RedHerrings = draft.Blueprint.RedHerrings.Select(redHerring => new RedHerringNode
            {
                SuspectId = redHerring.SuspectId,
                Suspicion = redHerring.Suspicion,
                Verification = redHerring.Verification,
                Resolution = redHerring.Resolution,
                AssetIds = draft.AssetStubs
                    .Where(asset => asset.ResolvesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
                    .Select(asset => asset.Id)
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            }).ToList()
        };
        draft.EvidenceGraph = graph;
        CaseGraphProjection.ProjectTransitionalClueLadder(draft);
        return graph;
    }
}
