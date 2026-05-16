using Azure.Core;
using Azure.Storage.Queues;
using System.Text.Json;

namespace CaseZeroApi.Services;

/// <summary>
/// Service for enqueuing forensic analysis requests to Azure Storage Queue.
/// Uses the same storage account as the case bundles (managed identity in Azure,
/// connection string / Azurite locally).
/// </summary>
public class ForensicQueueService : IForensicQueueService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<ForensicQueueService> _logger;
    private const string QUEUE_NAME = "forensic-requests";

    public ForensicQueueService(
        IConfiguration configuration,
        ILogger<ForensicQueueService> logger,
        TokenCredential credential)
    {
        _logger = logger;

        _queueClient = CaseGeneratorStorageFactory.CreateQueueClient(configuration, credential, QUEUE_NAME);

        // Best-effort queue creation. With least-privilege MI (Storage Queue Data
        // Message Sender) the create call returns 403 — that's fine as long as the
        // queue has been pre-provisioned (Bicep creates it). Swallowing the
        // exception keeps app startup resilient regardless of the auth model.
        try
        {
            _queueClient.CreateIfNotExists();
            _logger.LogInformation("Forensic queue service initialized with queue: {QueueName}", QUEUE_NAME);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CreateIfNotExists failed for queue {QueueName}. This is expected when running with " +
                "least-privilege managed identity (queue must be pre-provisioned). Send will fail if " +
                "the queue does not exist.",
                QUEUE_NAME);
        }
    }

    public async Task EnqueueForensicRequestAsync(
        int requestId,
        string caseId,
        string userId,
        string inputAssetId,
        string analysisType)
    {
        var message = new ForensicRequestMessage
        {
            RequestId = requestId,
            CaseId = caseId,
            UserId = userId,
            InputAssetId = inputAssetId,
            AnalysisType = analysisType,
            EnqueuedAt = DateTime.UtcNow
        };

        var messageJson = JsonSerializer.Serialize(message);

        try
        {
            await _queueClient.SendMessageAsync(messageJson);
            _logger.LogInformation(
                "Enqueued forensic request to queue. RequestId={RequestId}, CaseId={CaseId}, AssetId={AssetId}",
                requestId, caseId, inputAssetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to enqueue forensic request. RequestId={RequestId}, CaseId={CaseId}",
                requestId, caseId);
            throw;
        }
    }

    /// <summary>
    /// Message format for forensic request queue.
    /// This will be consumed by an Azure Function with Queue Trigger (Task 36).
    /// </summary>
    private class ForensicRequestMessage
    {
        public int RequestId { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string InputAssetId { get; set; } = string.Empty;
        public string AnalysisType { get; set; } = string.Empty;
        public DateTime EnqueuedAt { get; set; }
    }
}
