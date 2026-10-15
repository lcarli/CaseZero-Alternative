using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class GenerationHarnessTests
{
    [Fact]
    public async Task EveryVersionedSkillIsInjectedIntoItsExecutedTask()
    {
        var provider = new RecordingProvider();
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        foreach (var task in new[] { "CaseBible", "Timeline", "AssetPlan", "ForensicsPlan", "AssetCard", "BlindPlayerInvestigator" })
            await TaskRunner.RunStructuredAsync<EmptyResult>(provider, NullLogger.Instance, task, "task system", "public context",
                """{"type":"object","additionalProperties":false}""", default, maxRetries: 0);
        foreach (var id in CaseGenerationSkillRegistry.RequiredIds)
            Assert.Contains(provider.Systems, prompt => prompt.Contains($"Runtime skill: {id}@1.0.0", StringComparison.Ordinal));
        Assert.Equal(6, runtime.Report.Tasks.Count);
        Assert.All(runtime.Report.Tasks, trace =>
        {
            Assert.Equal(64, trace.SchemaHash.Length);
            Assert.Equal(64, trace.SystemPromptHash.Length);
            Assert.Equal(64, trace.UserPromptHash.Length);
        });
    }

    [Fact]
    public void MissingSkillFailsExplicitly()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-skills-{Guid.NewGuid():N}");
        var exception = Assert.Throws<TaskContractException>(() => new CaseGenerationSkillRegistry(missing));
        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderCallBudgetIsAnExactUpperBound()
    {
        var provider = new RecordingProvider();
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        for (var call = 0; call < runtime.Report.Budget.MaximumCalls; call++)
            await CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "AssetCard", "system", "user", "{}", 1, default);
        var error = await Assert.ThrowsAsync<CaseGenerationBudgetException>(() =>
            CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "AssetCard", "system", "user", "{}", 1, default));
        Assert.Equal("call_budget_exhausted", error.Code);
        Assert.Equal(runtime.Report.Budget.MaximumCalls, provider.Calls);
    }

    [Fact]
    public async Task UnknownUsageIsConservativelyAccountedAndExplicit()
    {
        var provider = new RecordingProvider { ReturnUsage = false };
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        await CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "AssetCard", "system", "user", "{}", 1, default);
        Assert.Equal(1, runtime.Report.EstimatedUsageCalls);
        Assert.True(Assert.Single(runtime.Report.Tasks).UsageEstimated);
        Assert.Equal(4108, runtime.Report.InputTokens);
        Assert.Equal(2, runtime.Report.OutputTokens);
    }

    [Fact]
    public void ResumeRejectsDifferentSkillOrPipelineVersions()
    {
        var provider = new RecordingProvider();
        using var first = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        var previous = first.Report;
        previous.DefinitionHash = new string('0', 64);
        var error = Assert.Throws<CaseGenerationBudgetException>(() =>
            CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default, previous: previous));
        Assert.Equal("checkpoint_version_mismatch", error.Code);
    }

    [Fact]
    public void ResumeRejectsAnotherModelEvenWhenProviderTypeIsUnchanged()
    {
        var provider = new RecordingProvider();
        using var first = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        provider.GenerationIdentity = "synthetic-provider-model-2";
        var error = Assert.Throws<CaseGenerationBudgetException>(() =>
            CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default, previous: first.Report));
        Assert.Equal("checkpoint_version_mismatch", error.Code);
    }

    [Fact]
    public void DefinitionIncludesTheActualPublicSchemaAndEveryPackagedPrompt()
    {
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, new RecordingProvider(), default);
        Assert.Equal(CaseGenerationSkillRegistry.Hash(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"))),
            runtime.Report.Resources["Schemas/case.v2.schema.json"]);
        var prompts = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "agents", "case-v2"), "*.md", SearchOption.AllDirectories);
        Assert.NotEmpty(prompts);
        foreach (var prompt in prompts)
            Assert.Equal(CaseGenerationSkillRegistry.Hash(File.ReadAllText(prompt)),
                runtime.Report.Resources[Path.GetRelativePath(AppContext.BaseDirectory, prompt).Replace('\\', '/')]);
    }

    [Fact]
    public async Task FailureRetainsConservativeUsageAndItsTraceAcrossConfirmedResume()
    {
        var provider = new RecordingProvider { Failure = new HttpRequestException("Synthetic lost response") };
        CaseHarnessReport previous;
        using (var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "AssetCard", "system", "user", "{}", 1, default));
            var trace = Assert.Single(runtime.Report.Tasks);
            Assert.True(trace.UsageEstimated);
            Assert.Equal(30000, trace.OutputTokens);
            Assert.Equal(nameof(HttpRequestException), trace.FailureCode);
            previous = runtime.Report;
        }
        using var resumed = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default, previous: previous);
        Assert.Equal(1, resumed.Report.Calls);
        Assert.Equal(30000, resumed.Report.OutputTokens);
        Assert.Equal(previous.InputTokens, resumed.Report.InputTokens);
    }

    [Theory]
    [InlineData(-1, 1, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 1, -1)]
    [InlineData(1, 1, 100)]
    public async Task InvalidProviderUsageCannotRefundTheBudget(int input, int output, int total)
    {
        var provider = new RecordingProvider
        {
            UsageOverride = new() { PromptTokens = input, CompletionTokens = output, TotalTokens = total }
        };
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        await Assert.ThrowsAsync<TaskContractException>(() =>
            CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "AssetCard", "system", "user", "{}", 1, default));
        Assert.Equal(1, runtime.Report.Calls);
        Assert.Equal(30000, runtime.Report.OutputTokens);
        Assert.Equal(4108, runtime.Report.InputTokens);
        Assert.True(Assert.Single(runtime.Report.Tasks).UsageEstimated);
    }

    [Fact]
    public async Task RequestedOutputCeilingIsTheExactRemainingAllowance()
    {
        var provider = new RecordingProvider();
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        runtime.Report.OutputTokens = runtime.Report.Budget.MaximumOutputTokens - 7;
        await CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "AssetCard", "system", "user", "{}", 1, default);
        Assert.Equal(7, provider.LastOutputLimit);
        Assert.Equal(runtime.Report.Budget.MaximumOutputTokens - 6, runtime.Report.OutputTokens);
        Assert.Equal(7, Assert.Single(runtime.Report.Tasks).MaximumOutputTokens);
    }

    [Fact]
    public async Task ExhaustedInputAllowancePreventsAnotherProviderCall()
    {
        var provider = new RecordingProvider();
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        runtime.Report.InputTokens = runtime.Report.Budget.MaximumInputTokens;
        var error = await Assert.ThrowsAsync<CaseGenerationBudgetException>(() =>
            CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "AssetCard", "system", "user", "{}", 1, default));
        Assert.Equal("token_budget_exhausted", error.Code);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImagesAlwaysRetainExplicitUnitsAndFailureTraces(bool fail)
    {
        var provider = new RecordingProvider { Failure = fail ? new HttpRequestException("Synthetic image failure") : null };
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        if (fail)
            await Assert.ThrowsAsync<HttpRequestException>(() => CaseGenerationRuntime.ImageAsync(provider, "authored image", default));
        else
            Assert.NotEmpty(await CaseGenerationRuntime.ImageAsync(provider, "authored image", default));
        var trace = Assert.Single(runtime.Report.Tasks);
        Assert.Equal("image", trace.Kind);
        Assert.Equal(1, trace.ImageUnits);
        Assert.Equal(1, runtime.Report.ImageCalls);
        Assert.True(trace.UsageEstimated);
        Assert.Equal(64, trace.UserPromptHash.Length);
        Assert.Equal(fail ? nameof(HttpRequestException) : null, trace.FailureCode);
    }

    [Fact]
    public void PrivateCheckpointPreservesCanonicalPreparationFlags()
    {
        var checkpoint = CaseGenerationCheckpoint.Create(new() { CaseId = "case_private", Difficulty = "Rookie" });
        checkpoint.Draft.AssetFull.Add(new()
        {
            Id = "asset.record", Type = "document", Title = "Record", CanonicalContent = true, PreparedForReview = true
        });
        checkpoint.CapturePreparation();
        var replay = JsonSerializer.Deserialize<CaseGenerationCheckpoint>(
            JsonSerializer.Serialize(checkpoint, CaseDraft.CanonicalJsonOptions), CaseDraft.CanonicalJsonOptions)!;
        replay.RestorePreparation();
        Assert.True(Assert.Single(replay.Draft.AssetFull).CanonicalContent);
        Assert.True(Assert.Single(replay.Draft.AssetFull).PreparedForReview);
    }

    [Fact]
    public void InvalidRawRequestNeverReachesAStage()
    {
        Assert.Throws<GenerationRequestException>(() => CaseGenerationCheckpoint.Create(new() { Difficulty = "InvalidRank" }));
    }

    [Fact]
    public void DifficultyDoesNotBorrowTopologyFromRequiredRank()
    {
        Assert.False(DifficultyProfileCatalog.Get(new CaseDraft
        {
            Metadata = new() { Difficulty = "Detective", RequiredRank = "Rookie" }
        }).AllEvidenceInitial);
        Assert.True(DifficultyProfileCatalog.Get(new CaseDraft
        {
            Metadata = new() { Difficulty = "Rookie", RequiredRank = "Captain" }
        }).AllEvidenceInitial);
    }

    private sealed class EmptyResult;

    private sealed class RecordingProvider : ILLMProvider
    {
        public List<string> Systems { get; } = [];
        public int Calls { get; private set; }
        public bool ReturnUsage { get; set; } = true;
        public string GenerationIdentity { get; set; } = "synthetic-provider-model-1";
        public int? LastOutputLimit { get; private set; }
        public LLMUsage? UsageOverride { get; set; }
        public Exception? Failure { get; set; }
        public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema,
            int maximumOutputTokens, CancellationToken cancellationToken = default)
        {
            LastOutputLimit = maximumOutputTokens;
            return GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
        }
        public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Systems.Add(systemPrompt);
            if (Failure is not null) throw Failure;
            return Task.FromResult(new LLMResponse
            {
                Content = "{}",
                Usage = ReturnUsage ? UsageOverride ?? new LLMUsage { PromptTokens = 1, CompletionTokens = 1, TotalTokens = 2 } : null
            });
        }
        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (Failure is not null) throw Failure;
            return Task.FromResult<byte[]>([1, 2, 3]);
        }
        public Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
