namespace CaseGen.Functions.Services.CaseV2;

public static class ForensicContractValidator
{
    public static StageValidationReport Validate(CaseDraft draft)
    {
        var report = new StageValidationReport();
        var graph = draft.CaseGraph;
        var assetSpecs = graph.AssetSpecs.ToDictionary(spec => spec.Id, StringComparer.Ordinal);
        var observations = graph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        var entities = graph.Entities.ToDictionary(entity => entity.Id, StringComparer.Ordinal);
        var resultAssets = draft.ResultAssets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);

        ValidateAnalysisTypes(draft, report);
        foreach (var transform in graph.ForensicTransforms.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var method = ForensicMethodCatalog.Find(transform.MethodId);
            if (method is null)
            {
                report.Errors.Add($"forensics transform '{transform.Id}' uses unknown method '{transform.MethodId}'");
                continue;
            }

            if (!assetSpecs.TryGetValue(transform.InputAssetId, out var inputAsset))
            {
                report.Errors.Add($"forensics transform '{transform.Id}' references missing input asset spec '{transform.InputAssetId}'");
                continue;
            }
            if (!method.AcceptedAssetTypes.Contains(inputAsset.AssetType))
                report.Errors.Add($"forensics transform '{transform.Id}' method '{method.Id}' rejects asset type '{inputAsset.AssetType}'");
            if (!inputAsset.ContainedObjectIds.Contains(transform.InputObjectId, StringComparer.Ordinal))
                report.Errors.Add($"forensics transform '{transform.Id}' input object '{transform.InputObjectId}' is not contained in asset '{inputAsset.Id}'");
            if (!inputAsset.ContainedObjectTypes.TryGetValue(transform.InputObjectId, out var objectType))
                report.Errors.Add($"forensics transform '{transform.Id}' input object '{transform.InputObjectId}' has no typed object contract");
            else if (!method.AcceptedInputObjectTypes.Contains(objectType))
                report.Errors.Add($"forensics transform '{transform.Id}' method '{method.Id}' rejects object type '{objectType}'");

            if (method.RequiresReferenceSample
                && (string.IsNullOrWhiteSpace(transform.ReferenceSampleObjectId)
                    || !entities.ContainsKey(transform.ReferenceSampleObjectId)))
            {
                report.Errors.Add($"forensics transform '{transform.Id}' method '{method.Id}' requires a reference sample object");
            }
            if (method.RequiresChainOfCustody
                && (string.IsNullOrWhiteSpace(transform.ChainOfCustodyObservationId)
                    || !observations.ContainsKey(transform.ChainOfCustodyObservationId)))
            {
                report.Errors.Add($"forensics transform '{transform.Id}' method '{method.Id}' requires chain-of-custody evidence");
            }
            if (!method.ValidResultLayouts.Contains(transform.ResultLayoutId))
                report.Errors.Add($"forensics transform '{transform.Id}' result layout '{transform.ResultLayoutId}' is invalid for '{method.Id}'");

            if (transform.LimitationKeys.Count == 0)
                report.Errors.Add($"forensics transform '{transform.Id}' has no limitation statement");
            foreach (var limitationKey in transform.LimitationKeys.Where(key => !method.LimitationKeys.Contains(key)))
                report.Errors.Add($"forensics transform '{transform.Id}' uses unsupported limitation '{limitationKey}'");

            ValidateProducedObservations(transform, method, observations, report);
            ValidateResultDocument(draft, transform, method, resultAssets, entities, report);
            ValidateDerivationScope(graph, transform, method, report);
        }

        ValidateDecisiveAttribution(draft, report);
        return report;
    }

    private static void ValidateAnalysisTypes(CaseDraft draft, StageValidationReport report)
    {
        foreach (var analysis in draft.AnalysisTypes.OrderBy(item => item.Type, StringComparer.Ordinal))
        {
            var method = ForensicMethodCatalog.Find(analysis.Type);
            if (method is null)
            {
                report.Errors.Add($"forensics analysis '{analysis.Type}' is not in the immutable method catalog");
                continue;
            }
            var actual = analysis.AvailableFor.Order(StringComparer.OrdinalIgnoreCase).ToArray();
            var expected = method.AcceptedAssetTypes.Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
            {
                report.Errors.Add($"forensics analysis '{analysis.Type}' availableFor differs from immutable catalog [{string.Join(", ", expected)}]");
            }
        }
    }

    private static void ValidateProducedObservations(
        ForensicTransform transform,
        ForensicMethodDefinition method,
        IReadOnlyDictionary<string, EvidenceObservation> observations,
        StageValidationReport report)
    {
        if (transform.ProducedObservationIds.Count == 0)
            report.Errors.Add($"forensics transform '{transform.Id}' produces no observations");
        foreach (var observationId in transform.ProducedObservationIds)
        {
            if (!observations.TryGetValue(observationId, out var observation))
            {
                report.Errors.Add($"forensics transform '{transform.Id}' references missing produced observation '{observationId}'");
                continue;
            }
            if (observation.SourceAssetId != transform.ResultAssetId)
                report.Errors.Add($"forensics observation '{observationId}' is owned by '{observation.SourceAssetId}', not result '{transform.ResultAssetId}'");
            if (observation.ForensicProperty is null
                || !method.ProducibleProperties.Contains(observation.ForensicProperty.Value))
            {
                report.Errors.Add($"forensics observation '{observationId}' property '{observation.ForensicProperty}' cannot be produced by '{method.Id}'");
            }
        }
        foreach (var property in transform.ProducedProperties.Where(property => !method.ProducibleProperties.Contains(property)))
            report.Errors.Add($"forensics transform '{transform.Id}' claims unsupported property '{property}'");
    }

    private static void ValidateResultDocument(
        CaseDraft draft,
        ForensicTransform transform,
        ForensicMethodDefinition method,
        IReadOnlyDictionary<string, CaseGen.Functions.Models.CaseV2.EvidenceAsset> resultAssets,
        IReadOnlyDictionary<string, CaseEntity> entities,
        StageValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(transform.ResultAssetId)
            || !resultAssets.TryGetValue(transform.ResultAssetId, out var resultAsset))
        {
            report.Errors.Add($"forensics transform '{transform.Id}' has no generated result asset");
            return;
        }

        var localization = method.Localize(draft.Request.Language);
        var text = EvidenceContentText.Extract(resultAsset);
        var body = EvidenceContentText.ExtractBody(resultAsset);
        var submittedName = entities.TryGetValue(transform.InputObjectId, out var inputObject)
            ? inputObject.DisplayName
            : transform.InputObjectId;
        if (!body.Contains(transform.InputObjectId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(submittedName)
                || !body.Contains(submittedName, StringComparison.OrdinalIgnoreCase)))
        {
            report.Errors.Add($"forensics result '{resultAsset.Id}' Items Submitted does not identify contained object '{transform.InputObjectId}'");
        }
        if (!body.Contains(localization.Labels["itemsSubmitted"], StringComparison.OrdinalIgnoreCase))
            report.Errors.Add($"forensics result '{resultAsset.Id}' lacks localized Items Submitted label");
        if (!body.Contains(localization.Labels["limitations"], StringComparison.OrdinalIgnoreCase))
            report.Errors.Add($"forensics result '{resultAsset.Id}' lacks localized Limitations label");
        foreach (var limitationKey in transform.LimitationKeys.Where(localization.Limitations.ContainsKey))
        {
            if (!text.Contains(localization.Limitations[limitationKey], StringComparison.OrdinalIgnoreCase))
                report.Errors.Add($"forensics result '{resultAsset.Id}' omits limitation '{limitationKey}'");
        }
    }

    private static void ValidateDerivationScope(
        CaseGraph graph,
        ForensicTransform transform,
        ForensicMethodDefinition method,
        StageValidationReport report)
    {
        var produced = transform.ProducedObservationIds.ToHashSet(StringComparer.Ordinal);
        foreach (var derivation in graph.Derivations.Where(derivation =>
                     derivation.PremiseIds.Any(produced.Contains)))
        {
            if (derivation.Rule != DerivationRule.CrossSourceCorroboration
                && derivation.Rule != DerivationRule.Exclusion
                && !method.AllowedDerivationRules.Contains(derivation.Rule))
            {
                report.Errors.Add($"forensics derivation '{derivation.Id}' overclaims '{method.Id}' observation with rule '{derivation.Rule}'");
            }
            if (derivation.IsCulpritConclusion
                && derivation.PremiseIds.All(produced.Contains))
            {
                report.Errors.Add($"forensics derivation '{derivation.Id}' claims the culprit from forensic observations alone");
            }
        }
    }

    private static void ValidateDecisiveAttribution(CaseDraft draft, StageValidationReport report)
    {
        var decisiveClueIds = draft.Blueprint.ClueLadder
            .Where(clue => clue.SourceType.Equals("forensic", StringComparison.OrdinalIgnoreCase)
                           && clue.Strength == "decisive"
                           && clue.SupportsSuspectId == draft.CulpritId)
            .Select(clue => clue.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (DifficultyProfileCatalog.Get(draft).AllEvidenceInitial || decisiveClueIds.Count == 0)
            return;

        var owners = draft.ForensicStubs.Where(stub =>
                stub.Findings
                && stub.MatchedSuspectId == draft.CulpritId
                && stub.SupportsClueIds.Any(decisiveClueIds.Contains))
            .ToList();
        if (owners.Count != 1)
        {
            report.Errors.Add(
                $"forensics decisive culprit attribution requires exactly one valid outcome; found {owners.Count} for clues [{string.Join(", ", decisiveClueIds.Order(StringComparer.Ordinal))}]");
        }
    }
}
