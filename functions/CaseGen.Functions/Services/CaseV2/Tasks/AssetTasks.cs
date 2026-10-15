using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

public class AssetPlanTask(ILLMProvider llm, ILogger logger)
{
    private sealed class Output
    {
        [JsonPropertyName("assets")] public List<AssetStub> Assets { get; set; } = new();
    }

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);
        var budget = EvidenceArchetypeCatalog.GetBudget(draft);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("AssetPlan", new Dictionary<string, object?>
        {
            ["difficulty"] = budget.Difficulty, ["portfolio_count"] = portfolio.Count,
            ["min_assets"] = budget.MinAssets, ["max_assets"] = budget.MaxAssets,
            ["min_investigative_assets"] = budget.MinInvestigativeAssets, ["max_investigative_assets"] = budget.MaxInvestigativeAssets,
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = prompts.RenderUser("AssetPlan", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["portfolio_json"] = JsonSerializer.Serialize(portfolio.Select(s => new
            {
                id = s.InstanceId, archetypeId = s.Id, s.Family, s.Type, layoutHint = s.Layout, s.DisplayName,
                s.Purpose, evidenceRole = s.EvidenceRole, subjectSuspectId = s.SubjectSuspectId,
                imagePurpose = s.ImagePurpose, suggestedTitle = s.SuggestedTitle
            })),
            ["repair_guidance"] = repairGuidance ?? "(none)"
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, "AssetPlan", system, user,
            MicrotaskSchemas.AssetPlan, ct, validate: result => ValidatePlan(draft, result.Assets, portfolio));
        draft.AssetStubs = output.Assets;
    }

    public static IReadOnlyList<string> BuildPreferredLayoutPortfolio(CaseDraft draft) =>
        EvidenceArchetypeCatalog.BuildPortfolio(draft).Select(s => s.Layout).ToList();

    private static void ValidatePlan(CaseDraft draft, List<AssetStub> assets, IReadOnlyList<EvidenceDossierSlot> portfolio)
    {
        MicrotaskValidation.Unique(assets.Select(a => a.Id), "asset plan");
        Require(assets.Count == portfolio.Count && assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal)
            .SetEquals(portfolio.Select(s => s.InstanceId)), "AssetPlan must echo each assigned portfolio slot; missing assets are not fabricated.");
        var clues = draft.Blueprint.ClueLadder.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var decoys = draft.Blueprint.RedHerrings.Select(d => d.SuspectId).ToHashSet(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            var slot = portfolio.Single(s => s.InstanceId == asset.Id);
            Require(asset.ArchetypeId == slot.Id && asset.Type == slot.Type && asset.LayoutHint == slot.Layout
                && asset.EvidenceRole == slot.EvidenceRole && asset.SubjectSuspectId == slot.SubjectSuspectId && asset.ImagePurpose == slot.ImagePurpose,
                $"Asset '{asset.Id}' changed its assigned archetype, format, layout or subject.");
            Require(asset.Visibility == "initial", "The dossier plan must preserve initially available portfolio assets.");
            MicrotaskValidation.Unique(asset.ContainedObjectIds, "contained object IDs");
            Require(asset.SupportsClueIds.All(id => clues.TryGetValue(id, out var clue) && clue.SourceType != "forensic"),
                $"Asset '{asset.Id}' contains an unknown or premature forensic clue.");
            Require(asset.ForensicInputClueIds.All(id => clues.TryGetValue(id, out var clue) && clue.SourceType == "forensic"),
                $"Asset '{asset.Id}' refers to an unknown forensic opportunity.");
            Require(asset.IntroducesRedHerringSuspectIds.Concat(asset.ResolvesRedHerringSuspectIds).All(decoys.Contains),
                "AssetPlan references an unknown decoy.");
            if (!EvidenceRoles.IsInvestigative(asset.EvidenceRole))
                Require(asset.SupportsClueIds.Count + asset.ForensicInputClueIds.Count + asset.IntroducesRedHerringSuspectIds.Count
                    + asset.ResolvesRedHerringSuspectIds.Count == 0, "Contextual portfolio slots cannot own proof or alibi verification.");
        }
        foreach (var clue in clues.Values)
        {
            var owners = assets.Count(a => (clue.SourceType == "forensic" ? a.ForensicInputClueIds : a.SupportsClueIds)
                .Contains(clue.Id, StringComparer.Ordinal));
            Require(clue.SourceType == "forensic" ? owners == 1 : owners > 0,
                $"Canonical clue '{clue.Id}' requires {(clue.SourceType == "forensic" ? "exactly one forensic input owner" : "an assigned source owner")}.");
        }
        foreach (var decoy in decoys)
            Require(assets.Any(a => a.IntroducesRedHerringSuspectIds.Contains(decoy, StringComparer.Ordinal))
                && assets.Any(a => a.ResolvesRedHerringSuspectIds.Contains(decoy, StringComparer.Ordinal)),
                $"Decoy '{decoy}' requires explicit suspicion and verification source assignments.");
    }
}

public class AssetCardTask(ILLMProvider llm, ILogger logger)
{
    public async Task<EvidenceAsset> RunAsync(CaseDraft draft, AssetStub stub, CancellationToken ct, string? repairGuidance = null)
    {
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("AssetCard", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language), ["layout_hint"] = stub.LayoutHint,
            ["asset_type"] = stub.Type, ["must_have_body_doc"] = (stub.Type != "photo").ToString().ToLowerInvariant(),
            ["authorship_instructions"] = CaseBibleAuthorship.Instructions
        });
        var user = prompts.RenderUser("AssetCard", new Dictionary<string, object?>
        {
            ["source_context_json"] = CaseBibleAuthorship.Context(draft, stub.Id),
            ["asset_slot_json"] = JsonSerializer.Serialize(new
            {
                stub.Id, stub.Type, stub.Title, stub.Visibility, stub.LayoutHint, stub.EvidenceRole, stub.SubjectSuspectId, stub.ImagePurpose
            }, CaseDraft.CanonicalJsonOptions)
        });
        return await TaskRunner.RunStructuredAsync<EvidenceAsset>(llm, logger, $"AssetCard:{stub.Id}", system, user,
            MicrotaskSchemas.AssetCard, ct, validate: asset =>
            {
                MicrotaskValidation.Equal(asset.Id, stub.Id, "asset.id");
                CaseBibleAuthorship.Asset(draft, asset);
            });
    }
}

public class TimelineTask(ILLMProvider llm, ILogger logger)
{
    public Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        _ = llm;
        ct.ThrowIfCancellationRequested();
        draft.Timeline = PlayerTimelineCompiler.Compile(draft, logger);
        draft.TemporalEvents = new();
        return Task.CompletedTask;
    }
}
