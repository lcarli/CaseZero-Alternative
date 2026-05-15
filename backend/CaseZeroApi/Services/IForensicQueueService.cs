namespace CaseZeroApi.Services;

/// <summary>
/// Service interface for enqueuing forensic analysis requests to Azure Storage Queue.
/// </summary>
public interface IForensicQueueService
{
    /// <summary>
    /// Enqueues a forensic analysis request for asynchronous processing.
    /// </summary>
    /// <param name="requestId">The ForensicRequest ID</param>
    /// <param name="caseId">The case ID</param>
    /// <param name="userId">The user ID who made the request</param>
    /// <param name="inputAssetId">The asset ID to analyze</param>
    /// <param name="analysisType">The type of analysis to perform</param>
    /// <returns>Task representing the async operation</returns>
    Task EnqueueForensicRequestAsync(
        int requestId,
        string caseId,
        string userId,
        string inputAssetId,
        string analysisType);
}
