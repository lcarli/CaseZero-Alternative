using Azure.AI.Inference;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class AzureFoundryLLMProvider : ILLMProvider
{
    private readonly ChatCompletionsClient _client;
    private readonly ILogger<AzureFoundryLLMProvider> _logger;
    private readonly string _modelName;

    public AzureFoundryLLMProvider(IConfiguration configuration, ILogger<AzureFoundryLLMProvider> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureFoundry:Endpoint"]
            ?? throw new InvalidOperationException("AzureFoundry:Endpoint not configured");

        _modelName = configuration["AzureFoundry:ModelName"]
            ?? throw new InvalidOperationException("AzureFoundry:ModelName not configured");

        var credential = new DefaultAzureCredential();

        _client = new ChatCompletionsClient(
            new Uri(endpoint), 
            credential
        );

        _logger.LogInformation("Azure Foundry LLM Provider initialized with endpoint: {Endpoint}", endpoint);
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Azure Foundry generating text response for prompt: {UserPrompt}",
                userPrompt.Substring(0, Math.Min(100, userPrompt.Length)));

            var requestOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                Model = _modelName,
                Temperature = 0.7f,
                MaxTokens = 4000
            };

            var response = await _client.CompleteAsync(requestOptions, cancellationToken);

            var chatCompletions = response.Value;
            if (!string.IsNullOrEmpty(chatCompletions?.Content))
            {
                var content = chatCompletions.Content;
                _logger.LogInformation("Azure Foundry generated response with {TokenCount} tokens",
                    chatCompletions.Usage?.TotalTokens ?? 0);
                return content ?? "";
            }

            throw new InvalidOperationException("No response generated from Azure Foundry");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate text with Azure Foundry");
            throw;
        }
    }

    public async Task<string> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Azure Foundry generating structured response with schema for prompt: {UserPrompt}",
                userPrompt.Substring(0, Math.Min(100, userPrompt.Length)));

            // Enhance the system prompt to include JSON schema requirements
            var enhancedSystemPrompt = $@"{systemPrompt}

            IMPORTANT: You must respond with valid JSON that conforms to this exact schema:
            {jsonSchema}

            Your response must be only valid JSON, no additional text or formatting.";

            var requestOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatRequestSystemMessage(enhancedSystemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                Model = _modelName,
                Temperature = 0.3f, // Lower temperature for more consistent structured output
                MaxTokens = 4000
            };

            var response = await _client.CompleteAsync(requestOptions, cancellationToken);

            var chatCompletions = response.Value;
            if (!string.IsNullOrEmpty(chatCompletions?.Content))
            {
                var content = chatCompletions.Content;

                // Validate that the response is valid JSON
                try
                {
                    JsonDocument.Parse(content ?? "{}");
                    _logger.LogInformation("Azure Foundry generated structured response with {TokenCount} tokens",
                        chatCompletions.Usage?.TotalTokens ?? 0);
                    return content ?? "{}";
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Azure Foundry response was not valid JSON, attempting to fix");

                    // Try to extract JSON from the response
                    var cleanedContent = ExtractJsonFromResponse(content ?? "");
                    JsonDocument.Parse(cleanedContent); // Validate the cleaned content
                    return cleanedContent;
                }
            }

            throw new InvalidOperationException("No structured response generated from Azure Foundry");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate structured response with Azure Foundry");
            throw;
        }
    }

    private string ExtractJsonFromResponse(string response)
    {
        // Try to find JSON content within the response
        var startIndex = response.IndexOf('{');
        var lastIndex = response.LastIndexOf('}');

        if (startIndex >= 0 && lastIndex > startIndex)
        {
            return response.Substring(startIndex, lastIndex - startIndex + 1);
        }

        // If no JSON found, return empty object
        return "{}";
    }
}
