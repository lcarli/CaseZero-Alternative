namespace CaseGen.Functions.Services.CaseV2;

public sealed record CaseV2TokenUsage(long InputTokens, long OutputTokens, long TotalTokens);

public static class CaseV2TokenUsageTracker
{
    private static readonly AsyncLocal<Counter?> Current = new();

    public static IDisposable Begin()
    {
        var prior = Current.Value;
        var counter = new Counter();
        Current.Value = counter;
        return new Scope(prior, counter);
    }

    public static void Record(LLMUsage? usage)
    {
        if (usage is null || Current.Value is not { } counter)
            return;
        Interlocked.Add(ref counter.Input, usage.PromptTokens);
        Interlocked.Add(ref counter.Output, usage.CompletionTokens);
        Interlocked.Add(ref counter.Total, usage.TotalTokens);
    }

    public static CaseV2TokenUsage Snapshot()
    {
        var counter = Current.Value;
        return counter is null
            ? new CaseV2TokenUsage(0, 0, 0)
            : new CaseV2TokenUsage(
                Interlocked.Read(ref counter.Input),
                Interlocked.Read(ref counter.Output),
                Interlocked.Read(ref counter.Total));
    }

    private sealed class Counter
    {
        public long Input;
        public long Output;
        public long Total;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Counter? _prior;
        public Scope(Counter? prior, Counter current)
        {
            _prior = prior;
            Current.Value = current;
        }
        public void Dispose() => Current.Value = _prior;
    }
}

public sealed class UsageTrackingLLMProvider : ILLMProvider
{
    private readonly ILLMProvider _inner;

    public UsageTrackingLLMProvider(ILLMProvider inner)
    {
        _inner = inner;
    }

    public async Task<LLMResponse> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var response = await _inner.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
        CaseV2TokenUsageTracker.Record(response.Usage);
        return response;
    }

    public async Task<LLMResponse> GenerateStructuredResponseAsync(
        string systemPrompt,
        string userPrompt,
        string jsonSchema,
        CancellationToken cancellationToken = default)
    {
        var response = await _inner.GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
        CaseV2TokenUsageTracker.Record(response.Usage);
        return response;
    }

    public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
        _inner.GenerateImageAsync(prompt, cancellationToken);

    public Task<byte[]> GenerateImageWithReferenceAsync(
        string prompt,
        byte[] referenceImage,
        byte[]? maskImage = null,
        CancellationToken cancellationToken = default) =>
        _inner.GenerateImageWithReferenceAsync(prompt, referenceImage, maskImage, cancellationToken);
}
