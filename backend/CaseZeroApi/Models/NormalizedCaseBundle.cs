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

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = string.Empty;

        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; }

        [JsonPropertyName("documents")]
        public List<NormalizedDocument> Documents { get; set; } = new();

        [JsonPropertyName("media")]
        public List<NormalizedMedia> Media { get; set; } = new();

        [JsonPropertyName("gatingGraph")]
        public GatingGraph GatingGraph { get; set; } = new();

        [JsonPropertyName("metadata")]
        public NormalizedCaseMetadata Metadata { get; set; } = new();
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
}