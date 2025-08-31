using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class CaseLoggingService : ICaseLoggingService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<CaseLoggingService> _logger;
    private readonly string _logsContainer;

    public CaseLoggingService(IConfiguration configuration, ILogger<CaseLoggingService> logger)
    {
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("Storage connection string not configured");

        _blobServiceClient = new BlobServiceClient(connectionString);
        _logger = logger;
        _logsContainer = "logs";
    }

    public void LogOrchestratorStep(string caseId, string step, string details = "")
    {
        var message = details.Length > 0 
            ? $"🔄 [{caseId}] {step}: {details}"
            : $"🔄 [{caseId}] {step}";
            
        _logger.LogInformation(message);
    }

    public void LogOrchestratorProgress(string caseId, int currentStep, int totalSteps, string stepName)
    {
        var progressBar = GenerateProgressBar(currentStep, totalSteps);
        _logger.LogInformation("📊 [{CaseId}] Progress {CurrentStep}/{TotalSteps} {ProgressBar} - {StepName}", 
            caseId, currentStep, totalSteps, progressBar, stepName);
    }

    public async Task LogDetailedAsync(string caseId, string source, string level, string message, object? data = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow,
                CaseId = caseId,
                Source = source,
                Level = level,
                Message = message,
                Data = data
            };

            var logText = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }) + Environment.NewLine;

            await AppendToLogBlobAsync(caseId, logText, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log detailed entry for case {CaseId}", caseId);
        }
    }

    public async Task LogLLMInteractionAsync(string caseId, string provider, string promptType, string prompt, string response, int? tokenCount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract only the properties content for cleaner logs
            var cleanedResponse = ExtractPropertiesFromStructuredResponse(response, promptType);
            
            var interaction = new
            {
                Timestamp = DateTime.UtcNow,
                CaseId = caseId,
                Source = "LLM",
                Level = "INFO",
                Provider = provider,
                PromptType = promptType,
                PromptPreview = prompt.Length > 200 ? prompt.Substring(0, 200) + "..." : prompt,
                ResponsePreview = cleanedResponse.Length > 500 ? cleanedResponse.Substring(0, 500) + "..." : cleanedResponse,
                PromptLength = prompt.Length,
                ResponseLength = response.Length,
                TokenCount = tokenCount,
                FullPrompt = prompt,
                FullResponse = cleanedResponse // Use cleaned response instead of raw response
            };

            var logText = JsonSerializer.Serialize(interaction, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }) + Environment.NewLine;

            await AppendToLogBlobAsync(caseId, logText, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log LLM interaction for case {CaseId}", caseId);
        }
    }

    public async Task<string> GetDetailedLogAsync(string caseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetLogsContainerAsync(cancellationToken);
            var blobName = GetLogBlobName(caseId);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return "No detailed logs found for this case.";
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detailed log for case {CaseId}", caseId);
            return $"Error retrieving logs: {ex.Message}";
        }
    }

    private async Task AppendToLogBlobAsync(string caseId, string logText, CancellationToken cancellationToken)
    {
        var containerClient = await GetLogsContainerAsync(cancellationToken);
        var blobName = GetLogBlobName(caseId);
        var appendBlobClient = containerClient.GetAppendBlobClient(blobName);

        // Create the append blob if it doesn't exist
        if (!await appendBlobClient.ExistsAsync(cancellationToken))
        {
            await appendBlobClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        // Append the log entry
        var bytes = Encoding.UTF8.GetBytes(logText);
        using var stream = new MemoryStream(bytes);
        await appendBlobClient.AppendBlockAsync(stream, cancellationToken: cancellationToken);
    }

    private async Task<BlobContainerClient> GetLogsContainerAsync(CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_logsContainer);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return containerClient;
    }

    private static string GetLogBlobName(string caseId)
    {
        return $"{caseId}-detailed.log";
    }

    private static string GenerateProgressBar(int current, int total, int width = 20)
    {
        var progress = (double)current / total;
        var filled = (int)(progress * width);
        var empty = width - filled;
        
        return $"[{'█'.ToString().PadRight(filled, '█')}{'░'.ToString().PadRight(empty, '░')}] {progress:P0}";
    }

    public async Task MigrateLogAsync(string fromCaseId, string toCaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.Equals(fromCaseId, toCaseId, StringComparison.OrdinalIgnoreCase))
                return;

            var containerClient = await GetLogsContainerAsync(cancellationToken);
            var srcBlobName = GetLogBlobName(fromCaseId);
            var dstBlobName = GetLogBlobName(toCaseId);
            
            var srcBlob = containerClient.GetAppendBlobClient(srcBlobName);
            var dstBlob = containerClient.GetAppendBlobClient(dstBlobName);

            // Check if source blob exists
            if (!await srcBlob.ExistsAsync(cancellationToken))
                return;

            // Create destination blob if it doesn't exist
            await dstBlob.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            // Copy content from source to destination
            var downloadResponse = await srcBlob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            using (var sourceStream = downloadResponse.Value.Content)
            {
                using var memoryStream = new MemoryStream();
                await sourceStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                await dstBlob.AppendBlockAsync(memoryStream, cancellationToken: cancellationToken);
            }

            // Delete the source blob after successful migration
            await srcBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            
            _logger.LogInformation("Successfully migrated log from {FromCaseId} to {ToCaseId}", fromCaseId, toCaseId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate log from {FromCaseId} to {ToCaseId}", fromCaseId, toCaseId);
        }
    }

    /// <summary>
    /// Extracts only the properties content from structured responses, removing schema wrapper
    /// </summary>
    private static string ExtractPropertiesFromStructuredResponse(string response, string promptType)
    {
        // Only process structured responses (not regular text generation)
        if (promptType != "StructuredGeneration")
            return response;

        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            // Check if this looks like a schema wrapper (has $schema, title, type, properties)
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("$schema", out _) &&
                root.TryGetProperty("title", out _) &&
                root.TryGetProperty("type", out _) &&
                root.TryGetProperty("properties", out var propertiesElement))
            {
                // Extract just the properties content
                var options = new JsonSerializerOptions { WriteIndented = true };
                return JsonSerializer.Serialize(propertiesElement, options);
            }

            // If it doesn't match the schema wrapper pattern, return original
            return response;
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return original response
            return response;
        }
        catch (Exception)
        {
            // For any other errors, return original response
            return response;
        }
    }
}
