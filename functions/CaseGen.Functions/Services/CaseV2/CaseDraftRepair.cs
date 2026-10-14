using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Newtonsoft.Json.Linq;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class CaseDraftRepair
{
    [JsonPropertyName("baseRevision")] public int BaseRevision { get; set; }
    [JsonPropertyName("changes")] public List<Change> Changes { get; set; } = new();

    public sealed class Change
    {
        [JsonPropertyName("target")] public string Target { get; set; } = "";
        [JsonPropertyName("reason")] public string Reason { get; set; } = "";
        [JsonPropertyName("value")] public JsonElement Value { get; set; }
    }

    public static string BuildSchema(CaseDraft draft, string publicSchema)
    {
        var (schemas, definitions) = ComponentSchemas(publicSchema);
        var alternatives = new JArray();
        foreach (var component in draft.GetComponents())
        {
            if (component.Key == "caseBible") continue;
            alternatives.Add(new JObject
            {
                ["type"] = "object", ["additionalProperties"] = false,
                ["required"] = new JArray("target", "reason", "value"),
                ["properties"] = new JObject
                {
                    ["target"] = new JObject { ["type"] = "string", ["enum"] = new JArray(component.Key) },
                    ["reason"] = new JObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 2000, ["pattern"] = @"\S" },
                    ["value"] = SchemaFor(component.Key, schemas)
                }
            });
        }
        return new JObject
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["required"] = new JArray("baseRevision", "changes"),
            ["properties"] = new JObject
            {
                ["baseRevision"] = new JObject { ["type"] = "integer", ["enum"] = new JArray(draft.Revision) },
                ["changes"] = new JObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 40,
                    ["items"] = new JObject { ["oneOf"] = alternatives } }
            },
            ["$defs"] = definitions
        }.ToString(Newtonsoft.Json.Formatting.None);
    }

    public CaseDraft Apply(CaseDraft original, string publicSchema)
    {
        Require(BaseRevision == original.Revision, "Repair belongs to a different draft revision.");
        MicrotaskValidation.Unique(Changes.Select(c => c.Target), "repair targets");
        var before = original.GetComponents();
        Require(Changes.Count is >= 1 and <= 40, "Repair must contain 1-40 component replacements.");
        var candidate = original.Copy();
        foreach (var change in Changes)
        {
            Require(before.ContainsKey(change.Target), $"Unknown repair target '{change.Target}'.");
            Require(change.Target != "caseBible", "Canonical facts and evidence plans cannot be replaced by a repair.");
            Replace(candidate, change);
        }
        Validate(candidate, original, publicSchema);
        var after = candidate.GetComponents();
        var changed = Changes.Where(c => !JsonNode.DeepEquals(before[c.Target], after[c.Target]))
            .Select(c => c.Target).ToHashSet(StringComparer.Ordinal);
        Require(changed.Count > 0, "Repair does not change any component.");
        CheckDependencies(original, candidate, changed, Changes.Select(c => c.Target).ToHashSet(StringComparer.Ordinal));
        candidate.Revision = original.Revision + 1;
        return candidate;
    }

    public static void Validate(CaseDraft candidate, CaseDraft baseline, string publicSchema)
    {
        var (schemas, definitions) = ComponentSchemas(publicSchema);
        var components = candidate.GetComponents();
        var properties = new JObject();
        foreach (var component in components)
            properties[component.Key] = SchemaFor(component.Key, schemas);
        var schema = new JObject
        {
            ["type"] = "object", ["required"] = new JArray(components.Select(c => c.Key)), ["additionalProperties"] = false,
            ["properties"] = properties, ["$defs"] = definitions
        };
        StructuredJson.Validate(components.ToJsonString(), StructuredJson.ParseSchema(schema.ToString()));
        Require(candidate.CaseId == baseline.CaseId && candidate.CulpritId == baseline.CulpritId,
            "Repair must preserve caseId and culpritId.");
        Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(candidate.Bible, CaseDraft.CanonicalJsonOptions),
            JsonSerializer.SerializeToNode(baseline.Bible, CaseDraft.CanonicalJsonOptions)), "Repair must preserve the canonical CaseBible.");
        if (candidate.Bible is not null)
        {
            Require(candidate.Metadata.Location == baseline.Metadata.Location &&
                candidate.Metadata.IncidentDate == baseline.Metadata.IncidentDate && candidate.Metadata.OpenedAt == baseline.Metadata.OpenedAt &&
                JsonNode.DeepEquals(JsonSerializer.SerializeToNode(candidate.Metadata.Victim), JsonSerializer.SerializeToNode(baseline.Metadata.Victim)),
                "Repair cannot change canonical location, dates or victim identity.");
        }
        Require(candidate.SuspectFull.Any(s => s.Id == candidate.CulpritId), "Culprit does not exist.");
        CheckIdentities(candidate.SuspectFull.Select(s => s.Id), baseline.SuspectFull.Select(s => s.Id), "suspects");
        CheckIdentities(candidate.AssetFull.Select(a => a.Id), baseline.AssetFull.Select(a => a.Id), "initial assets");
        CheckIdentities(candidate.ResultAssets.Select(a => a.Id), baseline.ResultAssets.Select(a => a.Id), "result assets");
        CheckIdentities(candidate.FollowUpEmails.Select(e => e.Id), baseline.FollowUpEmails.Select(e => e.Id), "initial emails");
        CheckIdentities(candidate.ResultEmails.Select(e => e.Id), baseline.ResultEmails.Select(e => e.Id), "result emails");
        CheckIdentities(candidate.Questions.Select(q => q.Id), baseline.Questions.Select(q => q.Id), "questions");
        CheckIdentities(candidate.ForensicFull.Select(Pair), baseline.ForensicFull.Select(Pair), "forensic pairs");
        CheckIdentities(candidate.AnalysisTypes.Select(t => t.Type), baseline.AnalysisTypes.Select(t => t.Type), "analysis types");
        MicrotaskValidation.Equal(candidate.Metadata.Difficulty, candidate.Request.Difficulty ?? "Detective", "metadata.difficulty");
        MicrotaskValidation.Equal(candidate.Metadata.RequiredRank, candidate.Request.RequiredRank ?? candidate.Request.Difficulty ?? "Detective", "metadata.requiredRank");
        if (candidate.Request.Title is not null) MicrotaskValidation.Equal(candidate.Metadata.Title, candidate.Request.Title, "metadata.title");
        if (candidate.Request.Location is not null) MicrotaskValidation.Equal(candidate.Metadata.Location, candidate.Request.Location, "metadata.location");
        Require(DateTimeOffset.Parse(candidate.Metadata.OpenedAt, CultureInfo.InvariantCulture) >
                DateTimeOffset.Parse(candidate.Metadata.IncidentDate, CultureInfo.InvariantCulture),
            "openedAt must be later than incidentDate.");
        foreach (var suspect in candidate.SuspectFull)
            MicrotaskValidation.Equal(suspect.Name, baseline.SuspectFull.Single(s => s.Id == suspect.Id).Name, "suspect.name");
        foreach (var asset in candidate.AssetFull.Concat(candidate.ResultAssets))
        {
            var previous = baseline.AssetFull.Concat(baseline.ResultAssets).Single(a => a.Id == asset.Id);
            MicrotaskValidation.Equal(asset.Type, previous.Type, "asset.type");
            MicrotaskValidation.Asset(asset);
            if (candidate.AssetFull.Contains(asset))
            {
                Require(asset.BodyDoc is not null || asset.Type is "photo" or "audio", "Initial documents require bodyDoc.");
                var expected = candidate.Bible?.Sources.Single(s => s.Id == asset.Id).Visibility ?? "initial";
                MicrotaskValidation.Equal(asset.Visibility, expected, "investigation asset visibility");
            }
        }
        foreach (var email in candidate.FollowUpEmails.Concat(candidate.ResultEmails))
        {
            var previous = baseline.FollowUpEmails.Concat(baseline.ResultEmails).Single(e => e.Id == email.Id);
            MicrotaskValidation.Equal(email.Visibility, previous.Visibility, "email.visibility");
            MicrotaskValidation.Email(email, candidate);
        }
        var assets = candidate.AssetFull.Concat(candidate.ResultAssets).ToDictionary(a => a.Id, StringComparer.Ordinal);
        var emails = MicrotaskValidation.EmailIds(candidate);
        foreach (var outcome in candidate.ForensicFull)
        {
            var previous = baseline.ForensicFull.Single(f => Pair(f) == Pair(outcome));
            Require(outcome.Findings == previous.Findings && outcome.MatchedSuspectId == previous.MatchedSuspectId,
                "Repair cannot fabricate findings or change a forensic suspect match.");
            Require(outcome.ResultAssetId == previous.ResultAssetId && outcome.ResultEmailId == previous.ResultEmailId,
                "Repair must preserve forensic result references.");
            Require(assets.TryGetValue(outcome.InputAssetId, out var input) &&
                candidate.AnalysisTypes.Any(t => t.Type == outcome.AnalysisType && t.AvailableFor.Contains(input.Type)),
                $"Unsupported forensic pair '{Pair(outcome)}'.");
            Require(outcome.MatchedSuspectId is null || candidate.SuspectFull.Any(s => s.Id == outcome.MatchedSuspectId), "Forensic match references an unknown suspect.");
            Require(outcome.MatchedAssetId == previous.MatchedAssetId &&
                (outcome.MatchedAssetId is null || assets.ContainsKey(outcome.MatchedAssetId)), "Repair cannot change or invent a matched asset.");
            Require(!outcome.Findings || (outcome.ResultAssetId is not null && assets.ContainsKey(outcome.ResultAssetId) &&
                outcome.ResultEmailId is not null && emails.Contains(outcome.ResultEmailId)), "Findings require existing result material.");
        }
        MicrotaskValidation.Rules(candidate.Rules, new CaseDraft
        {
            AssetStubs = candidate.AssetStubs, AssetFull = candidate.AssetFull, ResultAssets = candidate.ResultAssets,
            SuspectStubs = candidate.SuspectStubs, FollowUpEmails = candidate.FollowUpEmails, ResultEmails = candidate.ResultEmails,
            AnalysisTypes = candidate.AnalysisTypes
        });
        Require(candidate.RequiredEvidenceIds.All(assets.ContainsKey), "Required evidence references unknown assets.");
        var findings = candidate.ForensicFull.Where(f => f.Findings).Select(Pair).ToHashSet(StringComparer.Ordinal);
        Require(candidate.RequiredAnalysisIds.All(findings.Contains), "Required analysis does not have findings.");
        foreach (var question in candidate.Questions)
        {
            var previous = baseline.Questions.Single(q => q.Id == question.Id);
            CheckIdentities(question.Options.Select(o => o.Id), previous.Options.Select(o => o.Id), "options");
            MicrotaskValidation.Unique(question.Options.Select(o => o.Label), "option labels");
            Require(question.Weight == previous.Weight, "Repair must preserve question weight.");
            Require(question.Options.Any(o => o.Id == question.CorrectOptionId), "Correct option is missing.");
        }
        foreach (var item in candidate.TemporalEvents)
        {
            Require(item.Payload is not null, "Temporal event requires a payload.");
            Require(item.Payload!.AssetId is null || assets.ContainsKey(item.Payload.AssetId), "Temporal event asset is unknown.");
            Require(item.Payload.EmailId is null || emails.Contains(item.Payload.EmailId), "Temporal event email is unknown.");
            Require(item.Payload.AssetId is not null || item.Payload.EmailId is not null || !string.IsNullOrWhiteSpace(item.Payload.Message),
                "Temporal event payload is empty.");
        }
        CheckIdentities(candidate.TemporalEvents.Select(e => e.Id), baseline.TemporalEvents.Select(e => e.Id), "temporal events");
    }

    private static void Replace(CaseDraft draft, Change change)
    {
        T Read<T>() => change.Value.Deserialize<T>(CaseDraft.CanonicalJsonOptions)
            ?? throw new TaskContractException($"Repair target '{change.Target}' is null.");
        switch (change.Target)
        {
            case "metadata": draft.Metadata = Read<PlotMetadata>(); break;
            case "briefing": draft.Briefing = Read<PlotBriefingEmail>(); break;
            case "timeline": draft.Timeline = Read<List<EvidenceTimelineEntry>>(); break;
            case "temporalEvents": draft.TemporalEvents = Read<List<EvidenceTemporalEvent>>(); break;
            case "analysisTypes": draft.AnalysisTypes = Read<List<ForensicsAnalysisType>>(); break;
            case "rules": draft.Rules = Read<List<RulesRule>>(); break;
            case "solution":
                var solution = Read<SolutionResult>();
                Require(solution.CulpritId == draft.CulpritId, "Repair cannot replace culpritId.");
                Require(solution.MinimumScore == 0.7 && solution.MaxAttempts == 3 &&
                    solution.PartialCreditRules.CulpritWeight == 0.4 && solution.PartialCreditRules.EvidenceWeight == 0.2 &&
                    solution.PartialCreditRules.AnalysisWeight == 0.2 && solution.PartialCreditRules.QuestionsWeight == 0.2,
                    "Repair cannot change the scoring contract.");
                draft.RequiredEvidenceIds = solution.RequiredEvidenceIds;
                draft.RequiredAnalysisIds = solution.RequiredAnalysisIds;
                draft.Questions = solution.Questions;
                draft.Explanation = solution.Explanation;
                draft.ExplanationSupport = solution.ExplanationSupport;
                break;
            default:
                if (change.Target.StartsWith("suspect.", StringComparison.Ordinal))
                {
                    var suspect = Read<PlotSuspect>();
                    MicrotaskValidation.Equal(suspect.Id, change.Target, "repair suspect.id");
                    ReplaceIn(draft.SuspectFull, s => s.Id, change.Target, suspect);
                }
                else if (change.Target.Contains(':'))
                {
                    var outcome = Read<ForensicsOutcome>();
                    MicrotaskValidation.Equal(Pair(outcome), change.Target, "repair forensic pair");
                    ReplaceIn(draft.ForensicFull, Pair, change.Target, outcome);
                }
                else if (change.Target.StartsWith("asset.", StringComparison.Ordinal))
                {
                    var asset = Read<EvidenceAsset>();
                    MicrotaskValidation.Equal(asset.Id, change.Target, "repair asset.id");
                    asset.CanonicalContent = draft.AssetFull.Concat(draft.ResultAssets).Single(a => a.Id == change.Target).CanonicalContent;
                    ReplaceIn(draft.AssetFull.Any(a => a.Id == change.Target) ? draft.AssetFull : draft.ResultAssets, a => a.Id, change.Target, asset);
                }
                else if (change.Target.StartsWith("email.", StringComparison.Ordinal))
                {
                    var email = Read<EvidenceEmail>();
                    MicrotaskValidation.Equal(email.Id, change.Target, "repair email.id");
                    ReplaceIn(draft.FollowUpEmails.Any(e => e.Id == change.Target) ? draft.FollowUpEmails : draft.ResultEmails, e => e.Id, change.Target, email);
                }
                else throw new TaskContractException($"Unsupported repair target '{change.Target}'.");
                break;
        }
    }

    private static void CheckDependencies(CaseDraft before, CaseDraft after, HashSet<string> changed, HashSet<string> covered)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        var beforeMetadata = before.GetComponents()["metadata"]!;
        var afterMetadata = after.GetComponents()["metadata"]!;
        if (changed.Any(t => t.StartsWith("suspect.", StringComparison.Ordinal)) || changed.Contains("timeline") ||
            changed.Contains("metadata") && new[] { "title", "description", "location", "incidentDate", "openedAt", "category", "briefing", "victim" }
                .Any(field => !JsonNode.DeepEquals(beforeMetadata[field], afterMetadata[field])))
        {
            required.UnionWith(before.AssetFull.Concat(before.ResultAssets).Select(a => a.Id));
            required.UnionWith(before.FollowUpEmails.Concat(before.ResultEmails).Select(e => e.Id));
            required.UnionWith(before.ForensicFull.Select(Pair));
            required.UnionWith(new[] { "briefing", "solution" });
        }
        foreach (var outcome in before.ForensicFull)
        {
            if (!changed.Contains(Pair(outcome)) && !changed.Contains(outcome.InputAssetId) &&
                (outcome.ResultAssetId is null || !changed.Contains(outcome.ResultAssetId)) &&
                (outcome.ResultEmailId is null || !changed.Contains(outcome.ResultEmailId)) &&
                !changed.Contains("analysisTypes")) continue;
            required.Add(Pair(outcome));
            if (outcome.ResultAssetId is not null) required.Add(outcome.ResultAssetId);
            if (outcome.ResultEmailId is not null) required.Add(outcome.ResultEmailId);
            required.Add("solution");
        }
        if (changed.Overlaps(new[] { "rules", "temporalEvents" })) required.Add("solution");
        foreach (var asset in before.AssetFull.Concat(before.ResultAssets).Where(a => changed.Contains(a.Id)))
        {
            required.UnionWith(before.FollowUpEmails.Concat(before.ResultEmails).Where(e => e.Attachments.Contains(asset.Id)).Select(e => e.Id));
            if (before.Bible is not null
                ? before.Bible.Sources.Single(s => s.Id == "email.briefing").Attachments.Contains(asset.Id)
                : before.AssetFull.Take(2).Any(a => a.Id == asset.Id))
                required.Add("briefing");
            if (before.RequiredEvidenceIds.Contains(asset.Id)) required.Add("solution");
        }
        var missing = required.Except(covered).ToArray();
        Require(missing.Length == 0, $"Repair must regenerate/reconcile dependent components: {string.Join(", ", missing)}.");
    }

    private static (Dictionary<string, JObject> Schemas, JObject Definitions) ComponentSchemas(string publicSchema)
    {
        var definitions = new JObject();
        JObject Micro(string json)
        {
            var schema = JObject.Parse(json);
            if (schema["$defs"] is JObject defs)
                foreach (var definition in defs.Properties()) definitions[definition.Name] = definition.Value.DeepClone();
            schema.Remove("$defs");
            return schema;
        }
        var canonical = JObject.Parse(publicSchema);
        var publicDefs = (JObject)canonical["$defs"]!;
        foreach (var definition in publicDefs.Properties())
        {
            var node = definition.Value.DeepClone();
            foreach (var reference in ((JContainer)node).DescendantsAndSelf().OfType<JProperty>().Where(p => p.Name == "$ref"))
                reference.Value = reference.Value.Value<string>()!.Replace("#/$defs/", "#/$defs/Public", StringComparison.Ordinal);
            definitions["Public" + definition.Name] = node;
        }
        var forensic = (JObject)definitions["PublicForensicOutcome"]!.DeepClone();
        forensic["required"] = new JArray(((JObject)forensic["properties"]!).Properties().Select(p => p.Name));
        forensic["properties"]!["conclusionText"] = Micro(MicrotaskSchemas.ForensicOutcome)["properties"]!["conclusionText"]!.DeepClone();
        forensic["properties"]!["support"] = Micro(MicrotaskSchemas.ForensicOutcome)["properties"]!["support"]!.DeepClone();
        ((JArray)forensic["required"]!).Add("support");
        var plannedOutcome = Micro(MicrotaskSchemas.ForensicsPlan)["properties"]!["outcomes"]!["items"]!["properties"]!;
        forensic["properties"]!["analysisType"] = plannedOutcome["analysisType"]!.DeepClone();
        foreach (var name in new[] { "matchedSuspectId", "matchedAssetId", "resultAssetId", "resultEmailId" })
        {
            var properties = (JObject)forensic["properties"]!;
            properties[name] = new JObject { ["anyOf"] = new JArray(properties[name]!.DeepClone(), new JObject { ["type"] = "null" }) };
        }
        var rules = (JObject)Micro(MicrotaskSchemas.Rules)["properties"]!["rules"]!;
        rules["maxItems"] = 14;
        var solution = (JObject)definitions["PublicSolution"]!.DeepClone();
        solution["required"] = new JArray(((JObject)solution["properties"]!).Properties().Select(p => p.Name));
        var skeleton = Micro(MicrotaskSchemas.SolutionSkeleton);
        solution["properties"]!["requiredEvidenceIds"] = skeleton["properties"]!["requiredEvidenceIds"]!.DeepClone();
        solution["properties"]!["requiredAnalysisIds"] = skeleton["properties"]!["requiredAnalysisIds"]!.DeepClone();
        solution["properties"]!["questions"] = new JObject
        {
            ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 4, ["items"] = Micro(MicrotaskSchemas.Question)
        };
        solution["properties"]!["explanation"] = Micro(MicrotaskSchemas.Explanation)["properties"]!["explanation"]!.DeepClone();
        solution["properties"]!["explanationSupport"] = Micro(MicrotaskSchemas.Explanation)["properties"]!["support"]!.DeepClone();
        ((JArray)solution["required"]!).Add("explanationSupport");
        return (new Dictionary<string, JObject>
        {
            ["metadata"] = (JObject)Micro(MicrotaskSchemas.PlotOutline)["properties"]!["metadata"]!,
            ["suspect"] = Micro(MicrotaskSchemas.SuspectCard), ["asset"] = Micro(MicrotaskSchemas.AssetCard),
            ["email"] = (JObject)Micro(MicrotaskSchemas.InitialEmails)["properties"]!["emails"]!["items"]!,
            ["forensic"] = forensic, ["briefing"] = Micro(MicrotaskSchemas.BriefingEmail),
            ["timeline"] = (JObject)Micro(MicrotaskSchemas.Timeline)["properties"]!["timeline"]!,
            ["temporalEvents"] = (JObject)Micro(MicrotaskSchemas.Timeline)["properties"]!["temporalEvents"]!,
            ["analysisTypes"] = (JObject)Micro(MicrotaskSchemas.CaseBible)["properties"]!["analysisTypes"]!,
            ["rules"] = rules, ["solution"] = solution, ["caseBible"] = Micro(MicrotaskSchemas.CaseBible)
        }, definitions);
    }

    private static JObject SchemaFor(string target, Dictionary<string, JObject> schemas)
    {
        var key = target.Contains(':') ? "forensic" : target.StartsWith("suspect.", StringComparison.Ordinal) ? "suspect"
            : target.StartsWith("asset.", StringComparison.Ordinal) ? "asset"
            : target.StartsWith("email.", StringComparison.Ordinal) ? "email" : target;
        return (JObject)schemas[key].DeepClone();
    }

    private static string Pair(ForensicsOutcome outcome) => $"{outcome.InputAssetId}:{outcome.AnalysisType}";

    private static void CheckIdentities(IEnumerable<string> current, IEnumerable<string> previous, string field)
    {
        var ids = current.ToArray();
        MicrotaskValidation.Unique(ids, field);
        Require(ids.ToHashSet(StringComparer.Ordinal).SetEquals(previous), $"Repair must preserve {field} IDs.");
    }

    private static void ReplaceIn<T>(List<T> items, Func<T, string> id, string target, T replacement)
    {
        var index = items.FindIndex(item => id(item) == target);
        Require(index >= 0, $"Repair target '{target}' is not present.");
        items[index] = replacement;
    }
}
