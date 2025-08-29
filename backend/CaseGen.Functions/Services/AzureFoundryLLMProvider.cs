using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class AzureFoundryLLMProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureFoundryLLMProvider> _logger;

    public AzureFoundryLLMProvider(IConfiguration configuration, ILogger<AzureFoundryLLMProvider> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureFoundry:Endpoint"]
            ?? throw new InvalidOperationException("AzureFoundry:Endpoint not configured");

        var deploymentName = configuration["AzureFoundry:ModelName"]
            ?? throw new InvalidOperationException("AzureFoundry:ModelName not configured");

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions{TenantId = configuration["AzureFoundry:TenantId"]});

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);
        _chatClient = azureClient.GetChatClient(deploymentName);

        _logger.LogInformation("Azure Foundry LLM Provider initialized with endpoint: {Endpoint}, model: {Model}", 
            endpoint, deploymentName);
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Azure Foundry generating text response for prompt: {UserPrompt}",
                userPrompt.Substring(0, Math.Min(100, userPrompt.Length)));

            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 10000
            };

            // Enable the new max_completion_tokens property
            #pragma warning disable AOAI001
            requestOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
            #pragma warning restore AOAI001

            var messages = new List<ChatMessage>()
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, requestOptions, cancellationToken);

            var content = response.Value.Content[0].Text;
            _logger.LogInformation("Azure Foundry generated response with {TokenCount} tokens",
                response.Value.Usage?.TotalTokenCount ?? 0);
            
            return content ?? "";
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

            IMPORTANTE: Você deve responder com um JSON válido que esteja em total conformidade com este esquema exato:
            {jsonSchema}

            Sua resposta deve ser apenas JSON válido, sem texto ou formatação adicional.";

            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 10000
            };

            // Enable the new max_completion_tokens property
            #pragma warning disable AOAI001
            requestOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
            #pragma warning restore AOAI001

            var messages = new List<ChatMessage>()
            {
                new SystemChatMessage(enhancedSystemPrompt),
                new UserChatMessage(userPrompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, requestOptions, cancellationToken);

            var content = response.Value.Content[0].Text;

            // Validate that the response is valid JSON
            try
            {
                JsonDocument.Parse(content ?? "{}");
                _logger.LogInformation("Azure Foundry generated structured response with {TokenCount} tokens",
                    response.Value.Usage?.TotalTokenCount ?? 0);
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