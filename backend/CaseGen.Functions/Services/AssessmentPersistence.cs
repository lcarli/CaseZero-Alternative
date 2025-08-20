using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

/// <summary>
/// Interface for assessment persistence operations with proper null safety
/// </summary>
public interface IAssessmentPersistence
{
    /// <summary>
    /// Creates an assessment run from JSON data with proper null checking
    /// </summary>
    /// <param name="runId">The run ID (required)</param>
    /// <param name="tenantId">The tenant ID (required)</param>
    /// <param name="jsonData">JSON data containing assessment information</param>
    /// <returns>Assessment run object</returns>
    Task<AssessmentRun> CreateAssessmentRunAsync(string runId, string tenantId, string jsonData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Parses a date time string safely
    /// </summary>
    /// <param name="dateTimeString">Date time string to parse</param>
    /// <returns>Parsed DateTimeOffset or default value if null/invalid</returns>
    DateTimeOffset ParseDateTimeSafely(string? dateTimeString);
    
    /// <summary>
    /// Validates assessment run data
    /// </summary>
    /// <param name="assessmentRun">Assessment run to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateAssessmentRun(AssessmentRun? assessmentRun);
}

/// <summary>
/// Implementation of assessment persistence with proper null safety
/// </summary>
public class AssessmentPersistence : IAssessmentPersistence
{
    /// <inheritdoc />
    public async Task<AssessmentRun> CreateAssessmentRunAsync(string runId, string tenantId, string jsonData, CancellationToken cancellationToken = default)
    {
        // Validate required parameters
        ArgumentException.ThrowIfNullOrEmpty(runId, nameof(runId));
        ArgumentException.ThrowIfNullOrEmpty(tenantId, nameof(tenantId));
        ArgumentException.ThrowIfNullOrEmpty(jsonData, nameof(jsonData));
        
        // Parse JSON data safely
        var assessmentData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
        if (assessmentData == null)
        {
            throw new ArgumentException("Invalid JSON data", nameof(jsonData));
        }
        
        // Extract date time strings safely
        var startedAtString = assessmentData.GetValueOrDefault("startedAt")?.ToString();
        var completedAtString = assessmentData.GetValueOrDefault("completedAt")?.ToString();
        
        // Parse dates safely using our helper method
        var startedAt = ParseDateTimeSafely(startedAtString);
        var completedAt = ParseDateTimeSafely(completedAtString);
        
        // Create assessment run with proper null safety
        var assessmentRun = new AssessmentRun
        {
            RunId = runId, // Non-null, validated above
            TenantId = tenantId, // Non-null, validated above
            SpecVersion = assessmentData.GetValueOrDefault("specVersion")?.ToString() ?? "1.0",
            Category = assessmentData.GetValueOrDefault("category")?.ToString() ?? "General",
            Results = Array.Empty<AssessmentResult>(), // Safe default
            Snapshot = null, // Explicitly nullable
            StartedAt = startedAt,
            CompletedAt = completedAt,
            TotalChecks = Convert.ToInt32(assessmentData.GetValueOrDefault("totalChecks") ?? 0),
            CompliantChecks = Convert.ToInt32(assessmentData.GetValueOrDefault("compliantChecks") ?? 0),
            NonCompliantChecks = Convert.ToInt32(assessmentData.GetValueOrDefault("nonCompliantChecks") ?? 0),
            ManualChecks = Convert.ToInt32(assessmentData.GetValueOrDefault("manualChecks") ?? 0)
        };
        
        return await Task.FromResult(assessmentRun);
    }
    
    /// <inheritdoc />
    public DateTimeOffset ParseDateTimeSafely(string? dateTimeString)
    {
        // Fix for CS8604: Existence possible d'un argument de référence null pour le paramètre 'input'
        if (string.IsNullOrWhiteSpace(dateTimeString))
        {
            return DateTimeOffset.UtcNow; // Safe default
        }
        
        // Use TryParse to avoid exceptions
        if (DateTimeOffset.TryParse(dateTimeString, out var result))
        {
            return result;
        }
        
        return DateTimeOffset.UtcNow; // Safe fallback
    }
    
    /// <inheritdoc />
    public bool ValidateAssessmentRun(AssessmentRun? assessmentRun)
    {
        if (assessmentRun == null)
        {
            return false;
        }
        
        // Validate required fields are not null or empty
        return !string.IsNullOrWhiteSpace(assessmentRun.RunId) &&
               !string.IsNullOrWhiteSpace(assessmentRun.TenantId) &&
               !string.IsNullOrWhiteSpace(assessmentRun.SpecVersion) &&
               assessmentRun.Results != null;
    }
}