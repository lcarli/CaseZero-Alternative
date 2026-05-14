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
     "properties":{"rules":{"type":"array","minItems":1,"maxItems":10,
       "items":{"type":"object","required":["ruleId","trigger","actions"],
         "properties":{"ruleId":{"type":"string","pattern":"^rule\\.[a-z0-9_]+$"}}}}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var revealCandidates = draft.ForensicFull
            .Where(f => f.Findings && (f.ResultEmailId is not null || f.ResultAssetId is not null))
            .Select(f => new { f.InputAssetId, f.AnalysisType, f.ResultEmailId, f.ResultAssetId });

        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            culpritId = draft.CulpritId,
            assets = draft.AssetStubs.Select(a => a.Id),
            resultAssets = draft.ResultAssets.Select(a => a.Id),
            emails = draft.FollowUpEmails.Select(e => e.Id),
            resultEmails = draft.ResultEmails.Select(e => e.Id),
            forensicReveals = revealCandidates,
            briefingEmailId = "email.briefing"
        });

        var system = @"You are writing the `rules` array for the case runtime.
Each rule fires at most once per session. Output ONE rule per forensicReveal (`trigger.type: forensics_complete`) whose actions reveal that outcome's resultEmail (and resultAsset if any).
You may also emit:
- a rule that fires `send_notification` (level=info) when the player opens a key result email and the matchedSuspect is the culprit;
- a rule that triggers off `email_opened` for the briefing email to set initial visibility status notes.
All IDs MUST exist in the supplied context.";

        var user = $@"CONTEXT:
{ctxJson}

Emit JSON only.";

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Rules", system, user, Schema, ct);
        draft.Rules = o.Rules;
    }
}
