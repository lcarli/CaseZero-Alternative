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

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var caseId = ExtractCaseIdFromPrompt(userPrompt) ?? "unknown";
        
        try
        {
            // Clean console log
            _logger.LogInformation("🤖 Generating LLM response...");
            
            // Detailed logging to blob
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "INFO", 
                "Starting LLM text generation", 
                new { PromptLength = userPrompt.Length, SystemPromptLength = systemPrompt.Length }, 
                cancellationToken);
            
            var response = await _llmProvider.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
            
            // Log the interaction details to blob
            await _caseLogging.LogLLMInteractionAsync(caseId, _llmProvider.GetType().Name, 
                "TextGeneration", userPrompt, response, null, cancellationToken);
                
            _logger.LogInformation("✅ LLM response generated successfully");
            
            return response;
        }
        catch (Exception ex)
        {
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR", 
                "Failed to generate LLM response", 
                new { Error = ex.Message, StackTrace = ex.StackTrace }, 
                cancellationToken);
                
            _logger.LogError("❌ LLM generation failed: {Error}", ex.Message);
            throw;
        }
    }

    public async Task<string> GenerateStructuredAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        var caseId = ExtractCaseIdFromPrompt(userPrompt) ?? "unknown";
        
        try
        {
            // Clean console log
            _logger.LogInformation("📋 Generating structured LLM response...");
            
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
                
            _logger.LogInformation("✅ Structured LLM response generated successfully");
            
            return response;
        }
        catch (Exception ex)
        {
            await _caseLogging.LogDetailedAsync(caseId, "LLMService", "ERROR", 
                "Failed to generate structured LLM response", 
                new { Error = ex.Message, StackTrace = ex.StackTrace }, 
                cancellationToken);
                
            _logger.LogError("❌ Structured LLM generation failed: {Error}", ex.Message);
            throw;
        }
    }

    private static string? ExtractCaseIdFromPrompt(string prompt)
    {
        // Try to extract case ID from the prompt
        // Look for patterns like "CASE-2024-001" or similar
        var patterns = new[]
        {
            @"CASE-\d{4}-\d{3}",
            @"case-\d{4}-\d{3}",
            @"Case-\d{4}-\d{3}"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(prompt, pattern);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }
}