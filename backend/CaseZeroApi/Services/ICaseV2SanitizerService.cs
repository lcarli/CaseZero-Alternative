using CaseZeroApi.Models.CaseV2;

namespace CaseZeroApi.Services;

public interface ICaseV2SanitizerService
{
    CaseV2Sanitized Sanitize(CaseV2 caseData);
    Task<CaseV2Sanitized> SanitizeForUserAsync(CaseV2 caseData, string userId, string caseId, CancellationToken ct = default);
}
