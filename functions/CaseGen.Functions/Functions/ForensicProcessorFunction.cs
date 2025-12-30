using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Azure Function with Queue Trigger for processing forensic analysis requests.
/// Task 36: Queue Trigger to consume messages from "forensic-requests" queue.
/// Tasks 37-40: Processing logic (case.json load, rules engine, reveal_email, completion).
/// </summary>
public class ForensicProcessorFunction
{
    private readonly ILogger<ForensicProcessorFunction> _logger;

    public ForensicProcessorFunction(ILogger<ForensicProcessorFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ForensicProcessorFunction))]
    public async Task Run(
        [QueueTrigger("forensic-requests", Connection = "AzureWebJobsStorage")] string queueMessage)
    {
        _logger.LogInformation("Forensic processor triggered. Message: {Message}", queueMessage);

        try
        {
            // Parse queue message
            var request = JsonSerializer.Deserialize<ForensicRequestMessage>(queueMessage);
            if (request == null)
            {
                _logger.LogError("Failed to deserialize queue message: {Message}", queueMessage);
                return;
            }

            _logger.LogInformation(
                "Processing forensic request: RequestId={RequestId}, CaseId={CaseId}, AssetId={AssetId}, Type={AnalysisType}",
                request.RequestId, request.CaseId, request.InputAssetId, request.AnalysisType);

            // TODO (Task 37): Load case.json from blob storage
            // var caseJson = await _caseV1StorageService.GetCaseRawAsync(request.CaseId);

            // TODO (Task 37): Find matching rule in case.json
            // var rule = FindMatchingRule(caseJson, request.InputAssetId, request.AnalysisType);

            // TODO (Task 38): Execute reveal_email action
            // if (rule?.Action == "reveal_email") {
            //     await RevealEmailAsync(request.UserId, request.CaseId, rule.EmailId);
            // }

            // TODO (Task 39): Update ForensicRequest status to "completed"
            // await UpdateForensicRequestStatusAsync(request.RequestId, "completed", rule?.EmailId);

            // TODO (Task 40): Send SignalR notification to client
            // await _signalRHub.Clients.User(request.UserId).SendAsync("ForensicResultReady", request.RequestId);

            _logger.LogInformation(
                "Forensic request processing placeholder completed. RequestId={RequestId}",
                request.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forensic request from queue");
            throw; // Will trigger Azure Functions retry logic
        }
    }

    /// <summary>
    /// Message format matching the queue service in the API.
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
