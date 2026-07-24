using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Stage 1 — establishes the private, typed source of truth for every downstream task.</summary>
public sealed class CaseBibleTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;

    public CaseBibleTask(ILLMProvider llm, ILogger logger)
    {
        _llm = llm;
        _logger = logger;
    }

    private const string Schema = """
    {
      "type":"object",
      "required":["version","world","institutions","locations","travelRelationships","people","relationships","employments","workSchedules","devices","accounts","vehicles","incident","truthTimeline","sources","facts","observations","difficultyIntent","investigationConstraints"],
      "properties":{
        "version":{"type":"string"},
        "world":{"type":"object","required":["language","locationDisplayName","city","region","country","jurisdiction","timeZoneId","utcOffset","policeAgencyId","investigatorPersonId"]},
        "institutions":{"type":"array","minItems":1,"maxItems":6,"items":{"type":"object","required":["id","name","kind"],"properties":{"id":{"type":"string","pattern":"^organization\\.[a-z0-9_]+$"}}}},
        "locations":{"type":"array","minItems":2,"maxItems":10,"items":{"type":"object","required":["id","name","kind","address"],"properties":{"id":{"type":"string","pattern":"^location\\.[a-z0-9_]+$"},"address":{"type":"object","required":["line1","city","country"]}}}},
        "travelRelationships":{"type":"array","minItems":1,"maxItems":16,"items":{"type":"object","required":["id","fromLocationId","toLocationId","mode","typicalMinutes","minimumMinutes"],"properties":{"id":{"type":"string","pattern":"^travel\\.[a-z0-9_]+$"},"typicalMinutes":{"type":"integer","minimum":1,"maximum":240},"minimumMinutes":{"type":"integer","minimum":1,"maximum":240}}}},
        "people":{"type":"array","minItems":4,"maxItems":10,"items":{"type":"object","required":["id","name","age","roles","occupation","publicRole","relationshipToVictim","background","alibiVerified","email"],"properties":{"id":{"type":"string","pattern":"^person\\.[a-z0-9_]+$"},"suspectId":{"type":["string","null"],"pattern":"^suspect\\.[a-z0-9_]+$"},"age":{"type":"integer","minimum":1,"maximum":110},"roles":{"type":"array","minItems":1,"items":{"type":"string","enum":["Suspect","Victim","Witness","Investigator","Other"]}},"email":{"type":["string","null"]}}}},
        "relationships":{"type":"array","minItems":2,"maxItems":20,"items":{"type":"object","required":["id","fromPersonId","toPersonId","type","description"],"properties":{"id":{"type":"string","pattern":"^relationship\\.[a-z0-9_]+$"}}}},
        "employments":{"type":"array","minItems":1,"maxItems":12,"items":{"type":"object","required":["id","personId","institutionId","locationId","jobTitle"],"properties":{"id":{"type":"string","pattern":"^employment\\.[a-z0-9_]+$"}}}},
        "workSchedules":{"type":"array","minItems":1,"maxItems":18,"items":{"type":"object","required":["id","personId","locationId","start","end","label"],"properties":{"id":{"type":"string","pattern":"^schedule\\.[a-z0-9_]+$"}}}},
        "devices":{"type":"array","minItems":0,"maxItems":10,"items":{"type":"object","required":["id","type","identifier","accountIds"],"properties":{"id":{"type":"string","pattern":"^device\\.[a-z0-9_]+$"}}}},
        "accounts":{"type":"array","minItems":0,"maxItems":10,"items":{"type":"object","required":["id","provider","handle","identifier","deviceIds"],"properties":{"id":{"type":"string","pattern":"^account\\.[a-z0-9_]+$"}}}},
        "vehicles":{"type":"array","minItems":0,"maxItems":8,"items":{"type":"object","required":["id","plate","make","model","color"],"properties":{"id":{"type":"string","pattern":"^vehicle\\.[a-z0-9_]+$"}}}},
        "incident":{"type":"object","required":["crimeType","culpritPersonId","victimPersonId","primaryLocationId","occurredAt","discoveredAt","reportedAt","openedAt","crimeMechanism","culpritObjective","concealment","caseHook","privateSummary"]},
        "truthTimeline":{"type":"array","minItems":4,"maxItems":12,"items":{"type":"object","required":["id","kind","time","locationId","participantIds","deviceIds","accountIds","vehicleIds","factIds","event","publicDescription","visibility","source","verified","importance","causalRole"],"properties":{"id":{"type":"string","pattern":"^event\\.[a-z0-9_]+$"},"kind":{"type":"string","enum":["Occurred","Created","Modified","Scanned","Uploaded","Submitted","Approved","Paid","Accessed","Entered","Exited","Called","Messaged","Discovered","Reported","Collected","AnalysisRequested","AnalysisCompleted"]},"publicDescription":{"type":["string","null"]},"visibility":{"type":"string","enum":["Public","Private"]},"source":{"type":"string","enum":["investigation","witness","sensor","forensic"]},"importance":{"type":"string","enum":["low","medium","high","critical"]}}}},
        "sources":{"type":"array","minItems":4,"maxItems":18,"items":{"type":"object","required":["id","kind","name","description"],"properties":{"id":{"type":"string","pattern":"^source\\.[a-z0-9_]+$"},"kind":{"type":"string","enum":["Document","DigitalRecord","Photograph","Testimony","PhysicalObject","InstitutionalRecord","ForensicResult"]}}}},
        "facts":{"type":"array","minItems":6,"maxItems":28,"items":{"type":"object","required":["id","predicate","subjectId","visibility","truthStatus"],"properties":{"id":{"type":"string","pattern":"^fact\\.[a-z0-9_]+$"},"objectId":{"type":["string","null"]},"literalValue":{"type":["string","null"]},"literalType":{"type":["string","null"],"enum":["String","Boolean","Integer","Decimal","DateTime","Duration",null]},"visibility":{"type":"string","enum":["Public","Private"]},"truthStatus":{"type":"string","enum":["Confirmed","Disputed","False"]},"intentionalConflictId":{"type":["string","null"],"pattern":"^conflict\\.[a-z0-9_]+$"}}}},
        "observations":{"type":"array","minItems":6,"maxItems":28,"items":{"type":"object","required":["id","factId","sourceId","statement","fidelity","reliability","visibility"],"properties":{"id":{"type":"string","pattern":"^observation\\.[a-z0-9_]+$"},"fidelity":{"type":"string","enum":["DirectRecord","Testimony","Hearsay","Circumstantial"]},"reliability":{"type":"string","enum":["Verified","Corroborated","Unverified","Disputed"]},"visibility":{"type":"string","enum":["Public","Private"]},"intentionalConflictId":{"type":["string","null"],"pattern":"^conflict\\.[a-z0-9_]+$"}}}},
        "difficultyIntent":{"type":"object","required":["difficulty","clues","proofPaths","decoyArcs","forensicOpportunities","intentionalConflicts","ambiguityNotes"],
          "properties":{
            "clues":{"type":"array","minItems":4,"maxItems":12,"items":{"type":"object","required":["id","observationIds","supportsPersonId","inference","strength","sourceType","role"],"properties":{"id":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"},"supportsPersonId":{"type":["string","null"],"pattern":"^person\\.[a-z0-9_]+$"},"strength":{"type":"string","enum":["context","supporting","decisive"]},"sourceType":{"type":"string","enum":["document","digital","photo","witness","forensic"]},"role":{"type":"string","enum":["context","identity","action","benefit","alibi","forensicAttribution"]}}}},
            "proofPaths":{"type":"array","minItems":1,"maxItems":4,"items":{"type":"object","required":["id","supportsPersonId","clueIds","conclusionFactId","independenceKey","description"],"properties":{"id":{"type":"string","pattern":"^proof\\.[a-z0-9_]+$"}}}},
            "decoyArcs":{"type":"array","minItems":1,"maxItems":5,"items":{"type":"object","required":["id","suspectPersonId","suspicion","suspicionObservationIds","verificationObservationIds","resolution"],"properties":{"id":{"type":"string","pattern":"^decoy\\.[a-z0-9_]+$"}}}},
            "forensicOpportunities":{"type":"array","minItems":0,"maxItems":4,"items":{"type":"object","required":["id","inputEntityId","inputSourceId","resultObservationId","methodHint","purpose"],"properties":{"id":{"type":"string","pattern":"^forensic\\.[a-z0-9_]+$"}}}},
            "intentionalConflicts":{"type":"array","minItems":0,"maxItems":4,"items":{"type":"object","required":["id","factIds","observationIds","resolutionObservationIds","purpose"],"properties":{"id":{"type":"string","pattern":"^conflict\\.[a-z0-9_]+$"}}}},
            "ambiguityNotes":{"type":"array","maxItems":6,"items":{"type":"string"}}
          }},
        "investigationConstraints":{"type":"object","required":["openingObservationIds","requiredProcedureNotes","prohibitedAssumptions","allowedForensicMethodIds","mustPreserveFactIds"]}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var profile = DifficultyProfileCatalog.Get(draft.Request.Difficulty ?? draft.Request.RequiredRank);
        var catalog = AgentPromptCatalog.Default;
        var system = catalog.RenderSystem("CaseBible", new Dictionary<string, object?>
        {
            ["difficulty"] = profile.Name,
            ["min_suspects"] = profile.MinSuspects,
            ["max_suspects"] = profile.MaxSuspects,
            ["min_proof_paths"] = profile.Topology.MinIndependentProofPaths,
            ["required_decoy_arcs"] = profile.Topology.RequiredDecoyArcs,
            ["min_forensic_hops"] = profile.Topology.MinForensicHops,
            ["all_evidence_initial"] = profile.AllEvidenceInitial.ToString().ToLowerInvariant(),
            ["requires_conflicting_observation"] = profile.Topology.RequiresConflictingObservation.ToString().ToLowerInvariant()
        });
        var methodCatalog = JsonSerializer.Serialize(ForensicMethodCatalog.All.Select(method => new
        {
            method.Id,
            method.AcceptedInputObjectTypes,
            method.AcceptedAssetTypes,
            method.ProducibleProperties,
            method.LimitationKeys
        }));
        var effectiveGuidance = repairGuidance;
        CaseBibleValidationReport? lastValidation = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var user = catalog.RenderUser("CaseBible", new Dictionary<string, object?>
            {
                ["case_id"] = draft.CaseId,
                ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
                ["theme_hint"] = draft.Request.Theme ?? "(model chooses)",
                ["title_hint"] = draft.Request.Title ?? "(model chooses later)",
                ["location_hint"] = draft.Request.Location ?? "(model chooses)",
                ["difficulty"] = profile.Name,
                ["seed"] = draft.Request.Seed?.ToString() ?? "(none)",
                ["forensic_method_catalog"] = methodCatalog,
                ["repair_guidance"] = string.IsNullOrWhiteSpace(effectiveGuidance) ? "(none)" : effectiveGuidance
            });

            var bible = await TaskRunner.RunStructuredAsync<CaseBible>(
                _llm,
                _logger,
                attempt == 1 ? "CaseBible" : "CaseBible:semanticRetry",
                system,
                user,
                Schema,
                ct);
            CaseBibleNormalizer.ApplyRequestOverrides(bible, draft.Request);
            CaseBibleNormalizer.Normalize(bible);
            lastValidation = CaseBibleValidator.Validate(
                bible,
                profile.Name,
                draft.Request.Language,
                required: true);
            if (lastValidation.IsValid)
            {
                draft.CaseBible = bible;
                return;
            }
            if (attempt == 1)
            {
                effectiveGuidance = string.Join(
                    Environment.NewLine,
                    new[] { repairGuidance }
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Concat(lastValidation.Errors.Take(20)));
                _logger.LogWarning(
                    "Case Bible failed semantic validation; retrying once with {Count} actionable errors",
                    lastValidation.Issues.Count);
            }
        }

        throw new CaseBibleValidationException(
            lastValidation ?? throw new InvalidOperationException("Case Bible validation did not run."));
    }
}
