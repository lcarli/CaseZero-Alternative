namespace CaseZeroApi.Services;

public interface IBlobStorageService
{
    /// <summary>
    /// Lists all generated cases from blob storage
    /// </summary>
    Task<List<CaseManifest>> ListCasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full normalized case bundle for a specific case ID
    /// </summary>
    Task<Models.NormalizedCaseBundle?> GetCaseBundleAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific document from a case
    /// </summary>
    Task<string?> GetCaseDocumentAsync(string caseId, string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the URL for a media file
    /// </summary>
    string GetMediaUrl(string caseId, string fileName);
}

/// <summary>
/// Represents the manifest file for a generated case
/// </summary>
public class CaseManifest
{
    public string CaseId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public CaseCounts? Counts { get; set; }
    public Dictionary<string, string>? ValidationResults { get; set; }
    public List<ManifestFile>? Manifest { get; set; }
    public string? RedTeamAnalysis { get; set; }
}

public class CaseCounts
{
    public int Documents { get; set; }
    public int Media { get; set; }
    public int Suspects { get; set; }
}

public class ManifestFile
{
    public string Filename { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? Sha256 { get; set; }
    public string? MimeType { get; set; }
}
