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

    private static readonly string Schema = MicrotaskSchemas.BriefingEmail;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = $@"You are writing ONLY the briefing email from the chief of police, addressed to Detective Alex Morgan.
4-6 paragraph markdown body. Urge discretion. Reference only the source's known observations. Do NOT name a culprit.
{CaseBibleAuthorship.Instructions}";
        var context = draft.Bible is null ? draft.ToSummaryJson() : CaseBibleAuthorship.Context(draft, "email.briefing");
        var user = $@"AUTHOR'S READ-ONLY CONTEXT:
{context}

Emit JSON only with from / subject / body / support.";
        var b = await TaskRunner.RunStructuredAsync<PlotBriefingEmail>(_llm, _logger, "BriefingEmail", system, user, Schema, ct,
            validate: email =>
            {
                if (draft.Bible is null) return;
                var plan = draft.Bible.Sources.Single(s => s.Id == "email.briefing");
                MicrotaskValidation.Equal(email.From, plan.From!, "briefing.from");
                MicrotaskValidation.Equal(email.Subject, plan.Title, "briefing.subject");
                CaseBibleAuthorship.Source(draft, "email.briefing", email.Support, email.Body);
            });
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

    private static readonly string Schema = MicrotaskSchemas.InitialEmails;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        if (draft.Bible is not null)
        {
            var sources = draft.Bible.Sources.Where(s => s.Kind == "email").ToList();
            var results = await Task.WhenAll(sources.Select(async source =>
            {
                var output = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, $"InitialEmail:{source.Id}",
                    $"Write 0-3 OPTIONAL emails, but in this call emit EXACTLY ONE planned email.\n{CaseBibleAuthorship.Instructions}",
                    CaseBibleAuthorship.Context(draft, source.Id), Schema, ct, validate: o =>
                    {
                        TaskContractException.Require(o.Emails.Count == 1 && o.Emails[0].Id == source.Id, "Return exactly the planned email.");
                        CaseBibleAuthorship.Email(draft, o.Emails[0]);
                    });
                return output.Emails[0];
            }));
            draft.FollowUpEmails = results.ToList();
            return;
        }
        var system = @"You are writing 0-3 OPTIONAL initial-visibility emails from witnesses, partner, or the chief (NOT forensic-lab results).
All emails are `visibility: initial` — they show up immediately. If the case doesn't naturally need any, emit `emails: []`.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToReviewJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "InitialEmails", system, user, Schema, ct,
            validate: output =>
            {
                MicrotaskValidation.Unique(output.Emails.Select(e => e.Id)
                    .Concat(MicrotaskValidation.EmailIds(draft)), "emails");
                foreach (var email in output.Emails)
                {
                    MicrotaskValidation.Equal(email.Visibility, "initial", "email.visibility");
                    MicrotaskValidation.Email(email, draft);
                }
            });
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

    private static readonly string Schema = MicrotaskSchemas.Rules;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        // Mechanical reveal rules (forensics_complete → reveal_email/asset) are pre-built
        // in code by MechanicalRulesBuilder before this task runs. Here we ask the LLM
        // ONLY for narrative rules — notifications, alibi nudges, multi-condition beats —
        // so it can never drop or duplicate the critical reveal chain.

        var unlockMode = string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase)
            ? "all_initial" : "gated";

        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            language = draft.Request.Language ?? "en-US",
            unlockMode,
            assets = draft.AssetStubs.Select(a => a.Id),
            resultAssets = draft.ResultAssets.Select(a => a.Id),
            emails = draft.FollowUpEmails.Select(e => e.Id),
            resultEmails = draft.ResultEmails.Select(e => e.Id),
            suspects = draft.SuspectFull.Select(s => new { s.Id, s.Name, s.AlibiVerified }),
            briefingEmailId = "email.briefing",
            analysisTypes = draft.AnalysisTypes,
            preBuiltRules = draft.Rules,
            canonical = draft.Bible is null ? draft.GetComponents() : System.Text.Json.Nodes.JsonNode.Parse(
                CaseBibleAuthorship.Context(draft, draft.Bible.Sources.Where(s => s.Type != "profile").Select(s => s.Id).ToArray()))
        });

        var system = @"You are writing ONLY the narrative `rules` for the case runtime.
Mechanical reveal rules (forensics_complete → reveal_email / reveal_asset) ALREADY EXIST in the case
(see `preBuiltRules` in the context). DO NOT re-emit them — emit ONLY new rules that add narrative beats.

ALLOWED trigger.type values (use EXACTLY these strings, no others):
  forensics_complete, attachment_download, asset_viewed, email_opened, time_elapsed, suspect_viewed, multiple_conditions

ALLOWED action.type values:
  reveal_email, reveal_asset, reveal_suspect, add_email_attachment, send_notification, update_suspect_status, mark_alibi_verified

Sensible narrative rules to consider (emit 0-4 total — quality over quantity):
- `email_opened` of a key lab-result email → `send_notification` (level info) suggesting examination of its documented findings.
- Independently supported alibi verification is already in the public profiles; do not invent verification or clear suspects.
- `asset_viewed` of a turning-point asset → `send_notification` (level info or warn) nudging the next step.
- `multiple_conditions` AND/OR composing two earlier triggers when the case naturally needs a delayed reveal.

You may emit ZERO rules if the case doesn't need any extra narrative beats. NEVER duplicate a preBuiltRule.
All referenced IDs MUST exist in the supplied context.
When caseBible is present, all discovery and alibi support is already compiled. Emit ONLY neutral send_notification
actions, never a new reveal, guilt announcement, exoneration or verification shortcut.";

        var user = $@"CONTEXT:
{ctxJson}

Emit JSON only. The `rules` array contains ONLY new narrative rules (do not echo preBuiltRules).";

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Rules", system, user, Schema, ct,
            validate: output =>
            {
                MicrotaskValidation.Rules(output.Rules, draft);
                if (draft.Bible is not null)
                    TaskContractException.Require(output.Rules.SelectMany(r => r.Actions).All(a => a.Type == "send_notification"),
                        "Canonical discovery/verification cannot be rewritten by narrative rules.");
            });
        draft.Rules.AddRange(o.Rules);
    }
}
