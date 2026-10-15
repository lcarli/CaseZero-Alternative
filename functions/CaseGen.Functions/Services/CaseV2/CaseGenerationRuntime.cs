using System.Text;
using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class CaseGenerationBudgetException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record GenerationCallBudget(int MaximumCalls, long MaximumInputTokens, long MaximumOutputTokens,
    int MaximumConcurrency, int TimeoutSeconds, int TotalTimeoutSeconds)
{
    public static GenerationCallBudget For(DifficultyProfile profile) => new(
        80 + profile.MaxAssets * 4 + profile.MaxAnalyses * 8,
        500_000 + profile.Topology.MaxDerivationDepth * 100_000L,
        80_000 + profile.MaxAssets * 8_000L,
        4, 180, profile.Name == "Rookie" ? 1800 : 3600);
}

public sealed class GenerationTaskTrace
{
    public string Task { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public string SchemaHash { get; set; } = string.Empty;
    public string SystemPromptHash { get; set; } = string.Empty;
    public string UserPromptHash { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public double LatencyMs { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public string? FailureCode { get; set; }
    public bool UsageEstimated { get; set; }
    public string Kind { get; set; } = "structured";
    public int MaximumOutputTokens { get; set; }
    public int ImageUnits { get; set; }
}

public sealed class CaseHarnessReport
{
    public string Version { get; set; } = CaseGenerationRuntime.Version;
    public string Provider { get; set; } = string.Empty;
    public string DefinitionHash { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Skills { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Resources { get; set; } = new Dictionary<string, string>();
    public GenerationCallBudget Budget { get; set; } = GenerationCallBudget.For(DifficultyProfileCatalog.Get("Rookie"));
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public int EstimatedUsageCalls { get; set; }
    public int ImageCalls { get; set; }
    public double ActiveMilliseconds { get; set; }
    public List<GenerationTaskTrace> Tasks { get; set; } = [];
}

public sealed class CaseGenerationRuntime : IDisposable
{
    public const string Version = "canonical-harness-2";
    private static readonly AsyncLocal<CaseGenerationRuntime?> CurrentScope = new();
    private readonly CaseGenerationRuntime? _previous;
    private readonly CaseGenerationSkillRegistry _skills;
    private readonly DifficultyProfile _profile;
    private readonly SemaphoreSlim _concurrency;
    private readonly CancellationTokenSource _timeout;
    private readonly object _sync = new();
    private readonly System.Diagnostics.Stopwatch _active = System.Diagnostics.Stopwatch.StartNew();
    public CaseHarnessReport Report { get; }
    public CancellationToken CancellationToken => _timeout.Token;

    private CaseGenerationRuntime(GenerateCaseV2Request request, ILLMProvider provider,
        CaseGenerationSkillRegistry skills, CancellationToken ct, CaseHarnessReport? previous)
    {
        _previous = CurrentScope.Value;
        _skills = skills;
        _profile = DifficultyProfileCatalog.Get(request.Difficulty);
        var budget = GenerationCallBudget.For(_profile);
        var resources = DefinitionResources(AppContext.BaseDirectory);
        Report = new()
        {
            Provider = provider.GenerationIdentity,
            Skills = skills.Fingerprints,
            Resources = resources,
            Budget = budget,
            DefinitionHash = CaseGenerationSkillRegistry.Hash(JsonSerializer.Serialize(new
            {
                pipeline = Version, skills = skills.Fingerprints, budget, provider = provider.GenerationIdentity,
                code = typeof(CaseGenerationRuntime).Assembly.ManifestModule.ModuleVersionId,
                resources
            }))
        };
        if (previous is not null)
        {
            if (previous.DefinitionHash != Report.DefinitionHash || previous.Version != Report.Version)
                throw new CaseGenerationBudgetException("checkpoint_version_mismatch",
                    "Checkpoint skills, pipeline, provider or budget differ from the current deployment.");
            Report.Calls = previous.Calls;
            Report.InputTokens = previous.InputTokens;
            Report.OutputTokens = previous.OutputTokens;
            Report.EstimatedUsageCalls = previous.EstimatedUsageCalls;
            Report.ImageCalls = previous.ImageCalls;
            Report.ActiveMilliseconds = previous.ActiveMilliseconds;
            Report.Tasks = previous.Tasks.ToList();
            var remaining = budget.TotalTimeoutSeconds * 1000 - Report.ActiveMilliseconds;
            if (remaining <= 0)
                throw new CaseGenerationBudgetException("generation_timeout", "Case generation exhausted its active execution budget.");
        }
        if (Report.Calls < 0 || Report.InputTokens < 0 || Report.OutputTokens < 0 || Report.ActiveMilliseconds < 0
            || Report.Calls > budget.MaximumCalls || Report.InputTokens > budget.MaximumInputTokens
            || Report.OutputTokens > budget.MaximumOutputTokens || !double.IsFinite(Report.ActiveMilliseconds))
            throw new CaseGenerationBudgetException("checkpoint_budget_invalid", "Checkpoint usage counters are invalid.");
        _concurrency = new(budget.MaximumConcurrency);
        _timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _timeout.CancelAfter(TimeSpan.FromMilliseconds(budget.TotalTimeoutSeconds * 1000 - Report.ActiveMilliseconds));
        CurrentScope.Value = this;
    }

    internal static IReadOnlyDictionary<string, string> DefinitionResources(string root)
    {
        var schema = Path.Combine(root, "Schemas", "case.v2.schema.json");
        var prompts = Path.Combine(root, "agents", "case-v2");
        if (!File.Exists(schema) || !Directory.Exists(prompts))
            throw new TaskContractException("Harness identity requires the packaged public schema and task prompts.");
        var files = Directory.GetFiles(prompts, "*.md", SearchOption.AllDirectories).Append(schema)
            .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal).ToList();
        if (files.Count < 2)
            throw new TaskContractException("Harness identity has no packaged task prompts.");
        return files.ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'),
            path => CaseGenerationSkillRegistry.Hash(File.ReadAllText(path)), StringComparer.Ordinal);
    }

    public static CaseGenerationRuntime Begin(GenerateCaseV2Request request, ILLMProvider provider, CancellationToken ct,
        CaseGenerationSkillRegistry? skills = null, CaseHarnessReport? previous = null) => new(request, provider,
            skills ?? new(Path.Combine(AppContext.BaseDirectory, "Skills", "CaseV2")), ct, previous);

    public static string SystemPrompt(string task, string prompt) =>
        CurrentScope.Value is { } scope
            ? $"{prompt}\n\n{string.Join("\n\n", scope._skills.ForTask(task).Select(skill => skill.ToPrompt()))}\n" +
                $"Difficulty contract: {JsonSerializer.Serialize(scope._profile)}"
            : prompt;

    public static Task<LLMResponse> StructuredAsync(ILLMProvider provider, ILogger logger, string task,
        string system, string user, string schema, int attempt, CancellationToken ct) =>
        CurrentScope.Value is { } scope
            ? scope.RunStructuredAsync(provider, logger, task, system, user, schema, attempt, ct)
            : provider.GenerateStructuredResponseAsync(system, user, schema, ct);

    private async Task<LLMResponse> RunStructuredAsync(ILLMProvider provider, ILogger logger, string task,
        string system, string user, string schema, int attempt, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, CancellationToken);
        await _concurrency.WaitAsync(linked.Token);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var trace = new GenerationTaskTrace
        {
            Task = task, Attempt = attempt,
            Skills = _skills.ForTask(task).Select(skill => $"{skill.Id}@{skill.Version}:{skill.Hash}").ToList(),
            SchemaHash = CaseGenerationSkillRegistry.Hash(schema),
            SystemPromptHash = CaseGenerationSkillRegistry.Hash(system),
            UserPromptHash = CaseGenerationSkillRegistry.Hash(user)
        };
        try
        {
            ReserveCall(trace, Encoding.UTF8.GetByteCount(system + user + schema) + 4096);
            using var call = CancellationTokenSource.CreateLinkedTokenSource(ct, CancellationToken);
            call.CancelAfter(TimeSpan.FromSeconds(Report.Budget.TimeoutSeconds));
            LLMResponse response;
            try
            {
                response = await provider.GenerateStructuredResponseAsync(system, user, schema, trace.MaximumOutputTokens, call.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && !CancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Generation task {task} exceeded its {Report.Budget.TimeoutSeconds}s timeout.");
            }
            if (response.Usage is { } usage
                && (usage.PromptTokens < 0 || usage.CompletionTokens < 0 || usage.TotalTokens < 0
                    || (long)usage.PromptTokens + usage.CompletionTokens != usage.TotalTokens))
                throw new TaskContractException("Provider returned invalid token usage; the reserved upper bound remains charged.");
            var input = response.Usage?.PromptTokens ?? trace.InputTokens;
            var output = response.Usage?.CompletionTokens
                ?? Math.Min(trace.MaximumOutputTokens, Encoding.UTF8.GetByteCount(response.Content));
            if (response.Usage is null)
                logger.LogWarning("Task {Task} has no provider token usage; budget uses conservative UTF-8 byte upper bounds", task);
            lock (_sync)
            {
                Report.InputTokens += input - trace.InputTokens;
                Report.OutputTokens += output - trace.OutputTokens;
                trace.InputTokens = input;
                trace.OutputTokens = output;
                trace.UsageEstimated = response.Usage is null;
                if (!trace.UsageEstimated) Report.EstimatedUsageCalls--;
                if (output > trace.MaximumOutputTokens)
                    throw new CaseGenerationBudgetException("provider_output_limit_exceeded", "Provider exceeded the requested output-token ceiling.");
                if (Report.InputTokens > Report.Budget.MaximumInputTokens || Report.OutputTokens > Report.Budget.MaximumOutputTokens)
                    throw new CaseGenerationBudgetException("token_budget_exhausted", "Case generation exceeded its bounded token budget.");
            }
            return response;
        }
        catch (Exception exception) when (exception is CaseGenerationBudgetException or TaskContractException or JsonException
            or Azure.RequestFailedException or System.ClientModel.ClientResultException or HttpRequestException
            or TimeoutException or OperationCanceledException or InvalidOperationException or NotSupportedException)
        {
            trace.FailureCode = exception is CaseGenerationBudgetException budget ? budget.Code : exception.GetType().Name;
            throw;
        }
        finally
        {
            trace.LatencyMs = started.Elapsed.TotalMilliseconds;
            lock (_sync) Report.Tasks.Add(trace);
            _concurrency.Release();
        }
    }

    public static async Task<byte[]> ImageAsync(ILLMProvider provider, string prompt, CancellationToken ct)
    {
        if (CurrentScope.Value is not { } scope)
            return await provider.GenerateImageAsync(prompt, ct);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, scope.CancellationToken);
        await scope._concurrency.WaitAsync(linked.Token);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var trace = new GenerationTaskTrace
        {
            Task = "ImageRender", Kind = "image", Attempt = 1, UserPromptHash = CaseGenerationSkillRegistry.Hash(prompt)
        };
        try
        {
            scope.ReserveCall(trace, Encoding.UTF8.GetByteCount(prompt) + 4096, image: true);
            using var call = CancellationTokenSource.CreateLinkedTokenSource(ct, scope.CancellationToken);
            call.CancelAfter(TimeSpan.FromSeconds(scope.Report.Budget.TimeoutSeconds));
            try { return await provider.GenerateImageAsync(prompt, call.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && !scope.CancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Image generation exceeded its bounded timeout.");
            }
        }
        catch (Exception exception) when (exception is CaseGenerationBudgetException or TaskContractException or JsonException
            or Azure.RequestFailedException or System.ClientModel.ClientResultException or HttpRequestException
            or TimeoutException or OperationCanceledException or InvalidOperationException)
        {
            trace.FailureCode = exception is CaseGenerationBudgetException budget ? budget.Code : exception.GetType().Name;
            throw;
        }
        finally
        {
            trace.LatencyMs = started.Elapsed.TotalMilliseconds;
            lock (scope._sync) scope.Report.Tasks.Add(trace);
            scope._concurrency.Release();
        }
    }

    private void ReserveCall(GenerationTaskTrace trace, long input, bool image = false)
    {
        lock (_sync)
        {
            if (Report.Calls >= Report.Budget.MaximumCalls)
                throw new CaseGenerationBudgetException("call_budget_exhausted", "Case generation exceeded its bounded provider-call budget.");
            if (input > Report.Budget.MaximumInputTokens - Report.InputTokens
                || (!image && Report.OutputTokens >= Report.Budget.MaximumOutputTokens))
                throw new CaseGenerationBudgetException("token_budget_exhausted", "Case generation has no remaining token allowance.");
            trace.InputTokens = input;
            trace.MaximumOutputTokens = image ? 0 : (int)Math.Min(30000, Report.Budget.MaximumOutputTokens - Report.OutputTokens);
            trace.OutputTokens = trace.MaximumOutputTokens;
            trace.UsageEstimated = true;
            trace.ImageUnits = image ? 1 : 0;
            Report.Calls++;
            Report.InputTokens += input;
            Report.OutputTokens += trace.OutputTokens;
            Report.EstimatedUsageCalls++;
            if (image) Report.ImageCalls++;
        }
    }

    public void Dispose()
    {
        Report.ActiveMilliseconds += _active.Elapsed.TotalMilliseconds;
        CurrentScope.Value = _previous;
        _timeout.Dispose();
        _concurrency.Dispose();
    }
}
