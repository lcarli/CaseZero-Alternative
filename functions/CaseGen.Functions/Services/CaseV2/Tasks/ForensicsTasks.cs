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
        var system = @"You are planning the **forensics layer** for an in-progress case.
1. Choose only method IDs from the immutable forensic catalog supplied below. `availableFor` is informational output only and will be discarded; never invent or extend method compatibility.
2. List only useful outcomes — each is (inputAssetId × analysisType) plus `findings: true`, optional `matchedSuspectId`, a ONE-LINE role, and `supportsClueIds`.
3. EXACTLY ONE outcome MUST have `findings: true` AND `matchedSuspectId == culpritId` — that is the smoking gun.
Other outcomes may implicate a decoy with lower confidence or have `matchedSuspectId: null`, but they must still produce a useful result.
4. Use only asset IDs that exist in the draft. `inputObjectId` MUST be one of that asset spec's `containedObjectIds`. For every supported forensic clue, `inputAssetId` MUST be the unique asset whose `forensicInputClueIds` contains that clue ID. Never analyze an attachment, device, PDF, or record merely mentioned or pictured by the asset.
5. Every blueprint clue with `sourceType=forensic` MUST be assigned exactly once through `supportsClueIds`. Never assign a non-forensic clue.

SCIENTIFIC COMPATIBILITY:
- Follow the catalog's accepted asset types, accepted contained-object types, producible properties, reference-sample requirement, chain-of-custody requirement, limitation keys, and result layouts exactly.
- Every `producedProperties` value and `limitationKeys` value must come from the selected method.
- Never claim fingerprints, DNA, fibers, chemical traces, toolmarks, or other physical examination unless a future catalog method explicitly permits it.
- For a `forensicAttribution` clue, materialize the canonical B→A correlation: connect the opaque action/session/file identifier B to endpoint identifier A from the initial identity clue. Do not skip directly from generic timestamps or software names to a person.
- The matched suspect is an internal scoring reference. The lab conclusion may state an objective account/device/document match, but must not declare guilt.";
        var forensicClues = draft.Blueprint.ClueLadder
            .Where(clue => string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var desiredOutcomes = Math.Clamp(
            Math.Max(profile.MinAnalyses, forensicClues.Count),
            profile.MinAnalyses,
            profile.MaxAnalyses);
        system += $"\nEmit exactly {desiredOutcomes} useful outcomes, all with `findings: true`.";
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
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

IMMUTABLE FORENSIC METHOD CATALOG:
{catalogJson}
{(string.IsNullOrWhiteSpace(repairGuidance) ? string.Empty : $"\nREPAIR REQUIREMENTS:\n{repairGuidance}\n")}

Emit JSON only.";
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

    private static void NormalizeMethodContract(CaseDraft draft, ForensicOutcomeStub outcome)
    {
        var assetSpec = draft.CaseGraph.AssetSpecs.FirstOrDefault(spec => spec.Id == outcome.InputAssetId);
        if (assetSpec is null)
            return;
        var compatible = (from pair in assetSpec.ContainedObjectTypes
                          from method in ForensicMethodCatalog.All
                          where method.AcceptedAssetTypes.Contains(assetSpec.AssetType)
                                && method.AcceptedInputObjectTypes.Contains(pair.Value)
                                && !method.RequiresReferenceSample
                          let overlap = outcome.ProducedProperties.Count(method.ProducibleProperties.Contains)
                          orderby method.Id == outcome.AnalysisType descending,
                              overlap descending,
                              method.RequiresChainOfCustody,
                              method.Id
                          select (Method: method, ObjectId: pair.Key))
            .FirstOrDefault();
        if (compatible.Method is null)
            return;

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
                .FirstOrDefault(source => source.Id == outcome.InputAssetId)?
                .ObservationIds.FirstOrDefault();
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
        var system = @"You are writing the **detailed forensic outcome** for ONE (inputAssetId × analysisType) pair.

Produce:
- `conclusionText`: 1-3 sentence neutral lab conclusion. If `findings == false`, keep it dry (no actionable lead).
- `resultAsset` (ONLY if `findings == true`): a hidden PDF report (`asset.report_<slug>`). MUST include:
    * `title` — official short report name
    * `description` — 1-2 sentence summary visible in case-file listing
    * `bodyDoc` — STRUCTURED EvidenceDocument with `layout: ""ForensicReport""` (preferred) or `MedicalReport` if it is an autopsy / ME finding. Use sections in this order: Items Submitted (keyValue), Methodology (narrative), Findings (narrative + table if applicable), Conclusions (narrative), Limitations (narrative or callout). Include a `signature` block (Examiner). Anchor all dates/times to `incidentDate`/`openedAt`. Leave `body` as empty string.
- `resultEmail` (ONLY if `findings == true`): a hidden email from the lab (`email.lab_<slug>`). Body is 3-5 paragraph markdown summarising the report.
If `findings == false`, set `resultAsset` and `resultEmail` to null.
Every supplied canonical forensic clue must appear as a concrete objective observation in the result report. Preserve exact identifiers, values, timestamps, and limitations. Do not declare guilt.
The Items Submitted section must identify the exact `inputObjectId`. Use only properties and limitation statements allowed by the immutable method contract. Do not analyze anything merely mentioned or pictured by the input asset.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

OUTCOME TO DETAIL:
inputAssetId={stub.InputAssetId}
inputObjectId={stub.InputObjectId}
analysisType={stub.AnalysisType}
findings={stub.Findings}
matchedSuspectId={stub.MatchedSuspectId ?? "null"}
role={stub.Role}
supportsClueIds={string.Join(", ", stub.SupportsClueIds)}
producedProperties={string.Join(", ", stub.ProducedProperties)}
limitationKeys={string.Join(", ", stub.LimitationKeys)}
referenceSampleObjectId={stub.ReferenceSampleObjectId ?? "null"}
chainOfCustodyObservationId={stub.ChainOfCustodyObservationId ?? "null"}
resultLayoutId={stub.ResultLayoutId}

CANONICAL FORENSIC CLUES TO MATERIALIZE:
{System.Text.Json.JsonSerializer.Serialize(supportedClues)}

ACTUAL INPUT ASSET TO ANALYZE:
{System.Text.Json.JsonSerializer.Serialize(inputAsset)}

PRIVATE TYPED ASSET SPEC:
{System.Text.Json.JsonSerializer.Serialize(inputAssetSpec)}

EXACT CONTAINED OBJECT SUBMITTED:
{System.Text.Json.JsonSerializer.Serialize(inputObject)}

IMMUTABLE METHOD CONTRACT AND LOCALIZED LABELS:
{System.Text.Json.JsonSerializer.Serialize(method is null ? null : new
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
})}

Emit JSON only.";

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
