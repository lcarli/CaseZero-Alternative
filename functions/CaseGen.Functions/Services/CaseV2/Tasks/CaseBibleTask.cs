using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

public sealed class CaseBibleTask(ILLMProvider llm, ILogger logger)
{
    private static readonly string Schema = MicrotaskSchemas.CaseBible;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        if (draft.CaseBible is not null)
        {
            CaseBibleValidator.Plan(draft, draft.CaseBible);
            return;
        }
        var profile = DifficultyProfileCatalog.Get(draft.Request.Difficulty);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("CaseBible", new Dictionary<string, object?>
        {
            ["difficulty"] = profile.Name, ["min_suspects"] = profile.MinSuspects, ["max_suspects"] = profile.MaxSuspects,
            ["min_proof_paths"] = profile.Topology.MinIndependentProofPaths, ["min_non_forensic_origins"] = profile.MinIndependentCulpritSources,
            ["required_decoy_arcs"] = profile.Topology.RequiredDecoyArcs, ["min_forensic_hops"] = profile.Topology.MinForensicHops,
            ["all_evidence_initial"] = profile.AllEvidenceInitial.ToString().ToLowerInvariant(),
            ["requires_conflicting_observation"] = profile.Topology.RequiresConflictingObservation.ToString().ToLowerInvariant()
        });
        var user = prompts.RenderUser("CaseBible", new Dictionary<string, object?>
        {
            ["case_id"] = draft.CaseId, ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["theme_hint"] = draft.Request.Theme ?? "(model chooses)", ["title_hint"] = draft.Request.Title ?? "(model chooses later)",
            ["location_hint"] = draft.Request.Location ?? "(model chooses)", ["difficulty"] = profile.Name,
            ["seed"] = draft.Request.Seed?.ToString() ?? "(none)",
            ["forensic_method_catalog"] = JsonSerializer.Serialize(ForensicMethodCatalog.All.Select(m => new
            {
                m.Id, m.AcceptedInputObjectTypes, m.AcceptedAssetTypes, m.ProducibleProperties, m.LimitationKeys,
                m.RequiresReferenceSample, m.RequiresChainOfCustody, m.AllowedDerivationRules
            })),
            ["repair_guidance"] = repairGuidance ?? "(none)"
        });
        var bible = await TaskRunner.RunStructuredAsync<CaseBible>(llm, logger, "CaseBible", system, user, Schema, ct,
            validate: candidate =>
            {
                CaseBibleNormalizer.Normalize(candidate);
                TaskContractException.Require(candidate.World.Language == ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
                    && candidate.DifficultyIntent.Difficulty == profile.Name, "CaseBible must echo the selected language and difficulty.");
                if (draft.Request.Location is not null)
                    MicrotaskValidation.Equal(candidate.World.LocationDisplayName, draft.Request.Location, "world.locationDisplayName");
                CaseBibleValidator.Plan(draft, candidate);
            });
        draft.CaseBible = bible;
    }
}
