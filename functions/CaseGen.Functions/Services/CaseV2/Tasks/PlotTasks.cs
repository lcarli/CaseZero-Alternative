using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

public sealed class PlotOutlineTask(ILLMProvider llm, ILogger logger)
{
    private sealed class Output
    {
        [JsonPropertyName("metadata")] public PlotMetadata Metadata { get; set; } = new();
        [JsonPropertyName("clueOrderIds")] public List<string> ClueOrderIds { get; set; } = new();
        [JsonPropertyName("decoyOrderIds")] public List<string> DecoyOrderIds { get; set; } = new();
    }

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var bible = draft.CaseBible ?? throw new TaskContractException("PlotOutline requires a validated CaseBible.");
        var profile = DifficultyProfileCatalog.Get(draft.Request.Difficulty);
        var existingTitle = string.IsNullOrWhiteSpace(draft.Metadata.Title) ? null : draft.Metadata.Title;
        var fixedTitle = existingTitle ?? draft.Request.Title;
        var clueIds = existingTitle is null ? bible.DifficultyIntent.Clues.Select(c => c.Id).ToList()
            : draft.Blueprint.ClueLadder.Select(c => c.Id).ToList();
        var decoyIds = existingTitle is null ? bible.DifficultyIntent.DecoyArcs.Select(d => d.Id).ToList()
            : draft.Blueprint.RedHerrings.Select(d => d.Id).ToList();
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("PlotOutline", new Dictionary<string, object?>
        {
            ["difficulty"] = profile.Name, ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = prompts.RenderUser("PlotOutline", new Dictionary<string, object?>
        {
            ["case_id"] = draft.CaseId, ["title_hint"] = fixedTitle ?? "(model chooses)",
            ["required_rank"] = CaseBibleNormalizer.CanonicalRequiredRank(draft),
            ["source_context_json"] = CaseBibleAuthorship.Context(draft, "email.briefing"),
            ["presentation_ids_json"] = JsonSerializer.Serialize(new
            {
                clueIds, decoyIds, preserveOrder = existingTitle is not null
            })
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, "PlotOutline", system, user, MicrotaskSchemas.PlotOutline, ct,
            validate: result =>
            {
                MicrotaskValidation.Equal(result.Metadata.Difficulty, profile.Name, "metadata.difficulty");
                MicrotaskValidation.Equal(result.Metadata.RequiredRank, CaseBibleNormalizer.CanonicalRequiredRank(draft), "metadata.requiredRank");
                if (fixedTitle is not null) MicrotaskValidation.Equal(result.Metadata.Title, fixedTitle, "metadata.title");
                MicrotaskValidation.Unique(result.ClueOrderIds, "clueOrderIds");
                MicrotaskValidation.Unique(result.DecoyOrderIds, "decoyOrderIds");
                TaskContractException.Require(result.ClueOrderIds.ToHashSet(StringComparer.Ordinal).SetEquals(bible.DifficultyIntent.Clues.Select(c => c.Id))
                    && result.DecoyOrderIds.ToHashSet(StringComparer.Ordinal).SetEquals(bible.DifficultyIntent.DecoyArcs.Select(d => d.Id)),
                    "Presentation order must contain every canonical clue and decoy ID exactly once.");
                if (existingTitle is not null)
                    TaskContractException.Require(result.ClueOrderIds.SequenceEqual(clueIds) && result.DecoyOrderIds.SequenceEqual(decoyIds),
                        "Presentation repair must preserve the existing canonical clue and decoy order.");
            });
        draft.Metadata = output.Metadata;
        CaseBibleNormalizer.ProjectIntoDraft(draft, output.ClueOrderIds, output.DecoyOrderIds);
    }
}

public sealed class SuspectCardTask(ILLMProvider llm, ILogger logger)
{
    public async Task<PlotSuspect> RunAsync(CaseDraft draft, SuspectStub stub, CancellationToken ct, string? repairGuidance = null)
    {
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("SuspectCard", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language), ["authorship_instructions"] = CaseBibleAuthorship.Instructions
        });
        var user = prompts.RenderUser("SuspectCard", new Dictionary<string, object?>
        {
            ["source_context_json"] = CaseBibleAuthorship.ProfileContext(draft, stub.Id)
        });
        return await TaskRunner.RunStructuredAsync<PlotSuspect>(llm, logger, $"SuspectCard:{stub.Id}", system, user,
            MicrotaskSchemas.SuspectCard, ct, validate: suspect =>
            {
                MicrotaskValidation.Equal(suspect.Id, stub.Id, "suspect.id");
                TaskContractException.Require(!suspect.AlibiVerified, "A profile author's own stated alibi is not independently verified.");
                CaseBibleAuthorship.Suspect(draft, suspect);
            });
    }
}
