using System.Text.Json.Serialization;

namespace CaseZeroApi.Services
{
    #region Models
    public sealed record CaseSeed(
        string Title,
        string Location,
        DateTimeOffset IncidentDateTime,
        string Pitch,
        string Twist,
        string Difficulty = "Médio",
        int TargetDurationMinutes = 60,
        string? Constraints = null,
        string? Timezone = "America/Toronto"
    );

    public sealed class GenerationOptions
    {
        public bool GenerateImages { get; init; } = true;
        public static GenerationOptions Default => new();
    }

    public sealed class CaseContext
    {
        public string CaseId { get; }
        public string CaseJson { get; }

        public CaseContext(CaseSeed seed, string caseJson)
        {
            CaseJson = caseJson;
            CaseId = CaseUtils.ExtractCaseId(caseJson) ?? "CASE-UNDEFINED";
        }
    }

    public sealed class CasePackage
    {
        public string CaseId { get; set; } = string.Empty;
        public string CaseJson { get; set; } = string.Empty;
        public List<GeneratedDoc> Interrogatorios { get; set; } = new();
        public List<GeneratedDoc> Relatorios { get; set; } = new();
        public List<GeneratedDoc> Laudos { get; set; } = new();
        public GeneratedDoc? EvidenceManifest { get; set; }
        public List<ImagePrompt> ImagePrompts { get; set; } = new();
    }

    public enum DocumentKind { Interrogatorio, Relatorio, Laudo, Manifest }

    public sealed class GeneratedDoc
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DocumentKind Kind { get; set; }
    }

    public sealed class ImagePrompt
    {
        [JsonPropertyName("evidenceId")] public string? EvidenceId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("intendedUse")] public string IntendedUse { get; set; } = string.Empty;
        [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
        [JsonPropertyName("negativePrompt")] public string NegativePrompt { get; set; } = string.Empty;
        [JsonPropertyName("constraints")] public Dictionary<string, object> Constraints { get; set; } = new();
    }
    #endregion
}