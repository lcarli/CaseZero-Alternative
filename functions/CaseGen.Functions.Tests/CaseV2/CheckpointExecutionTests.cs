using System.Text.Json;
using CaseGen.Functions.Functions.CaseV2;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CheckpointExecutionTests
{
    [Theory]
    [InlineData("another-job", 1, null)]
    [InlineData("casev2-bound", 0, null)]
    [InlineData("casev2-bound", 2, null)]
    [InlineData("casev2-bound", 3, "casev2-bound")]
    [InlineData("casev2-bound", 2, "casev2-other")]
    public async Task InvalidStageBindingIsRejectedBeforeStorageOrGeneration(string job, int stage, string? previousJob)
    {
        var request = Request();
        var previous = previousJob is null ? null : Reference(previousJob, CaseGenerationStage.Planning, request);
        var store = new MemoryStore();
        var generator = new StageGenerator();
        using var context = new StartCaseV2GenerationTests.TestContext(default);
        await Assert.ThrowsAsync<TaskContractException>(() => Activity(generator, store)
            .RunAsync(new(job, request, (CaseGenerationStage)stage, previous), context));
        Assert.Equal(0, store.Reads);
        Assert.Equal(0, store.Finds);
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task PreviousReferenceFromAnotherRequestIsRejectedBeforeAnyRead()
    {
        var request = Request();
        var different = Request();
        different.Theme = "A different frozen request";
        var store = new MemoryStore();
        using var context = new StartCaseV2GenerationTests.TestContext(default);
        await Assert.ThrowsAsync<TaskContractException>(() => Activity(new(), store)
            .RunAsync(new("casev2-bound", request, CaseGenerationStage.Authorship,
                Reference("casev2-bound", CaseGenerationStage.Planning, different)), context));
        Assert.Equal(0, store.Reads);
        Assert.Equal(0, store.Finds);
    }

    [Fact]
    public async Task ConfirmedStageReplaysWithoutCallingTheGeneratorOrRestoringFiles()
    {
        var store = new MemoryStore();
        var generator = new StageGenerator();
        var input = new CaseStageInput("casev2-bound", Request(), CaseGenerationStage.Planning);
        using var context = new StartCaseV2GenerationTests.TestContext(default);
        var first = await Activity(generator, store).RunAsync(input, context);
        var second = await Activity(generator, store).RunAsync(input, context);
        Assert.Equal(first.Reference, second.Reference);
        Assert.Equal(1, generator.Calls);
        Assert.Equal(1, store.Begins);
        Assert.False(store.LastRestoreAssets);
    }

    [Fact]
    public async Task LostActivityResponseAfterCheckpointDoesNotRepeatTheStage()
    {
        var store = new MemoryStore { LoseSaveResponse = true };
        var generator = new StageGenerator();
        var input = new CaseStageInput("casev2-bound", Request(), CaseGenerationStage.Planning);
        using var context = new StartCaseV2GenerationTests.TestContext(default);
        await Assert.ThrowsAsync<HttpRequestException>(() => Activity(generator, store).RunAsync(input, context));
        var replay = await Activity(generator, store).RunAsync(input, context);
        Assert.Equal(CaseGenerationStage.Planning, replay.Reference.Stage);
        Assert.Equal(1, generator.Calls);
    }

    [Fact]
    public async Task UnconfirmedExecutionNeverRunsAgainAndRetainsTheFailureReport()
    {
        var store = new MemoryStore();
        var generator = new StageGenerator { Failure = new HttpRequestException("Paid response lost") };
        var input = new CaseStageInput("casev2-bound", Request(), CaseGenerationStage.Planning);
        using var context = new StartCaseV2GenerationTests.TestContext(default);
        await Assert.ThrowsAsync<HttpRequestException>(() => Activity(generator, store).RunAsync(input, context));
        var error = await Assert.ThrowsAsync<TaskContractException>(() => Activity(generator, store).RunAsync(input, context));
        Assert.Contains("unconfirmed", error.Message);
        Assert.Equal(1, generator.Calls);
        Assert.Equal("Paid response lost", Assert.Single(store.Failures));
    }

    [Fact]
    public async Task RestoredPayloadCannotSilentlyReplaceTheFrozenRequest()
    {
        var store = new MemoryStore();
        var request = Request();
        var checkpoint = CaseGenerationCheckpoint.Create(request);
        checkpoint.JobId = "casev2-bound";
        checkpoint.CompletedStage = CaseGenerationStage.Planning;
        checkpoint.Draft.Request.Theme = "Changed after the reference was frozen";
        store.Checkpoints[CaseGenerationStage.Planning] = checkpoint;
        using var context = new StartCaseV2GenerationTests.TestContext(default);
        var generator = new StageGenerator();
        await Assert.ThrowsAsync<TaskContractException>(() => Activity(generator, store).RunAsync(
            new("casev2-bound", Request(), CaseGenerationStage.Authorship,
                Reference("casev2-bound", CaseGenerationStage.Planning, Request())), context));
        Assert.Equal(0, generator.Calls);
        Assert.Equal(0, store.Begins);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedStageWorkspaceIsCleanedOnSuccessAndRequestedCancellation(bool cancel)
    {
        var root = Path.Combine(Path.GetTempPath(), "casegen-checkpoint", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "case_stage");
        var generator = new StageGenerator { Workspace = directory };
        using var source = new CancellationTokenSource();
        generator.OnStage = cancel ? source.Cancel : null;
        using var context = new StartCaseV2GenerationTests.TestContext(source.Token);
        try
        {
            var run = Activity(generator, new()).RunAsync(new("casev2-bound", Request(), CaseGenerationStage.Planning), context);
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            else await run;
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static GenerateCaseV2Request Request() => new()
    {
        CaseId = "case_stage", Difficulty = "Rookie", Language = "pt-BR", WriteToDisk = false
    };

    private static CaseCheckpointReference Reference(string job, CaseGenerationStage stage, GenerateCaseV2Request request) =>
        new(job, stage, $"{job}/checkpoints/{(int)stage}-{stage}.json", new('a', 64), new('b', 64),
            CaseGenerationCheckpoint.Fingerprint(request));

    private static CaseV2GenerateStageActivity Activity(StageGenerator generator, MemoryStore store) =>
        new(generator, store, new NullReporterFactory(), NullLogger<CaseV2GenerateStageActivity>.Instance);

    private sealed class NullReporterFactory : IJobPhaseReporterFactory, IJobPhaseReporter
    {
        public IJobPhaseReporter Create(string jobId) => this;
        public bool IsConfigured => false;
        public Task ReportStartedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ReportAttemptStartedAsync(int attempt, int maxAttempts, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReportPhaseAsync(string phase, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReportRetryScheduledAsync(string error, DateTimeOffset retryAt, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReportCompletedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ReportFailedAsync(string error, CancellationToken ct = default) => Task.CompletedTask;
        public Task<JobPhaseStatus?> GetAsync(CancellationToken ct = default) => Task.FromResult<JobPhaseStatus?>(null);
    }

    private sealed class StageGenerator : ICaseV2GeneratorService
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; set; }
        public string? Workspace { get; set; }
        public Action? OnStage { get; set; }
        public Task<CaseGenerationCheckpoint> GenerateStageAsync(CaseGenerationCheckpoint checkpoint, CaseGenerationStage stage,
            IJobPhaseReporter reporter, CancellationToken ct = default)
        {
            Calls++;
            if (Workspace is not null)
            {
                Directory.CreateDirectory(Workspace);
                checkpoint.StagedCaseDirectory = Workspace;
            }
            OnStage?.Invoke();
            ct.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            checkpoint.CompletedStage = stage;
            checkpoint.Harness = new() { DefinitionHash = new('b', 64), Calls = 1 };
            return Task.FromResult(checkpoint);
        }
        public Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<GenerateCaseV2Response> GenerateAsync(GenerateCaseV2Request request, IJobPhaseReporter reporter, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class MemoryStore : ICaseGenerationCheckpointStore
    {
        public Dictionary<CaseGenerationStage, CaseGenerationCheckpoint> Checkpoints { get; } = [];
        private readonly HashSet<CaseGenerationStage> _started = [];
        public List<string> Failures { get; } = [];
        public int Reads { get; private set; }
        public int Finds { get; private set; }
        public int Begins { get; private set; }
        public bool LastRestoreAssets { get; private set; }
        public bool LoseSaveResponse { get; set; }

        public Task<CaseCheckpointReference?> FindAsync(string jobId, CaseGenerationStage stage, CancellationToken ct)
        {
            Finds++;
            return Task.FromResult(Checkpoints.TryGetValue(stage, out var checkpoint)
                ? Reference(jobId, stage, checkpoint.Draft.Request) : null);
        }
        public Task<CaseGenerationCheckpoint> ReadAsync(CaseCheckpointReference reference, CancellationToken ct, bool restoreAssets = true)
        {
            Reads++;
            LastRestoreAssets = restoreAssets;
            return Task.FromResult(Copy(Checkpoints[reference.Stage]));
        }
        public Task<CaseCheckpointReference> SaveAsync(string jobId, CaseGenerationCheckpoint checkpoint, CancellationToken ct)
        {
            var stage = checkpoint.CompletedStage!.Value;
            Checkpoints[stage] = Copy(checkpoint);
            if (LoseSaveResponse)
            {
                LoseSaveResponse = false;
                throw new HttpRequestException("Saved checkpoint, response lost");
            }
            return Task.FromResult(Reference(jobId, stage, checkpoint.Draft.Request));
        }
        public Task<bool> TryBeginStageAsync(CaseStageInput input, CaseGenerationCheckpoint checkpoint, CancellationToken ct)
        {
            Begins++;
            return Task.FromResult(_started.Add(input.Stage));
        }
        public Task RecordFailureAsync(CaseStageInput input, CaseGenerationCheckpoint checkpoint, string failure, CancellationToken ct)
        {
            Failures.Add(failure);
            return Task.CompletedTask;
        }
        private static CaseGenerationCheckpoint Copy(CaseGenerationCheckpoint checkpoint) =>
            JsonSerializer.Deserialize<CaseGenerationCheckpoint>(JsonSerializer.Serialize(checkpoint, CaseDraft.CanonicalJsonOptions),
                CaseDraft.CanonicalJsonOptions)!;
    }
}
