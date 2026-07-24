using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Stage 3 — Asset Plan. Tiny output: list of asset stubs (id+type+title+role+visibility).</summary>
public class AssetPlanTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public AssetPlanTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output
    {
        [JsonPropertyName("assets")] public List<AssetStub> Assets { get; set; } = new();
    }

    private const string Schema = """
    {
      "type":"object","required":["assets"],
      "properties":{
        "assets":{"type":"array","minItems":10,"maxItems":22,
          "items":{"type":"object","required":["archetypeId","id","type","title","role","evidenceRole","visibility","layoutHint","supportsClueIds","forensicInputClueIds","introducesRedHerringSuspectIds","resolvesRedHerringSuspectIds","containedObjectIds"],
            "properties":{
              "archetypeId":{"type":"string","pattern":"^[a-z0-9_]+$"},
              "id":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"},
              "type":{"type":"string","enum":["photo","pdf","document","digital"]},
              "evidenceRole":{"type":"string","enum":["primary","corroborative","contextual"]},
              "subjectSuspectId":{"type":["string","null"],"pattern":"^suspect\\.[a-z0-9_]+$"},
              "imagePurpose":{"type":["string","null"],"enum":["scene","suspect_portrait","object","surveillance",null]},
              "visibility":{"type":"string","enum":["initial","hidden"]},
              "layoutHint":{"type":"string","enum":["Photo","PoliceReport","WitnessStatement","InterviewTranscript","AudioTranscript","MedicalReport","EvidenceLog","Memo","CustodyForm","NewspaperClipping","PersonalLetter","Receipt","SearchWarrant","DispatchLog","CaseMap","Calendar","CallLog","PosExport","PhoneDump","SensorLog","BrowserHistory","BankStatement","GpsTrack","FileListing","ChatExport","EmailExport","AccessLog"]},
              "supportsClueIds":{"type":"array","minItems":0,"maxItems":3,
                "items":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"}},
              "forensicInputClueIds":{"type":"array","items":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"}},
              "introducesRedHerringSuspectIds":{"type":"array","items":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"}},
              "resolvesRedHerringSuspectIds":{"type":"array","items":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"}}
              ,"containedObjectIds":{"type":"array","minItems":1,"items":{"type":"string","pattern":"^(document|object|device|account|session|communication|transaction)\\.[a-z0-9_]+$"}}
            }}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);
        var budget = EvidenceArchetypeCatalog.GetBudget(draft);
        var portfolioJson = System.Text.Json.JsonSerializer.Serialize(portfolio.Select(a => new
        {
            id = a.InstanceId,
            archetypeId = a.Id,
            a.Family,
            a.Type,
            layoutHint = a.Layout,
            a.DisplayName,
            a.Purpose,
            evidenceRole = a.EvidenceRole,
            subjectSuspectId = a.SubjectSuspectId,
            imagePurpose = a.ImagePurpose,
            suggestedTitle = a.SuggestedTitle
        }));
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("AssetPlan", new Dictionary<string, object?>
        {
            ["difficulty"] = budget.Difficulty,
            ["portfolio_count"] = portfolio.Count,
            ["min_assets"] = budget.MinAssets,
            ["max_assets"] = budget.MaxAssets,
            ["min_investigative_assets"] = budget.MinInvestigativeAssets,
            ["max_investigative_assets"] = budget.MaxInvestigativeAssets,
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = prompts.RenderUser("AssetPlan", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["portfolio_json"] = portfolioJson,
            ["repair_guidance"] = string.IsNullOrWhiteSpace(repairGuidance) ? "(none)" : repairGuidance
        });
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "AssetPlan", system, user, Schema, ct);
        // Force all initial-stage assets to be visibility=initial
        foreach (var a in o.Assets) a.Visibility = "initial";
        NormalizePortfolio(draft, o.Assets, portfolio);
        NormalizeDeclaredOwnership(draft, o.Assets);
        draft.AssetStubs = o.Assets;
    }

    public static IReadOnlyList<string> BuildPreferredLayoutPortfolio(CaseDraft draft)
        => EvidenceArchetypeCatalog.BuildPortfolio(draft).Select(a => a.Layout).ToList();

    private void NormalizePortfolio(
        CaseDraft draft,
        List<AssetStub> assets,
        IReadOnlyList<EvidenceDossierSlot> portfolio)
    {
        var unused = assets.ToList();
        var normalized = new List<AssetStub>(portfolio.Count);
        foreach (var slot in portfolio)
        {
            var asset = unused.FirstOrDefault(candidate => candidate.Id == slot.InstanceId)
                        ?? unused.FirstOrDefault(candidate =>
                            candidate.ArchetypeId == slot.Id
                            && candidate.SubjectSuspectId == slot.SubjectSuspectId)
                        ?? unused.FirstOrDefault(candidate => candidate.ArchetypeId == slot.Id)
                        ?? new AssetStub();
            unused.Remove(asset);

            asset.ArchetypeId = slot.Id;
            asset.Id = slot.InstanceId;
            asset.Type = slot.Type;
            asset.LayoutHint = slot.Layout;
            asset.EvidenceRole = slot.EvidenceRole;
            asset.SubjectSuspectId = slot.SubjectSuspectId;
            asset.ImagePurpose = slot.ImagePurpose;
            if (string.IsNullOrWhiteSpace(asset.Title))
                asset.Title = slot.SuggestedTitle ?? slot.DisplayName;
            if (string.IsNullOrWhiteSpace(asset.Role))
                asset.Role = draft.Request.Language?.ToLowerInvariant() switch
                {
                    "pt-br" => "Complementa o dossiê da investigação.",
                    "es-es" => "Complementa el expediente de la investigación.",
                    "fr-fr" => "Complète le dossier d’enquête.",
                    _ => slot.Purpose
                };
            if (asset.ContainedObjectIds.Count == 0)
                asset.ContainedObjectIds.Add($"object.{slot.InstanceId["asset.".Length..]}");
            normalized.Add(asset);
        }

        assets.Clear();
        assets.AddRange(normalized);
    }

    private static void NormalizeDeclaredOwnership(CaseDraft draft, List<AssetStub> assets)
    {
        var validInitialClueIds = draft.Blueprint.ClueLadder
            .Where(clue => !string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
            .Select(clue => clue.Id)
            .ToHashSet(StringComparer.Ordinal);
        var initialClueSources = draft.Blueprint.ClueLadder
            .Where(clue => validInitialClueIds.Contains(clue.Id))
            .ToDictionary(clue => clue.Id, clue => clue.SourceType, StringComparer.Ordinal);
        var validForensicClueIds = draft.Blueprint.ClueLadder
            .Where(clue => string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
            .Select(clue => clue.Id)
            .ToHashSet(StringComparer.Ordinal);
        var validDecoyIds = draft.Blueprint.RedHerrings
            .Select(redHerring => redHerring.SuspectId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            if (!EvidenceRoles.IsInvestigative(asset.EvidenceRole))
            {
                asset.SupportsClueIds.Clear();
                asset.ForensicInputClueIds.Clear();
                asset.IntroducesRedHerringSuspectIds.Clear();
                asset.ResolvesRedHerringSuspectIds.Clear();
                continue;
            }
            asset.SupportsClueIds = asset.SupportsClueIds
                .Where(clueId => validInitialClueIds.Contains(clueId)
                                 && SupportsClueSource(asset, initialClueSources[clueId]))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.ForensicInputClueIds = asset.ForensicInputClueIds
                .Where(clueId => validForensicClueIds.Contains(clueId)
                                 && SupportsForensicInput(asset))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.IntroducesRedHerringSuspectIds = asset.IntroducesRedHerringSuspectIds
                .Where(validDecoyIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.ResolvesRedHerringSuspectIds = asset.ResolvesRedHerringSuspectIds
                .Where(validDecoyIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.ContainedObjectIds = asset.ContainedObjectIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        AssignMissingClues(
            validInitialClueIds,
            clueId => assets.Where(asset => SupportsClueSource(
                asset,
                draft.Blueprint.ClueLadder.First(clue => clue.Id == clueId).SourceType)));
        AssignMissingClues(validForensicClueIds, _ => assets.Where(SupportsForensicInput));

        foreach (var redHerring in draft.Blueprint.RedHerrings)
        {
            if (!assets.Any(asset => asset.IntroducesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal)))
                LeastLoaded(InvestigativeAssets()).IntroducesRedHerringSuspectIds.Add(redHerring.SuspectId);
            if (!assets.Any(asset => asset.ResolvesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal)))
                LeastLoaded(InvestigativeAssets()).ResolvesRedHerringSuspectIds.Add(redHerring.SuspectId);
        }

        void AssignMissingClues(
            IEnumerable<string> clueIds,
            Func<string, IEnumerable<AssetStub>> candidates)
        {
            foreach (var clueId in clueIds.Order(StringComparer.Ordinal))
            {
                var forensic = validForensicClueIds.Contains(clueId);
                if (assets.Any(asset => (forensic ? asset.ForensicInputClueIds : asset.SupportsClueIds)
                        .Contains(clueId, StringComparer.Ordinal)))
                    continue;
                var eligible = candidates(clueId)
                    .Where(asset => EvidenceRoles.IsInvestigative(asset.EvidenceRole))
                    .ToList();
                if (eligible.Count == 0)
                    throw new InvalidOperationException(
                        $"No investigative asset is compatible with clue '{clueId}'.");
                var target = LeastLoaded(eligible);
                (forensic ? target.ForensicInputClueIds : target.SupportsClueIds).Add(clueId);
            }
        }

        IEnumerable<AssetStub> InvestigativeAssets() =>
            assets.Where(asset => EvidenceRoles.IsInvestigative(asset.EvidenceRole));

        static AssetStub LeastLoaded(IEnumerable<AssetStub> candidates) =>
            candidates.OrderBy(asset => asset.SupportsClueIds.Count
                                        + asset.ForensicInputClueIds.Count
                                        + asset.IntroducesRedHerringSuspectIds.Count
                                        + asset.ResolvesRedHerringSuspectIds.Count)
                .ThenBy(asset => asset.Id, StringComparer.Ordinal)
                .First();

        static bool SupportsClueSource(AssetStub asset, string sourceType) =>
            sourceType.ToLowerInvariant() switch
            {
                "photo" => asset.Type.Equals("photo", StringComparison.OrdinalIgnoreCase),
                "digital" => asset.Type.Equals("digital", StringComparison.OrdinalIgnoreCase),
                "witness" => asset.LayoutHint.Equals("InterviewTranscript", StringComparison.OrdinalIgnoreCase)
                             || asset.Type is "document" or "pdf",
                _ => asset.Type is "document" or "pdf" or "digital"
            };

        static bool SupportsForensicInput(AssetStub asset) =>
            EvidenceRoles.IsInvestigative(asset.EvidenceRole)
            && asset.ContainedObjectIds.Count > 0
            && asset.SupportsClueIds.Count > 0
            && !string.Equals(asset.ArchetypeId, "suspect_interview", StringComparison.Ordinal)
            && !string.Equals(asset.ImagePurpose, ImagePurposes.SuspectPortrait, StringComparison.Ordinal);
    }
}

/// <summary>Stage 4 — Asset Card. One call per asset.</summary>
public class AssetCardTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public AssetCardTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private const string Schema = """
    {
      "type":"object","required":["id","type","title","description","visibility","category","body","bodyDoc"],
      "properties":{
        "id":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"},
        "type":{"type":"string"},
        "visibility":{"type":"string","enum":["initial","hidden"]},
        "body":{"type":"string"},
        "bodyDoc":{"type":["object","null"],
          "properties":{
            "layout":{"type":"string"},
            "title":{"type":"string"},
            "subtitle":{"type":["string","null"]},
            "classification":{"type":"string"},
            "header":{"type":"array","items":{"type":"object","required":["label","value"]}},
            "sections":{"type":"array","minItems":1,
              "items":{"type":"object","required":["kind"],
                "properties":{"kind":{"type":"string","enum":["narrative","table","keyValue","transcript","code","callout"]}}}},
            "signature":{"type":["object","null"]}
          }}
      }
    }
    """;

    public async Task<EvidenceAsset> RunAsync(
        CaseDraft draft,
        AssetStub stub,
        CancellationToken ct,
        string? repairGuidance = null)
    {
        var supportedClues = draft.Blueprint.ClueLadder
            .Where(clue => stub.SupportsClueIds.Contains(clue.Id, StringComparer.Ordinal))
            .ToList();
        var supportedCluesJson = System.Text.Json.JsonSerializer.Serialize(supportedClues);
        var resolvedRedHerrings = draft.Blueprint.RedHerrings
            .Where(redHerring => stub.ResolvesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
            .ToList();
        var resolvedRedHerringsJson = System.Text.Json.JsonSerializer.Serialize(resolvedRedHerrings);
        var introducedRedHerrings = draft.Blueprint.RedHerrings
            .Where(redHerring => stub.IntroducesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
            .ToList();
        var introducedRedHerringsJson = System.Text.Json.JsonSerializer.Serialize(introducedRedHerrings);
        var assetSpec = draft.CaseGraph.AssetSpecs.FirstOrDefault(spec => spec.Id == stub.Id);
        var observationsById = draft.CaseBible?.Observations.ToDictionary(
            observation => observation.Id,
            StringComparer.Ordinal) ?? new Dictionary<string, CaseBibleObservation>(StringComparer.Ordinal);
        var factsById = draft.CaseBible?.Facts.ToDictionary(
            fact => fact.Id,
            StringComparer.Ordinal) ?? new Dictionary<string, CaseBibleFact>(StringComparer.Ordinal);
        var assignedObservations =
            (assetSpec?.ObservationIds ?? [])
            .Where(observationsById.ContainsKey)
            .Select(observationId =>
            {
                var observation = observationsById[observationId];
                factsById.TryGetValue(observation.FactId, out var fact);
                return new
                {
                    observation.Id,
                    observation.Statement,
                    observation.ObservedValue,
                    observation.FactId,
                    canonicalValue = fact?.CanonicalValue
                };
            })
            .ToArray();
        var assignedObservationsJson = System.Text.Json.JsonSerializer.Serialize(assignedObservations);
        var graphObservations = draft.CaseGraph.Observations.ToDictionary(
            observation => observation.Id,
            StringComparer.Ordinal);
        var graphFacts = draft.CaseGraph.Facts.ToDictionary(
            fact => fact.Id,
            StringComparer.Ordinal);
        var graphEntities = draft.CaseGraph.Entities.ToDictionary(
            entity => entity.Id,
            StringComparer.Ordinal);
        var assignedCanonicalValues = (assetSpec?.ObservationIds ?? [])
            .Where(graphObservations.ContainsKey)
            .Select(id => graphObservations[id])
            .Where(observation => graphFacts.ContainsKey(observation.FactId))
            .Select(observation => CanonicalValue(graphFacts[observation.FactId], graphEntities))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var mustHaveBodyDoc = stub.Type is "pdf" or "document" or "digital";
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("AssetCard", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["layout_hint"] = stub.LayoutHint,
            ["asset_type"] = stub.Type,
            ["must_have_body_doc"] = mustHaveBodyDoc.ToString().ToLowerInvariant()
        });
        var user = prompts.RenderUser("AssetCard", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["asset_stub_json"] = System.Text.Json.JsonSerializer.Serialize(stub),
            ["supported_clues_json"] = supportedCluesJson,
            ["assigned_observations_json"] = assignedObservationsJson,
            ["introduced_decoys_json"] = introducedRedHerringsJson,
            ["resolved_decoys_json"] = resolvedRedHerringsJson,
            ["repair_guidance"] = string.IsNullOrWhiteSpace(repairGuidance) ? "(none)" : repairGuidance
        });
        var a = await TaskRunner.RunStructuredAsync<EvidenceAsset>(_llm, _logger, $"AssetCard:{stub.Id}", system, user, Schema, ct);
        a.Id = stub.Id;
        a.Type = stub.Type;
        a.Title = string.IsNullOrEmpty(a.Title) ? stub.Title : a.Title;
        a.Visibility = "initial";
        a.EvidenceRole = stub.EvidenceRole;
        a.SubjectSuspectId = stub.SubjectSuspectId;
        a.ImagePurpose = stub.ImagePurpose;
        if (stub.Type == "photo")
        {
            if (assignedCanonicalValues.Length > 0)
                a.Body = $"{a.Body}\nRequired visible details: {string.Join("; ", assignedCanonicalValues)}";
        }
        if (mustHaveBodyDoc && a.BodyDoc is not null && !string.IsNullOrWhiteSpace(stub.LayoutHint))
            a.BodyDoc.Layout = stub.LayoutHint;

        if (a.BodyDoc is not null)
            a.BodyDoc.Sections = a.BodyDoc.Sections.Where(IsSubstantiveSection).ToList();

        MaterializeCanonicalFacts(
            a,
            supportedClues,
            introducedRedHerrings,
            resolvedRedHerrings,
            draft.Request.Language);
        MaterializeAssignedCanonicalValues(a, assignedCanonicalValues, draft.Request.Language);
        if (stub.Type == "photo" && string.IsNullOrWhiteSpace(a.Body))
            a.Body = string.IsNullOrWhiteSpace(a.Description) ? a.Title : a.Description;
        if (mustHaveBodyDoc && a.BodyDoc is null)
        {
            _logger.LogWarning(
                "AssetCard:{Id} expected a bodyDoc but the LLM omitted it; creating a deterministic fallback document",
                stub.Id);
            a.BodyDoc = new EvidenceDocument
            {
                Layout = string.IsNullOrWhiteSpace(stub.LayoutHint) ? "GeneralReport" : stub.LayoutHint,
                Language = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
                Title = a.Title
            };
        }
        if (a.BodyDoc is not null && a.BodyDoc.Sections.Count == 0)
        {
            a.BodyDoc.Sections.Add(new EvidenceSection
            {
                Kind = "narrative",
                Text = !string.IsNullOrWhiteSpace(a.Body)
                    ? a.Body
                    : string.IsNullOrWhiteSpace(a.Description) ? a.Title : a.Description
            });
        }
        return a;
    }

    private static bool IsSubstantiveSection(EvidenceSection section) =>
        section.Kind switch
        {
            "table" => section.Table?.Rows.Count > 0,
            "transcript" => section.Transcript?.Count > 0,
            "code" => !string.IsNullOrWhiteSpace(section.Code?.Content),
            "keyValue" => section.Items?.Any(item =>
                !string.IsNullOrWhiteSpace(item.Label) || !string.IsNullOrWhiteSpace(item.Value)) == true,
            _ => !string.IsNullOrWhiteSpace(section.Text)
        };

    private static string CanonicalValue(
        CanonicalFact fact,
        IReadOnlyDictionary<string, CaseEntity> entities)
    {
        if (fact.LiteralValue is not null)
            return fact.LiteralValue;
        if (fact.ObjectId == "organization.case_context")
            return string.Empty;
        if (fact.ObjectId is not null && entities.TryGetValue(fact.ObjectId, out var entity))
            return string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Id : entity.DisplayName;
        return fact.ObjectId ?? string.Empty;
    }

    private static void MaterializeAssignedCanonicalValues(
        EvidenceAsset asset,
        IReadOnlyList<string> assignedCanonicalValues,
        string? language)
    {
        var existing = EvidenceContentText.Extract(asset);
        var missing = assignedCanonicalValues
            .Where(value => !existing.Contains(value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (missing.Length == 0)
            return;

        var heading = language?.ToLowerInvariant() switch
        {
            "pt-br" => "Detalhes registrados na fonte",
            "es-es" => "Detalles registrados en la fuente",
            "fr-fr" => "Détails consignés dans la source",
            _ => "Details recorded in the source"
        };
        var text = string.Join("\n", missing.Select(value => $"- {value}"));
        if (asset.BodyDoc is not null)
        {
            asset.BodyDoc.Sections.Add(new EvidenceSection
            {
                Kind = "narrative",
                Heading = heading,
                Text = text
            });
            return;
        }

        asset.Body = string.Join(
            "\n\n",
            new[] { asset.Body ?? string.Empty, $"## {heading}\n{text}" }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void MaterializeCanonicalFacts(
        EvidenceAsset asset,
        IReadOnlyList<CanonicalClue> supportedClues,
        IReadOnlyList<CanonicalRedHerring> introducedRedHerrings,
        IReadOnlyList<CanonicalRedHerring> resolvedRedHerrings,
        string? language)
    {
        var clueDiscoveries = supportedClues
            .Select(clue => clue.Discovery)
            .Where(discovery => !string.IsNullOrWhiteSpace(discovery))
            .ToList();
        var suspicions = introducedRedHerrings
            .Select(redHerring => redHerring.Suspicion)
            .Where(suspicion => !string.IsNullOrWhiteSpace(suspicion))
            .ToList();
        var resolutions = resolvedRedHerrings
            .Select(redHerring => redHerring.Verification)
            .Where(verification => !string.IsNullOrWhiteSpace(verification))
            .ToList();
        if (clueDiscoveries.Count == 0 && suspicions.Count == 0 && resolutions.Count == 0) return;

        var (clueHeading, suspicionHeading, verificationHeading) = language?.ToLowerInvariant() switch
        {
            "pt-br" => ("Observações documentadas", "Elementos de suspeita", "Verificações complementares"),
            "es-es" => ("Observaciones documentadas", "Elementos de sospecha", "Verificaciones complementarias"),
            "fr-fr" => ("Observations documentées", "Éléments de soupçon", "Vérifications complémentaires"),
            _ => ("Documented observations", "Suspicion indicators", "Supporting checks")
        };

        if (asset.BodyDoc is not null)
        {
            if (clueDiscoveries.Count > 0)
                asset.BodyDoc.Sections.Add(new EvidenceSection
                {
                    Kind = "narrative",
                    Heading = clueHeading,
                    Text = string.Join("\n", clueDiscoveries.Select(discovery => $"- {discovery}"))
                });
            if (suspicions.Count > 0)
                asset.BodyDoc.Sections.Add(new EvidenceSection
                {
                    Kind = "narrative",
                    Heading = suspicionHeading,
                    Text = string.Join("\n", suspicions.Select(suspicion => $"- {suspicion}"))
                });
            if (resolutions.Count > 0)
                asset.BodyDoc.Sections.Add(new EvidenceSection
                {
                    Kind = "narrative",
                    Heading = verificationHeading,
                    Text = string.Join("\n", resolutions.Select(resolution => $"- {resolution}"))
                });
            return;
        }

        var additions = new List<string>();
        if (clueDiscoveries.Count > 0)
            additions.Add($"## {clueHeading}\n{string.Join("\n", clueDiscoveries.Select(discovery => $"- {discovery}"))}");
        if (suspicions.Count > 0)
            additions.Add($"## {suspicionHeading}\n{string.Join("\n", suspicions.Select(suspicion => $"- {suspicion}"))}");
        if (resolutions.Count > 0)
            additions.Add($"## {verificationHeading}\n{string.Join("\n", resolutions.Select(resolution => $"- {resolution}"))}");
        asset.Body = string.Join("\n\n", new[] { asset.Body ?? string.Empty }.Concat(additions)).Trim();
    }
}

/// <summary>Stage 5 — Timeline + temporalEvents.</summary>
public class TimelineTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public TimelineTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output
    {
        [JsonPropertyName("timeline")] public List<EvidenceTimelineEntry> Timeline { get; set; } = new();
        [JsonPropertyName("temporalEvents")] public List<EvidenceTemporalEvent> TemporalEvents { get; set; } = new();
    }

    private const string Schema = """
    {
      "type":"object","required":["timeline","temporalEvents"],
      "properties":{
        "timeline":{"type":"array","minItems":2,"maxItems":8,
          "items":{"type":"object","required":["time","event"],
            "properties":{
              "source":{"type":"string","enum":["investigation","witness","sensor","forensic"]},
              "importance":{"type":"string","enum":["low","medium","high","critical"]}}}},
        "temporalEvents":{"type":"array","minItems":0,"maxItems":3,
          "items":{"type":"object","required":["id","triggerAtMinutes","type"],
            "properties":{
              "id":{"type":"string","pattern":"^tevt\\.[a-z0-9_]+$"},
              "type":{"type":"string","enum":["memo","witness","alert","email"]}}}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        await Task.CompletedTask;
        draft.Timeline = PlayerTimelineCompiler.Compile(draft, _logger);
        // Temporal events remain empty until they can be compiled from explicit,
        // payload-bearing evidence specs. Empty scripted events are worse than none.
        draft.TemporalEvents = new();
    }
}
