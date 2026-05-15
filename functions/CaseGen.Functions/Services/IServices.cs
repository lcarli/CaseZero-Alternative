namespace CaseGen.Functions.Services;

/// <summary>
/// Provider-agnostic LLM client used by every v2 task. <see cref="AzureFoundryLLMProvider"/>
/// is the Azure OpenAI implementation; <see cref="MockLLMProvider"/> is for tests.
/// </summary>
public interface ILLMProvider
{
    Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// PDF generation used by <see cref="CaseV2.AssetRenderingService"/> to materialise
/// document-typed assets onto disk during a v2 generation run.
/// </summary>
public interface IPdfRenderingService
{
    Task<byte[]> GenerateTestPdfAsync(string title, string markdownContent, string documentType = "general", CancellationToken cancellationToken = default);
}

public class LLMResponse
{
    public string Content { get; set; } = string.Empty;
    public LLMUsage? Usage { get; set; }
}

public class LLMUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
