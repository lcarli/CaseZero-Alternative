using CaseZeroApi.Models.CaseV1;

namespace CaseZeroApi.Services;

/// <summary>
/// Service for accessing case.json v1.0 from Azure Blob Storage
/// </summary>
public interface ICaseV1StorageService
{
    /// <summary>
    /// List all available cases in the 'cases' container
    /// </summary>
    Task<List<CaseV1Metadata>> ListCasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific case.json v1.0 (sanitized - no rules, only initial visibility)
    /// </summary>
    Task<CaseV1?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get raw case.json v1.0 (unsanitized - includes all data, for admin/debug only)
    /// </summary>
    Task<CaseV1?> GetCaseRawAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get asset file as stream
    /// </summary>
    Task<Stream?> GetAssetAsync(string caseId, string assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get asset file content type
    /// </summary>
    Task<string?> GetAssetContentTypeAsync(string caseId, string assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a case exists
    /// </summary>
    Task<bool> CaseExistsAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get raw case.json content as string (for rules engine)
    /// </summary>
    Task<string> GetCaseJsonAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get email JSON content as string
    /// </summary>
    Task<string> GetEmailAsync(string caseId, string emailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save email JSON content (for rules engine - add_email_attachment action)
    /// </summary>
    Task SaveEmailAsync(string caseId, string emailId, string content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal metadata for case listing
/// </summary>
public class CaseV1Metadata
{
    public string CaseId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Difficulty { get; set; }
    public string Category { get; set; } = string.Empty;
    public int EstimatedTimeMinutes { get; set; }
}
