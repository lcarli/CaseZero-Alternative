using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: CaseGen.Pilot <repository-root> <new-run-directory>");
    return 64;
}

var repository = Path.GetFullPath(args[0]);
var runDirectory = Path.GetFullPath(args[1]);
if (Directory.Exists(runDirectory))
    throw new InvalidOperationException("The pilot requires a new run directory; existing evidence will not be overwritten.");
Directory.CreateDirectory(runDirectory);

var settingsPath = Path.Combine(repository, "functions", "CaseGen.Functions", "local.settings.json");
using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
if (settings.RootElement.TryGetProperty("Values", out var localValues))
{
    foreach (var entry in localValues.EnumerateObject())
        values[entry.Name.Replace("__", ":")] = entry.Value.GetString();
}
var configuration = new ConfigurationBuilder()
    .AddJsonFile(settingsPath, optional: false)
    .AddInMemoryCollection(values)
    .AddEnvironmentVariables()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["CaseGenV2:CasesBasePath"] = Path.Combine(runDirectory, "cases"),
        ["LLM:UseAzureFoundry"] = "true"
    })
    .Build();

using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    })
    .SetMinimumLevel(LogLevel.Information)
    .AddFilter(typeof(AzureFoundryLLMProvider).FullName, LogLevel.Warning));

var reporter = new LocalPhaseReporter(runDirectory);
var provider = new TracingProvider(
    new AzureFoundryLLMProvider(configuration, loggerFactory.CreateLogger<AzureFoundryLLMProvider>()),
    runDirectory, reporter);
using var services = new ServiceCollection()
    .AddSingleton(loggerFactory)
    .AddCaseGenApplicationServices(configuration)
    .BuildServiceProvider();
var templates = services.GetRequiredService<IEvidenceTemplateRegistry>();
var renderer = new CapturingRenderer(new AssetRenderingService(
    new PdfRenderingService(configuration, loggerFactory.CreateLogger<PdfRenderingService>()),
    provider,
    services.GetRequiredService<IEvidenceDocumentRenderer>(),
    templates, loggerFactory.CreateLogger<AssetRenderingService>()), runDirectory);
var consistency = new CapturingConsistency(
    new ConsistencyValidator(loggerFactory.CreateLogger<ConsistencyValidator>()), runDirectory);
var generator = new CaseV2GeneratorService(provider, configuration, renderer,
    new DisabledPublisher(),
    new MechanicalRulesBuilder(loggerFactory.CreateLogger<MechanicalRulesBuilder>()),
    consistency, loggerFactory.CreateLogger<CaseV2GeneratorService>());
var request = new GenerateCaseV2Request
{
    CaseId = "case_pilot_rookie_" + DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..8],
    Theme = "Nonviolent theft of a small historic artifact at a fictional museum in Curitiba. " +
        "A grounded, beginner-friendly documentary investigation with a small cast, " +
        "complementary physical or documentary observations, and plausible divergent statements. " +
        "Do not use real people or real institutions. Avoid graphic content.",
    Location = "Museu Municipal do Vale, Curitiba, Brasil",
    Difficulty = "Rookie",
    RequiredRank = "Rookie",
    Language = "pt-BR",
    WriteToDisk = true
};
await Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "request.json"), request);
await Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "execution.json"), new
{
    startedAt = DateTimeOffset.UtcNow,
    targetFramework = "net9.0",
    provider = nameof(AzureFoundryLLMProvider),
    authentication = "API key from local configuration",
    publicationEnabled = false,
    nativeStructuredOutputsConfigured = configuration.GetValue<bool>("AzureFoundry:UseStructuredOutputs", false),
    casesBasePath = configuration["CaseGenV2:CasesBasePath"]
});
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
await reporter.ReportStartedAsync();
var watch = Stopwatch.StartNew();
try
{
    var result = await generator.GenerateAsync(request, reporter, cancellation.Token);
    await Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "response.json"), result);
    await File.WriteAllTextAsync(Path.Combine(runDirectory, "candidate.case.json"), result.CaseJson);
    await provider.WriteSummaryAsync(watch.Elapsed);
    if (!result.Quality.Approved)
    {
        await reporter.ReportFailedAsync(string.Join("; ", result.Quality.Issues
            .Where(issue => issue.Blocking).Select(issue => $"{issue.Code}: {issue.Message}")));
        Console.WriteLine($"PILOT BLOCKED. Evidence: {runDirectory}");
        return 2;
    }
    if (!result.Quality.Persisted || result.Quality.Published || result.BlobsPublished != 0)
        throw new InvalidOperationException("The pilot did not satisfy isolated local persistence without publication.");
    await reporter.ReportCompletedAsync();
    Console.WriteLine($"PILOT APPROVED. Case: {result.OutputPath}; evidence: {runDirectory}");
    return 0;
}
catch (Exception exception)
{
    await Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "failure.json"), new
    {
        failedAt = DateTimeOffset.UtcNow,
        phase = reporter.CurrentPhase,
        exceptionType = exception.GetType().FullName,
        error = exception.ToString()
    });
    await provider.WriteSummaryAsync(watch.Elapsed);
    await reporter.ReportFailedAsync(exception.Message);
    Console.Error.WriteLine($"PILOT FAILED in {reporter.CurrentPhase}: {exception.Message}");
    Console.Error.WriteLine($"Evidence: {runDirectory}");
    return 1;
}

static class Artifacts
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task WriteJsonAsync<T>(string path, T value, CancellationToken ct = default)
        => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Options), ct);
}

sealed class DisabledPublisher : ICaseV2BlobPublisher
{
    public bool IsConfigured => false;
    public Task<string?> GetPublicationETagAsync(string caseId, CancellationToken ct = default)
        => throw new InvalidOperationException("Cloud publication is prohibited for this local pilot.");
    public Task<int> PublishAsync(CaseBundle bundle, CancellationToken ct = default)
        => throw new InvalidOperationException("Cloud publication is prohibited for this local pilot.");
}

sealed class LocalPhaseReporter(string runDirectory) : IJobPhaseReporter
{
    private JobPhaseStatus? status;
    public string? CurrentPhase { get; private set; }
    public bool IsConfigured => true;

    public Task ReportStartedAsync(CancellationToken ct = default) => WriteAsync(_ => { }, ct);
    public Task ReportAttemptStartedAsync(int attempt, int maxAttempts, CancellationToken ct = default)
        => WriteAsync(progress => progress.StartAttempt(attempt, maxAttempts, DateTimeOffset.UtcNow), ct);
    public Task ReportRetryScheduledAsync(string error, DateTimeOffset retryAt, CancellationToken ct = default)
        => WriteAsync(progress => progress.ScheduleRetry(error, retryAt, DateTimeOffset.UtcNow), ct);
    public Task ReportPhaseAsync(string phase, CancellationToken ct = default)
    {
        CurrentPhase = phase;
        Console.WriteLine($"PHASE {phase}");
        return WriteAsync(progress => progress.StartStage(phase, DateTimeOffset.UtcNow, retry: false), ct);
    }
    public Task ReportCompletedAsync(CancellationToken ct = default) =>
        WriteAsync(progress => progress.Complete(DateTimeOffset.UtcNow), ct);
    public Task ReportFailedAsync(string error, CancellationToken ct = default) =>
        WriteAsync(progress => progress.Fail(error, DateTimeOffset.UtcNow), ct);
    public Task<JobPhaseStatus?> GetAsync(CancellationToken ct = default) => Task.FromResult(status);

    private async Task WriteAsync(Action<JobPhaseStatus> update, CancellationToken ct)
    {
        status ??= JobPhaseStatus.Started(Path.GetFileName(runDirectory), DateTimeOffset.UtcNow);
        update(status);
        await Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "phase.json"), status, ct);
        await File.AppendAllTextAsync(Path.Combine(runDirectory, "phases.jsonl"),
            JsonSerializer.Serialize(status) + Environment.NewLine, ct);
    }
}

sealed class TracingProvider(ILLMProvider inner, string runDirectory, LocalPhaseReporter reporter) : ILLMProvider
{
    public string GenerationIdentity => inner.GenerationIdentity;
    private int sequence;
    private readonly ConcurrentDictionary<int, object> calls = new();

    public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        => TextAsync("text", systemPrompt, userPrompt, null,
            () => inner.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken), cancellationToken);

    public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
        => TextAsync("structured", systemPrompt, userPrompt, jsonSchema,
            () => inner.GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken), cancellationToken);

    public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema,
        int maximumOutputTokens, CancellationToken cancellationToken = default)
        => TextAsync("structured", systemPrompt, userPrompt, jsonSchema,
            () => inner.GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, maximumOutputTokens, cancellationToken),
            cancellationToken, maximumOutputTokens);

    public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
        => ImageAsync(prompt, () => inner.GenerateImageAsync(prompt, cancellationToken), false, cancellationToken);

    public Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null, CancellationToken cancellationToken = default)
        => ImageAsync(prompt, () => inner.GenerateImageWithReferenceAsync(prompt, referenceImage, maskImage, cancellationToken), true, cancellationToken);

    private async Task<LLMResponse> TextAsync(string kind, string system, string user, string? schema,
        Func<Task<LLMResponse>> invoke, CancellationToken ct, int? maximumOutputTokens = null)
    {
        var id = Interlocked.Increment(ref sequence);
        var directory = Path.Combine(runDirectory, "calls", $"{id:D3}_{reporter.CurrentPhase}");
        Directory.CreateDirectory(directory);
        await Artifacts.WriteJsonAsync(Path.Combine(directory, "request.json"),
            new { id, kind, phase = reporter.CurrentPhase, startedAt = DateTimeOffset.UtcNow, system, user, schema, maximumOutputTokens }, ct);
        Console.WriteLine($"CALL {id:D3} {kind} started ({reporter.CurrentPhase})");
        var watch = Stopwatch.StartNew();
        try
        {
            var response = await invoke();
            await File.WriteAllTextAsync(Path.Combine(directory, "response.txt"), response.Content, ct);
            var summary = new { id, kind, phase = reporter.CurrentPhase, elapsedMs = watch.Elapsed.TotalMilliseconds, response.Usage, success = true };
            calls[id] = summary;
            await Artifacts.WriteJsonAsync(Path.Combine(directory, "result.json"), summary, ct);
            Console.WriteLine($"CALL {id:D3} completed in {watch.Elapsed.TotalSeconds:F1}s; tokens={response.Usage?.TotalTokens}");
            return response;
        }
        catch (Exception exception)
        {
            var summary = new { id, kind, elapsedMs = watch.Elapsed.TotalMilliseconds, success = false, error = exception.ToString() };
            calls[id] = summary;
            await Artifacts.WriteJsonAsync(Path.Combine(directory, "result.json"), summary, CancellationToken.None);
            throw;
        }
    }

    private async Task<byte[]> ImageAsync(string prompt, Func<Task<byte[]>> invoke, bool withReference, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref sequence);
        var directory = Path.Combine(runDirectory, "calls", $"{id:D3}_{reporter.CurrentPhase}_image");
        Directory.CreateDirectory(directory);
        await Artifacts.WriteJsonAsync(Path.Combine(directory, "request.json"),
            new { id, kind = "image", prompt, withReference, startedAt = DateTimeOffset.UtcNow }, ct);
        Console.WriteLine($"CALL {id:D3} image started");
        var watch = Stopwatch.StartNew();
        try
        {
            var bytes = await invoke();
            await File.WriteAllBytesAsync(Path.Combine(directory, "response.image"), bytes, ct);
            var summary = new { id, kind = "image", elapsedMs = watch.Elapsed.TotalMilliseconds, bytes = bytes.Length, success = true };
            calls[id] = summary;
            await Artifacts.WriteJsonAsync(Path.Combine(directory, "result.json"), summary, ct);
            Console.WriteLine($"CALL {id:D3} image completed in {watch.Elapsed.TotalSeconds:F1}s");
            return bytes;
        }
        catch (Exception exception)
        {
            var summary = new { id, kind = "image", elapsedMs = watch.Elapsed.TotalMilliseconds, success = false, error = exception.ToString() };
            calls[id] = summary;
            await Artifacts.WriteJsonAsync(Path.Combine(directory, "result.json"), summary, CancellationToken.None);
            throw;
        }
    }

    public Task WriteSummaryAsync(TimeSpan elapsed)
        => Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "usage.json"), new
        {
            elapsedSeconds = elapsed.TotalSeconds,
            callCount = calls.Count,
            calls = calls.OrderBy(entry => entry.Key).Select(entry => entry.Value)
        });
}

sealed class CapturingConsistency(IConsistencyValidator inner, string runDirectory) : IConsistencyValidator
{
    private int sequence;
    public ConsistencyReport Validate(CaseDraft draft)
    {
        var id = Interlocked.Increment(ref sequence);
        File.WriteAllText(Path.Combine(runDirectory, $"draft_{id:D2}_before_consistency.json"),
            JsonSerializer.Serialize(draft, Artifacts.Options));
        File.WriteAllText(Path.Combine(runDirectory, $"review_{id:D2}.json"), draft.ToReviewJson());
        var report = inner.Validate(draft);
        File.WriteAllText(Path.Combine(runDirectory, $"draft_{id:D2}_after_consistency.json"),
            JsonSerializer.Serialize(draft, Artifacts.Options));
        File.WriteAllText(Path.Combine(runDirectory, $"consistency_{id:D2}.json"),
            JsonSerializer.Serialize(report, Artifacts.Options));
        return report;
    }
}

sealed class CapturingRenderer(IAssetRenderingService inner, string runDirectory) : IAssetRenderingService
{
    private int preparationSequence;
    public void PrepareForReview(IEnumerable<EvidenceAsset> assets)
    {
        var material = assets.ToList();
        inner.PrepareForReview(material);
        File.WriteAllText(Path.Combine(runDirectory, $"prepared_assets_{Interlocked.Increment(ref preparationSequence):D2}.json"),
            JsonSerializer.Serialize(material, Artifacts.Options));
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadPreparedDocumentsAsync(
        IEnumerable<EvidenceAsset> assets, CancellationToken ct = default)
    {
        var texts = await inner.ReadPreparedDocumentsAsync(assets, ct);
        await Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "readable-evidence.json"), texts, ct);
        return texts;
    }

    public async Task<AssetRenderingReport> RenderAllAsync(string caseId, string casesBasePath,
        IEnumerable<EvidenceAsset> assets, CancellationToken ct = default)
    {
        try
        {
            var report = await inner.RenderAllAsync(caseId, casesBasePath, assets, ct);
            await Artifacts.WriteJsonAsync(Path.Combine(runDirectory, "rendering.json"), report, ct);
            return report;
        }
        finally
        {
            var source = Path.Combine(casesBasePath, caseId, "assets");
            if (Directory.Exists(source))
            {
                var destination = Path.Combine(runDirectory, "rendered-evidence");
                Directory.CreateDirectory(destination);
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, overwrite: false);
                }
            }
        }
    }
}
