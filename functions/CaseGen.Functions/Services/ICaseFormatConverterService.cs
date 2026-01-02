namespace CaseGen.Functions.Services;

/// <summary>
/// Interface for converting NormalizedCaseBundle to case.json v1.0 format
/// </summary>
public interface ICaseFormatConverterService
{
    /// <summary>
    /// Converts NormalizedCaseBundle to case.json v1.0 format
    /// </summary>
    Task<string> ConvertToV1Async(Models.NormalizedCaseBundle bundle, CancellationToken cancellationToken = default);
}
