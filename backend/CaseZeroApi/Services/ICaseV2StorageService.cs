using CaseZeroApi.Models.CaseV2;

namespace CaseZeroApi.Services;

public interface ICaseV2StorageService
{
    Task<List<CaseV2Metadata2>> ListCasesAsync(CancellationToken ct = default);
    Task<CaseV2?> GetRawAsync(string caseId, CancellationToken ct = default);
    Task<CaseV2Sanitized?> GetForUserAsync(string caseId, string userId, CancellationToken ct = default);
    Task<Stream?> GetAssetStreamAsync(string caseId, string assetId, CancellationToken ct = default);
    Task<string?> GetAssetContentTypeAsync(string caseId, string assetId, CancellationToken ct = default);
    Task<bool> CaseExistsAsync(string caseId, CancellationToken ct = default);
}

public class CaseV2Metadata2
{
    public string CaseId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public string? Briefing { get; set; }
    public List<string>? Tags { get; set; }
    public string? RequiredRank { get; set; }
}
