using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public sealed record EvidenceSemanticReviewRequest(
    string AssetId,
    string Description,
    string Body,
    IReadOnlyList<string> AllowedObservationIds,
    IReadOnlyList<string> AllowedCanonicalStatements);

public sealed record EvidenceSemanticReviewFinding(
    string Code,
    string AssetId,
    string Message);

public interface IEvidenceSemanticReviewer
{
    IReadOnlyList<EvidenceSemanticReviewFinding> Review(EvidenceSemanticReviewRequest request);
}

public static class EvidenceAssetContract
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> LayoutsByType =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["photo"] = new HashSet<string>(["Photo"], StringComparer.Ordinal),
            ["pdf"] = DocumentLayouts(),
            ["document"] = DocumentLayouts(),
            ["digital"] = new HashSet<string>(
            [
                "CallLog", "PosExport", "PhoneDump", "SensorLog", "BrowserHistory",
                "BankStatement", "GpsTrack", "FileListing", "ChatExport", "EmailExport", "AccessLog"
            ], StringComparer.Ordinal)
        };

    public static bool IsLayoutCompatible(string assetType, string layoutId) =>
        LayoutsByType.TryGetValue(assetType, out var layouts) && layouts.Contains(layoutId);

    public static ForensicInputObjectType InferContainedObjectType(string assetType, string layoutId) =>
        layoutId switch
        {
            "CallLog" or "ChatExport" or "EmailExport" or "AudioTranscript" =>
                ForensicInputObjectType.CommunicationRecord,
            "PhoneDump" => ForensicInputObjectType.DeviceImage,
            "SensorLog" or "AccessLog" => ForensicInputObjectType.AccessRecord,
            "BankStatement" or "PosExport" or "Receipt" => ForensicInputObjectType.FinancialRecord,
            "GpsTrack" => ForensicInputObjectType.LocationTrack,
            "BrowserHistory" or "FileListing" => ForensicInputObjectType.SessionRecord,
            "Photo" => ForensicInputObjectType.Image,
            _ when assetType.Equals("digital", StringComparison.OrdinalIgnoreCase) =>
                ForensicInputObjectType.DigitalFile,
            _ => ForensicInputObjectType.Document
        };

    private static IReadOnlySet<string> DocumentLayouts() =>
        new HashSet<string>(
        [
            "PoliceReport", "WitnessStatement", "InterviewTranscript", "AudioTranscript",
            "MedicalReport", "EvidenceLog", "Memo", "CustodyForm", "NewspaperClipping",
            "PersonalLetter", "Receipt", "SearchWarrant", "DispatchLog", "CaseMap", "Calendar",
            "ForensicReport"
        ], StringComparer.Ordinal);
}

public static class EvidenceContractValidator
{
    private static readonly string[] PlaceholderTerms =
    [
        "placeholder", "lorem ipsum", "todo", "to be completed", "pending content",
        "conteúdo pendente", "contenido pendiente", "contenu en attente"
    ];

    private static readonly string[] InnocenceLabels =
    [
        "innocent", "not guilty", "cleared suspect", "suspect cleared",
        "inocente", "não culpado", "suspeito inocentado",
        "no culpable", "sospechoso exonerado",
        "non coupable", "suspect innocenté"
    ];

    private static readonly string[] OverclaimTerms =
    [
        "culprit", "guilty", "committed the crime",
        "culpado", "autoria do crime", "cometeu o crime",
        "culpable", "cometió el delito",
        "coupable", "a commis le crime"
    ];

    public static StageValidationReport Validate(
        CaseDraft draft,
        IEvidenceSemanticReviewer? semanticReviewer = null)
    {
        var report = new StageValidationReport();
        ValidateAssetSpecs(draft.CaseGraph, report);
        ValidateAssetContent(draft, report, semanticReviewer);
        ValidateDecoyArcs(draft, report);
        return report;
    }

    public static StageValidationReport ValidatePlan(CaseDraft draft)
    {
        var report = new StageValidationReport();
        ValidateAssetSpecs(draft.CaseGraph, report);
        ValidateDecoyArcs(draft, report);
        return report;
    }

    public static StageValidationReport ValidateContent(
        CaseDraft draft,
        IEvidenceSemanticReviewer? semanticReviewer = null)
    {
        var report = new StageValidationReport();
        ValidateAssetContent(draft, report, semanticReviewer);
        return report;
    }

    private static void ValidateAssetSpecs(CaseGraph graph, StageValidationReport report)
    {
        var sources = graph.Sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
        var observations = graph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        var entityIds = graph.Entities.Select(entity => entity.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var spec in graph.AssetSpecs.OrderBy(spec => spec.Id, StringComparer.Ordinal))
        {
            if (!sources.ContainsKey(spec.Id))
                report.Errors.Add($"evidencePlan asset spec '{spec.Id}' has no graph source");
            if (string.IsNullOrWhiteSpace(spec.ArchetypeId))
                report.Errors.Add($"evidencePlan asset spec '{spec.Id}' has no archetypeId");
            if (spec.ObservationIds.Count == 0 && spec.ContainedObjectIds.Count == 0)
                report.Errors.Add($"evidencePlan asset spec '{spec.Id}' is an empty placeholder");
            if (!EvidenceAssetContract.IsLayoutCompatible(spec.AssetType, spec.LayoutId))
                report.Errors.Add($"evidencePlan asset spec '{spec.Id}' layout '{spec.LayoutId}' is incompatible with type '{spec.AssetType}'");

            foreach (var observationId in spec.ObservationIds)
            {
                if (!observations.TryGetValue(observationId, out var observation))
                    report.Errors.Add($"evidencePlan asset spec '{spec.Id}' references missing observation '{observationId}'");
                else if (observation.SourceAssetId != spec.Id)
                    report.Errors.Add($"evidencePlan observation '{observationId}' is owned by '{observation.SourceAssetId}', not '{spec.Id}'");
            }

            foreach (var objectId in spec.ContainedObjectIds)
            {
                if (!entityIds.Contains(objectId))
                    report.Errors.Add($"evidencePlan asset spec '{spec.Id}' contains missing object '{objectId}'");
                if (!spec.ContainedObjectTypes.ContainsKey(objectId))
                    report.Errors.Add($"evidencePlan asset spec '{spec.Id}' has no forensic object type for '{objectId}'");
            }
        }
    }

    private static void ValidateAssetContent(
        CaseDraft draft,
        StageValidationReport report,
        IEvidenceSemanticReviewer? semanticReviewer)
    {
        var assets = draft.AssetFull.Concat(draft.ResultAssets)
            .GroupBy(asset => asset.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var facts = draft.CaseGraph.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        var observations = draft.CaseGraph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        var entities = draft.CaseGraph.Entities.ToDictionary(entity => entity.Id, StringComparer.Ordinal);

        foreach (var spec in draft.CaseGraph.AssetSpecs.OrderBy(spec => spec.Id, StringComparer.Ordinal))
        {
            if (!assets.TryGetValue(spec.Id, out var asset))
            {
                report.Errors.Add($"evidenceContent planned asset '{spec.Id}' was not generated");
                continue;
            }

            var body = EvidenceContentText.ExtractBody(asset);
            var completeText = EvidenceContentText.Extract(asset);
            if (IsPlaceholder(asset, body))
                report.Errors.Add($"evidenceContent asset '{asset.Id}' is empty or contains placeholder content");
            if (asset.BodyDoc is not null
                && !string.IsNullOrWhiteSpace(spec.LayoutId)
                && !string.IsNullOrWhiteSpace(asset.BodyDoc.Layout)
                && asset.BodyDoc.Layout != spec.LayoutId)
            {
                report.Errors.Add($"evidenceContent asset '{asset.Id}' rendered layout '{asset.BodyDoc.Layout}' instead of '{spec.LayoutId}'");
            }

            var allowedStatements = new List<string>();
            foreach (var observationId in spec.ObservationIds)
            {
                if (!observations.TryGetValue(observationId, out var observation)
                    || !facts.TryGetValue(observation.FactId, out var fact))
                {
                    continue;
                }

                var expected = CanonicalStatement(fact, entities);
                if (!string.IsNullOrWhiteSpace(expected))
                {
                    allowedStatements.Add(expected);
                    if (!completeText.Contains(expected, StringComparison.OrdinalIgnoreCase))
                        report.Errors.Add($"evidenceContent asset '{asset.Id}' does not materialize observation '{observationId}' value '{expected}'");
                }
            }

            foreach (var privateFact in facts.Values.Where(fact => fact.Visibility == FactVisibility.Private))
            {
                var assigned = spec.ObservationIds.Any(id =>
                    observations.TryGetValue(id, out var observation)
                    && observation.FactId == privateFact.Id);
                var privateValue = CanonicalStatement(privateFact, entities);
                if (!assigned
                    && privateValue.Length >= 4
                    && completeText.Contains(privateValue, StringComparison.OrdinalIgnoreCase))
                {
                    report.Errors.Add($"evidenceContent asset '{asset.Id}' leaks unassigned private fact '{privateFact.Id}'");
                }
            }

            if (!string.IsNullOrWhiteSpace(asset.Description))
            {
                if (OverclaimTerms.Any(term =>
                        asset.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                        && !body.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    report.Errors.Add($"evidenceContent asset '{asset.Id}' description states a stronger conclusion than its rendered body");
                }
                foreach (var conclusion in facts.Values.Where(fact =>
                             fact.Predicate is "isCulprit" or "excludedFromCrime"))
                {
                    var value = CanonicalStatement(conclusion, entities);
                    if (value.Length >= 4
                        && asset.Description.Contains(value, StringComparison.OrdinalIgnoreCase)
                        && !body.Contains(value, StringComparison.OrdinalIgnoreCase))
                    {
                        report.Errors.Add($"evidenceContent asset '{asset.Id}' description overstates its rendered body using fact '{conclusion.Id}'");
                    }
                }
            }

            if (semanticReviewer is not null)
            {
                var request = new EvidenceSemanticReviewRequest(
                    asset.Id,
                    asset.Description ?? string.Empty,
                    body,
                    spec.ObservationIds,
                    allowedStatements);
                foreach (var finding in semanticReviewer.Review(request))
                    report.Errors.Add($"evidenceContent asset '{finding.AssetId}' semantic {finding.Code}: {finding.Message}");
            }
        }
    }

    private static void ValidateDecoyArcs(CaseDraft draft, StageValidationReport report)
    {
        var graph = draft.CaseGraph;
        var observations = graph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        var derivations = graph.Derivations.ToDictionary(derivation => derivation.Id, StringComparer.Ordinal);
        var reachable = ReachabilityClosureEngine.Calculate(graph).FinalObservationIds.ToHashSet(StringComparer.Ordinal);
        var assets = draft.AssetFull.Concat(draft.ResultAssets)
            .GroupBy(asset => asset.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var nonCulprits = graph.SuspectReferences
            .Select(reference => reference.SuspectId)
            .Where(id => id != draft.CulpritId)
            .Distinct(StringComparer.Ordinal);
        foreach (var suspectId in nonCulprits)
        {
            var arc = graph.DecoyArcs.SingleOrDefault(candidate => candidate.SuspectId == suspectId);
            if (arc is null)
            {
                report.Errors.Add($"evidencePlan decoy suspect '{suspectId}' has no typed DecoyArc");
                continue;
            }

            if (arc.SuspicionObservationIds.Count == 0)
                report.Errors.Add($"evidencePlan decoy arc '{suspectId}' has no plausible suspicion observation");
            if (arc.VerificationObservationIds.Count == 0)
                report.Errors.Add($"evidencePlan decoy arc '{suspectId}' has no verification observation");
            foreach (var observationId in arc.SuspicionObservationIds.Concat(arc.VerificationObservationIds))
            {
                if (!observations.ContainsKey(observationId))
                    report.Errors.Add($"evidencePlan decoy arc '{suspectId}' references missing observation '{observationId}'");
            }
            if (arc.MustBePlayerReachable
                && arc.VerificationObservationIds.Any(id => !reachable.Contains(id)))
            {
                report.Errors.Add($"evidencePlan decoy arc '{suspectId}' has an unreachable verification observation");
            }
            if (!derivations.TryGetValue(arc.ResolutionDerivationId, out var resolution)
                || resolution.Rule != DerivationRule.Exclusion)
            {
                report.Errors.Add($"evidencePlan decoy arc '{suspectId}' has no valid exclusion derivation '{arc.ResolutionDerivationId}'");
            }
            else if (!resolution.PremiseIds.Any(arc.VerificationObservationIds.Contains))
            {
                report.Errors.Add($"evidencePlan decoy arc '{suspectId}' is excluded without positive verification evidence");
            }

            foreach (var observation in arc.SuspicionObservationIds
                         .Concat(arc.VerificationObservationIds)
                         .Where(observations.ContainsKey)
                         .Select(id => observations[id]))
            {
                if (assets.TryGetValue(observation.SourceAssetId, out var asset)
                    && InnocenceLabels.Any(term =>
                        EvidenceContentText.Extract(asset).Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    report.Errors.Add($"evidenceContent decoy source '{asset.Id}' explicitly labels '{suspectId}' innocent");
                }
            }
        }
    }

    private static bool IsPlaceholder(EvidenceAsset asset, string body)
    {
        if (PlaceholderTerms.Any(term => body.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (asset.Type.Equals("photo", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(asset.Body);
        if (asset.BodyDoc is null || asset.BodyDoc.Sections.Count == 0)
            return string.IsNullOrWhiteSpace(asset.Body);
        return asset.BodyDoc.Sections.Any(section =>
            section.Kind == "table" && (section.Table is null || section.Table.Rows.Count == 0)
            || section.Kind == "transcript" && (section.Transcript is null || section.Transcript.Count == 0)
            || section.Kind == "code" && string.IsNullOrWhiteSpace(section.Code?.Content));
    }

    private static string CanonicalStatement(
        CanonicalFact fact,
        IReadOnlyDictionary<string, CaseEntity> entities)
    {
        if (fact.LiteralValue is not null)
            return fact.LiteralValue;
        if (fact.ObjectId is not null && entities.TryGetValue(fact.ObjectId, out var entity))
            return string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Id : entity.DisplayName;
        return fact.ObjectId ?? string.Empty;
    }
}

public static class EvidenceContentText
{
    public static string Extract(EvidenceAsset asset) =>
        string.Join("\n", new[] { asset.Title, asset.Description, ExtractBody(asset) }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    public static string ExtractBody(EvidenceAsset asset)
    {
        var values = new List<string?> { asset.Body };
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
