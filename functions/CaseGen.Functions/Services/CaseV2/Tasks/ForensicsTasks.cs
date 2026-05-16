using System.Text.Json.Serialization;
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
        [JsonPropertyName("analysisTypes")] public List<ForensicsAnalysisType> AnalysisTypes { get; set; } = new();
        [JsonPropertyName("outcomes")] public List<ForensicOutcomeStub> Outcomes { get; set; } = new();
    }

    private const string Schema = """
    {
      "type":"object","required":["analysisTypes","outcomes"],
      "properties":{
        "analysisTypes":{"type":"array","minItems":2,"maxItems":4,
          "items":{"type":"object","required":["type","durationMinutes","availableFor"]}},
        "outcomes":{"type":"array","minItems":1,"maxItems":5,
          "items":{"type":"object","required":["inputAssetId","analysisType","findings","role"],
            "properties":{
              "inputAssetId":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"},
              "matchedSuspectId":{"type":["string","null"],"pattern":"^suspect\\.[a-z0-9_]+$"}}}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are planning the **forensics layer** for an in-progress case.
1. Define 2-4 `analysisTypes` (PascalCase names like ""DigitalForensics"", ""Fingerprint"", ""DNA"", ""Ballistics"") with realistic `durationMinutes` and `availableFor` lists keyed off the asset `type` strings.
2. List 1-5 `outcomes` — each is (inputAssetId × analysisType) plus a boolean `findings`, optional `matchedSuspectId`, and a ONE-LINE role.
3. EXACTLY ONE outcome MUST have `findings: true` AND `matchedSuspectId == culpritId` — that is the smoking gun.
Other outcomes may have `findings: false` or implicate decoys (with lower confidence — `matchedSuspectId` may be set but ambiguous).
Use only asset IDs that exist in the draft.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "ForensicsPlan", system, user, Schema, ct);

        // Enforce exactly one smoking gun.
        var sg = o.Outcomes.FirstOrDefault(x => x.Findings && x.MatchedSuspectId == draft.CulpritId);
        if (sg is null)
        {
            var first = o.Outcomes.FirstOrDefault(x => x.Findings) ?? o.Outcomes.FirstOrDefault();
            if (first is not null)
            {
                first.Findings = true;
                first.MatchedSuspectId = draft.CulpritId;
                _logger.LogWarning("ForensicsPlan had no smoking gun — forced {Outcome} to be the smoking gun for culprit {Culprit}",
                    $"{first.InputAssetId}:{first.AnalysisType}", draft.CulpritId);
            }
        }

        draft.AnalysisTypes = o.AnalysisTypes;
        draft.ForensicStubs = o.Outcomes;
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
        "resultAsset":{"type":["object","null"],"properties":{"id":{"type":"string","pattern":"^asset\\.report_[a-z0-9_]+$"},"type":{"type":"string","enum":["pdf","document"]},"title":{"type":"string"},"description":{"type":"string"},"body":{"type":"string"},"bodyDoc":{"type":["object","null"]},"visibility":{"type":"string","enum":["hidden"]}}},
        "resultEmail":{"type":["object","null"],"properties":{"id":{"type":"string","pattern":"^email\\.lab_[a-z0-9_]+$"},"from":{"type":"string"},"subject":{"type":"string"},"body":{"type":"string"},"sentAt":{"type":"string"},"visibility":{"type":"string","enum":["hidden"]}}}
      }
    }
    """;

    public async Task<(ForensicsOutcome full, EvidenceAsset? asset, EvidenceEmail? email)> RunAsync(CaseDraft draft, ForensicOutcomeStub stub, CancellationToken ct)
    {
        var system = @"You are writing the **detailed forensic outcome** for ONE (inputAssetId × analysisType) pair.

Produce:
- `conclusionText`: 1-3 sentence neutral lab conclusion. If `findings == false`, keep it dry (no actionable lead).
- `resultAsset` (ONLY if `findings == true`): a hidden PDF report (`asset.report_<slug>`). MUST include:
    * `title` — official short report name
    * `description` — 1-2 sentence summary visible in case-file listing
    * `bodyDoc` — STRUCTURED EvidenceDocument with `layout: ""ForensicReport""` (preferred) or `MedicalReport` if it is an autopsy / ME finding. Use sections in this order: Items Submitted (keyValue), Methodology (narrative), Findings (narrative + table if applicable), Conclusions (narrative), Limitations (narrative or callout). Include a `signature` block (Examiner). Anchor all dates/times to `incidentDate`/`openedAt`. Leave `body` as empty string.
- `resultEmail` (ONLY if `findings == true`): a hidden email from the lab (`email.lab_<slug>`). Body is 3-5 paragraph markdown summarising the report.
If `findings == false`, set `resultAsset` and `resultEmail` to null.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

OUTCOME TO DETAIL:
inputAssetId={stub.InputAssetId}
analysisType={stub.AnalysisType}
findings={stub.Findings}
matchedSuspectId={stub.MatchedSuspectId ?? "null"}
role={stub.Role}

Emit JSON only.";

        var output = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, $"ForensicOutcome:{stub.InputAssetId}:{stub.AnalysisType}", system, user, Schema, ct);

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
}
