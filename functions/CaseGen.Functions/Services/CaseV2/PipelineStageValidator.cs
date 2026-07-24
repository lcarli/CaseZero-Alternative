using System.Globalization;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class StageValidationReport
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public static class PipelineStageValidator
{
    public static StageValidationReport ValidateCaseBible(CaseDraft draft, bool required = false)
    {
        var report = new StageValidationReport();
        report.Errors.AddRange(CaseBibleValidator.Validate(draft, required).Errors);
        return report;
    }

    public static StageValidationReport ValidateLocale(CaseDraft draft) =>
        LocaleProfileCatalog.Validate(draft);

    public static StageValidationReport ValidateDifficultyTopology(CaseDraft draft) =>
        DifficultyTopologyValidator.Validate(draft);

    public static StageValidationReport ValidateBlueprint(CaseDraft draft)
    {
        var report = new StageValidationReport();
        var profile = DifficultyProfileCatalog.Get(draft);
        if (draft.SuspectStubs.Count < profile.MinSuspects || draft.SuspectStubs.Count > profile.MaxSuspects)
            report.Errors.Add($"{profile.Name} requires {profile.MinSuspects}-{profile.MaxSuspects} suspects");
        if (!draft.SuspectStubs.Any(suspect => suspect.Id == draft.CulpritId))
            report.Errors.Add("culpritId is not present in suspect stubs");

        var culpritClaims = draft.Blueprint.ClueLadder
            .Where(clue => clue.SupportsSuspectId == draft.CulpritId
                && clue.Strength is "supporting" or "decisive")
            .ToList();
        if (culpritClaims.Count < profile.MinIndependentCulpritSources)
            report.Errors.Add($"culprit has only {culpritClaims.Count} supporting claims; {profile.Name} requires {profile.MinIndependentCulpritSources}");
        if (culpritClaims.Select(clue => clue.SourceType).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            < profile.MinIndependentCulpritSources)
            report.Errors.Add("culprit-supporting claims do not use enough independent source types");
        if (culpritClaims.Count(clue => clue.Strength == "decisive") != 1)
            report.Errors.Add("blueprint must contain exactly one decisive culprit-supporting clue");
        if (profile.AllEvidenceInitial && draft.Blueprint.ClueLadder.Any(clue => clue.SourceType == "forensic"))
            report.Errors.Add("Rookie blueprint cannot depend on forensic clues");
        if (!profile.AllEvidenceInitial
            && culpritClaims.Any(clue => clue.Strength == "decisive"
                && !string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase)))
            report.Errors.Add("non-Rookie decisive culprit clue must be delivered by the forensic stage");
        if (!profile.AllEvidenceInitial
            && !culpritClaims.Any(clue => clue.Role == "identity"))
            report.Errors.Add("non-Rookie culprit chain requires an initial identity-bridge clue");
        if (!profile.AllEvidenceInitial
            && !culpritClaims.Any(clue => clue.Role == "action"))
            report.Errors.Add("non-Rookie culprit chain requires an independent action clue");
        if (!profile.AllEvidenceInitial
            && !culpritClaims.Any(clue => clue.Role == "forensicAttribution"
                && clue.Strength == "decisive"))
            report.Errors.Add("non-Rookie culprit chain requires a decisive forensic-attribution clue");

        var parsedEvents = new List<DateTimeOffset>();
        foreach (var beat in draft.Blueprint.IncidentSequence)
        {
            if (!DateTimeOffset.TryParse(beat.Time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                report.Errors.Add($"incident beat '{beat.Id}' has invalid timestamp '{beat.Time}'");
            else
                parsedEvents.Add(parsed);
        }
        if (!parsedEvents.SequenceEqual(parsedEvents.OrderBy(value => value)))
            report.Errors.Add("incident sequence is not chronological");
        return report;
    }

    public static StageValidationReport ValidateEvidencePlan(CaseDraft draft)
    {
        var report = new StageValidationReport();
        var profile = DifficultyProfileCatalog.Get(draft);
        var graph = EvidenceGraphCompiler.Compile(draft);
        if (draft.AssetStubs.Count < profile.MinAssets || draft.AssetStubs.Count > profile.MaxAssets)
            report.Errors.Add($"{profile.Name} requires {profile.MinAssets}-{profile.MaxAssets} total dossier assets");
        var investigativeCount = draft.AssetStubs.Count(asset =>
            EvidenceRoles.IsInvestigative(asset.EvidenceRole));
        if (investigativeCount < profile.MinInvestigativeAssets
            || investigativeCount > profile.MaxInvestigativeAssets)
        {
            report.Errors.Add($"{profile.Name} requires {profile.MinInvestigativeAssets}-{profile.MaxInvestigativeAssets} investigative assets");
        }
        foreach (var claim in graph.Claims.Where(claim =>
                     !string.Equals(claim.SourceType, "forensic", StringComparison.OrdinalIgnoreCase)
                     && claim.AssetIds.Count == 0))
            report.Errors.Add($"clue '{claim.ClueId}' has no evidence asset");
        foreach (var redHerring in graph.RedHerrings.Where(redHerring => redHerring.AssetIds.Count == 0))
            report.Errors.Add($"red herring for '{redHerring.SuspectId}' has no resolution asset");

        var culpritClaims = graph.Claims
            .Where(claim => claim.SupportsSuspectId == draft.CulpritId
                && claim.Strength is "supporting" or "decisive"
                && claim.AssetIds.Count > 0)
            .ToList();
        var independentSources = culpritClaims
            .SelectMany(claim => string.Equals(claim.SourceType, "forensic", StringComparison.OrdinalIgnoreCase)
                ? new[] { $"forensic:{claim.ClueId}" }
                : claim.AssetIds.ToArray())
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (independentSources < profile.MinIndependentCulpritSources)
            report.Errors.Add("culprit claims are not distributed across enough independent assets");
        if (culpritClaims.Select(claim => claim.SourceType).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            < profile.MinIndependentCulpritSources)
            report.Errors.Add("culprit claims are not distributed across enough source types");
        Merge(report, EvidenceContractValidator.ValidatePlan(draft));
        return report;
    }

    public static StageValidationReport ValidateEvidenceContent(CaseDraft draft)
    {
        var report = new StageValidationReport();
        var assets = draft.AssetFull.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        foreach (var claim in draft.EvidenceGraph.Claims.Where(claim =>
                     !string.Equals(claim.SourceType, "forensic", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var assetId in claim.AssetIds)
            {
                if (!assets.TryGetValue(assetId, out var asset))
                {
                    report.Errors.Add($"planned asset '{assetId}' was not generated");
                    continue;
                }
                var content = RenderedText(asset);
                if (!content.Contains(claim.Discovery, StringComparison.OrdinalIgnoreCase))
                    report.Errors.Add($"asset '{assetId}' does not materialize clue '{claim.ClueId}'");
            }
        }
        foreach (var redHerring in draft.EvidenceGraph.RedHerrings)
        {
            if (!redHerring.AssetIds.Any(assetId =>
                    assets.TryGetValue(assetId, out var asset)
                    && RenderedText(asset).Contains(redHerring.Verification, StringComparison.OrdinalIgnoreCase)))
                report.Errors.Add($"no generated asset materializes the resolution for '{redHerring.SuspectId}'");
        }
        Merge(report, EvidenceContractValidator.ValidateContent(draft));
        return report;
    }

    public static StageValidationReport ValidateForensics(CaseDraft draft)
    {
        var report = new StageValidationReport();
        var profile = DifficultyProfileCatalog.Get(draft);
        if (profile.AllEvidenceInitial)
        {
            if (draft.AnalysisTypes.Count > 0 || draft.ForensicFull.Count > 0)
                report.Errors.Add("Rookie cannot contain a forensic workflow");
            return report;
        }

        EvidenceGraphCompiler.Compile(draft);
        var assets = draft.AssetFull.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        foreach (var clue in draft.Blueprint.ClueLadder.Where(clue =>
                     string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase)))
        {
            var inputOwners = draft.AssetStubs.Where(asset =>
                asset.ForensicInputClueIds.Contains(clue.Id, StringComparer.Ordinal)).ToList();
            if (inputOwners.Count != 1)
                report.Errors.Add($"forensic clue '{clue.Id}' must have exactly one analyzable input asset");
        }
        var analyses = draft.AnalysisTypes.ToDictionary(analysis => analysis.Type, StringComparer.Ordinal);
        var findingCount = draft.ForensicFull.Count(outcome => outcome.Findings);
        if (findingCount < profile.MinAnalyses || findingCount > profile.MaxAnalyses)
            report.Errors.Add($"{profile.Name} requires {profile.MinAnalyses}-{profile.MaxAnalyses} useful forensic outcomes");
        foreach (var outcome in draft.ForensicFull)
        {
            if (!assets.TryGetValue(outcome.InputAssetId, out var asset))
            {
                report.Errors.Add($"forensic input '{outcome.InputAssetId}' is not an initial asset");
                continue;
            }
            if (!analyses.TryGetValue(outcome.AnalysisType, out var analysis)
                || !analysis.AvailableFor.Contains(asset.Type, StringComparer.OrdinalIgnoreCase))
                report.Errors.Add($"analysis '{outcome.AnalysisType}' is incompatible with asset '{outcome.InputAssetId}' ({asset.Type})");
        }

        var resultAssets = draft.ResultAssets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        foreach (var clue in draft.Blueprint.ClueLadder.Where(clue =>
                     string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase)))
        {
            var owners = draft.ForensicStubs.Where(stub =>
                stub.SupportsClueIds.Contains(clue.Id, StringComparer.Ordinal)).ToList();
            if (owners.Count != 1)
            {
                report.Errors.Add($"forensic clue '{clue.Id}' must be assigned to exactly one outcome");
                continue;
            }

            var owner = owners[0];
            var inputOwner = draft.AssetStubs.SingleOrDefault(asset =>
                asset.ForensicInputClueIds.Contains(clue.Id, StringComparer.Ordinal));
            if (inputOwner is null || owner.InputAssetId != inputOwner.Id)
            {
                report.Errors.Add($"forensic clue '{clue.Id}' outcome must analyze its owned input asset");
                continue;
            }
            var full = draft.ForensicFull.FirstOrDefault(outcome =>
                outcome.InputAssetId == owner.InputAssetId
                && outcome.AnalysisType == owner.AnalysisType);
            if (full?.ResultAssetId is null
                || !resultAssets.TryGetValue(full.ResultAssetId, out var resultAsset)
                || !RenderedText(resultAsset).Contains(clue.Discovery, StringComparison.OrdinalIgnoreCase))
                report.Errors.Add($"forensic outcome does not materialize clue '{clue.Id}'");
        }
        Merge(report, ForensicContractValidator.Validate(draft));
        return report;
    }

    private static void Merge(StageValidationReport target, StageValidationReport source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
    }

    private static string RenderedText(CaseGen.Functions.Models.CaseV2.EvidenceAsset asset)
    {
        var values = new List<string?>
        {
            asset.Body,
            asset.Description
        };
        if (asset.BodyDoc is not null)
        {
            values.Add(asset.BodyDoc.Title);
            values.Add(asset.BodyDoc.Subtitle);
            values.Add(asset.BodyDoc.Classification);
            values.AddRange(asset.BodyDoc.Header.SelectMany(item => new[] { item.Label, item.Value }));
            foreach (var section in asset.BodyDoc.Sections)
            {
                values.Add(section.Heading);
                values.Add(section.Text);
                if (section.Table is not null)
                {
                    values.AddRange(section.Table.Columns);
                    values.AddRange(section.Table.Rows.SelectMany(row => row));
                    values.Add(section.Table.Footnote);
                }
                if (section.Items is not null)
                    values.AddRange(section.Items.SelectMany(item => new[] { item.Label, item.Value }));
                if (section.Transcript is not null)
                    values.AddRange(section.Transcript.SelectMany(entry =>
                        new[] { entry.Timestamp, entry.Speaker, entry.Text }));
                values.Add(section.Code?.Content);
            }
        }
        return string.Join("\n", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
