using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CaseGen.Functions.Services.CaseV2;

public static class MicrotaskSchemas
{
    private static readonly string[] AssetTypes = { "photo", "pdf", "document", "digital" };
    private static readonly Dictionary<string, JObject> Definitions = BuildDefinitions();

    public static string PlotOutline { get; } = Schema(Object(
        ("metadata", Object(
            ("title", Text(200, 3)), ("description", Text(3000, 10)), ("location", Text(300)),
            ("incidentDate", DateTimeText()), ("openedAt", DateTimeText()),
            ("difficulty", Enum(GenerationRequestValidator.Ranks)), ("requiredRank", Enum(GenerationRequestValidator.Ranks)),
            ("category", Text(100)), ("briefing", Text(3000)), ("tags", Array(Text(100), 0, 10)),
            ("victim", Nullable(Object(("name", Text(200)), ("age", Integer(0, 120)), ("occupation", Text(200))))))),
        ("suspects", Array(Object(("id", Id("suspect")), ("name", Text(200)), ("role", Text(500)), ("isCulprit", Boolean())), 3, 6)),
        ("culpritId", Id("suspect"))));

    public static string SuspectCard { get; } = Schema(Object(
        ("id", Id("suspect")), ("name", Text(200)), ("age", Integer(0, 120)),
        ("occupation", Text(300)), ("relationship", Text(500)), ("motive", Text(3000)),
        ("alibi", Text(3000)), ("alibiVerified", Boolean()), ("background", Text(5000)), ("support", Ref("Support"))));

    public static string AssetPlan { get; } = Schema(Object(("assets", Array(Object(
        ("id", Id("asset")), ("type", Enum(AssetTypes)), ("title", Text(200)),
        ("role", Text(1000)), ("visibility", Enum("initial"))), 4, 10))));

    public static string AssetCard { get; } = Schema(Ref("Asset"));

    public static string Timeline { get; } = Schema(Object(
        ("timeline", Array(Object(("time", DateTimeText()), ("event", Text(3000)),
            ("source", Enum("investigation", "witness", "sensor", "forensic")),
            ("verified", Boolean()), ("importance", Enum("low", "medium", "high", "critical"))), 2, 8)),
        ("temporalEvents", Array(Object(("id", Id("tevt")), ("triggerAtMinutes", Integer(5, 120)),
            ("type", Enum("memo", "witness", "alert", "email")),
            ("payload", Object(("emailId", Nullable(Id("email"))), ("assetId", Nullable(Id("asset"))),
                ("message", Nullable(Text(3000)))))), 0, 3))));

    public static string ForensicsPlan { get; } = Schema(Object(
        ("analysisTypes", Array(Object(("type", AnalysisType()), ("durationMinutes", Integer(1, 600)),
            ("availableFor", Array(Enum(AssetTypes), 1, AssetTypes.Length, unique: true))), 2, 4)),
        ("outcomes", Array(Object(("inputAssetId", Id("asset")), ("analysisType", AnalysisType()),
            ("findings", Boolean()), ("matchedSuspectId", Nullable(Id("suspect"))), ("role", Text(1000))), 1, 5))));

    public static string ForensicOutcome { get; } = Schema(Object(
        ("conclusionText", Text(5000)), ("support", Ref("Support")),
        ("resultAsset", Nullable(Ref("Asset"))), ("resultEmail", Nullable(Ref("Email")))));

    public static string BriefingEmail { get; } = Schema(Object(
        ("from", Text(300)), ("subject", Text(300)), ("body", Text(40000)), ("support", Ref("Support"))));

    public static string InitialEmails { get; } = Schema(Object(("emails", Array(Ref("Email"), 0, 3))));

    public static string Rules { get; } = Schema(Object(("rules", Array(Object(
        ("ruleId", Id("rule")), ("description", Nullable(Text(2000))),
        ("trigger", Ref("Trigger")), ("actions", Array(Ref("Action"), 1, 6))), 0, 4))));

    public static string SolutionSkeleton { get; } = Schema(Object(
        ("requiredEvidenceIds", Array(Id("asset"), 1, 3, unique: true)),
        ("requiredAnalysisIds", Array(AnalysisId(), 0, 5, unique: true)),
        ("questionTopics", Array(Object(("id", Id("q")), ("topic", Text(200)), ("weight", Weight())), 2, 4))));

    public static string Question { get; } = Schema(Object(
        ("id", Id("q")), ("prompt", Text(500)),
        ("options", Array(Object(("id", Id("opt")), ("label", Text(1000))), 3, 4)),
        ("correctOptionId", Id("opt")), ("weight", Weight()), ("support", Ref("Support"))));

    public static string Explanation { get; } = Schema(Object(
        ("explanation", Text(10000, 40)), ("support", Ref("Support"))));

    public static string CaseBible { get; } = Schema(Object(
        ("crimeFactId", Id("fact")),
        ("entities", Array(Object(("id", EntityId()), ("kind", Enum("person", "place", "object", "organization")),
            ("name", Text(200)), ("knownFactIds", Ids("fact", 0, 30))), 4, 20)),
        ("facts", Array(Object(("id", Id("fact")), ("statement", Text(2000)),
            ("kind", Enum("event", "observation", "motive", "identification", "exclusion", "no_findings", "similarity", "context")),
            ("entityIds", Array(EntityId(), 1, 10, unique: true)), ("time", Nullable(Ref("TimeWindow"))),
            ("isPrivate", Boolean()), ("decisive", Boolean())), 4, 30)),
        ("claims", Array(Object(("id", Id("claim")), ("statement", Text(2000)),
            ("speakerEntityId", EntityId()), ("truthStatus", Enum("accurate", "unverified", "mistaken", "deceptive")),
            ("relatedFactIds", Ids("fact", 1, 10)), ("time", Nullable(Ref("TimeWindow")))), 0, 15)),
        ("sources", Array(Object(("id", SourceId()),
            ("kind", Enum("asset", "result_asset", "email", "result_email", "briefing", "profile")),
            ("type", Enum("photo", "pdf", "document", "digital", "email", "profile")),
            ("title", Text(200)), ("role", Text(1000)), ("authorEntityId", EntityId()),
            ("from", Nullable(Text(300))), ("recordedAt", DateTimeText()), ("visibility", Enum("initial", "hidden")),
            ("factIds", Ids("fact", 0, 20)), ("claimIds", Ids("claim", 0, 10)),
            ("attachments", Ids("asset", 0, 10)), ("discovery", Nullable(Ref("Trigger")))), 8, 30)),
        ("chronology", Array(Object(("referenceId", ReferenceId()), ("sourceId", SourceId()),
            ("source", Enum("investigation", "witness", "sensor", "forensic")),
            ("importance", Enum("low", "medium", "high", "critical"))), 2, 8)),
        ("analysisTypes", Array(Object(("type", AnalysisType()), ("durationMinutes", Integer(1, 600)),
            ("availableFor", Array(Enum(AssetTypes), 1, 4, unique: true))), 1, 4)),
        ("outcomes", Array(Object(("inputAssetId", Id("asset")), ("analysisType", AnalysisType()),
            ("findings", Boolean()), ("matchedSuspectId", Nullable(Id("suspect"))),
            ("matchBasis", Enum("none", "individualizing", "similarity", "exclusion")),
            ("factIds", Ids("fact", 1, 10)), ("resultAssetId", Nullable(Id("asset"))),
            ("resultEmailId", Nullable(Id("email"))), ("limitations", Text(2000))), 0, 5)),
        ("alibis", Array(Object(("suspectId", Id("suspect")), ("verifiedFactIds", Ids("fact", 0, 10)),
            ("sourceIds", Array(SourceId(), 0, 10, unique: true))), 3, 6)),
        ("proofSets", Array(Object(("id", Id("proof")), ("conclusionFactId", Id("fact")),
            ("factIds", Ids("fact", 2, 10)), ("sourceIds", Array(SourceId(), 2, 8, unique: true)),
            ("analysisIds", Array(AnalysisId(), 0, 5, unique: true))), 1, 3)),
        ("primaryProofSetId", Id("proof")),
        ("questions", Array(Object(("id", Id("q")), ("topic", Text(200)), ("weight", Weight()), ("answer", Text(1000)),
            ("factIds", Ids("fact", 1, 10)), ("sourceIds", Array(SourceId(), 1, 8, unique: true))), 2, 4)),
        ("explanationFactIds", Ids("fact", 2, 20))));

    public static string Solver { get; } = Schema(Object(
        ("suspectId", Id("suspect")), ("evidenceIds", Array(Id("asset"), 0, 4, unique: true)),
        ("analysisIds", Array(AnalysisId(), 0, 5, unique: true)),
        ("answers", Array(Object(("questionId", Id("q")), ("optionId", Id("opt"))), 0, 4)),
        ("rationale", Nullable(Text(3000)))));

    private static Dictionary<string, JObject> BuildDefinitions()
    {
        var section = new JObject
        {
            ["oneOf"] = new JArray(
                Object(("kind", Enum("narrative", "callout")), ("heading", Nullable(Text(300))), ("text", Text(40000))),
                Object(("kind", Enum("table")), ("heading", Nullable(Text(300))), ("table", Ref("Table"))),
                Object(("kind", Enum("keyValue")), ("heading", Nullable(Text(300))), ("items", Array(Ref("KeyValue"), 1, 30))),
                Object(("kind", Enum("transcript")), ("heading", Nullable(Text(300))), ("transcript", Array(Ref("TranscriptEntry"), 1, 100))),
                Object(("kind", Enum("code")), ("heading", Nullable(Text(300))), ("code", Ref("Code"))))
        };
        return new Dictionary<string, JObject>
        {
            ["TimeWindow"] = Object(("precision", Enum("exact", "interval", "estimate")),
                ("earliest", DateTimeText()), ("latest", DateTimeText())),
            ["Support"] = Array(Object(("referenceId", ReferenceId()), ("excerpt", Text(5000))), 0, 30),
            ["Asset"] = Object(("id", Id("asset")), ("type", Enum(AssetTypes)), ("title", Text(200)),
                ("description", Text(3000)), ("visibility", Enum("initial", "hidden")), ("category", Text(100)),
                ("body", new JObject { ["type"] = new JArray("string", "null"), ["maxLength"] = 40000 }),
                ("bodyDoc", Nullable(Ref("Document"))), ("support", Ref("Support"))),
            ["Email"] = Object(("id", Id("email")), ("from", Text(300)), ("subject", Text(300)),
                ("body", Text(40000)), ("sentAt", DateTimeText()), ("priority", Nullable(Enum("normal", "high", "urgent"))),
                ("attachments", Array(Id("asset"), 0, 10, unique: true)), ("visibility", Enum("initial", "hidden")),
                ("support", Ref("Support"))),
            ["Document"] = Object(
                ("layout", Enum("PoliceReport", "InterviewTranscript", "WitnessStatement", "Memo", "ForensicReport",
                    "EvidenceLog", "GeneralReport", "CallLog", "PosExport", "PhoneDump", "SensorLog", "BrowserHistory",
                    "BankStatement", "GpsTrack", "FileListing", "ChatExport", "EmailExport", "AccessLog")),
                ("title", Text(200)), ("subtitle", Nullable(Text(300))), ("classification", Text(200)),
                ("header", Array(Ref("KeyValue"), 0, 20)), ("sections", Array(Ref("Section"), 1, 30)),
                ("signature", Nullable(Ref("Signature")))),
            ["Section"] = section,
            ["KeyValue"] = Object(("label", Text(200)), ("value", Text(3000))),
            ["Table"] = Object(("columns", Array(Text(200), 1, 12)),
                ("rows", Array(Array(new JObject { ["type"] = "string", ["maxLength"] = 3000 }, 1, 12), 1, 100)),
                ("footnote", Nullable(Text(3000)))),
            ["TranscriptEntry"] = Object(("timestamp", Nullable(Text(100))), ("speaker", Nullable(Text(200))), ("text", Text(5000))),
            ["Code"] = Object(("language", Nullable(Text(100))), ("content", Text(40000))),
            ["Signature"] = Object(("name", Text(200)), ("title", Nullable(Text(200))),
                ("badge", Nullable(Text(100))), ("signedAt", Nullable(DateTimeText()))),
            ["Trigger"] = Object(
                ("type", Enum("forensics_complete", "attachment_download", "asset_viewed", "email_opened",
                    "time_elapsed", "suspect_viewed", "multiple_conditions")),
                ("inputAssetId", Nullable(Id("asset"))), ("analysisType", Nullable(AnalysisType())),
                ("emailId", Nullable(Id("email"))), ("assetId", Nullable(Id("asset"))),
                ("suspectId", Nullable(Id("suspect"))), ("atMinutes", Nullable(Integer(0, 600))),
                ("operator", Nullable(Enum("AND", "OR"))), ("conditions", Nullable(Array(Ref("Trigger"), 2, 6)))),
            ["Action"] = Object(
                ("type", Enum("reveal_email", "reveal_asset", "reveal_suspect", "add_email_attachment",
                    "send_notification", "update_suspect_status", "mark_alibi_verified")),
                ("emailId", Nullable(Id("email"))), ("assetId", Nullable(Id("asset"))), ("suspectId", Nullable(Id("suspect"))),
                ("level", Nullable(Enum("info", "warn", "critical"))), ("message", Nullable(Text(3000))),
                ("status", Nullable(Enum("suspect", "cleared", "confirmed_culprit"))), ("verified", Nullable(Boolean())))
        };
    }

    private static string Schema(JObject root)
    {
        var definitions = new JObject();
        void IncludeReferences(JObject value)
        {
            foreach (var reference in value.DescendantsAndSelf().OfType<JProperty>()
                         .Where(p => p.Name == "$ref").Select(p => p.Value.Value<string>()!).ToArray())
            {
                var name = reference["#/$defs/".Length..];
                if (definitions.ContainsKey(name)) continue;
                var definition = (JObject)Definitions[name].DeepClone();
                definitions[name] = definition;
                IncludeReferences(definition);
            }
        }
        IncludeReferences(root);
        if (root["$ref"] is not null)
        {
            var name = root["$ref"]!.Value<string>()!["#/$defs/".Length..];
            root = (JObject)Definitions[name].DeepClone();
        }
        if (definitions.Count > 0) root["$defs"] = definitions;
        return root.ToString(Formatting.None);
    }

    private static JObject Object(params (string Name, JObject Schema)[] properties) => new()
    {
        ["type"] = "object", ["additionalProperties"] = false,
        ["required"] = new JArray(properties.Select(p => p.Name)),
        ["properties"] = new JObject(properties.Select(p => new JProperty(p.Name, p.Schema)))
    };

    private static JObject Text(int maximum, int minimum = 1) => new()
    {
        ["type"] = "string", ["minLength"] = minimum, ["maxLength"] = maximum, ["pattern"] = @"\S"
    };

    private static JObject Id(string prefix) => new()
    {
        ["type"] = "string", ["pattern"] = $"^{prefix}\\.[a-z0-9_]+$", ["maxLength"] = 128
    };

    private static JObject DateTimeText() => new() { ["type"] = "string", ["format"] = "date-time", ["maxLength"] = 50 };
    private static JObject AnalysisType() => new() { ["type"] = "string", ["pattern"] = "^[A-Z][A-Za-z0-9_]*$", ["maxLength"] = 100 };
    private static JObject AnalysisId() => new() { ["type"] = "string", ["pattern"] = "^asset\\.[a-z0-9_]+:[A-Za-z0-9_]+$", ["maxLength"] = 230 };
    private static JObject EntityId() => new() { ["type"] = "string", ["pattern"] = "^(entity|suspect)\\.[a-z0-9_]+$", ["maxLength"] = 128 };
    private static JObject SourceId() => new() { ["type"] = "string", ["pattern"] = "^(asset|email|suspect)\\.[a-z0-9_]+$", ["maxLength"] = 128 };
    private static JObject ReferenceId() => new() { ["type"] = "string", ["pattern"] = "^(fact|claim)\\.[a-z0-9_]+$", ["maxLength"] = 128 };
    private static JObject Ids(string prefix, int minimum, int maximum) => Array(Id(prefix), minimum, maximum, unique: true);
    private static JObject Boolean() => new() { ["type"] = "boolean" };
    private static JObject Integer(int minimum, int maximum) => new() { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum };
    private static JObject Weight() => new() { ["type"] = "number", ["enum"] = new JArray(0.5, 1.0) };
    private static JObject Enum(params string[] values) => Enum((IEnumerable<string>)values);
    private static JObject Enum(IEnumerable<string> values) => new() { ["type"] = "string", ["enum"] = new JArray(values) };
    private static JObject Ref(string name) => new() { ["$ref"] = $"#/$defs/{name}" };
    private static JObject Nullable(JObject value) => new() { ["anyOf"] = new JArray(value, new JObject { ["type"] = "null" }) };

    private static JObject Array(JObject items, int minimum, int maximum, bool unique = false) => new()
    {
        ["type"] = "array", ["minItems"] = minimum, ["maxItems"] = maximum,
        ["items"] = items, ["uniqueItems"] = unique
    };
}
