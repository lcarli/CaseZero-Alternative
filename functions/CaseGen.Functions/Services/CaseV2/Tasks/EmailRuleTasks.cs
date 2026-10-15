using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

public class BriefingEmailTask(ILLMProvider llm, ILogger logger)
{
    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var bible = draft.CaseBible ?? throw new TaskContractException("Briefing requires the canonical opening observations.");
        var investigator = bible.People.Single(p => p.Id == bible.World.InvestigatorPersonId);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("BriefingEmail", new Dictionary<string, object?>
        {
            ["police_agency"] = draft.Blueprint.Locale.PoliceAgency, ["investigator_name"] = investigator.Name,
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["authorship_instructions"] = CaseBibleAuthorship.Instructions
        });
        var user = prompts.RenderUser("BriefingEmail", new Dictionary<string, object?>
        {
            ["source_context_json"] = CaseBibleAuthorship.Context(draft, "email.briefing"),
            ["email_plan_json"] = JsonSerializer.Serialize(new { from = investigator.Email, subject = draft.Metadata.Title })
        });
        draft.Briefing = await TaskRunner.RunStructuredAsync<PlotBriefingEmail>(llm, logger, "BriefingEmail", system, user,
            MicrotaskSchemas.BriefingEmail, ct, validate: email =>
            {
                MicrotaskValidation.Equal(email.From, investigator.Email!, "briefing.from");
                MicrotaskValidation.Equal(email.Subject, draft.Metadata.Title, "briefing.subject");
                CaseBibleAuthorship.Source(draft, "email.briefing", email.Support, email.Body);
            });
    }
}

public class InitialEmailsTask(ILLMProvider llm, ILogger logger)
{
    private sealed class Output
    {
        [JsonPropertyName("emails")] public List<EvidenceEmail> Emails { get; set; } = new();
    }

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var plans = CaseBibleAuthorship.InitialEmailPlans(draft);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("InitialEmails", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["is_rookie"] = DifficultyProfileCatalog.Get(draft).AllEvidenceInitial.ToString().ToLowerInvariant(),
            ["authorship_instructions"] = CaseBibleAuthorship.Instructions
        });
        var user = prompts.RenderUser("InitialEmails", new Dictionary<string, object?>
        {
            ["email_plans_json"] = JsonSerializer.Serialize(plans.Select(p => new
            {
                p.Id, p.From, p.Subject, p.SentAt, p.Attachments, visibility = "initial",
                sourceContext = JsonSerializer.Deserialize<JsonElement>(CaseBibleAuthorship.ObservationContext(draft, p.Observations))
            }), CaseDraft.CanonicalJsonOptions)
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, "InitialEmails", system, user,
            MicrotaskSchemas.InitialEmails, ct, validate: result =>
            {
                MicrotaskValidation.Unique(result.Emails.Select(e => e.Id), "initial emails");
                Require(result.Emails.Select(e => e.Id).ToHashSet(StringComparer.Ordinal).SetEquals(plans.Select(p => p.Id)),
                    "Initial emails must echo exactly the planned source correspondence.");
                foreach (var email in result.Emails) CaseBibleAuthorship.Email(draft, email);
            });
        draft.FollowUpEmails = output.Emails;
    }
}

public class RulesTask(ILLMProvider llm, ILogger logger)
{
    private sealed class Output
    {
        [JsonPropertyName("rules")] public List<RulesRule> Rules { get; set; } = new();
    }

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var unlockMode = DifficultyProfileCatalog.Get(draft).AllEvidenceInitial ? "all_initial" : "gated";
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("Rules", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language), ["unlock_mode"] = unlockMode
        });
        var user = prompts.RenderUser("Rules", new Dictionary<string, object?>
        {
            ["context_json"] = JsonSerializer.Serialize(new
            {
                unlockMode, assets = draft.AssetFull.Concat(draft.ResultAssets).Select(a => a.Id),
                emails = draft.FollowUpEmails.Concat(draft.ResultEmails).Select(e => e.Id).Append("email.briefing"),
                suspects = draft.SuspectFull.Select(s => new { s.Id, s.Name }),
                analysisTypes = draft.AnalysisTypes, preBuiltRules = draft.Rules
            })
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, "Rules", system, user, MicrotaskSchemas.Rules, ct,
            validate: result =>
            {
                MicrotaskValidation.Rules(result.Rules, draft);
                Require(result.Rules.SelectMany(r => r.Actions).All(a => a.Type == "send_notification"),
                    "Narrative rules cannot alter planned discovery, alibi verification or suspect status.");
                if (unlockMode == "all_initial")
                    Require(result.Rules.Count == 0, "Rookie has no interactive analysis or reveal gates.");
            });
        draft.Rules.AddRange(output.Rules);
    }
}
