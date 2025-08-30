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
            var interaction = new
            {
                Timestamp = DateTime.UtcNow,
                CaseId = caseId,
                Source = "LLM",
                Level = "INFO",
                Provider = provider,
                PromptType = promptType,
                PromptPreview = prompt.Length > 200 ? prompt.Substring(0, 200) + "..." : prompt,
                ResponsePreview = response.Length > 500 ? response.Substring(0, 500) + "..." : response,
                PromptLength = prompt.Length,
                ResponseLength = response.Length,
                TokenCount = tokenCount,
                FullPrompt = prompt,
                FullResponse = response
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
        return $"case-{caseId}-detailed.log";
    }

    private static string GenerateProgressBar(int current, int total, int width = 20)
    {
        var progress = (double)current / total;
        var filled = (int)(progress * width);
        var empty = width - filled;
        
        return $"[{'█'.ToString().PadRight(filled, '█')}{'░'.ToString().PadRight(empty, '░')}] {progress:P0}";
    }
}
