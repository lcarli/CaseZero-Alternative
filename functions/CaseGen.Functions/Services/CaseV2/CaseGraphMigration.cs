using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace CaseGen.Functions.Services.CaseV2;

public sealed record CaseGraphFeatureOptions(bool Enabled)
{
    public static CaseGraphFeatureOptions From(IConfiguration configuration) =>
        new(configuration.GetValue("CaseGenV2:CaseGraphEnabled", false));
}

public sealed record PublicContractParityIssue(string Surface, string Message);

public sealed class PublicContractParityReport
{
    public List<PublicContractParityIssue> Issues { get; } = new();
    public bool IsEquivalent => Issues.Count == 0;
}

public static class CaseGraphGenerationCoordinator
{
    public static void PrepareCanonicalGraph(CaseDraft draft, bool graphModeEnabled)
    {
        if (draft.CaseGraph.IsEmpty)
            CaseGraphProjection.ProjectTransitionalClueLadder(draft);
        if (graphModeEnabled && draft.CaseGraph.Origin != CaseGraphOrigin.HandAuthored)
            draft.CaseGraph.Origin = CaseGraphOrigin.Generated;
    }
}

public static class CaseGraphPublicContractCompiler
{
    private static readonly string[] Surfaces =
    [
        "metadata", "suspects", "assets", "emails", "timeline", "temporalEvents",
        "forensicsDefaults", "forensicOutcomes", "rules", "solution", "gameMetadata"
    ];

    public static JsonObject Compile(CaseDraft draft, JsonObject legacyContract)
    {
        var assetIds = draft.CaseGraph.Sources
            .Where(source => source.Kind == EvidenceSourceKind.Asset)
            .Select(source => source.Id)
            .ToHashSet(StringComparer.Ordinal);
        var emailIds = draft.CaseGraph.Sources
            .Where(source => source.Kind == EvidenceSourceKind.Email)
            .Select(source => source.Id)
            .Append("email.briefing")
            .ToHashSet(StringComparer.Ordinal);
        var suspectIds = draft.CaseGraph.SuspectReferences
            .Select(reference => reference.SuspectId)
            .ToHashSet(StringComparer.Ordinal);
        var transformKeys = draft.CaseGraph.ForensicTransforms
            .Select(transform => $"{transform.InputAssetId}\u001f{transform.MethodId}")
            .ToHashSet(StringComparer.Ordinal);

        var compiled = new JsonObject
        {
            ["version"] = legacyContract["version"]?.DeepClone(),
            ["caseId"] = legacyContract["caseId"]?.DeepClone(),
            ["metadata"] = legacyContract["metadata"]?.DeepClone(),
            ["assets"] = Filter(legacyContract["assets"] as JsonArray, item =>
                assetIds.Contains(item?["id"]?.GetValue<string>() ?? string.Empty)),
            ["emails"] = Filter(legacyContract["emails"] as JsonArray, item =>
                emailIds.Contains(item?["id"]?.GetValue<string>() ?? string.Empty)),
            ["suspects"] = Filter(legacyContract["suspects"] as JsonArray, item =>
                suspectIds.Contains(item?["id"]?.GetValue<string>() ?? string.Empty)),
            ["timeline"] = legacyContract["timeline"]?.DeepClone(),
            ["temporalEvents"] = legacyContract["temporalEvents"]?.DeepClone(),
            ["rules"] = legacyContract["rules"]?.DeepClone(),
            ["forensicsDefaults"] = legacyContract["forensicsDefaults"]?.DeepClone(),
            ["forensicOutcomes"] = Filter(legacyContract["forensicOutcomes"] as JsonArray, item =>
            {
                if (transformKeys.Count == 0)
                    return false;
                var key = $"{item?["inputAssetId"]?.GetValue<string>()}\u001f{item?["analysisType"]?.GetValue<string>()}";
                return transformKeys.Contains(key);
            }),
            ["solution"] = legacyContract["solution"]?.DeepClone(),
            ["gameMetadata"] = legacyContract["gameMetadata"]?.DeepClone()
        };
        return compiled;
    }

    public static PublicContractParityReport Compare(JsonObject legacyContract, JsonObject graphContract)
    {
        var report = new PublicContractParityReport();
        foreach (var surface in Surfaces)
        {
            var legacy = legacyContract[surface]?.ToJsonString() ?? "null";
            var graph = graphContract[surface]?.ToJsonString() ?? "null";
            if (!string.Equals(legacy, graph, StringComparison.Ordinal))
                report.Issues.Add(new PublicContractParityIssue(surface, $"public surface '{surface}' differs between legacy and CaseGraph compilation"));
        }
        return report;
    }

    private static JsonArray Filter(JsonArray? values, Func<JsonNode?, bool> predicate)
    {
        var result = new JsonArray();
        if (values is null)
            return result;
        foreach (var value in values.Where(predicate))
            result.Add(value?.DeepClone());
        return result;
    }
}

public static class GenerationPersistenceGate
{
    public static bool CanPersist(
        bool writeToDisk,
        IReadOnlyCollection<string> validationErrors,
        FinalValidationReport finalValidation) =>
        writeToDisk
        && validationErrors.Count == 0
        && finalValidation.Issues.All(issue => !issue.Blocking);
}
