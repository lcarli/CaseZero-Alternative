using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Stage 8 — Briefing Email (the chief-of-police email).</summary>
public class BriefingEmailTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public BriefingEmailTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private const string Schema = """
    {"type":"object","required":["from","subject","body"],
     "properties":{"from":{"type":"string"},"subject":{"type":"string"},"body":{"type":"string"}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are writing ONLY the briefing email from the chief of police, addressed to Detective Alex Morgan.
4-6 paragraph markdown body. Urge discretion. Reference the victim, location, and the urgency. Do NOT name the culprit.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

Emit JSON only with from / subject / body.";
        var b = await TaskRunner.RunStructuredAsync<PlotBriefingEmail>(_llm, _logger, "BriefingEmail", system, user, Schema, ct);
        draft.Briefing = b;
    }
}

/// <summary>Stage 9 — Initial follow-up emails (0-3) from witnesses, partner, chief follow-ups, etc.</summary>
public class InitialEmailsTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public InitialEmailsTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output { [JsonPropertyName("emails")] public List<EvidenceEmail> Emails { get; set; } = new(); }

    private const string Schema = """
    {"type":"object","required":["emails"],
     "properties":{"emails":{"type":"array","minItems":0,"maxItems":3,
       "items":{"type":"object","required":["id","from","subject","body","sentAt","visibility"],
         "properties":{
           "id":{"type":"string","pattern":"^email\\.[a-z0-9_]+$"},
           "visibility":{"type":"string","enum":["initial"]}}}}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are writing 0-3 OPTIONAL initial-visibility emails from witnesses, partner, or the chief (NOT forensic-lab results).
All emails are `visibility: initial` — they show up immediately. If the case doesn't naturally need any, emit `emails: []`.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "InitialEmails", system, user, Schema, ct);
        foreach (var e in o.Emails) e.Visibility = "initial";
        draft.FollowUpEmails = o.Emails;
    }
}

/// <summary>Stage 10 — Rules wiring (forensics_complete → reveal_email/asset, etc.).</summary>
public class RulesTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public RulesTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output { [JsonPropertyName("rules")] public List<RulesRule> Rules { get; set; } = new(); }

    private const string Schema = """
    {"type":"object","required":["rules"],
     "properties":{"rules":{"type":"array","minItems":0,"maxItems":10,
       "items":{"type":"object","required":["ruleId","trigger","actions"],
         "properties":{
           "ruleId":{"type":"string","pattern":"^rule\\.[a-z0-9_]+$"},
           "trigger":{"type":"object","required":["type"],
             "properties":{"type":{"type":"string","enum":["forensics_complete","attachment_download","asset_viewed","email_opened","time_elapsed","suspect_viewed","multiple_conditions"]}}},
           "actions":{"type":"array","minItems":1,"maxItems":6,
             "items":{"type":"object","required":["type"],
               "properties":{"type":{"type":"string","enum":["reveal_email","reveal_asset","reveal_suspect","add_email_attachment","send_notification","update_suspect_status","mark_alibi_verified"]}}}}}}}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var revealCandidates = draft.ForensicFull
            .Where(f => f.Findings && (f.ResultEmailId is not null || f.ResultAssetId is not null))
            .Select(f => new { f.InputAssetId, f.AnalysisType, f.ResultEmailId, f.ResultAssetId });

        var unlockMode = string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase)
            ? "all_initial" : "gated";

        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            culpritId = draft.CulpritId,
            unlockMode,
            assets = draft.AssetStubs.Select(a => a.Id),
            resultAssets = draft.ResultAssets.Select(a => a.Id),
            emails = draft.FollowUpEmails.Select(e => e.Id),
            resultEmails = draft.ResultEmails.Select(e => e.Id),
            forensicReveals = revealCandidates,
            briefingEmailId = "email.briefing"
        });

        var system = @"You are writing the `rules` array for the case runtime.

ALLOWED trigger.type values (use EXACTLY these strings, no others):
  forensics_complete, attachment_download, asset_viewed, email_opened, time_elapsed, suspect_viewed, multiple_conditions

ALLOWED action.type values (use EXACTLY these strings, no others):
  reveal_email, reveal_asset, reveal_suspect, add_email_attachment, send_notification, update_suspect_status, mark_alibi_verified

DO NOT invent new action types (e.g. `set_visibility` does NOT exist — use `reveal_email`/`reveal_asset`/`reveal_suspect` instead).

Each rule fires at most once per session. Emit AT LEAST one rule per `forensicReveals` entry (trigger `forensics_complete`
matching that inputAssetId+analysisType, actions reveal_email + reveal_asset for the result).

When `unlockMode == ""all_initial""` (Rookie cases), emit only 0-2 rules at most — mostly `send_notification` flavour for narrative
beats, because revealing is already automatic.

When `unlockMode == ""gated""`, emit a rule per forensicReveal plus optionally one rule that fires `send_notification` (level=info)
when the player opens a key result email pointing at the culprit, and one rule that `mark_alibi_verified` for a verified-decoy
suspect when the briefing email is opened.

All referenced IDs MUST exist in the supplied context.";

        var user = $@"CONTEXT:
{ctxJson}

Emit JSON only.";

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Rules", system, user, Schema, ct);
        draft.Rules = o.Rules;
    }
}
