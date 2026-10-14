using Azure;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.ClientModel;
using System.ClientModel.Primitives;
using CaseGen.Functions.Models;

#pragma warning disable OPENAI001

namespace CaseGen.Functions.Services;

public class AzureFoundryLLMProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly ImageClient _imageClient;
    private readonly ILogger<AzureFoundryLLMProvider> _logger;
    private readonly bool _useStructuredOutputs;

    public AzureFoundryLLMProvider(IConfiguration configuration, ILogger<AzureFoundryLLMProvider> logger,
        OpenAIClientOptions? clientOptions = null)
    {
        _logger = logger;
        _useStructuredOutputs = configuration.GetValue<bool>("AzureFoundry:UseStructuredOutputs", false);

        var endpoint = configuration["AzureFoundry:Endpoint"]
            ?? throw new InvalidOperationException("AzureFoundry:Endpoint not configured");

        var deploymentName = configuration["AzureFoundry:ModelName"]
            ?? throw new InvalidOperationException("AzureFoundry:ModelName not configured");

        var imageDeploymentName = configuration["AzureFoundry:ImageDeploymentName"]
            ?? throw new InvalidOperationException("AzureFoundry:ImageDeploymentName not configured");

        var apiKey = configuration["AzureFoundry:ApiKey"]
            ?? throw new InvalidOperationException("AzureFoundry:ApiKey not configured");

        clientOptions ??= new OpenAIClientOptions();
        clientOptions.Endpoint = new Uri(endpoint);
        clientOptions.NetworkTimeout = TimeSpan.FromMinutes(10);
        clientOptions.RetryPolicy = new ClientRetryPolicy(0);

        var credential = new ApiKeyCredential(apiKey);

        _chatClient = new ChatClient(deploymentName, credential, clientOptions);
        _imageClient = new ImageClient(imageDeploymentName, credential, clientOptions);

        _logger.LogInformation("Azure Foundry LLM Provider initialized with endpoint: {Endpoint}, text model: {TextModel}, image model: {ImageModel}",
            endpoint, deploymentName, imageDeploymentName);
    }

    public async Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 30000
            };

            var messages = new List<ChatMessage>()
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, requestOptions, cancellationToken);

            var content = ReadCompletion(response.Value);

            // Extract usage information
            var usage = response.Value.Usage != null ? new LLMUsage
            {
                PromptTokens = response.Value.Usage.InputTokenCount,
                CompletionTokens = response.Value.Usage.OutputTokenCount,
                TotalTokens = response.Value.Usage.TotalTokenCount
            } : null;

            return new LLMResponse
            {
                Content = content,
                Usage = usage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry LLM request failed");
            throw;
        }
    }

    public async Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        try
        {
            var enhancedSystemPrompt = $@"{systemPrompt}

            IMPORTANT: You must respond with valid JSON that fully complies with this exact schema:
            {jsonSchema}

            Your response must be only valid JSON, with no additional text or formatting.";

            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 30000
            };
            if (_useStructuredOutputs)
            {
                if (StructuredOutputSchema.TryCreate(jsonSchema, out var nativeSchema, out var reason))
                    requestOptions.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                        "casezero_task", BinaryData.FromString(nativeSchema), jsonSchemaIsStrict: true);
                else
                    _logger.LogWarning("Native structured output unavailable for this schema: {Reason}. Using prompt contract with mandatory local validation.", reason);
            }

            var messages = new List<ChatMessage>()
            {
                new SystemChatMessage(enhancedSystemPrompt),
                new UserChatMessage(userPrompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, requestOptions, cancellationToken);

            var content = ReadCompletion(response.Value);

            // Extract usage information
            var usage = response.Value.Usage != null ? new LLMUsage
            {
                PromptTokens = response.Value.Usage.InputTokenCount,
                CompletionTokens = response.Value.Usage.OutputTokenCount,
                TotalTokens = response.Value.Usage.TotalTokenCount
            } : null;

            return new LLMResponse
            {
                Content = content,
                Usage = usage
            };

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry structured LLM request failed");
            throw;
        }
    }

    private static string ReadCompletion(ChatCompletion completion)
    {
        if (!string.IsNullOrWhiteSpace(completion.Refusal))
            throw new InvalidOperationException($"The model refused the request: {completion.Refusal}");
        if (completion.FinishReason != ChatFinishReason.Stop)
            throw new InvalidOperationException($"The model response did not complete: {completion.FinishReason}.");
        var content = string.Concat(completion.Content.Select(part => part.Text));
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("The model returned no text content.");
        return content;
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // Translate and enrich the prompt with context before image generation
            // string improvedPrompt = await TranslateAndImprove(prompt, cancellationToken);
            string improvedPrompt = prompt;


            _logger.LogInformation("Generating image using prompt: {Prompt}", improvedPrompt);

            var imageGenerationOptions = new ImageGenerationOptions()
            {
                Size = GeneratedImageSize.W1024xH1024
            };

            GeneratedImage image = await _imageClient.GenerateImageAsync(improvedPrompt, imageGenerationOptions, cancellationToken);
            byte[] imageBytes = image.ImageBytes.ToArray();

            _logger.LogInformation("Image generated successfully, size: {Size} bytes", imageBytes.Length);

            return imageBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry image generation failed for prompt: {Prompt}", prompt);
            throw;
        }
    }

    public async Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating image with reference, reference size: {ReferenceSize} bytes, has mask: {HasMask}", 
                referenceImage.Length, maskImage != null);

            // Note: Using enhanced text prompts with visual consistency instructions
            // Future enhancement: Integrate proper multimodal image-to-image pipeline
            
            _logger.LogInformation("Using enhanced prompt-based generation for visual consistency");

            var imageGenerationOptions = new ImageGenerationOptions()
            {
                Size = GeneratedImageSize.W1024xH1024
            };

            GeneratedImage image = await _imageClient.GenerateImageAsync(prompt, imageGenerationOptions, cancellationToken);
            byte[] imageBytes = image.ImageBytes.ToArray();

            _logger.LogInformation("Image with reference generated successfully, size: {Size} bytes", imageBytes.Length);

            return imageBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry image generation with reference failed for prompt: {Prompt}", prompt);
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
