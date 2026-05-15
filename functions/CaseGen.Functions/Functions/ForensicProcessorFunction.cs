using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using CaseGen.Functions.Services;
using CaseGen.Functions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Azure.Functions.Worker.Http;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Azure Function with Queue Trigger for processing forensic analysis requests.
/// Task 36: Queue Trigger to consume messages from "forensic-requests" queue.
/// Tasks 37-40: Processing logic (case.json load, rules engine, reveal_email, completion).
/// </summary>
public class ForensicProcessorFunction
{
    private readonly ILogger<ForensicProcessorFunction> _logger;
    private readonly IStorageService _storageService;
    private readonly ApplicationDbContext _dbContext;

    public ForensicProcessorFunction(
        ILogger<ForensicProcessorFunction> logger,
        IStorageService storageService,
        ApplicationDbContext dbContext)
    {
        _logger = logger;
        _storageService = storageService;
        _dbContext = dbContext;
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

            // Task 37: Load case.json from blob storage
            var caseJson = await LoadCaseJsonAsync(request.CaseId);
            if (caseJson == null)
            {
                _logger.LogError("Failed to load case.json for case {CaseId}", request.CaseId);
                return;
            }

            // Task 37: Find matching rule in case.json
            var matchingRule = FindMatchingRule(caseJson, request.InputAssetId, request.AnalysisType);
            
            if (matchingRule == null)
            {
                _logger.LogWarning(
                    "No matching rule found for RequestId={RequestId}, AssetId={AssetId}, AnalysisType={AnalysisType}",
                    request.RequestId, request.InputAssetId, request.AnalysisType);
                // Generate "no findings" email (future enhancement)
                return;
            }

            _logger.LogInformation(
                "Found matching rule: Action={Action}, EmailId={EmailId}",
                matchingRule.Action, matchingRule.EmailId);

            // Task 38: Execute reveal_email action
            if (matchingRule.Action == "reveal_email" && !string.IsNullOrEmpty(matchingRule.EmailId))
            {
                await RevealEmailAsync(request.UserId, request.CaseId, matchingRule.EmailId);
            }

            // Task 39: Update ForensicRequest status to "completed"
            await UpdateForensicRequestStatusAsync(request.RequestId, "completed", matchingRule.EmailId);

            // Task 40: Send SignalR notification to client
            await SendSignalRNotificationAsync(request.UserId, request.RequestId, request.CaseId);

            _logger.LogInformation(
                "Forensic request processing completed for RequestId={RequestId}",
                request.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forensic request from queue");
            throw; // Will trigger Azure Functions retry logic
        }
    }

    /// <summary>
    /// Task 37: Load case.json from blob storage.
    /// </summary>
    private async Task<CaseJsonV1?> LoadCaseJsonAsync(string caseId)
    {
        try
        {
            var containerName = "cases"; // Same as CaseV1StorageService
            var blobPath = $"{caseId}/case.json";
            
            var jsonContent = await _storageService.GetFileAsync(containerName, blobPath);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            return JsonSerializer.Deserialize<CaseJsonV1>(jsonContent, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load case.json for case {CaseId}", caseId);
            return null;
        }
    }

    /// <summary>
    /// Task 37: Find matching rule based on inputAssetId and analysisType.
    /// Rules structure: rules.forensics[].inputAssetId + analysisType -> action + emailId
    /// </summary>
    private ForensicRule? FindMatchingRule(CaseJsonV1 caseJson, string inputAssetId, string analysisType)
    {
        try
        {
            var forensicRules = caseJson.Rules?.Forensics;
            if (forensicRules == null || forensicRules.Count == 0)
            {
                _logger.LogWarning("No forensic rules defined in case.json");
                return null;
            }

            var rule = forensicRules.FirstOrDefault(r =>
                r.InputAssetId == inputAssetId &&
                string.Equals(r.AnalysisType, analysisType, StringComparison.OrdinalIgnoreCase));

            if (rule != null)
            {
                _logger.LogInformation(
                    "Matched rule: InputAssetId={InputAssetId}, AnalysisType={AnalysisType}, Action={Action}, EmailId={EmailId}",
                    rule.InputAssetId, rule.AnalysisType, rule.Action, rule.EmailId);
            }

            return rule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matching rule");
            return null;
        }
    }
/// <summary>
    /// Task 38: Execute reveal_email action by inserting emailId into CaseSessionVisibleEmails.
    /// </summary>
    private async Task RevealEmailAsync(string userId, string caseId, string emailId)
    {
        try
        {
            // Check if already visible
            var existingRecord = await _dbContext.CaseSessionVisibleEmails
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CaseId == caseId && e.EmailId == emailId);

            if (existingRecord != null)
            {
                _logger.LogInformation(
                    "Email already visible: UserId={UserId}, CaseId={CaseId}, EmailId={EmailId}",
                    userId, caseId, emailId);
                return;
            }

            // Insert new visible email
            var visibleEmail = new Models.CaseSessionVisibleEmails
            {
                UserId = userId,
                CaseId = caseId,
                EmailId = emailId,
                RevealedAt = DateTime.UtcNow
            };

            _dbContext.CaseSessionVisibleEmails.Add(visibleEmail);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Email revealed successfully: UserId={UserId}, CaseId={CaseId}, EmailId={EmailId}",
                userId, caseId, emailId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to reveal email: UserId={UserId}, CaseId={CaseId}, EmailId={EmailId}",
                userId, caseId, emailId);
            throw;
        }
    }

    /// <summary>
    /// Task 39: Update ForensicRequest status to completed and set ResultEmailId.
    /// </summary>
    private async Task UpdateForensicRequestStatusAsync(int requestId, string status, string? resultEmailId)
    {
        try
        {
            var forensicRequest = await _dbContext.ForensicRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (forensicRequest == null)
            {
                _logger.LogWarning("ForensicRequest not found: RequestId={RequestId}", requestId);
                return;
            }

            forensicRequest.Status = status;
            forensicRequest.CompletedAt = DateTime.UtcNow;
            forensicRequest.ResultEmailId = resultEmailId;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "ForensicRequest updated: RequestId={RequestId}, Status={Status}, ResultEmailId={ResultEmailId}",
                requestId, status, resultEmailId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to update ForensicRequest: RequestId={RequestId}",
                requestId);
            throw;
        }
    }

    /// <summary>
    /// Task 40: Send SignalR notification to client using Azure SignalR Service.
    /// Note: This is a placeholder - actual implementation depends on Azure SignalR Service configuration.
    /// For now, we log the notification event.
    /// </summary>
    private async Task SendSignalRNotificationAsync(string userId, int requestId, string caseId)
    {
        try
        {
            // TODO: Implement SignalR notification using Azure SignalR Service output binding
            // This requires:
            // 1. Azure SignalR Service connection string in local.settings.json / App Settings
            // 2. SignalR output binding configuration
            // 3. Frontend SignalR client connection
            
            // For now, log the notification
            _logger.LogInformation(
                "SignalR notification: UserId={UserId}, RequestId={RequestId}, CaseId={CaseId}, Event=ForensicResultReady",
                userId, requestId, caseId);
            
            // Placeholder for actual SignalR message:
            // var message = new SignalRMessage
            // {
            //     UserId = userId,
            //     Target = "ForensicResultReady",
            //     Arguments = new object[] { new { RequestId = requestId, CaseId = caseId } }
            // };
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to send SignalR notification: UserId={UserId}, RequestId={RequestId}",
                userId, requestId);
            // Don't throw - notification failure shouldn't fail the entire process
        }
    }

    #region DTOs

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

    /// <summary>
    /// Simplified case.json v1.0 structure for forensic processing.
    /// </summary>
    private class CaseJsonV1
    {
        public RulesSection? Rules { get; set; }
    }

    private class RulesSection
    {
        public List<ForensicRule>? Forensics { get; set; }
    }

    private class ForensicRule
    {
        public string InputAssetId { get; set; } = string.Empty;
        public string AnalysisType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // "reveal_email"
        public string? EmailId { get; set; }
    }

    #endregion
}
