using Azure.Storage.Queues;
using System.Text.Json;

namespace CaseZeroApi.Services;

/// <summary>
/// Service for enqueuing forensic analysis requests to Azure Storage Queue.
/// Uses the same storage account as BlobStorageService (Azurite for local dev).
/// </summary>
public class ForensicQueueService : IForensicQueueService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<ForensicQueueService> _logger;
    private const string QUEUE_NAME = "forensic-requests";

    public ForensicQueueService(IConfiguration configuration, ILogger<ForensicQueueService> logger)
    {
        _logger = logger;

        // Same connection string logic as BlobStorageService
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? "UseDevelopmentStorage=true"; // Azurite default

        _queueClient = new QueueClient(connectionString, QUEUE_NAME);

        // Create queue if it doesn't exist (safe for Azurite and Azure)
        try
        {
            _queueClient.CreateIfNotExists();
            _logger.LogInformation("Forensic queue service initialized with queue: {QueueName}", QUEUE_NAME);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize forensic queue: {QueueName}", QUEUE_NAME);
            throw;
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
