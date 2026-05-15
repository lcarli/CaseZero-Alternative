namespace CaseGen.Functions.Services;

/// <summary>
/// Service for converting v2-hierarchical format to case.json v1.0 format
/// </summary>
public interface ICaseFormatConverterService
{
    /// <summary>
    /// Converts v2-hierarchical case to case.json v1.0 format by loading individual document/media files
    /// </summary>
    /// <param name="caseId">The case ID (e.g., CASE-20260102-e6dcc658)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>case.json v1.0 as JSON string</returns>
    Task<string> ConvertCaseToV1Async(string caseId, CancellationToken cancellationToken = default);
}
