using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

public class LLMService : ILLMService
{
    private readonly ILLMProvider _llmProvider;
    private readonly ILogger<LLMService> _logger;

    public LLMService(ILLMProvider llmProvider, ILogger<LLMService> logger)
    {
        _llmProvider = llmProvider;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating response for prompt: {UserPrompt}", 
                userPrompt.Substring(0, Math.Min(100, userPrompt.Length)));
            
            return await _llmProvider.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate LLM response");
            throw;
        }
    }

    public async Task<string> GenerateStructuredAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating structured response with schema for prompt: {UserPrompt}", 
                userPrompt.Substring(0, Math.Min(100, userPrompt.Length)));
            
            return await _llmProvider.GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate structured LLM response");
            throw;
        }
    }
}