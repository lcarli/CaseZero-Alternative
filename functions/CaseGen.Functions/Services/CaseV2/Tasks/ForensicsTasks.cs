using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

public class ForensicsPlanTask(ILLMProvider llm, ILogger logger)
{
    private sealed class Output
    {
        [JsonPropertyName("analysisTypes")] public List<ForensicsAnalysisType> AnalysisTypes { get; set; } = new();
        [JsonPropertyName("outcomes")] public List<ForensicOutcomeStub> Outcomes { get; set; } = new();
    }

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        Require(!DifficultyProfileCatalog.Get(draft).AllEvidenceInitial, "Rookie has no interactive forensic planning.");
        var bible = draft.CaseBible ?? throw new TaskContractException("ForensicsPlan requires canonical forensic opportunities.");
        var opportunities = bible.DifficultyIntent.ForensicOpportunities;
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("ForensicsPlan", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["desired_outcomes"] = opportunities.Count
        });
        var user = prompts.RenderUser("ForensicsPlan", new Dictionary<string, object?>
        {
            ["source_plan_json"] = JsonSerializer.Serialize(new
            {
                assets = draft.CaseGraph.AssetSpecs.Select(s => new
                {
                    s.Id, s.AssetType, s.ContainedObjectIds, s.ContainedObjectTypes, s.ObservationIds,
                    forensicClueIds = draft.AssetStubs.Single(a => a.Id == s.Id).ForensicInputClueIds
                }),
                opportunities = opportunities.Select(o => new
                {
                    o.Id, o.InputEntityId, o.InputSourceId, o.ResultObservationId, o.MethodHint,
                    clueIds = bible.DifficultyIntent.Clues.Where(c => c.ObservationIds.Contains(o.ResultObservationId)).Select(c => c.Id)
                })
            }),
            ["method_catalog_json"] = JsonSerializer.Serialize(ForensicMethodCatalog.All
                .Where(m => bible.InvestigationConstraints.AllowedForensicMethodIds.Contains(m.Id, StringComparer.Ordinal)).Select(m => new
            {
                m.Id, m.AcceptedInputObjectTypes, m.AcceptedAssetTypes, m.ProducibleProperties, m.LimitationKeys,
                m.RequiresReferenceSample, m.RequiresChainOfCustody, m.ValidResultLayouts,
                localization = m.Localize(draft.Request.Language)
            }))
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, "ForensicsPlan", system, user,
            MicrotaskSchemas.ForensicsPlan, ct, validate: result => ValidatePlan(draft, result.AnalysisTypes, result.Outcomes));
        draft.AnalysisTypes = output.AnalysisTypes;
        draft.ForensicStubs = output.Outcomes;
    }

    public static void ValidatePlan(CaseDraft draft, IReadOnlyList<ForensicsAnalysisType> analysisTypes, IReadOnlyList<ForensicOutcomeStub> outcomes)
    {
        var bible = draft.CaseBible ?? throw new TaskContractException("Canonical forensic opportunities are missing.");
        MicrotaskValidation.Unique(analysisTypes.Select(t => t.Type), "analysis types");
        MicrotaskValidation.Unique(outcomes.Select(o => $"{o.InputAssetId}:{o.AnalysisType}"), "forensic pairs");
        foreach (var type in analysisTypes)
        {
            var method = ForensicMethodCatalog.Find(type.Type) ?? throw new TaskContractException($"Unknown forensic method '{type.Type}'.");
            Require(bible.InvestigationConstraints.AllowedForensicMethodIds.Contains(type.Type, StringComparer.Ordinal),
                $"Forensic method '{type.Type}' is not permitted by the frozen canonical case.");
            Require(type.AvailableFor.ToHashSet(StringComparer.Ordinal).SetEquals(method.AcceptedAssetTypes)
                && type.DurationMinutes is >= 1 and <= 1440, "Analysis availability and duration must obey the immutable method catalog.");
        }
        foreach (var outcome in outcomes)
        {
            var method = ForensicMethodCatalog.Find(outcome.AnalysisType) ?? throw new TaskContractException($"Unknown forensic method '{outcome.AnalysisType}'.");
            var spec = draft.CaseGraph.AssetSpecs.SingleOrDefault(s => s.Id == outcome.InputAssetId);
            Require(spec is not null && spec.ContainedObjectIds.Contains(outcome.InputObjectId)
                && spec.ContainedObjectTypes.TryGetValue(outcome.InputObjectId, out var objectType)
                && method.AcceptedInputObjectTypes.Contains(objectType) && method.AcceptedAssetTypes.Contains(spec.AssetType),
                $"Forensic pair '{outcome.InputAssetId}:{outcome.AnalysisType}' requires a real compatible contained input object.");
            Require(analysisTypes.Any(t => t.Type == outcome.AnalysisType), "Forensic pair has no matching analysis type.");
            Require(outcome.ProducedProperties.All(method.ProducibleProperties.Contains)
                && (!outcome.Findings || outcome.ProducedProperties.Count > 0), "Forensic outputs exceed the method's capabilities.");
            Require(outcome.LimitationKeys.ToHashSet(StringComparer.Ordinal).SetEquals(method.LimitationKeys),
                "Forensic limitations cannot be dropped or invented.");
            Require(method.ValidResultLayouts.Contains(outcome.ResultLayoutId), "Forensic result layout is unsupported.");
            Require(!method.RequiresReferenceSample || outcome.ReferenceSampleObjectId is not null
                && draft.CaseGraph.AssetSpecs.Any(s => s.ContainedObjectIds.Contains(outcome.ReferenceSampleObjectId)),
                "This forensic method requires an existing reference sample.");
            Require(!method.RequiresChainOfCustody || outcome.ChainOfCustodyObservationId is not null
                && draft.CaseGraph.Observations.Any(o => o.Id == outcome.ChainOfCustodyObservationId
                    && o.SourceAssetId == outcome.InputAssetId), "This forensic method requires a source-linked chain-of-custody observation.");
            var observations = CaseBibleAuthorship.OutcomeObservations(draft, outcome);
            Require(outcome.SupportsClueIds.All(id => bible.DifficultyIntent.Clues.Any(c => c.Id == id && c.SourceType == "forensic")),
                "Forensic outcome references an unknown or non-forensic clue.");
            foreach (var observation in observations)
                Require(bible.DifficultyIntent.ForensicOpportunities.Any(o => o.ResultObservationId == observation.Id
                    && o.MethodHint == outcome.AnalysisType)
                    && draft.AssetStubs.Any(a => a.Id == outcome.InputAssetId && a.ForensicInputClueIds.Any(id =>
                        bible.DifficultyIntent.Clues.Any(c => c.Id == id && c.ObservationIds.Contains(observation.Id)))),
                    "Forensic outcome changed its assigned input owner or canonical planned method.");
            if (!outcome.Findings)
            {
                Require(outcome.ProducedProperties.Count == 0, "A no-findings outcome cannot claim produced observations.");
                Require(outcome.MatchedSuspectId is null && outcome.SupportsClueIds.Count == 0,
                    "No findings cannot identify a suspect, establish innocence or become a proof premise.");
            }
            if (outcome.MatchedSuspectId is { } suspectId)
            {
                var person = CaseBibleNormalizer.FindSuspect(draft, suspectId);
                Require(person is not null && outcome.Findings && observations.Any(o =>
                    o.Reliability is ObservationReliability.Verified or ObservationReliability.Corroborated
                    && o.ObservedValue == person.Id
                    && bible.Facts.Any(f => f.Id == o.FactId && f.ObjectId == person.Id
                        && f.TruthStatus == FactTruthStatus.Confirmed))
                    && !method.ProducibleProperties.Any(p => p is ForensicObservationProperty.HandwritingSimilarity
                        or ForensicObservationProperty.LinguisticSimilarity or ForensicObservationProperty.VisibleImageFeature),
                    "A suspect match needs an explicitly planned identifying observation; similarity is not individualization.");
            }
        }
        foreach (var opportunity in bible.DifficultyIntent.ForensicOpportunities)
            Require(outcomes.Any(o => o.Findings && o.AnalysisType == opportunity.MethodHint
                && CaseBibleAuthorship.OutcomeObservations(draft, o).Any(v => v.Id == opportunity.ResultObservationId)),
                $"Forensic opportunity '{opportunity.Id}' has no valid assigned result owner.");
    }
}

public class ForensicOutcomeTask(ILLMProvider llm, ILogger logger)
{
    public class Output : CaseAuthoredContent
    {
        [JsonPropertyName("conclusionText")] public string? ConclusionText { get; set; }
        [JsonPropertyName("resultAsset")] public EvidenceAsset? ResultAsset { get; set; }
        [JsonPropertyName("resultEmail")] public EvidenceEmail? ResultEmail { get; set; }
    }

    public async Task<(ForensicsOutcome full, EvidenceAsset? asset, EvidenceEmail? email)> RunAsync(
        CaseDraft draft, ForensicOutcomeStub stub, CancellationToken ct)
    {
        var bible = draft.CaseBible ?? throw new TaskContractException("Forensic authorship requires canonical source observations.");
        var input = draft.AssetFull.SingleOrDefault(a => a.Id == stub.InputAssetId)
            ?? throw new TaskContractException($"Forensic input '{stub.InputAssetId}' is missing.");
        var observations = CaseBibleAuthorship.OutcomeObservations(draft, stub);
        var method = ForensicMethodCatalog.Get(stub.AnalysisType);
        var localization = method.Localize(draft.Request.Language);
        var ids = CaseBibleAuthorship.ResultIds(stub);
        var title = $"{localization.Name} — {input.Title}";
        var sentAt = observations.Select(o => bible.Sources.Single(s => s.Id == o.SourceId).RecordedAt!)
            .OrderBy(t => DateTimeOffset.Parse(t)).LastOrDefault() ?? bible.Incident.OpenedAt;
        var sender = bible.People.Single(p => p.Id == bible.World.InvestigatorPersonId).Email!;
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("ForensicOutcome", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["authorship_instructions"] = CaseBibleAuthorship.Instructions
        });
        var user = prompts.RenderUser("ForensicOutcome", new Dictionary<string, object?>
        {
            ["source_context_json"] = CaseBibleAuthorship.ObservationContext(draft, observations),
            ["input_material_json"] = JsonSerializer.Serialize(new { input.Id, input.Title, readableText = CaseBibleAuthorship.Text(input) }),
            ["result_plan_json"] = JsonSerializer.Serialize(new
            {
                stub.InputAssetId, stub.InputObjectId, stub.AnalysisType, stub.Findings,
                resultAssetId = ids.AssetId, resultEmailId = ids.EmailId, title, from = sender, sentAt,
                visibility = "hidden", layout = stub.ResultLayoutId,
                noFindingsText = NoFindingsText(draft.Request.Language), limitations = stub.LimitationKeys.Select(k => localization.Limitations[k])
            }),
            ["method_contract_json"] = JsonSerializer.Serialize(new { method.Id, method.ProducibleProperties, localization.Labels, localization.Limitations })
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, $"ForensicOutcome:{stub.InputAssetId}:{stub.AnalysisType}",
            system, user, MicrotaskSchemas.ForensicOutcome, ct, validate: result =>
            {
                if (!stub.Findings)
                {
                    Require(result.ResultAsset is null && result.ResultEmail is null && result.Support.Count == 0
                        && stub.MatchedSuspectId is null, "A no-findings outcome cannot fabricate result material, support or a suspect match.");
                    MicrotaskValidation.Equal(result.ConclusionText ?? "", NoFindingsText(draft.Request.Language), "no-findings conclusion");
                    return;
                }
                Require(observations.Count > 0 && result.ResultAsset is not null && result.ResultEmail is not null,
                    "Findings require assigned result observations, a readable report and its delivery email.");
                var asset = result.ResultAsset!;
                var email = result.ResultEmail!;
                Require(asset.Id == ids.AssetId && email.Id == ids.EmailId && asset.Type == "pdf"
                    && asset.Title == title && email.Subject == title && asset.Visibility == "hidden" && email.Visibility == "hidden",
                    "Forensic author changed a preassigned result ID, format, title or visibility.");
                Require(asset.BodyDoc is not null && asset.BodyDoc.Layout == stub.ResultLayoutId
                    && asset.BodyDoc.Language == ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
                    "Forensic findings require the planned readable bodyDoc and language.");
                MicrotaskValidation.Asset(asset);
                Require(email.From == sender && email.SentAt == sentAt && email.Attachments.SequenceEqual(new[] { asset.Id }),
                    "Forensic delivery must echo its planned sender, issue time and report attachment.");
                CaseBibleAuthorship.ObservationSupport(draft, observations, result.Support, result.ConclusionText ?? "", "forensic conclusion");
                CaseBibleAuthorship.ObservationSupport(draft, observations, asset.Support, CaseBibleAuthorship.Text(asset), asset.Id);
                CaseBibleAuthorship.ObservationSupport(draft, observations, email.Support, email.Body, email.Id);
                var readable = CaseBibleAuthorship.Text(asset);
                Require(readable.Contains(stub.InputObjectId, StringComparison.Ordinal)
                    && readable.Contains(localization.Labels["itemsSubmitted"], StringComparison.Ordinal)
                    && readable.Contains(localization.Labels["limitations"], StringComparison.Ordinal),
                    "The readable report must identify the actual submitted object and print the localized submission/limitation headings.");
                Require(stub.LimitationKeys.All(key => CaseBibleAuthorship.Text(asset).Contains(localization.Limitations[key], StringComparison.Ordinal)),
                    "The readable lab report must state every planned method limitation.");
                asset.CanonicalContent = true;
            });
        return (new ForensicsOutcome
        {
            InputAssetId = stub.InputAssetId, AnalysisType = stub.AnalysisType, Findings = stub.Findings,
            MatchedSuspectId = stub.MatchedSuspectId, ConclusionText = output.ConclusionText,
            ResultAssetId = output.ResultAsset?.Id, ResultEmailId = output.ResultEmail?.Id, Support = output.Support
        }, output.ResultAsset, output.ResultEmail);
    }

    public static string NoFindingsText(string? language) => ForensicMethodCatalog.NormalizeLanguage(language) switch
    {
        "pt-BR" => "Nenhum resultado utilizável foi obtido; isso não estabelece identidade nem inocência.",
        "es-ES" => "No se obtuvo ningún resultado utilizable; esto no establece identidad ni inocencia.",
        "fr-FR" => "Aucun résultat exploitable n’a été obtenu ; cela n’établit ni l’identité ni l’innocence.",
        _ => "No usable finding was obtained; this establishes neither identity nor innocence."
    };
}

public static class ForensicResultIdNormalizer
{
    public static void EnsureUnique(IEnumerable<(ForensicsOutcome full, EvidenceAsset? asset, EvidenceEmail? email)> details,
        IEnumerable<string> reservedAssetIds, IEnumerable<string> reservedEmailIds)
    {
        var assets = reservedAssetIds.ToHashSet(StringComparer.Ordinal);
        var emails = reservedEmailIds.ToHashSet(StringComparer.Ordinal);
        foreach (var (_, asset, email) in details)
        {
            if (asset is not null) Require(assets.Add(asset.Id), $"Duplicate planned forensic asset ID '{asset.Id}'. Replan before authorship.");
            if (email is not null) Require(emails.Add(email.Id), $"Duplicate planned forensic email ID '{email.Id}'. Replan before authorship.");
        }
    }
}
