using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public class LLMService : ILLMService
{
    private readonly ILLMProvider _llmProvider;
    private readonly ICaseLoggingService _caseLogging;

    public LLMService(ILLMProvider llmProvider, ICaseLoggingService caseLogging)
    {
        _llmProvider = llmProvider;
        _caseLogging = caseLogging;
    }

    public async Task<string> GenerateAsync(string caseId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clean console log for workflow tracking
            _caseLogging.LogOrchestratorStep(caseId, "LLM_TEXT_START", $"Starting text generation ({userPrompt.Length} chars)");

            // Detailed logging to blob storage
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "INFO",
                "Starting LLM text generation",
                new { 
                    PromptLength = userPrompt.Length, 
                    SystemPromptLength = systemPrompt.Length,
                    Provider = _llmProvider.GetType().Name
                },
                cancellationToken);

            var response = await _llmProvider.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);

            // Log the interaction details to blob storage with token usage
            var totalTokens = response.Usage?.TotalTokens;
            await _caseLogging.LogLLMInteractionAsync(caseId, _llmProvider.GetType().Name,
                "TextGeneration", userPrompt, response.Content, totalTokens, cancellationToken);

            // Clean console log for completion
            _caseLogging.LogOrchestratorStep(caseId, "LLM_TEXT_COMPLETE", 
                $"Generated {response.Content.Length} chars, tokens: {totalTokens?.ToString() ?? "unknown"}");

            return response.Content;
        }
        catch (Exception ex)
        {
            // Error logging to both console and blob storage
            _caseLogging.LogOrchestratorStep(caseId, "LLM_TEXT_ERROR", $"Text generation failed: {ex.Message}");
            
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR",
                "Failed to generate LLM response",
                new { 
                    Error = ex.Message, 
                    StackTrace = ex.StackTrace,
                    Provider = _llmProvider.GetType().Name,
                    PromptLength = userPrompt?.Length ?? 0
                },
                cancellationToken);

            throw;
        }
    }

    public async Task<string> GenerateStructuredAsync(string caseId, string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clean console log for workflow tracking
            _caseLogging.LogOrchestratorStep(caseId, "LLM_STRUCTURED_START", $"Starting structured generation ({userPrompt.Length} chars)");

            // Detailed logging to blob storage
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "INFO",
                "Starting structured LLM generation",
                new {
                    PromptLength = userPrompt.Length,
                    SystemPromptLength = systemPrompt.Length,
                    SchemaLength = jsonSchema.Length,
                    Provider = _llmProvider.GetType().Name
                },
                cancellationToken);

            var response = await _llmProvider.GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);

            // Log the interaction details to blob storage with token usage
            var totalTokens = response.Usage?.TotalTokens;
            await _caseLogging.LogLLMInteractionAsync(caseId, _llmProvider.GetType().Name,
                "StructuredGeneration", userPrompt, response.Content, totalTokens, cancellationToken);

            // Clean console log for completion
            _caseLogging.LogOrchestratorStep(caseId, "LLM_STRUCTURED_COMPLETE", 
                $"Generated structured response ({response.Content.Length} chars), tokens: {totalTokens?.ToString() ?? "unknown"}");

            return response.Content;
        }
        catch (Exception ex)
        {
            // Error logging to both console and blob storage
            _caseLogging.LogOrchestratorStep(caseId, "LLM_STRUCTURED_ERROR", $"Structured generation failed: {ex.Message}");
            
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR",
                "Failed to generate structured LLM response",
                new { 
                    Error = ex.Message, 
                    StackTrace = ex.StackTrace,
                    Provider = _llmProvider.GetType().Name,
                    PromptLength = userPrompt?.Length ?? 0,
                    SchemaLength = jsonSchema?.Length ?? 0
                },
                cancellationToken);

            throw;
        }
    }

    public async Task<byte[]> GenerateImageAsync(string caseId, string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clean console log for workflow tracking
            _caseLogging.LogOrchestratorStep(caseId, "LLM_IMAGE_START", $"Starting image generation ({prompt.Length} chars)");

            // Detailed logging to blob storage
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "INFO",
                "Starting LLM image generation",
                new { 
                    PromptLength = prompt.Length,
                    Provider = _llmProvider.GetType().Name
                },
                cancellationToken);

            var imageBytes = await _llmProvider.GenerateImageAsync(prompt, cancellationToken);

            // Log the interaction details to blob storage
            await _caseLogging.LogLLMInteractionAsync(caseId, _llmProvider.GetType().Name,
                "ImageGeneration", prompt, $"Generated image: {imageBytes.Length} bytes", null, cancellationToken);

            // Clean console log for completion
            _caseLogging.LogOrchestratorStep(caseId, "LLM_IMAGE_COMPLETE", $"Generated image ({imageBytes.Length:N0} bytes)");

            return imageBytes;
        }
        catch (Exception ex)
        {
            // Error logging to both console and blob storage
            _caseLogging.LogOrchestratorStep(caseId, "LLM_IMAGE_ERROR", $"Image generation failed: {ex.Message}");
            
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR",
                "Failed to generate LLM image",
                new { 
                    Error = ex.Message, 
                    StackTrace = ex.StackTrace,
                    Provider = _llmProvider.GetType().Name,
                    PromptLength = prompt?.Length ?? 0
                },
                cancellationToken);

            throw;
        }
    }
}