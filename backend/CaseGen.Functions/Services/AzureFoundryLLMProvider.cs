using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using OpenAI.Chat;
using OpenAI.Images;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class AzureFoundryLLMProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly ImageClient _imageClient;
    private readonly ILogger<AzureFoundryLLMProvider> _logger;

    public AzureFoundryLLMProvider(IConfiguration configuration, ILogger<AzureFoundryLLMProvider> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureFoundry:Endpoint"]
            ?? throw new InvalidOperationException("AzureFoundry:Endpoint not configured");

        var imageEndpoint = configuration["AzureFoundry:ImageEndpoint"]
            ?? throw new InvalidOperationException("AzureFoundry:ImageEndpoint not configured");

        var deploymentName = configuration["AzureFoundry:ModelName"]
            ?? throw new InvalidOperationException("AzureFoundry:ModelName not configured");

        var imageDeploymentName = configuration["AzureFoundry:ImageDeploymentName"]
            ?? throw new InvalidOperationException("AzureFoundry:ImageDeploymentName not configured");

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = configuration["AzureFoundry:TenantId"] });

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);
        var azureImageClient = new AzureOpenAIClient(new Uri(imageEndpoint), credential);
        _chatClient = azureClient.GetChatClient(deploymentName);
        _imageClient = azureImageClient.GetImageClient(imageDeploymentName);

        _logger.LogInformation("Azure Foundry LLM Provider initialized with endpoint: {Endpoint}, text model: {TextModel}, image model: {ImageModel}",
            endpoint, deploymentName, imageDeploymentName);
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
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

            return content ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry LLM request failed");
            throw;
        }
    }

    public async Task<string> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        try
        {
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

            // Validate JSON and normalize
            try
            {
                System.Text.Json.JsonDocument.Parse(content ?? "{}");
                var normalized = NormalizePlanJson(content ?? "{}");
                return normalized;
            }
            catch (System.Text.Json.JsonException)
            {
                var cleanedContent = ExtractJsonFromResponse(content ?? "");
                System.Text.Json.JsonDocument.Parse(cleanedContent);
                var normalized = NormalizePlanJson(cleanedContent);
                return normalized;
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry structured LLM request failed");
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

    private static string NormalizePlanJson(string content)
    {
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(content);
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                obj.Remove("$schema");

                var opts = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                return obj.ToJsonString(opts);
            }

            using var doc = System.Text.Json.JsonDocument.Parse(content);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {
            return content;
        }
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating image with DALL-E using prompt: {Prompt}", prompt);

            var imageGeneration = await _imageClient.GenerateImageAsync(
                prompt,
                new ImageGenerationOptions()
                {
                    Size = GeneratedImageSize.W1024xH1024,
                    Quality = GeneratedImageQuality.Standard,
                    ResponseFormat = GeneratedImageFormat.Bytes
                },
                cancellationToken);

            var imageBytes = imageGeneration.Value.ImageBytes.ToArray();
            
            _logger.LogInformation("Image generated successfully with size: {Size} bytes", imageBytes.Length);
            
            return imageBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry image generation failed for prompt: {Prompt}", prompt);
            throw;
        }
    }

}