using System.Text.Json.Serialization;

namespace CaseZeroApi.Models
{
    /// <summary>
    /// Models for CaseGen's NormalizedCaseBundle format
    /// These represent the output format from CaseGen.Functions
    /// </summary>
    
    public class NormalizedCaseBundle
    {
        [JsonPropertyName("caseId")]
        public string CaseId { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("entities")]
        public CaseEntities? Entities { get; set; }

        [JsonPropertyName("documents")]
        public DocumentsCollection? Documents { get; set; }

        [JsonPropertyName("context")]
        public CaseContext? Context { get; set; }

        // Legacy properties for backward compatibility
        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; }

        [JsonPropertyName("media")]
        public List<NormalizedMedia>? Media { get; set; }

        [JsonPropertyName("emails")]
        public List<NormalizedEmail>? Emails { get; set; }

        [JsonPropertyName("gatingGraph")]
        public GatingGraph? GatingGraph { get; set; }

        [JsonPropertyName("metadata")]
        public NormalizedCaseMetadata? Metadata { get; set; }
    }

    public class CaseEntities
    {
        [JsonPropertyName("suspects")]
        public List<string> Suspects { get; set; } = new();

        [JsonPropertyName("evidence")]
        public List<string> Evidence { get; set; } = new();

        [JsonPropertyName("witnesses")]
        public List<string> Witnesses { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class DocumentsCollection
    {
        [JsonPropertyName("items")]
        public List<string> Items { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class CaseContext
    {
        [JsonPropertyName("plan")]
        public Dictionary<string, string> Plan { get; set; } = new();

        [JsonPropertyName("expand")]
        public Dictionary<string, string> Expand { get; set; } = new();
    }

    public class NormalizedDocument
    {
        [JsonPropertyName("docId")]
        public string DocId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("sections")]
        public List<string> Sections { get; set; } = new();

        [JsonPropertyName("lengthTarget")]
        public List<int> LengthTarget { get; set; } = new();

        [JsonPropertyName("gated")]
        public bool Gated { get; set; }

        [JsonPropertyName("gatingRule")]
        public GatingRule? GatingRule { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("modifiedAt")]
        public DateTime? ModifiedAt { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class NormalizedMedia
    {
        [JsonPropertyName("evidenceId")]
        public string EvidenceId { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("constraints")]
        public Dictionary<string, object>? Constraints { get; set; }

        [JsonPropertyName("deferred")]
        public bool Deferred { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class GatingRule
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("evidenceId")]
        public string? EvidenceId { get; set; }

        [JsonPropertyName("docId")]
        public string? DocId { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public class GatingGraph
    {
        [JsonPropertyName("nodes")]
        public List<GatingNode> Nodes { get; set; } = new();

        [JsonPropertyName("edges")]
        public List<GatingEdge> Edges { get; set; } = new();

        [JsonPropertyName("hasCycles")]
        public bool HasCycles { get; set; }

        [JsonPropertyName("cycleDescription")]
        public List<string> CycleDescription { get; set; } = new();
    }

    public class GatingNode
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("gated")]
        public bool Gated { get; set; }

        [JsonPropertyName("unlockAction")]
        public string? UnlockAction { get; set; }

        [JsonPropertyName("requiredIds")]
        public List<string> RequiredIds { get; set; } = new();
    }

    public class GatingEdge
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("relationship")]
        public string Relationship { get; set; } = string.Empty;
    }

    public class NormalizedCaseMetadata
    {
        [JsonPropertyName("generatedBy")]
        public string GeneratedBy { get; set; } = string.Empty;

        [JsonPropertyName("pipeline")]
        public string Pipeline { get; set; } = string.Empty;

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("validationResults")]
        public Dictionary<string, object> ValidationResults { get; set; } = new();

        [JsonPropertyName("appliedRules")]
        public List<string> AppliedRules { get; set; } = new();
    }

    // Email system models
    public class NormalizedEmail
    {
        [JsonPropertyName("emailId")]
        public string EmailId { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("sentAt")]
        public string SentAt { get; set; } = string.Empty; // ISO-8601 with timezone

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "normal"; // "normal", "high", "urgent"

        [JsonPropertyName("attachments")]
        public List<string> Attachments { get; set; } = new();

        [JsonPropertyName("gated")]
        public bool Gated { get; set; }

        [JsonPropertyName("gatingRule")]
        public EmailGatingRule? GatingRule { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class EmailGatingRule
    {
        [JsonPropertyName("requiredNodeIds")]
        public List<string> RequiredNodeIds { get; set; } = new();

        [JsonPropertyName("unlockCondition")]
        public string UnlockCondition { get; set; } = string.Empty; // "read_all", "unlock_evidence", etc.
    }
}