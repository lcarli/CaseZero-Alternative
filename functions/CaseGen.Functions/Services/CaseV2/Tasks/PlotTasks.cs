using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>
/// Stage 2 — projects the canonical Case Bible into public metadata, suspect stubs,
/// and the investigation blueprint. The model may organize and phrase presentation
/// fields, but deterministic projection owns every canonical value.
/// </summary>
public sealed class PlotOutlineTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;

    public PlotOutlineTask(ILLMProvider llm, ILogger logger)
    {
        _llm = llm;
        _logger = logger;
    }

    private sealed class Output
    {
        [JsonPropertyName("metadata")] public PlotMetadata Metadata { get; set; } = new();
        [JsonPropertyName("clueOrderIds")] public List<string> ClueOrderIds { get; set; } = new();
        [JsonPropertyName("decoyOrderIds")] public List<string> DecoyOrderIds { get; set; } = new();
    }

    private const string Schema = """
    {
      "type":"object",
      "required":["metadata","clueOrderIds","decoyOrderIds"],
      "properties":{
        "metadata":{
          "type":"object",
          "required":["title","description","difficulty","requiredRank","category","briefing","tags"],
          "properties":{
            "title":{"type":"string"},
            "description":{"type":"string"},
            "difficulty":{"type":"string"},
            "requiredRank":{"type":"string"},
            "category":{"type":"string"},
            "briefing":{"type":"string"},
            "tags":{"type":"array","minItems":1,"maxItems":8,"items":{"type":"string"}}
          }
        },
        "clueOrderIds":{"type":"array","minItems":4,"maxItems":12,"items":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"}},
        "decoyOrderIds":{"type":"array","minItems":1,"maxItems":5,"items":{"type":"string","pattern":"^decoy\\.[a-z0-9_]+$"}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var bible = draft.CaseBible
                    ?? throw new InvalidOperationException("PlotOutline requires a validated Case Bible.");
        var profile = DifficultyProfileCatalog.Get(draft.Request.Difficulty ?? draft.Request.RequiredRank);
        var catalog = AgentPromptCatalog.Default;
        var system = catalog.RenderSystem("PlotOutline", new Dictionary<string, object?>
        {
            ["difficulty"] = profile.Name,
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = catalog.RenderUser("PlotOutline", new Dictionary<string, object?>
        {
            ["case_id"] = draft.CaseId,
            ["title_hint"] = draft.Request.Title ?? "(model chooses)",
            ["theme_hint"] = draft.Request.Theme ?? "(none)",
            ["case_bible_json"] = System.Text.Json.JsonSerializer.Serialize(bible),
            ["repair_guidance"] = string.IsNullOrWhiteSpace(repairGuidance) ? "(none)" : repairGuidance
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(
            _llm,
            _logger,
            "PlotOutline",
            system,
            user,
            Schema,
            ct);

        draft.Metadata = output.Metadata;
        if (!string.IsNullOrWhiteSpace(draft.Request.Title))
            draft.Metadata.Title = draft.Request.Title!;
        CaseBibleNormalizer.ProjectIntoDraft(draft, output.ClueOrderIds, output.DecoyOrderIds);
    }
}

/// <summary>Stage 3 — writes one player-visible suspect profile from canonical person data.</summary>
public sealed class SuspectCardTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;

    public SuspectCardTask(ILLMProvider llm, ILogger logger)
    {
        _llm = llm;
        _logger = logger;
    }

    private const string Schema = """
    {
      "type":"object",
      "required":["id","name","age","occupation","relationship","motive","alibi","alibiVerified","background"],
      "properties":{
        "id":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"},
        "alibiVerified":{"type":"boolean"}
      }
    }
    """;

    public async Task<PlotSuspect> RunAsync(
        CaseDraft draft,
        SuspectStub stub,
        CancellationToken ct,
        string? repairGuidance = null)
    {
        var canonicalPerson = CaseBibleNormalizer.FindSuspect(draft, stub.Id)
                              ?? throw new InvalidOperationException(
                                  $"Suspect '{stub.Id}' is not present in the canonical Case Bible.");
        var catalog = AgentPromptCatalog.Default;
        var system = catalog.RenderSystem("SuspectCard", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = catalog.RenderUser("SuspectCard", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["canonical_person_json"] = System.Text.Json.JsonSerializer.Serialize(canonicalPerson),
            ["suspect_id"] = stub.Id,
            ["suspect_name"] = stub.Name,
            ["is_culprit"] = stub.IsCulprit.ToString().ToLowerInvariant(),
            ["repair_guidance"] = string.IsNullOrWhiteSpace(repairGuidance) ? "(none)" : repairGuidance
        });

        var suspect = await TaskRunner.RunStructuredAsync<PlotSuspect>(
            _llm,
            _logger,
            $"SuspectCard:{stub.Id}",
            system,
            user,
            Schema,
            ct);
        suspect.Id = stub.Id;
        suspect.Name = canonicalPerson.Name;
        suspect.Age = canonicalPerson.Age;
        suspect.Occupation = canonicalPerson.Occupation;
        suspect.Relationship = canonicalPerson.RelationshipToVictim;
        suspect.Motive ??= string.Empty;
        suspect.Alibi = canonicalPerson.StatedAlibi ?? string.Empty;
        suspect.Background ??= canonicalPerson.Background;
        suspect.AlibiVerified = false;
        return suspect;
    }
}
