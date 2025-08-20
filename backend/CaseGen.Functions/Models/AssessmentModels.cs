using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models;

/// <summary>
/// Represents an assessment run with proper null safety
/// </summary>
public record AssessmentRun
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = "";
    
    [JsonPropertyName("tenantId")]
    public string TenantId { get; init; } = "";
    
    [JsonPropertyName("specVersion")]
    public string SpecVersion { get; init; } = "";
    
    [JsonPropertyName("category")]
    public string Category { get; init; } = "";
    
    [JsonPropertyName("results")]
    public AssessmentResult[] Results { get; init; } = Array.Empty<AssessmentResult>();
    
    [JsonPropertyName("snapshot")]
    public DiscoverySnapshot? Snapshot { get; init; }
    
    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }
    
    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; init; }
    
    [JsonPropertyName("totalChecks")]
    public int TotalChecks { get; init; }
    
    [JsonPropertyName("compliantChecks")]
    public int CompliantChecks { get; init; }
    
    [JsonPropertyName("nonCompliantChecks")]
    public int NonCompliantChecks { get; init; }
    
    [JsonPropertyName("manualChecks")]
    public int ManualChecks { get; init; }
}

/// <summary>
/// Represents an assessment result
/// </summary>
public record AssessmentResult
{
    [JsonPropertyName("checkId")]
    public string CheckId { get; init; } = "";
    
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "";
    
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Represents a discovery snapshot
/// </summary>
public record DiscoverySnapshot
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
    
    [JsonPropertyName("resourceCount")]
    public int ResourceCount { get; init; }
    
    [JsonPropertyName("services")]
    public string[] Services { get; init; } = Array.Empty<string>();
}