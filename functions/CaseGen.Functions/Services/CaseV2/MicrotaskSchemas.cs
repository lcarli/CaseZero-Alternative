using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CaseGen.Functions.Services.CaseV2;

public static class MicrotaskSchemas
{
    private static readonly string[] AssetTypes = ["photo", "pdf", "document", "digital"];
    private static readonly Dictionary<string, JObject> Definitions = BuildDefinitions();

    public static string Metadata { get; } = Schema(Ref("Metadata"));
    public static string PlotOutline { get; } = Schema(Object(
        ("metadata", Object(("title", Text(200)), ("description", Text(3000)),
            ("difficulty", Enum(GenerationRequestValidator.Ranks)), ("requiredRank", Enum(GenerationRequestValidator.Ranks)),
            ("category", Text(100)), ("briefing", Text(3000)), ("tags", Array(Text(100), 1, 8)))),
        ("clueOrderIds", Ids("clue", 1, 20)), ("decoyOrderIds", Ids("decoy", 0, 6))));
    public static string SuspectCard { get; } = Schema(Object(
        ("id", Id("suspect")), ("name", Text(200)), ("age", Integer(1, 110)),
        ("occupation", Text(300)), ("relationship", Text(500)), ("motive", NullableText(3000)),
        ("alibi", NullableText(3000)), ("alibiVerified", Boolean()), ("background", NullableText(5000)),
        ("photo", NullableText(1000)), ("relatedAssets", Ids("asset", 0, 20)), ("support", Ref("Support"))));
    public static string AssetPlan { get; } = Schema(Object(("assets", Array(Object(
        ("archetypeId", Text(100)), ("id", Id("asset")), ("type", Enum(AssetTypes)), ("title", Text(200)),
        ("role", Text(2000)), ("evidenceRole", Enum("primary", "corroborative", "contextual")),
        ("subjectSuspectId", Nullable(Id("suspect"))), ("imagePurpose", Nullable(Enum("scene", "suspect_portrait", "object", "surveillance"))),
        ("visibility", Enum("initial", "hidden")), ("layoutHint", Text(100)),
        ("supportsClueIds", Ids("clue", 0, 20)), ("forensicInputClueIds", Ids("clue", 0, 10)),
        ("introducesRedHerringSuspectIds", Ids("suspect", 0, 6)), ("resolvesRedHerringSuspectIds", Ids("suspect", 0, 6)),
        ("containedObjectIds", Array(ObjectId(), 1, 20, true))), 1, 40))));
    public static string AssetCard { get; } = Schema(Ref("Asset"));
    public static string Timeline { get; } = Schema(Object(
        ("timeline", Array(Object(("time", DateTimeText()), ("event", Text(3000)),
            ("source", Enum("investigation", "witness", "sensor", "forensic")), ("verified", Boolean()),
            ("importance", Enum("low", "medium", "high", "critical"))), 0, 30)),
        ("temporalEvents", Array(Object(("id", Id("tevt")), ("triggerAtMinutes", Integer(1, 600)),
            ("type", Enum("memo", "witness", "alert", "email")),
            ("payload", Object(("emailId", Nullable(Id("email"))), ("assetId", Nullable(Id("asset"))),
                ("message", NullableText(3000))))), 0, 10))));
    public static string ForensicsPlan { get; } = Schema(Object(
        ("analysisTypes", Array(Object(("type", AnalysisType()), ("durationMinutes", Integer(1, 1440)),
            ("availableFor", Array(Enum(AssetTypes), 1, 4, true))), 0, 10)),
        ("outcomes", Array(Object(("inputAssetId", Id("asset")), ("inputObjectId", ObjectId()),
            ("analysisType", AnalysisType()), ("findings", Boolean()), ("matchedSuspectId", Nullable(Id("suspect"))),
            ("role", Text(2000)), ("supportsClueIds", Ids("clue", 0, 20)),
            ("producedProperties", Array(Enum(System.Enum.GetNames<ForensicObservationProperty>()), 0, 12, true)),
            ("limitationKeys", Array(Text(200), 1, 12, true)), ("referenceSampleObjectId", Nullable(ObjectId())),
            ("chainOfCustodyObservationId", Nullable(Id("observation"))), ("resultLayoutId", Enum("ForensicReport"))), 0, 10))));
    public static string ForensicOutcome { get; } = Schema(Object(
        ("conclusionText", Text(10000)), ("support", Ref("Support")),
        ("resultAsset", Nullable(Ref("Asset"))), ("resultEmail", Nullable(Ref("Email")))));
    public static string BriefingEmail { get; } = Schema(Object(
        ("from", Text(300)), ("subject", Text(300)), ("body", Text(40000)), ("support", Ref("Support"))));
    public static string InitialEmails { get; } = Schema(Object(("emails", Array(Ref("Email"), 0, 3))));
    public static string Rules { get; } = Schema(Object(("rules", Array(Object(
        ("ruleId", Id("rule")), ("description", NullableText(2000)), ("trigger", Ref("Trigger")),
        ("actions", Array(Ref("Action"), 1, 6))), 0, 20))));
    public static string SolutionSkeleton { get; } = Schema(Object(
        ("requiredEvidenceIds", Ids("asset", 1, 20)), ("requiredAnalysisIds", Array(AnalysisId(), 0, 10, true)),
        ("questionTopics", Array(Object(("id", Id("q")), ("topic", Text(1000)), ("weight", Weight()),
            ("supportingEvidenceIds", Ids("asset", 1, 6))), 2, 4))));
    public static string Question { get; } = Schema(Object(
        ("id", Id("q")), ("prompt", Text(1000)), ("options", Array(Object(("id", Id("opt")), ("label", Text(2000))), 3, 4)),
        ("correctOptionId", Id("opt")), ("weight", Weight()), ("support", Ref("Support"))));
    public static string Explanation { get; } = Schema(Object(("explanation", Text(15000, 40)), ("support", Ref("Support"))));
    public static string Solver { get; } = Schema(Object(
        ("suspectId", Id("suspect")), ("evidenceIds", Ids("asset", 0, 20)), ("analysisIds", Array(AnalysisId(), 0, 10, true)),
        ("answers", Array(Object(("questionId", Id("q")), ("optionId", Id("opt"))), 0, 4)), ("rationale", NullableText(5000))));

    public static string CaseBible { get; } = Schema(Object(
        ("version", Enum("1.0")),
        ("world", Object(("language", Enum("en-US", "pt-BR", "es-ES", "fr-FR")),
            ("locationDisplayName", Text(300)), ("city", Text(200)), ("region", Text(200)), ("country", Text(200)),
            ("jurisdiction", Text(200)), ("timeZoneId", Text(100)), ("utcOffset", Text(20)),
            ("policeAgencyId", Id("organization")), ("investigatorPersonId", Id("person")))),
        ("institutions", Array(Object(("id", Id("organization")), ("name", Text(300)), ("kind", Text(100)),
            ("locationId", Nullable(Id("location"))), ("emailDomain", NullableText(200))), 1, 10)),
        ("locations", Array(Object(("id", Id("location")), ("name", Text(300)), ("kind", Text(100)),
            ("address", Object(("line1", Text(300)), ("line2", NullableText(300)), ("city", Text(200)),
                ("region", NullableText(200)), ("postalCode", NullableText(100)), ("country", Text(200)))),
            ("institutionId", Nullable(Id("organization")))), 2, 15)),
        ("travelRelationships", Array(Object(("id", Id("travel")), ("fromLocationId", Id("location")),
            ("toLocationId", Id("location")), ("mode", Text(100)), ("typicalMinutes", Integer(1, 1440)),
            ("minimumMinutes", Integer(1, 1440))), 1, 30)),
        ("people", Array(Object(("id", Id("person")), ("suspectId", Nullable(Id("suspect"))),
            ("name", Text(200)), ("age", Integer(1, 110)), ("roles", Array(Enum(System.Enum.GetNames<CaseBiblePersonRole>()), 1, 5, true)),
            ("occupation", Text(300)), ("publicRole", Text(500)), ("relationshipToVictim", Text(500)),
            ("background", Text(2000)), ("privateMotive", NullableText(2000)), ("statedAlibi", NullableText(2000)),
            ("alibiVerified", Boolean()), ("homeLocationId", Nullable(Id("location"))), ("phone", NullableText(100)),
            ("email", NullableText(300))), 4, 15)),
        ("relationships", Array(Object(("id", Id("relationship")), ("fromPersonId", Id("person")),
            ("toPersonId", Id("person")), ("type", Text(100)), ("description", Text(1000))), 1, 30)),
        ("employments", Array(Object(("id", Id("employment")), ("personId", Id("person")), ("institutionId", Id("organization")),
            ("locationId", Id("location")), ("jobTitle", Text(300)), ("startDate", NullableText(50)), ("endDate", NullableText(50))), 1, 20)),
        ("workSchedules", Array(Object(("id", Id("schedule")), ("personId", Id("person")), ("employmentId", Nullable(Id("employment"))),
            ("locationId", Id("location")), ("start", DateTimeText()), ("end", DateTimeText()), ("label", Text(500))), 1, 30)),
        ("devices", Array(Object(("id", Id("device")), ("type", Text(100)), ("manufacturer", NullableText(200)),
            ("model", NullableText(200)), ("identifier", Text(200)), ("ownerPersonId", Nullable(Id("person"))),
            ("assignedPersonId", Nullable(Id("person"))), ("usualLocationId", Nullable(Id("location"))),
            ("accountIds", Ids("account", 0, 20))), 0, 20)),
        ("accounts", Array(Object(("id", Id("account")), ("provider", Text(200)), ("handle", Text(200)), ("identifier", Text(200)),
            ("ownerPersonId", Nullable(Id("person"))), ("deviceIds", Ids("device", 0, 20))), 0, 20)),
        ("vehicles", Array(Object(("id", Id("vehicle")), ("ownerPersonId", Nullable(Id("person"))), ("plate", Text(100)),
            ("make", Text(100)), ("model", Text(100)), ("color", Text(100))), 0, 12)),
        ("incident", Object(("crimeType", Text(200)), ("culpritPersonId", Id("person")), ("victimPersonId", Id("person")),
            ("primaryLocationId", Id("location")), ("occurredAt", DateTimeText()), ("discoveredAt", DateTimeText()),
            ("reportedAt", DateTimeText()), ("openedAt", DateTimeText()), ("crimeMechanism", Text(3000)),
            ("culpritObjective", Text(2000)), ("concealment", Text(2000)), ("caseHook", Text(2000)), ("privateSummary", Text(3000)))),
        ("truthTimeline", Array(Object(("id", Id("event")), ("kind", Enum(System.Enum.GetNames<EventKind>())),
            ("time", DateTimeText()), ("endTime", Nullable(DateTimeText())), ("locationId", Id("location")),
            ("participantIds", Ids("person", 0, 15)), ("deviceIds", Ids("device", 0, 20)), ("accountIds", Ids("account", 0, 20)),
            ("vehicleIds", Ids("vehicle", 0, 12)), ("factIds", Ids("fact", 0, 40)), ("event", Text(3000)),
            ("publicDescription", NullableText(2000)), ("visibility", Visibility()), ("source", Enum("investigation", "witness", "sensor", "forensic")),
            ("verified", Boolean()), ("importance", Enum("low", "medium", "high", "critical")), ("causalRole", Text(2000))), 4, 20)),
        ("sources", Array(Object(("id", Id("source")), ("kind", Enum(System.Enum.GetNames<CaseBibleSourceKind>())),
            ("name", Text(300)), ("description", Text(2000)), ("custodianPersonId", Nullable(Id("person"))),
            ("custodianInstitutionId", Nullable(Id("organization"))), ("deviceId", Nullable(Id("device"))),
            ("accountId", Nullable(Id("account"))), ("authorPersonId", Nullable(Id("person"))), ("recordedAt", DateTimeText())), 4, 30)),
        ("facts", Array(Object(("id", Id("fact")), ("predicate", Text(200)), ("subjectId", EntityId()),
            ("objectId", Nullable(EntityId())), ("literalValue", NullableText(3000)),
            ("literalType", Nullable(Enum(System.Enum.GetNames<LiteralValueType>()))), ("literalUnit", NullableText(100)),
            ("eventId", Nullable(Id("event"))), ("visibility", Visibility()), ("truthStatus", Enum("Confirmed", "Disputed", "False")),
            ("intentionalConflictId", Nullable(Id("conflict")))), 6, 50)),
        ("observations", Array(Object(("id", Id("observation")), ("factId", Id("fact")), ("sourceId", Id("source")),
            ("statement", Text(3000)), ("observedValue", NullableText(3000)), ("observedAt", Nullable(DateTimeText())),
            ("fidelity", Enum(System.Enum.GetNames<ObservationFidelity>())), ("reliability", Enum(System.Enum.GetNames<ObservationReliability>())),
            ("visibility", Visibility()), ("intentionalConflictId", Nullable(Id("conflict"))), ("speakerPersonId", Nullable(Id("person")))), 6, 60)),
        ("difficultyIntent", Object(("difficulty", Enum(GenerationRequestValidator.Ranks)),
            ("clues", Array(Object(("id", Id("clue")), ("observationIds", Ids("observation", 1, 10)),
                ("supportsPersonId", Nullable(Id("person"))), ("inference", Text(3000)), ("strength", Enum("context", "supporting", "decisive")),
                ("sourceType", Enum("document", "digital", "photo", "witness", "forensic")),
                ("role", Enum("context", "identity", "action", "benefit", "alibi", "forensicAttribution"))), 4, 20)),
            ("proofPaths", Array(Object(("id", Id("proof")), ("supportsPersonId", Id("person")), ("clueIds", Ids("clue", 1, 20)),
                ("conclusionFactId", Id("fact")), ("independenceKey", Text(200)), ("description", Text(2000))), 1, 6)),
            ("decoyArcs", Array(Object(("id", Id("decoy")), ("suspectPersonId", Id("person")), ("suspicion", Text(2000)),
                ("suspicionObservationIds", Ids("observation", 1, 10)), ("verificationObservationIds", Ids("observation", 1, 10)),
                ("resolution", Text(2000))), 1, 6)),
            ("forensicOpportunities", Array(Object(("id", Id("forensic")), ("inputEntityId", EntityId()), ("inputSourceId", Id("source")),
                ("resultObservationId", Id("observation")), ("methodHint", AnalysisType()), ("purpose", Text(2000))), 0, 10)),
            ("intentionalConflicts", Array(Object(("id", Id("conflict")), ("factIds", Ids("fact", 0, 10)),
                ("observationIds", Ids("observation", 0, 10)), ("resolutionObservationIds", Ids("observation", 1, 10)),
                ("purpose", Text(2000))), 0, 10)), ("ambiguityNotes", Array(Text(2000), 0, 10)))),
        ("investigationConstraints", Object(("openingObservationIds", Ids("observation", 1, 20)),
            ("requiredProcedureNotes", Array(Text(2000), 0, 20)), ("prohibitedAssumptions", Array(Text(2000), 0, 20)),
            ("allowedForensicMethodIds", Array(AnalysisType(), 0, 10, true)), ("mustPreserveFactIds", Ids("fact", 0, 50))))));

    private static Dictionary<string, JObject> BuildDefinitions()
    {
        JObject Section(string kind, string field, JObject value) => Optional(Object(
            ("kind", Enum(kind)), ("heading", NullableText(300)), (field, value)),
            ("text", field == "text" ? null : Null()), ("table", field == "table" ? null : Null()),
            ("items", field == "items" ? null : Null()), ("transcript", field == "transcript" ? null : Null()),
            ("code", field == "code" ? null : Null()));
        return new()
        {
            ["Metadata"] = Object(("title", Text(200)), ("description", Text(3000)), ("location", Text(300)),
                ("incidentDate", DateTimeText()), ("openedAt", DateTimeText()), ("difficulty", Enum(GenerationRequestValidator.Ranks)),
                ("requiredRank", Enum(GenerationRequestValidator.Ranks)), ("category", Text(100)), ("briefing", Text(3000)),
                ("tags", Array(Text(100), 0, 10)), ("victim", Nullable(Object(("name", Text(200)), ("age", Integer(1, 110)), ("occupation", Text(300)))))),
            ["Support"] = Array(Object(("referenceId", Id("observation")), ("excerpt", Text(10000))), 0, 60),
            ["Asset"] = Object(("id", Id("asset")), ("type", Enum(AssetTypes)), ("title", Text(200)),
                ("description", NullableText(3000)), ("visibility", Enum("initial", "hidden")), ("category", NullableText(100)),
                ("evidenceRole", Nullable(Enum("primary", "corroborative", "contextual"))), ("subjectSuspectId", Nullable(Id("suspect"))),
                ("imagePurpose", Nullable(Enum("scene", "suspect_portrait", "object", "surveillance"))),
                ("body", NullableText(40000)), ("bodyDoc", Nullable(Ref("Document"))), ("support", Ref("Support"))),
            ["Email"] = Object(("id", Id("email")), ("from", Text(300)), ("subject", Text(300)), ("body", Text(40000)),
                ("sentAt", DateTimeText()), ("priority", Nullable(Enum("normal", "high", "urgent"))),
                ("attachments", Ids("asset", 0, 20)), ("visibility", Enum("initial", "hidden")), ("support", Ref("Support"))),
            ["Document"] = Object(("layout", Text(100)), ("language", Enum("en-US", "pt-BR", "es-ES", "fr-FR")),
                ("title", Text(200)), ("subtitle", NullableText(300)), ("classification", Text(200)),
                ("header", Array(Ref("KeyValue"), 0, 30)), ("sections", Array(Ref("Section"), 1, 40)),
                ("signature", Nullable(Ref("Signature")))),
            ["Section"] = new JObject { ["oneOf"] = new JArray(
                Section("narrative", "text", Text(40000)), Section("callout", "text", Text(40000)),
                Section("table", "table", Ref("Table")), Section("keyValue", "items", Array(Ref("KeyValue"), 1, 40)),
                Section("transcript", "transcript", Array(Ref("TranscriptEntry"), 1, 100)), Section("code", "code", Ref("Code"))) },
            ["KeyValue"] = Object(("label", Text(300)), ("value", Text(5000))),
            ["Table"] = Object(("columns", Array(Text(300), 1, 20)), ("rows", Array(Array(String(5000), 1, 20), 1, 100)),
                ("footnote", NullableText(3000))),
            ["TranscriptEntry"] = Object(("timestamp", NullableText(100)), ("speaker", NullableText(200)), ("text", Text(10000))),
            ["Code"] = Object(("language", NullableText(100)), ("content", Text(40000))),
            ["Signature"] = Object(("name", Text(200)), ("title", NullableText(200)), ("badge", NullableText(100)), ("signedAt", Nullable(DateTimeText()))),
            ["Trigger"] = Object(("type", Enum("forensics_complete", "attachment_download", "asset_viewed", "email_opened",
                "time_elapsed", "suspect_viewed", "multiple_conditions")), ("inputAssetId", Nullable(Id("asset"))),
                ("analysisType", Nullable(AnalysisType())), ("emailId", Nullable(Id("email"))), ("assetId", Nullable(Id("asset"))),
                ("suspectId", Nullable(Id("suspect"))), ("atMinutes", Nullable(Integer(0, 600))), ("operator", Nullable(Enum("AND", "OR"))),
                ("conditions", Nullable(Array(Ref("Trigger"), 2, 6)))),
            ["Action"] = Object(("type", Enum("reveal_email", "reveal_asset", "reveal_suspect", "add_email_attachment",
                "send_notification", "update_suspect_status", "mark_alibi_verified")), ("emailId", Nullable(Id("email"))),
                ("assetId", Nullable(Id("asset"))), ("suspectId", Nullable(Id("suspect"))), ("level", Nullable(Enum("info", "warn", "critical"))),
                ("message", NullableText(3000)), ("status", Nullable(Enum("suspect", "cleared", "confirmed_culprit"))), ("verified", Nullable(Boolean())))
        };
    }

    private static string Schema(JObject root)
    {
        var definitions = new JObject();
        void Include(JObject value)
        {
            foreach (var reference in value.DescendantsAndSelf().OfType<JProperty>().Where(p => p.Name == "$ref")
                         .Select(p => p.Value.Value<string>()!).ToArray())
            {
                var name = reference["#/$defs/".Length..];
                if (definitions.ContainsKey(name)) continue;
                var definition = (JObject)Definitions[name].DeepClone();
                definitions[name] = definition;
                Include(definition);
            }
        }
        Include(root);
        if (root["$ref"] is not null)
            root = (JObject)Definitions[root["$ref"]!.Value<string>()!["#/$defs/".Length..]].DeepClone();
        if (definitions.Count > 0) root["$defs"] = definitions;
        return root.ToString(Formatting.None);
    }

    private static JObject Object(params (string Name, JObject Schema)[] properties) => new()
    {
        ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JArray(properties.Select(p => p.Name)),
        ["properties"] = new JObject(properties.Select(p => new JProperty(p.Name, p.Schema)))
    };
    private static JObject Optional(JObject value, params (string Name, JObject? Schema)[] properties)
    {
        foreach (var property in properties.Where(p => p.Schema is not null))
            value["properties"]![property.Name] = property.Schema;
        return value;
    }
    private static JObject String(int maximum) => new() { ["type"] = "string", ["maxLength"] = maximum };
    private static JObject Text(int maximum, int minimum = 1) => new()
        { ["type"] = "string", ["minLength"] = minimum, ["maxLength"] = maximum, ["pattern"] = @"\S" };
    private static JObject NullableText(int maximum) => Nullable(String(maximum));
    private static JObject Id(string prefix) => new() { ["type"] = "string", ["pattern"] = $"^{prefix}\\.[a-z0-9_]+$", ["maxLength"] = 200 };
    private static JObject EntityId() => new() { ["type"] = "string", ["pattern"] = "^(person|organization|location|device|account|vehicle)\\.[a-z0-9_]+$" };
    private static JObject ObjectId() => new() { ["type"] = "string", ["pattern"] = "^(person|organization|location|vehicle|document|object|device|account|session|communication|transaction)\\.[a-z0-9_]+$" };
    private static JObject DateTimeText() => new() { ["type"] = "string", ["format"] = "date-time", ["pattern"] = @"(?:Z|[+-]\d{2}:\d{2})$", ["maxLength"] = 50 };
    private static JObject AnalysisType() => Enum(ForensicMethodCatalog.All.Select(m => m.Id));
    private static JObject AnalysisId() => new() { ["type"] = "string", ["pattern"] = "^asset\\.[a-z0-9_]+:[A-Za-z0-9_]+$", ["maxLength"] = 300 };
    private static JObject Ids(string prefix, int minimum, int maximum) => Array(Id(prefix), minimum, maximum, true);
    private static JObject Visibility() => Enum("Public", "Private");
    private static JObject Boolean() => new() { ["type"] = "boolean" };
    private static JObject Integer(int minimum, int maximum) => new() { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum };
    private static JObject Weight() => new() { ["type"] = "number", ["enum"] = new JArray(0.5, 1.0) };
    private static JObject Enum(params string[] values) => Enum((IEnumerable<string>)values);
    private static JObject Enum(IEnumerable<string> values) => new() { ["type"] = "string", ["enum"] = new JArray(values) };
    private static JObject Ref(string name) => new() { ["$ref"] = $"#/$defs/{name}" };
    private static JObject Null() => new() { ["type"] = "null" };
    private static JObject Nullable(JObject value) => new() { ["anyOf"] = new JArray(value, Null()) };
    private static JObject Array(JObject items, int minimum, int maximum, bool unique = false) => new()
        { ["type"] = "array", ["minItems"] = minimum, ["maxItems"] = maximum, ["items"] = items, ["uniqueItems"] = unique };
}
