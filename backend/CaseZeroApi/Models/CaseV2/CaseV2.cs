using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseZeroApi.Models.CaseV2;

public class CaseV2
{
    [JsonPropertyName("version")] public string Version { get; set; } = "2.0";
    [JsonPropertyName("caseId")] public string CaseId { get; set; } = string.Empty;
    [JsonPropertyName("metadata")] public CaseV2Metadata Metadata { get; set; } = new();
    [JsonPropertyName("assets")] public List<CaseV2Asset> Assets { get; set; } = new();
    [JsonPropertyName("emails")] public List<CaseV2Email> Emails { get; set; } = new();
    [JsonPropertyName("suspects")] public List<CaseV2Suspect> Suspects { get; set; } = new();
    [JsonPropertyName("timeline")] public List<CaseV2TimelineEvent>? Timeline { get; set; }
    [JsonPropertyName("temporalEvents")] public List<CaseV2TemporalEvent>? TemporalEvents { get; set; }
    [JsonPropertyName("rules")] public List<CaseV2Rule>? Rules { get; set; }
    [JsonPropertyName("forensicsDefaults")] public CaseV2ForensicsDefaults? ForensicsDefaults { get; set; }
    [JsonPropertyName("forensicOutcomes")] public List<CaseV2ForensicOutcome>? ForensicOutcomes { get; set; }
    [JsonPropertyName("solution")] public CaseV2Solution? Solution { get; set; }
    [JsonPropertyName("gameMetadata")] public CaseV2GameMetadata? GameMetadata { get; set; }
}

public class CaseV2Metadata
{
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("location")] public string Location { get; set; } = string.Empty;
    [JsonPropertyName("incidentDate")] public string IncidentDate { get; set; } = string.Empty;
    [JsonPropertyName("openedAt")] public string OpenedAt { get; set; } = string.Empty;
    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = string.Empty;
    [JsonPropertyName("requiredRank")] public string RequiredRank { get; set; } = string.Empty;
    [JsonPropertyName("unlockMode")] public string? UnlockMode { get; set; }
    [JsonPropertyName("estimatedDurationMinutes")] public int? EstimatedDurationMinutes { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("briefing")] public string? Briefing { get; set; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    [JsonPropertyName("victim")] public CaseV2Victim? Victim { get; set; }
}

public class CaseV2Victim
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("age")] public int? Age { get; set; }
    [JsonPropertyName("occupation")] public string? Occupation { get; set; }
    [JsonPropertyName("lastSeen")] public string? LastSeen { get; set; }
}

public class CaseV2Asset
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("uri")] public string Uri { get; set; } = string.Empty;
    [JsonPropertyName("checksum")] public string? Checksum { get; set; }
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "initial";
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    [JsonPropertyName("metadata")] public Dictionary<string, JsonElement>? Metadata { get; set; }
}

[JsonConverter(typeof(CaseV2EmailConverter))]
public class CaseV2Email
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("to")] public List<string> To { get; set; } = new();
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
    [JsonPropertyName("sentAt")] public string SentAt { get; set; } = string.Empty;
    [JsonPropertyName("priority")] public string? Priority { get; set; }
    [JsonPropertyName("attachments")] public List<string>? Attachments { get; set; }
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "initial";
    [JsonPropertyName("metadata")] public Dictionary<string, JsonElement>? Metadata { get; set; }
}

public class CaseV2EmailConverter : JsonConverter<CaseV2Email>
{
    public override CaseV2Email Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var email = new CaseV2Email();
        foreach (var prop in root.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "id": email.Id = prop.Value.GetString() ?? ""; break;
                case "from": email.From = prop.Value.GetString() ?? ""; break;
                case "to":
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                        email.To = prop.Value.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
                    else if (prop.Value.ValueKind == JsonValueKind.String)
                        email.To = new List<string> { prop.Value.GetString() ?? "" };
                    break;
                case "subject": email.Subject = prop.Value.GetString() ?? ""; break;
                case "body": email.Body = prop.Value.GetString() ?? ""; break;
                case "sentAt": email.SentAt = prop.Value.GetString() ?? ""; break;
                case "priority": email.Priority = prop.Value.GetString(); break;
                case "attachments":
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                        email.Attachments = prop.Value.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
                    break;
                case "visibility": email.Visibility = prop.Value.GetString() ?? "initial"; break;
                case "metadata":
                    email.Metadata = prop.Value.EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.Clone());
                    break;
            }
        }
        return email;
    }

    public override void Write(Utf8JsonWriter writer, CaseV2Email value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("from", value.From);
        writer.WritePropertyName("to");
        JsonSerializer.Serialize(writer, value.To, options);
        writer.WriteString("subject", value.Subject);
        writer.WriteString("body", value.Body);
        writer.WriteString("sentAt", value.SentAt);
        if (value.Priority is not null) writer.WriteString("priority", value.Priority);
        if (value.Attachments is not null)
        {
            writer.WritePropertyName("attachments");
            JsonSerializer.Serialize(writer, value.Attachments, options);
        }
        writer.WriteString("visibility", value.Visibility);
        if (value.Metadata is not null)
        {
            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            foreach (var kv in value.Metadata)
            {
                writer.WritePropertyName(kv.Key);
                kv.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}

public class CaseV2Suspect
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("alias")] public string? Alias { get; set; }
    [JsonPropertyName("age")] public int? Age { get; set; }
    [JsonPropertyName("occupation")] public string? Occupation { get; set; }
    [JsonPropertyName("relationship")] public string? Relationship { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("motive")] public string? Motive { get; set; }
    [JsonPropertyName("alibi")] public string? Alibi { get; set; }
    [JsonPropertyName("alibiVerified")] public bool AlibiVerified { get; set; } = false;
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("background")] public string? Background { get; set; }
    [JsonPropertyName("relatedAssets")] public List<string>? RelatedAssets { get; set; }
    [JsonPropertyName("photo")] public string? Photo { get; set; }
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "initial";
}

public class CaseV2TimelineEvent
{
    [JsonPropertyName("time")] public string Time { get; set; } = string.Empty;
    [JsonPropertyName("event")] public string Event { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("sourceAssetId")] public string? SourceAssetId { get; set; }
    [JsonPropertyName("verified")] public bool? Verified { get; set; }
    [JsonPropertyName("importance")] public string? Importance { get; set; }
}

public class CaseV2TemporalEvent
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("triggerAtMinutes")] public int TriggerAtMinutes { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("payload")] public CaseV2TemporalEventPayload? Payload { get; set; }
}

public class CaseV2TemporalEventPayload
{
    [JsonPropertyName("emailId")] public string? EmailId { get; set; }
    [JsonPropertyName("assetId")] public string? AssetId { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class CaseV2Rule
{
    [JsonPropertyName("ruleId")] public string RuleId { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("trigger")] public CaseV2Trigger Trigger { get; set; } = new();
    [JsonPropertyName("actions")] public List<CaseV2Action> Actions { get; set; } = new();
}

public class CaseV2Trigger
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("inputAssetId")] public string? InputAssetId { get; set; }
    [JsonPropertyName("analysisType")] public string? AnalysisType { get; set; }
    [JsonPropertyName("emailId")] public string? EmailId { get; set; }
    [JsonPropertyName("assetId")] public string? AssetId { get; set; }
    [JsonPropertyName("suspectId")] public string? SuspectId { get; set; }
    [JsonPropertyName("atMinutes")] public int? AtMinutes { get; set; }
    [JsonPropertyName("operator")] public string? Operator { get; set; }
    [JsonPropertyName("conditions")] public List<CaseV2Trigger>? Conditions { get; set; }
}

public class CaseV2Action
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("emailId")] public string? EmailId { get; set; }
    [JsonPropertyName("assetId")] public string? AssetId { get; set; }
    [JsonPropertyName("suspectId")] public string? SuspectId { get; set; }
    [JsonPropertyName("level")] public string? Level { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("verified")] public bool? Verified { get; set; }
}

public class CaseV2ForensicsDefaults
{
    [JsonPropertyName("analysisTypes")] public List<CaseV2AnalysisType> AnalysisTypes { get; set; } = new();
    [JsonPropertyName("noFindingsEmail")] public CaseV2NoFindingsEmailTemplate? NoFindingsEmail { get; set; }
}

public class CaseV2AnalysisType
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("durationMinutes")] public int DurationMinutes { get; set; }
    [JsonPropertyName("availableFor")] public List<string> AvailableFor { get; set; } = new();
}

public class CaseV2NoFindingsEmailTemplate
{
    [JsonPropertyName("template")] public string Template { get; set; } = string.Empty;
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
}

public class CaseV2ForensicOutcome
{
    [JsonPropertyName("inputAssetId")] public string InputAssetId { get; set; } = string.Empty;
    [JsonPropertyName("analysisType")] public string AnalysisType { get; set; } = string.Empty;
    [JsonPropertyName("findings")] public bool Findings { get; set; }
    [JsonPropertyName("matchedSuspectId")] public string? MatchedSuspectId { get; set; }
    [JsonPropertyName("matchedAssetId")] public string? MatchedAssetId { get; set; }
    [JsonPropertyName("conclusionText")] public string? ConclusionText { get; set; }
    [JsonPropertyName("resultAssetId")] public string? ResultAssetId { get; set; }
    [JsonPropertyName("resultEmailId")] public string? ResultEmailId { get; set; }
}

public class CaseV2Solution
{
    [JsonPropertyName("culpritId")] public string CulpritId { get; set; } = string.Empty;
    [JsonPropertyName("requiredEvidenceIds")] public List<string> RequiredEvidenceIds { get; set; } = new();
    [JsonPropertyName("requiredAnalysisIds")] public List<string> RequiredAnalysisIds { get; set; } = new();
    [JsonPropertyName("questions")] public List<CaseV2Question> Questions { get; set; } = new();
    [JsonPropertyName("explanation")] public string? Explanation { get; set; }
    [JsonPropertyName("minimumScore")] public double MinimumScore { get; set; } = 0.7;
    [JsonPropertyName("maxAttempts")] public int MaxAttempts { get; set; } = 3;
    [JsonPropertyName("partialCreditRules")] public CaseV2PartialCreditRules? PartialCreditRules { get; set; }
}

public class CaseV2Question
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
    [JsonPropertyName("options")] public List<CaseV2QuestionOption> Options { get; set; } = new();
    [JsonPropertyName("correctOptionId")] public string? CorrectOptionId { get; set; }
    [JsonPropertyName("weight")] public double? Weight { get; set; }
}

public class CaseV2QuestionOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
}

public class CaseV2PartialCreditRules
{
    [JsonPropertyName("culpritWeight")] public double CulpritWeight { get; set; } = 0.4;
    [JsonPropertyName("evidenceWeight")] public double EvidenceWeight { get; set; } = 0.2;
    [JsonPropertyName("analysisWeight")] public double AnalysisWeight { get; set; } = 0.2;
    [JsonPropertyName("questionsWeight")] public double QuestionsWeight { get; set; } = 0.2;
}

public class CaseV2GameMetadata
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = "2.0";
    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = string.Empty;
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    [JsonPropertyName("contentWarnings")] public List<string>? ContentWarnings { get; set; }
    [JsonPropertyName("localizations")] public List<string>? Localizations { get; set; }
    [JsonPropertyName("generation")] public CaseV2GenerationMetadata? Generation { get; set; }
}

public class CaseV2GenerationMetadata
{
    [JsonPropertyName("pipelineVersion")] public string? PipelineVersion { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("seed")] public long? Seed { get; set; }
    [JsonPropertyName("bundleChecksum")] public string? BundleChecksum { get; set; }
}
