using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

public class LLMService : ILLMService
{
    private readonly ILLMProvider _llmProvider;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<LLMService> _logger;

    public LLMService(ILLMProvider llmProvider, ICaseLoggingService caseLogging, ILogger<LLMService> logger)
    {
        _llmProvider = llmProvider;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string caseId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clean console log
            _logger.LogInformation("LLM: Starting text generation for case {CaseId}", caseId);

            // Detailed logging to blob
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "INFO",
                "Starting LLM text generation",
                new { PromptLength = userPrompt.Length, SystemPromptLength = systemPrompt.Length },
                cancellationToken);

            var response = await _llmProvider.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);

            // Log the interaction details to blob
            await _caseLogging.LogLLMInteractionAsync(caseId, _llmProvider.GetType().Name,
                "TextGeneration", userPrompt, response, null, cancellationToken);

            _logger.LogInformation("LLM: Text generation completed successfully for case {CaseId}", caseId);

            return response;
        }
        catch (Exception ex)
        {
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR",
                "Failed to generate LLM response",
                new { Error = ex.Message, StackTrace = ex.StackTrace },
                cancellationToken);

            _logger.LogError("LLM: Text generation failed for case {CaseId} - {Error}", caseId, ex.Message);
            throw;
        }
    }

    public async Task<string> GenerateStructuredAsync(string caseId, string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clean console log
            _logger.LogInformation("LLM: Starting structured generation for case {CaseId}", caseId);

            // Detailed logging to blob
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "INFO",
                "Starting structured LLM generation",
                new {
                    PromptLength = userPrompt.Length,
                    SystemPromptLength = systemPrompt.Length,
                    SchemaLength = jsonSchema.Length
                },
                cancellationToken);

            var response = await _llmProvider.GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);

            // Log the interaction details to blob
            await _caseLogging.LogLLMInteractionAsync(caseId, _llmProvider.GetType().Name,
                "StructuredGeneration", userPrompt, response, null, cancellationToken);

            _logger.LogInformation("LLM: Structured generation completed successfully for case {CaseId}", caseId);

            return response;
        }
        catch (Exception ex)
        {
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR",
                "Failed to generate structured LLM response",
                new { Error = ex.Message, StackTrace = ex.StackTrace },
                cancellationToken);

            _logger.LogError("LLM: Structured generation failed for case {CaseId} - {Error}", caseId, ex.Message);
            throw;
        }
    }

    public async Task<byte[]> GenerateImageAsync(string caseId, string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clean console log
            _logger.LogInformation("LLM: Starting image generation for case {CaseId}", caseId);

            // Detailed logging to blob
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "INFO",
                "Starting LLM image generation",
                new { PromptLength = prompt.Length },
                cancellationToken);

            var imageBytes = await _llmProvider.GenerateImageAsync(prompt, cancellationToken);

            // Log the interaction details to blob
            await _caseLogging.LogLLMInteractionAsync(caseId, _llmProvider.GetType().Name,
                "ImageGeneration", prompt, $"Generated image: {imageBytes.Length} bytes", null, cancellationToken);

            _logger.LogInformation("LLM: Image generation completed successfully for case {CaseId}, size: {Size} bytes", caseId, imageBytes.Length);

            return imageBytes;
        }
        catch (Exception ex)
        {
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR",
                "Failed to generate LLM image",
                new { Error = ex.Message, StackTrace = ex.StackTrace },
                cancellationToken);

            _logger.LogError("LLM: Image generation failed for case {CaseId} - {Error}", caseId, ex.Message);
            throw;
        }
    }
}