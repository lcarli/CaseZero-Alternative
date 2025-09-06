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

        var deploymentName = configuration["AzureFoundry:ModelName"]
            ?? throw new InvalidOperationException("AzureFoundry:ModelName not configured");

        var imageDeploymentName = configuration["AzureFoundry:ImageDeploymentName"]
            ?? throw new InvalidOperationException("AzureFoundry:ImageDeploymentName not configured");

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = configuration["AzureFoundry:TenantId"] });

        var azureClientOptions = new AzureOpenAIClientOptions()
        {
            NetworkTimeout = TimeSpan.FromMinutes(10)
        };

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential, azureClientOptions);
        _chatClient = azureClient.GetChatClient(deploymentName);
        _imageClient = azureClient.GetImageClient(imageDeploymentName);

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
            // Translate and enrich the prompt with context before image generation
            // string improvedPrompt = await TranslateAndImprove(prompt, cancellationToken);
            string improvedPrompt = prompt;


            _logger.LogInformation("Generating image with gpt-image-1 using prompt: {Prompt}", improvedPrompt);

            // For gpt-image-1, we need to use different configuration
            // gpt-image-1 always returns base64-encoded images and doesn't support ResponseFormat parameter
            var imageGeneration = await _imageClient.GenerateImageAsync(
                improvedPrompt,
                new ImageGenerationOptions()
                {
                    Size = GeneratedImageSize.W1024xH1024
                },
                cancellationToken);

            // gpt-image-1 always returns base64-encoded images, so we need to decode from base64
            byte[] imageBytes;
            if (!string.IsNullOrEmpty(imageGeneration.Value.ImageUri?.ToString()))
            {
                // If we get a URI, it's likely a data URI with base64 content
                var dataUri = imageGeneration.Value.ImageUri.ToString();
                if (dataUri.StartsWith("data:image"))
                {
                    var base64Data = dataUri.Substring(dataUri.IndexOf(',') + 1);
                    imageBytes = Convert.FromBase64String(base64Data);
                }
                else
                {
                    throw new InvalidOperationException("Unexpected image URI format from gpt-image-1");
                }
            }
            else if (imageGeneration.Value.ImageBytes != null && imageGeneration.Value.ImageBytes.Length > 0)
            {
                // Fallback: try ImageBytes if available
                imageBytes = imageGeneration.Value.ImageBytes.ToArray();
            }
            else
            {
                throw new InvalidOperationException("gpt-image-1 did not return image data in expected format");
            }

            _logger.LogInformation("Image generated successfully with gpt-image-1, size: {Size} bytes", imageBytes.Length);

            return imageBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry gpt-image-1 generation failed for prompt: {Prompt}", prompt);
            throw;
        }
    }

    private async Task<string> TranslateAndImprove(string prompt, CancellationToken cancellationToken = default)
    {
        string improvedPrompt = prompt;
        try
        {
            var translateOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 10000
            };
#pragma warning disable AOAI001
            translateOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
#pragma warning restore AOAI001

            var messages = new List<ChatMessage>()
                {
                    new SystemChatMessage("You are an expert prompt engineer for image generation. Translate the user's prompt to English and enrich it with concise, specific visual context (subject, setting, composition, lighting, mood, camera/style details) while preserving intent. Avoid unsafe or copyrighted content. Respond with only the final prompt text, no quotes or extra words."),
                    new UserChatMessage(prompt)
                };

            var translateResponse = await _chatClient.CompleteChatAsync(messages, translateOptions, cancellationToken);
            var translated = translateResponse.Value.Content[0].Text?.Trim();
            if (!string.IsNullOrWhiteSpace(translated))
            {
                improvedPrompt = translated;
            }
        }
        catch (Exception exTranslate)
        {
            _logger.LogWarning(exTranslate, "Falling back to original prompt after translation/context step failed.");
        }

        return improvedPrompt;
    }
}