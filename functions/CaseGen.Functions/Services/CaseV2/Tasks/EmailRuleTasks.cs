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
               "properties":{
                 "type":{"type":"string","enum":["reveal_email","reveal_asset","reveal_suspect","add_email_attachment","send_notification","update_suspect_status","mark_alibi_verified"]},
                 "status":{"type":["string","null"],"enum":["suspect","cleared","confirmed_culprit",null]},
                 "level":{"type":["string","null"],"enum":["info","warn","critical",null]}
               }}}}}}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        // Mechanical reveal rules (forensics_complete → reveal_email/asset) are pre-built
        // in code by MechanicalRulesBuilder before this task runs. Here we ask the LLM
        // ONLY for narrative rules — notifications, alibi nudges, multi-condition beats —
        // so it can never drop or duplicate the critical reveal chain.

        var unlockMode = string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase)
            ? "all_initial" : "gated";

        var existingRulesSummary = draft.Rules.Select(r => new
        {
            r.RuleId,
            trigger = r.Trigger.Type,
            actions = r.Actions.Select(a => a.Type).ToList()
        });

        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            culpritId = draft.CulpritId,
            unlockMode,
            assets = draft.AssetStubs.Select(a => a.Id),
            resultAssets = draft.ResultAssets.Select(a => a.Id),
            emails = draft.FollowUpEmails.Select(e => e.Id),
            resultEmails = draft.ResultEmails.Select(e => e.Id),
            suspects = draft.SuspectFull.Select(s => new { s.Id, s.Name, isCulprit = s.Id == draft.CulpritId, s.AlibiVerified }),
            briefingEmailId = "email.briefing",
            preBuiltRules = existingRulesSummary
        });

        var system = @"You are writing ONLY the narrative `rules` for the case runtime.
Mechanical reveal rules (forensics_complete → reveal_email / reveal_asset) ALREADY EXIST in the case
(see `preBuiltRules` in the context). DO NOT re-emit them — emit ONLY new rules that add narrative beats.

ALLOWED trigger.type values (use EXACTLY these strings, no others):
  forensics_complete, attachment_download, asset_viewed, email_opened, time_elapsed, suspect_viewed, multiple_conditions

ALLOWED action.type values:
  reveal_email, reveal_asset, reveal_suspect, add_email_attachment, send_notification, update_suspect_status, mark_alibi_verified

Sensible narrative rules to consider (emit 0-4 total — quality over quantity):
- `email_opened` of a key lab-result email → `send_notification` (level info) flagging the matched suspect.
- `email_opened` of the briefing → `mark_alibi_verified` for a decoy whose alibi was independently verified up-front.
- `asset_viewed` of a turning-point asset → `send_notification` (level info or warn) nudging the next step.
- `multiple_conditions` AND/OR composing two earlier triggers when the case naturally needs a delayed reveal.

You may emit ZERO rules if the case doesn't need any extra narrative beats. NEVER duplicate a preBuiltRule.
All referenced IDs MUST exist in the supplied context.";

        var user = $@"CONTEXT:
{ctxJson}

Emit JSON only. The `rules` array contains ONLY new narrative rules (do not echo preBuiltRules).";

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Rules", system, user, Schema, ct);

        // Append new rules to the pre-built ones, skipping duplicates.
        var existingIds = draft.Rules.Select(r => r.RuleId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in o.Rules)
        {
            // Defensive: coerce `rule_xxx` (underscore) → `rule.xxx` (dot) if the LLM drifts.
            if (r.RuleId.StartsWith("rule_", StringComparison.Ordinal))
                r.RuleId = "rule." + r.RuleId.Substring(5);
            if (r.RuleId.StartsWith("rule-", StringComparison.Ordinal))
                r.RuleId = "rule." + r.RuleId.Substring(5);
            if (string.IsNullOrEmpty(r.RuleId) || !existingIds.Add(r.RuleId)) continue;
            draft.Rules.Add(r);
        }
    }
}
