using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Legacy forensic planning; the canonical pipeline plans pairs in CaseBible.</summary>
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

    private static readonly string Schema = MicrotaskSchemas.ForensicsPlan;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are planning the **forensics layer** for an in-progress case.
1. Define 2-4 `analysisTypes` (PascalCase names like ""DigitalForensics"", ""Fingerprint"", ""DNA"", ""Ballistics"") with realistic `durationMinutes` and `availableFor` lists keyed off the asset `type` strings.
2. List 1-5 `outcomes` — each is (inputAssetId × analysisType) plus a boolean `findings`, optional `matchedSuspectId`, and a ONE-LINE role.
3. Outcomes contribute to a collective body of proof; no single culprit match is mandatory.
`matchedSuspectId` requires an individualizing method, never visual similarity. A no-findings outcome cannot prove innocence.
Use only asset IDs that exist in the draft.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToReviewJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "ForensicsPlan", system, user, Schema, ct,
            validate: output =>
            {
                MicrotaskValidation.Unique(output.AnalysisTypes.Select(t => t.Type), "analysisTypes");
                MicrotaskValidation.Unique(output.Outcomes.Select(x => $"{x.InputAssetId}:{x.AnalysisType}"), "outcomes");
                var suspects = draft.SuspectStubs.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var outcome in output.Outcomes)
                {
                    var asset = draft.AssetFull.SingleOrDefault(a => a.Id == outcome.InputAssetId);
                    TaskContractException.Require(asset is not null, $"Unknown forensic input '{outcome.InputAssetId}'.");
                    TaskContractException.Require(output.AnalysisTypes.Any(t =>
                        t.Type == outcome.AnalysisType && t.AvailableFor.Contains(asset!.Type)),
                        $"Analysis '{outcome.AnalysisType}' is not available for '{outcome.InputAssetId}'.");
                    TaskContractException.Require(outcome.MatchedSuspectId is null || suspects.Contains(outcome.MatchedSuspectId),
                        $"Unknown matched suspect '{outcome.MatchedSuspectId}'.");
                    TaskContractException.Require(outcome.Findings || outcome.MatchedSuspectId is null,
                        "A no-findings outcome cannot identify a matched suspect.");
                }
            });

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

    public class Output : CaseAuthoredContent
    {
        [JsonPropertyName("conclusionText")] public string? ConclusionText { get; set; }
        [JsonPropertyName("resultAsset")] public EvidenceAsset? ResultAsset { get; set; }
        [JsonPropertyName("resultEmail")] public EvidenceEmail? ResultEmail { get; set; }
    }

    private static readonly string Schema = MicrotaskSchemas.ForensicOutcome;

    public async Task<(ForensicsOutcome full, EvidenceAsset? asset, EvidenceEmail? email)> RunAsync(CaseDraft draft, ForensicOutcomeStub stub, CancellationToken ct)
    {
        var system = $@"You are writing the **detailed forensic outcome** for ONE (inputAssetId × analysisType) pair.

Produce:
- `conclusionText`: 1-3 sentence neutral lab conclusion. If `findings == false`, keep it dry (no actionable lead).
- `resultAsset` (ONLY if `findings == true`): a hidden PDF report (`asset.report_<slug>`). MUST include:
    * `title` — official short report name
    * `description` — 1-2 sentence summary visible in case-file listing
    * `body` — markdown long-form (8-20 paragraphs) that a PDF renderer will use. Include sections: Chain of Custody, Methodology, Findings, Conclusion. Dates and times MUST align with the case's `incidentDate`/`openedAt`.
- `resultEmail` (ONLY if `findings == true`): a hidden email from the lab (`email.lab_<slug>`). Body is 3-5 paragraph markdown summarising the report.
If `findings == false`, set `resultAsset` and `resultEmail` to null. Negative findings never independently clear a suspect.
Include support for the conclusion and separately for each authored report/email.
{CaseBibleAuthorship.Instructions}";
        var plan = draft.Bible?.Outcomes.Single(o => o.InputAssetId == stub.InputAssetId && o.AnalysisType == stub.AnalysisType);
        var sourceIds = new[] { plan?.ResultAssetId, plan?.ResultEmailId }.OfType<string>().ToArray();
        var context = draft.Bible is null ? draft.ToReviewJson() : sourceIds.Length > 0
            ? CaseBibleAuthorship.Context(draft, sourceIds)
            : System.Text.Json.JsonSerializer.Serialize(new
            {
                language = draft.Request.Language ?? "en-US", limitations = plan!.Limitations,
                facts = draft.Bible.Facts.Where(f => plan.FactIds.Contains(f.Id))
                    .Select(f => new { f.Id, f.Statement, f.Kind, f.Time })
            }, CaseDraft.CanonicalJsonOptions);
        var user = $@"AUTHOR'S READ-ONLY CONTEXT:
{context}

OUTCOME TO DETAIL:
inputAssetId={stub.InputAssetId}
analysisType={stub.AnalysisType}
findings={stub.Findings}
matchedSuspectId={stub.MatchedSuspectId ?? "null"}
role={stub.Role}

Emit JSON only.";

        var output = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, $"ForensicOutcome:{stub.InputAssetId}:{stub.AnalysisType}", system, user, Schema, ct,
            validate: o =>
            {
                if (plan is not null)
                    CaseBibleAuthorship.Support(o.Support, plan.FactIds, o.ConclusionText ?? "", "forensic conclusion");
                if (!stub.Findings)
                {
                    TaskContractException.Require(o.ResultAsset is null && o.ResultEmail is null,
                        "A no-findings outcome must have null resultAsset and resultEmail.");
                    return;
                }
                TaskContractException.Require(o.ResultAsset is not null && o.ResultEmail is not null,
                    "An outcome with findings requires both resultAsset and resultEmail.");
                var asset = o.ResultAsset!;
                var email = o.ResultEmail!;
                TaskContractException.Require(asset.Id.StartsWith("asset.report_", StringComparison.Ordinal) &&
                    !MicrotaskValidation.AssetIds(draft).Contains(asset.Id), "Result asset ID must be a new asset.report_<slug>.");
                TaskContractException.Require(asset.Type is "pdf" or "document", "A forensic result must be a PDF/document.");
                MicrotaskValidation.Equal(asset.Visibility, "hidden", "resultAsset.visibility");
                MicrotaskValidation.Asset(asset);
                if (plan is not null)
                {
                    MicrotaskValidation.Equal(asset.Id, plan.ResultAssetId!, "resultAsset.id");
                    MicrotaskValidation.Equal(email.Id, plan.ResultEmailId!, "resultEmail.id");
                    CaseBibleAuthorship.Asset(draft, asset);
                    CaseBibleAuthorship.Email(draft, email);
                }
                TaskContractException.Require(email.Id.StartsWith("email.lab_", StringComparison.Ordinal) &&
                    !MicrotaskValidation.EmailIds(draft).Contains(email.Id), "Result email ID must be a new email.lab_<slug>.");
                MicrotaskValidation.Equal(email.Visibility, "hidden", "resultEmail.visibility");
                var assets = MicrotaskValidation.AssetIds(draft);
                assets.Add(asset.Id);
                TaskContractException.Require(email.Attachments.All(assets.Contains), "Result email contains unknown attachments.");
            });

        var full = new ForensicsOutcome
        {
            InputAssetId = stub.InputAssetId,
            AnalysisType = stub.AnalysisType,
            Findings = stub.Findings,
            MatchedSuspectId = stub.MatchedSuspectId,
            ConclusionText = output.ConclusionText,
            ResultAssetId = output.ResultAsset?.Id,
            ResultEmailId = output.ResultEmail?.Id,
            Support = output.Support
        };

        return (full, output.ResultAsset, output.ResultEmail);
    }
}
