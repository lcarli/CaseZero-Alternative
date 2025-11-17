using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseGen.Functions.Services;

public class CaseLoggingService : ICaseLoggingService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<CaseLoggingService> _logger;
    private readonly string _logsContainer;
    private static readonly JsonSerializerOptions StructuredJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Detailed,
                Level = level,
                Source = source,
                Message = message,
                Data = data
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log detailed entry for case {CaseId}", caseId);
        }
    }

    public async Task LogStructuredAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.CaseId))
            {
                throw new ArgumentException("Structured log entry requires a case identifier", nameof(entry));
            }

            var normalized = NormalizeEntry(entry);
            var json = JsonSerializer.Serialize(normalized, StructuredJsonOptions) + "\n";
            await AppendToLogBlobAsync(normalized.CaseId, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write structured log entry for case {CaseId}", entry.CaseId);
        }
    }

    public async Task LogLLMInteractionAsync(string caseId, string provider, string promptType, string prompt, string response, int? tokenCount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract only the properties content for cleaner logs
            var cleanedResponse = ExtractPropertiesFromStructuredResponse(response, promptType);
            
            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.LlmInteraction,
                Source = provider,
                Message = $"{promptType} interaction",
                Tokens = tokenCount.HasValue ? new TokenUsageSummary { TotalTokens = tokenCount } : null,
                Data = new
                {
                    promptType,
                    metrics = new
                    {
                        promptLength = prompt.Length,
                        responseLength = response.Length,
                        tokenCount = tokenCount
                    },
                    preview = new
                    {
                        prompt = prompt.Length > 200 ? prompt[..200] + "..." : prompt,
                        response = cleanedResponse.Length > 500 ? cleanedResponse[..500] + "..." : cleanedResponse
                    },
                    fullContent = new
                    {
                        prompt,
                        response = cleanedResponse
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log LLM interaction for case {CaseId}", caseId);
        }
    }

    public async Task LogStepResponseAsync(string caseId, string stepName, string jsonResponse, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetLogsContainerAsync(cancellationToken);
            var folderName = $"{caseId}";
            var fileName = $"{stepName.ToLowerInvariant()}.json";
            var blobName = $"{folderName}/{fileName}";
            
            var blobClient = containerClient.GetBlobClient(blobName);
            
            // Format JSON nicely
            var formattedJson = FormatJsonResponse(jsonResponse);
            var bytes = Encoding.UTF8.GetBytes(formattedJson);
            
            using var stream = new MemoryStream(bytes);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);
            
            _logger.LogInformation("📁 Saved {StepName} response to {BlobName}", stepName, blobName);

            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Payload,
                Step = stepName,
                Message = "Step response saved",
                PayloadReference = blobName
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save step response for {CaseId} step {StepName}", caseId, stepName);
        }
    }

    public async Task LogStepMetadataAsync(string caseId, string stepName, object metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetLogsContainerAsync(cancellationToken);
            var folderName = $"{caseId}";
            var fileName = $"{stepName.ToLowerInvariant()}_metadata.json";
            var blobName = $"{folderName}/{fileName}";
            
            var blobClient = containerClient.GetBlobClient(blobName);
            
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            var bytes = Encoding.UTF8.GetBytes(metadataJson);
            using var stream = new MemoryStream(bytes);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);
            
            _logger.LogInformation("📊 Saved {StepName} metadata to {BlobName}", stepName, blobName);

            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Metadata,
                Step = stepName,
                Message = "Step metadata saved",
                PayloadReference = blobName,
                Data = metadata
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save step metadata for {CaseId} step {StepName}", caseId, stepName);
        }
    }

    private static string FormatJsonResponse(string jsonResponse)
    {
        try
        {
            // Try to parse and reformat the JSON for better readability
            var jsonDoc = JsonDocument.Parse(jsonResponse);
            return JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {
            // If parsing fails, return as-is
            return jsonResponse;
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
        return $"{caseId}/{caseId}.log";
    }

    private static string GenerateProgressBar(int current, int total, int width = 20)
    {
        var progress = (double)current / total;
        var filled = (int)(progress * width);
        var empty = width - filled;
        
        return $"[{'█'.ToString().PadRight(filled, '█')}{'░'.ToString().PadRight(empty, '░')}] {progress:P0}";
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

    /// <summary>
    /// Logs a workflow step with clear phase tracking
    /// </summary>
    public async Task LogWorkflowStepAsync(string caseId, string phase, string step, object? stepData = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = phase,
                Step = step,
                Message = $"Workflow step {step}",
                Data = stepData
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log workflow step for case {CaseId}", caseId);
        }
    }

    /// <summary>
    /// Logs workflow phase transitions with summary data
    /// </summary>
    public async Task LogPhaseTransitionAsync(string caseId, string fromPhase, string toPhase, object? phaseData = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.PhaseTransition,
                Phase = toPhase,
                Message = $"Transition {fromPhase} -> {toPhase}",
                Data = new
                {
                    from = fromPhase,
                    to = toPhase,
                    phaseData
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log phase transition for case {CaseId}", caseId);
        }
    }

    /// <summary>
    /// Creates an executive summary of the workflow execution
    /// </summary>
    public async Task CreateExecutiveSummaryAsync(string caseId, object summaryData, CancellationToken cancellationToken = default)
    {
        try
        {
            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.ExecutiveSummary,
                Message = "Executive summary created",
                Data = summaryData
            }, cancellationToken);

            // Also save as separate summary file
            var containerClient = await GetLogsContainerAsync(cancellationToken);
            var summaryBlobName = $"{caseId}/{caseId}_EXECUTIVE_SUMMARY.json";
            var summaryBlobClient = containerClient.GetBlobClient(summaryBlobName);
            var summaryDocument = new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff UTC"),
                caseId,
                logType = "executive_summary",
                summary = summaryData
            };
            
            var summaryJson = JsonSerializer.Serialize(summaryDocument, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            var bytes = Encoding.UTF8.GetBytes(summaryJson);
            using var stream = new MemoryStream(bytes);
            await summaryBlobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);

            await LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Metadata,
                Message = "Executive summary stored",
                PayloadReference = summaryBlobName
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create executive summary for case {CaseId}", caseId);
        }
    }

    private static StructuredLogEntry NormalizeEntry(StructuredLogEntry entry)
    {
        var timestamp = entry.TimestampUtc == default ? DateTime.UtcNow : entry.TimestampUtc;
        var level = string.IsNullOrWhiteSpace(entry.Level) ? "Information" : entry.Level;
        return entry with
        {
            TimestampUtc = timestamp,
            Level = level
        };
    }
}
