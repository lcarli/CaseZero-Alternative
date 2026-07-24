using System.Text.Json.Serialization;
using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Stage 6 — Forensics Plan. Picks which (asset × analysisType) pairs matter and which is the smoking gun.</summary>
public class ForensicsPlanTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public ForensicsPlanTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output
    {
        [JsonPropertyName("analysisTypes")] public List<ProposedAnalysisType> AnalysisTypes { get; set; } = new();
        [JsonPropertyName("outcomes")] public List<ForensicOutcomeStub> Outcomes { get; set; } = new();
    }

    private class ProposedAnalysisType
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("durationMinutes")] public int DurationMinutes { get; set; } = 60;
        [JsonPropertyName("availableFor")] public JsonElement AvailableFor { get; set; }
    }

    private const string Schema = """
    {
      "type":"object","required":["analysisTypes","outcomes"],
      "properties":{
        "analysisTypes":{"type":"array","minItems":1,"maxItems":4,
          "items":{"type":"object","required":["type","durationMinutes","availableFor"]}},
        "outcomes":{"type":"array","minItems":1,"maxItems":5,
          "items":{"type":"object","required":["inputAssetId","inputObjectId","analysisType","findings","role","supportsClueIds","producedProperties","limitationKeys","resultLayoutId"],
            "properties":{
              "inputAssetId":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"},
              "inputObjectId":{"type":"string","pattern":"^(document|object|device|account|session|communication|transaction)\\.[a-z0-9_]+$"},
              "matchedSuspectId":{"type":["string","null"],"pattern":"^suspect\\.[a-z0-9_]+$"},
              "supportsClueIds":{"type":"array","items":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"}},
              "producedProperties":{"type":"array","minItems":1,"items":{"type":"string","enum":["FileMetadata","DeviceCorrelation","SessionTimeline","TimestampCorrelation","AccountOwnership","TransactionCorrelation","AccessCorrelation","DocumentAuthenticity","HandwritingSimilarity","LinguisticSimilarity","VisibleImageFeature","SpatialMeasurement"]}},
              "limitationKeys":{"type":"array","minItems":1,"items":{"type":"string"}},
              "referenceSampleObjectId":{"type":["string","null"]},
              "chainOfCustodyObservationId":{"type":["string","null"]},
              "resultLayoutId":{"type":"string","enum":["ForensicReport"]}}}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var profile = DifficultyProfileCatalog.Get(draft);
        var forensicClues = draft.Blueprint.ClueLadder
            .Where(clue => string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var desiredOutcomes = ResolveDesiredOutcomeCount(profile, forensicClues.Count);
        var catalogJson = System.Text.Json.JsonSerializer.Serialize(
            ForensicMethodCatalog.All.Select(method => new
            {
                method.Id,
                acceptedInputObjectTypes = method.AcceptedInputObjectTypes,
                acceptedAssetTypes = method.AcceptedAssetTypes,
                producibleProperties = method.ProducibleProperties,
                method.LimitationKeys,
                method.RequiresReferenceSample,
                method.RequiresChainOfCustody,
                validResultLayouts = method.ValidResultLayouts,
                localization = method.Localize(draft.Request.Language)
            }));
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("ForensicsPlan", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["desired_outcomes"] = desiredOutcomes
        });
        var user = prompts.RenderUser("ForensicsPlan", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["method_catalog_json"] = catalogJson,
            ["repair_guidance"] = string.IsNullOrWhiteSpace(repairGuidance) ? "(none)" : repairGuidance
        });
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "ForensicsPlan", system, user, Schema, ct);
        o.Outcomes = o.Outcomes.Take(desiredOutcomes).ToList();
        foreach (var outcome in o.Outcomes)
        {
            outcome.SupportsClueIds = outcome.SupportsClueIds
                .Where(id => forensicClues.Any(clue => clue.Id == id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            outcome.ProducedProperties = outcome.ProducedProperties.Distinct().ToList();
            outcome.LimitationKeys = outcome.LimitationKeys.Distinct(StringComparer.Ordinal).ToList();
            NormalizeMethodContract(draft, outcome);
        }
        NormalizeDecisiveAttribution(draft, forensicClues, o.Outcomes);

        draft.AnalysisTypes = ForensicMethodCatalog
            .MaterializeAnalysisTypes(
                o.AnalysisTypes.Select(type => new ForensicsAnalysisType
                {
                    Type = type.Type,
                    DurationMinutes = type.DurationMinutes
                }),
                o.Outcomes)
            .ToList();
        draft.ForensicStubs = o.Outcomes;
    }

    private static void NormalizeDecisiveAttribution(
        CaseDraft draft,
        IReadOnlyList<CanonicalClue> forensicClues,
        IReadOnlyList<ForensicOutcomeStub> outcomes)
    {
        var decisiveClue = forensicClues.FirstOrDefault(clue => clue.Strength == "decisive");
        if (decisiveClue is null || outcomes.Count == 0)
            return;

        var owner = outcomes.FirstOrDefault(outcome =>
                        outcome.SupportsClueIds.Contains(decisiveClue.Id, StringComparer.Ordinal))
                    ?? outcomes[0];
        foreach (var outcome in outcomes)
        {
            if (!ReferenceEquals(outcome, owner))
                outcome.SupportsClueIds.RemoveAll(id => id == decisiveClue.Id);
        }
        if (!owner.SupportsClueIds.Contains(decisiveClue.Id, StringComparer.Ordinal))
            owner.SupportsClueIds.Add(decisiveClue.Id);
        owner.Findings = true;
        owner.MatchedSuspectId = draft.CulpritId;
    }

    private static int ResolveDesiredOutcomeCount(DifficultyProfile profile, int forensicClueCount) =>
        Math.Clamp(
            Math.Max(
                profile.MinAnalyses,
                forensicClueCount + profile.Topology.MinOptionalAnalyses),
            profile.MinAnalyses,
            profile.MaxAnalyses);

    private static void NormalizeMethodContract(CaseDraft draft, ForensicOutcomeStub outcome)
    {
        var selectedSpec = draft.CaseGraph.AssetSpecs.FirstOrDefault(spec => spec.Id == outcome.InputAssetId);
        if (selectedSpec is null)
            return;
        var ownsCanonicalClue = outcome.SupportsClueIds.Count > 0;
        IEnumerable<EvidenceAssetSpec> candidateSpecs = ownsCanonicalClue
            ? new[] { selectedSpec }
            : draft.CaseGraph.AssetSpecs
                .Where(spec => EvidenceRoles.IsInvestigative(spec.EvidenceRole)
                               && spec.ContainedObjectIds.Count > 0
                               && !string.Equals(spec.ArchetypeId, "suspect_interview", StringComparison.Ordinal)
                               && !string.Equals(spec.ImagePurpose, ImagePurposes.SuspectPortrait, StringComparison.Ordinal))
                .ToList();
        var compatible = (from spec in candidateSpecs
                          from pair in spec.ContainedObjectTypes
                          from method in ForensicMethodCatalog.All
                          where method.AcceptedAssetTypes.Contains(spec.AssetType)
                                && method.AcceptedInputObjectTypes.Contains(pair.Value)
                                && !method.RequiresReferenceSample
                                && (!method.RequiresChainOfCustody || spec.ObservationIds.Count > 0)
                          let overlap = outcome.ProducedProperties.Count(method.ProducibleProperties.Contains)
                          orderby spec.Id == outcome.InputAssetId descending,
                              method.Id == outcome.AnalysisType descending,
                              overlap descending,
                              method.RequiresChainOfCustody,
                              method.Id
                          select (Spec: spec, Method: method, ObjectId: pair.Key))
            .FirstOrDefault();
        if (compatible.Method is null)
            return;

        outcome.InputAssetId = compatible.Spec.Id;
        outcome.AnalysisType = compatible.Method.Id;
        outcome.InputObjectId = compatible.ObjectId;
        outcome.ProducedProperties = outcome.ProducedProperties
            .Where(compatible.Method.ProducibleProperties.Contains)
            .Distinct()
            .ToList();
        if (outcome.ProducedProperties.Count == 0)
            outcome.ProducedProperties.Add(compatible.Method.ProducibleProperties.Order().First());
        outcome.LimitationKeys = compatible.Method.LimitationKeys.ToList();
        outcome.ResultLayoutId = compatible.Method.ValidResultLayouts.Order(StringComparer.Ordinal).First();
        if (compatible.Method.RequiresChainOfCustody)
        {
            outcome.ChainOfCustodyObservationId = draft.CaseGraph.Sources
                .FirstOrDefault(source => source.Id == compatible.Spec.Id)?
                .ObservationIds.FirstOrDefault();
        }
        else
        {
            outcome.ChainOfCustodyObservationId = null;
        }
    }
}

/// <summary>Stage 7 — Forensic Outcome Detail. One call per outcome → conclusion + (if findings) result PDF asset + result email.</summary>
public class ForensicOutcomeTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public ForensicOutcomeTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    public class Output
    {
        [JsonPropertyName("conclusionText")] public string? ConclusionText { get; set; }
        [JsonPropertyName("resultAsset")] public EvidenceAsset? ResultAsset { get; set; }
        [JsonPropertyName("resultEmail")] public EvidenceEmail? ResultEmail { get; set; }
    }

    private const string Schema = """
    {
      "type":"object","required":["conclusionText","resultAsset","resultEmail"],
      "properties":{
        "conclusionText":{"type":"string"},
        "resultAsset":{"type":["object","null"],"required":["id","type","title","description","body","bodyDoc","visibility"],"properties":{"id":{"type":"string","pattern":"^asset\\.report_[a-z0-9_]+$"},"type":{"type":"string","enum":["pdf","document"]},"title":{"type":"string"},"description":{"type":"string"},"body":{"type":"string"},"bodyDoc":{"type":["object","null"],"properties":{"layout":{"type":"string"},"title":{"type":"string"},"sections":{"type":"array","minItems":1}}},"visibility":{"type":"string","enum":["hidden"]}}},
        "resultEmail":{"type":["object","null"],"required":["id","from","subject","body","sentAt","visibility"],"properties":{"id":{"type":"string","pattern":"^email\\.lab_[a-z0-9_]+$"},"from":{"type":"string"},"subject":{"type":"string"},"body":{"type":"string"},"sentAt":{"type":"string"},"visibility":{"type":"string","enum":["hidden"]}}}
      }
    }
    """;

    public async Task<(ForensicsOutcome full, EvidenceAsset? asset, EvidenceEmail? email)> RunAsync(CaseDraft draft, ForensicOutcomeStub stub, CancellationToken ct)
    {
        var supportedClues = draft.Blueprint.ClueLadder
            .Where(clue => stub.SupportsClueIds.Contains(clue.Id, StringComparer.Ordinal))
            .ToList();
        var inputAsset = draft.AssetFull.FirstOrDefault(asset => asset.Id == stub.InputAssetId);
        var inputAssetSpec = draft.CaseGraph.AssetSpecs.FirstOrDefault(asset => asset.Id == stub.InputAssetId);
        var inputObject = draft.CaseGraph.Entities.FirstOrDefault(entity => entity.Id == stub.InputObjectId);
        var method = ForensicMethodCatalog.Find(stub.AnalysisType);
        var localization = method?.Localize(draft.Request.Language);
        var methodContractJson = System.Text.Json.JsonSerializer.Serialize(method is null ? null : new
        {
            method.Id,
            method.AcceptedInputObjectTypes,
            method.AcceptedAssetTypes,
            method.ProducibleProperties,
            method.LimitationKeys,
            method.RequiresReferenceSample,
            method.RequiresChainOfCustody,
            method.ValidResultLayouts,
            Localization = localization
        });
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("ForensicOutcome", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = prompts.RenderUser("ForensicOutcome", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["outcome_stub_json"] = System.Text.Json.JsonSerializer.Serialize(stub),
            ["supported_clues_json"] = System.Text.Json.JsonSerializer.Serialize(supportedClues),
            ["input_asset_json"] = System.Text.Json.JsonSerializer.Serialize(inputAsset),
            ["input_asset_spec_json"] = System.Text.Json.JsonSerializer.Serialize(inputAssetSpec),
            ["input_object_json"] = System.Text.Json.JsonSerializer.Serialize(inputObject),
            ["method_contract_json"] = methodContractJson
        });

        var output = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, $"ForensicOutcome:{stub.InputAssetId}:{stub.AnalysisType}", system, user, Schema, ct);
        output.ConclusionText = NormalizeDuplicateOffsets(output.ConclusionText);
        if (output.ResultAsset is not null) NormalizeDuplicateOffsets(output.ResultAsset);
        if (output.ResultEmail is not null) NormalizeDuplicateOffsets(output.ResultEmail);
        ScrubInternalSuspectIds(output, draft);

        if (output.ResultAsset?.BodyDoc is not null)
            output.ResultAsset.BodyDoc.Language = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language);
        if (output.ResultAsset is not null && method is not null && localization is not null)
        {
            output.ResultAsset.BodyDoc ??= new EvidenceDocument
            {
                Layout = stub.ResultLayoutId,
                Language = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
                Title = output.ResultAsset.Title
            };
            output.ResultAsset.BodyDoc.Sections.Add(new EvidenceSection
            {
                Kind = "keyValue",
                Heading = localization.Labels["itemsSubmitted"],
                Items =
                [
                    new EvidenceKeyValue
                    {
                        Label = localization.Labels["itemsSubmitted"],
                        Value = stub.InputObjectId
                    }
                ]
            });
            if (supportedClues.Count > 0)
            {
                output.ResultAsset.BodyDoc.Sections.Add(new EvidenceSection
                {
                    Kind = "narrative",
                    Heading = localization.Labels.GetValueOrDefault("findings", "Findings"),
                    Text = string.Join("\n", supportedClues.Select(clue => $"- {clue.Discovery}"))
                });
            }
            output.ResultAsset.BodyDoc.Sections.Add(new EvidenceSection
            {
                Kind = "narrative",
                Heading = localization.Labels["limitations"],
                Text = string.Join("\n", stub.LimitationKeys
                    .Where(localization.Limitations.ContainsKey)
                    .Select(key => $"- {localization.Limitations[key]}"))
            });
        }
        if (output.ResultAsset is not null
            && output.ResultEmail is not null
            && !output.ResultEmail.Attachments.Contains(output.ResultAsset.Id, StringComparer.Ordinal))
            output.ResultEmail.Attachments.Add(output.ResultAsset.Id);

        var full = new ForensicsOutcome
        {
            InputAssetId = stub.InputAssetId,
            AnalysisType = stub.AnalysisType,
            Findings = stub.Findings,
            MatchedSuspectId = stub.MatchedSuspectId,
            ConclusionText = output.ConclusionText,
            ResultAssetId = output.ResultAsset?.Id,
            ResultEmailId = output.ResultEmail?.Id
        };

        if (stub.Findings && output.ResultAsset is null)
            _logger.LogWarning("Outcome {Stub} has findings=true but no resultAsset emitted", $"{stub.InputAssetId}:{stub.AnalysisType}");
        if (stub.Findings && output.ResultEmail is null)
            _logger.LogWarning("Outcome {Stub} has findings=true but no resultEmail emitted", $"{stub.InputAssetId}:{stub.AnalysisType}");

        // Force hidden visibility on lab results.
        if (output.ResultAsset is not null) output.ResultAsset.Visibility = "hidden";
        if (output.ResultEmail is not null) output.ResultEmail.Visibility = "hidden";

        return (full, output.ResultAsset, output.ResultEmail);
    }

    private static string? NormalizeDuplicateOffsets(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"([+-]\d{2}:\d{2})\1",
            "$1",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static void NormalizeDuplicateOffsets(EvidenceAsset asset)
    {
        asset.Title = NormalizeDuplicateOffsets(asset.Title) ?? string.Empty;
        asset.Description = NormalizeDuplicateOffsets(asset.Description);
        asset.Body = NormalizeDuplicateOffsets(asset.Body);
        if (asset.BodyDoc is null) return;
        asset.BodyDoc.Title = NormalizeDuplicateOffsets(asset.BodyDoc.Title) ?? string.Empty;
        asset.BodyDoc.Subtitle = NormalizeDuplicateOffsets(asset.BodyDoc.Subtitle);
        foreach (var item in asset.BodyDoc.Header)
            item.Value = NormalizeDuplicateOffsets(item.Value) ?? string.Empty;
        foreach (var section in asset.BodyDoc.Sections)
        {
            section.Text = NormalizeDuplicateOffsets(section.Text);
            if (section.Table is not null)
                foreach (var row in section.Table.Rows)
                    for (var i = 0; i < row.Count; i++)
                        row[i] = NormalizeDuplicateOffsets(row[i]) ?? string.Empty;
            if (section.Items is not null)
                foreach (var item in section.Items)
                    item.Value = NormalizeDuplicateOffsets(item.Value) ?? string.Empty;
            if (section.Transcript is not null)
                foreach (var entry in section.Transcript)
                {
                    entry.Timestamp = NormalizeDuplicateOffsets(entry.Timestamp);
                    entry.Text = NormalizeDuplicateOffsets(entry.Text) ?? string.Empty;
                }
            if (section.Code is not null)
                section.Code.Content = NormalizeDuplicateOffsets(section.Code.Content) ?? string.Empty;
        }
    }

    private static void NormalizeDuplicateOffsets(EvidenceEmail email)
    {
        email.Subject = NormalizeDuplicateOffsets(email.Subject) ?? string.Empty;
        email.Body = NormalizeDuplicateOffsets(email.Body) ?? string.Empty;
        email.SentAt = NormalizeDuplicateOffsets(email.SentAt) ?? string.Empty;
    }

    private static void ScrubInternalSuspectIds(Output output, CaseDraft draft)
    {
        foreach (var suspect in draft.SuspectFull)
        {
            output.ConclusionText = ReplaceInternalId(output.ConclusionText, suspect.Id, suspect.Name);
            if (output.ResultAsset is not null)
            {
                output.ResultAsset.Title = ReplaceInternalId(output.ResultAsset.Title, suspect.Id, suspect.Name) ?? string.Empty;
                output.ResultAsset.Description = ReplaceInternalId(output.ResultAsset.Description, suspect.Id, suspect.Name);
                output.ResultAsset.Body = ReplaceInternalId(output.ResultAsset.Body, suspect.Id, suspect.Name);
                if (output.ResultAsset.BodyDoc is not null)
                {
                    foreach (var item in output.ResultAsset.BodyDoc.Header)
                        item.Value = ReplaceInternalId(item.Value, suspect.Id, suspect.Name) ?? string.Empty;
                    foreach (var section in output.ResultAsset.BodyDoc.Sections)
                    {
                        section.Text = ReplaceInternalId(section.Text, suspect.Id, suspect.Name);
                        if (section.Table is not null)
                            foreach (var row in section.Table.Rows)
                                for (var i = 0; i < row.Count; i++)
                                    row[i] = ReplaceInternalId(row[i], suspect.Id, suspect.Name) ?? string.Empty;
                        if (section.Items is not null)
                            foreach (var item in section.Items)
                                item.Value = ReplaceInternalId(item.Value, suspect.Id, suspect.Name) ?? string.Empty;
                        if (section.Transcript is not null)
                            foreach (var entry in section.Transcript)
                                entry.Text = ReplaceInternalId(entry.Text, suspect.Id, suspect.Name) ?? string.Empty;
                        if (section.Code is not null)
                            section.Code.Content = ReplaceInternalId(section.Code.Content, suspect.Id, suspect.Name) ?? string.Empty;
                    }
                }
            }
            if (output.ResultEmail is not null)
            {
                output.ResultEmail.Subject = ReplaceInternalId(output.ResultEmail.Subject, suspect.Id, suspect.Name) ?? string.Empty;
                output.ResultEmail.Body = ReplaceInternalId(output.ResultEmail.Body, suspect.Id, suspect.Name) ?? string.Empty;
            }
        }
    }

    private static string? ReplaceInternalId(string? value, string internalId, string displayName) =>
        value?.Replace(internalId, displayName, StringComparison.OrdinalIgnoreCase);
}

public static class ForensicResultIdNormalizer
{
    public static void EnsureUnique(
        IEnumerable<(ForensicsOutcome full, EvidenceAsset? asset, EvidenceEmail? email)> details,
        IEnumerable<string> reservedAssetIds,
        IEnumerable<string> reservedEmailIds)
    {
        var assetIds = reservedAssetIds.ToHashSet(StringComparer.Ordinal);
        var emailIds = reservedEmailIds.ToHashSet(StringComparer.Ordinal);
        foreach (var (full, asset, email) in details)
        {
            if (asset is not null)
            {
                var previousId = asset.Id;
                asset.Id = UniqueId(asset.Id, assetIds);
                full.ResultAssetId = asset.Id;
                if (email is not null && asset.Id != previousId)
                {
                    for (var index = 0; index < email.Attachments.Count; index++)
                    {
                        if (email.Attachments[index] == previousId)
                            email.Attachments[index] = asset.Id;
                    }
                }
            }

            if (email is not null)
            {
                email.Id = UniqueId(email.Id, emailIds);
                full.ResultEmailId = email.Id;
            }
        }
    }

    private static string UniqueId(string baseId, ISet<string> usedIds)
    {
        var id = baseId;
        for (var suffix = 2; !usedIds.Add(id); suffix++)
            id = $"{baseId}_{suffix}";
        return id;
    }
}
