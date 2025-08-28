using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models;

public record PlanActivityModel
{
    public CaseGenerationRequest Request { get; init; }
    public string CaseId { get; init; }
}

public record NormalizeActivityModel
{
    public string[] Documents { get; init; }
    public string[] Media { get; init; }
}

public record PackageActivityModel
{
    public string FinalJson { get; init; }
    public string CaseId { get; init; }
}

public record CaseGenerationRequest
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("location")]
    public string Location { get; init; } = "";

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; init; } = "Iniciante";

    [JsonPropertyName("targetDurationMinutes")]
    public int TargetDurationMinutes { get; init; } = 60;

    [JsonPropertyName("generateImages")]
    public bool GenerateImages { get; init; } = true;

    [JsonPropertyName("constraints")]
    public string[] Constraints { get; init; } = Array.Empty<string>();

    [JsonPropertyName("timezone")]
    public string Timezone { get; init; } = "America/Sao_Paulo";
}

public record CaseGenerationStatus
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = "";
    
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    
    [JsonPropertyName("currentStep")]
    public string CurrentStep { get; init; } = "";
    
    [JsonPropertyName("completedSteps")]
    public string[] CompletedSteps { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; init; }
    
    [JsonPropertyName("progress")]
    public double Progress { get; init; }
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; init; }
    
    [JsonPropertyName("estimatedCompletion")]
    public DateTime? EstimatedCompletion { get; init; }
    
    [JsonPropertyName("error")]
    public string? Error { get; init; }
    
    [JsonPropertyName("output")]
    public CaseGenerationOutput? Output { get; init; }
}

public record CaseGenerationOutput
{
    [JsonPropertyName("bundlePath")]
    public string BundlePath { get; init; } = "";
    
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = "";
    
    [JsonPropertyName("files")]
    public GeneratedFile[] Files { get; init; } = Array.Empty<GeneratedFile>();
    
    [JsonPropertyName("metadata")]
    public CaseMetadata? Metadata { get; init; }
}

public record GeneratedFile
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";
    
    [JsonPropertyName("size")]
    public long Size { get; init; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}

public record CaseMetadata
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";
    
    [JsonPropertyName("difficulty")]
    public string Difficulty { get; init; } = "";
    
    [JsonPropertyName("estimatedDuration")]
    public int EstimatedDuration { get; init; }
    
    [JsonPropertyName("categories")]
    public string[] Categories { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; init; }
}

// Pipeline step definitions
public static class CaseGenerationSteps
{
    public const string Plan = "Plan";
    public const string Expand = "Expand";
    public const string Design = "Design";
    public const string GenDocs = "GenDocs";
    public const string GenMedia = "GenMedia";
    public const string Normalize = "Normalize";
    public const string Index = "Index";
    public const string RuleValidate = "RuleValidate";
    public const string RedTeam = "RedTeam";
    public const string Package = "Package";
    
    public static readonly string[] AllSteps = {
        Plan, Expand, Design, GenDocs, GenMedia, 
        Normalize, Index, RuleValidate, RedTeam, Package
    };
}