using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace CaseGen.Functions.Tests.CaseV2;

public class DirectCaseGenerationIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public DirectCaseGenerationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DirectHarness_ResolvesExactApplicationServicesWithMockProvider()
    {
        var configuration = BuildConfiguration(useAzureFoundry: false);
        using var services = BuildServices(configuration);
        using var scope = services.CreateScope();

        Assert.IsType<UsageTrackingLLMProvider>(scope.ServiceProvider.GetRequiredService<ILLMProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICaseV2GeneratorService>());
        Assert.False(scope.ServiceProvider.GetRequiredService<ICaseV2BlobPublisher>().IsConfigured);
        Assert.True(CaseGraphFeatureOptions.From(configuration).Enabled);
    }

    [Fact]
    [Trait("Category", "RealGeneration")]
    public async Task DirectHarness_GeneratesAndValidatesRealCase_WhenEnvironmentEnabled()
    {
        if (Environment.GetEnvironmentVariable("CASEZERO_RUN_REAL_GENERATION") != "1")
            return;

        var configuration = BuildConfiguration(useAzureFoundry: true);
        Assert.True(configuration.GetValue("LLM:UseAzureFoundry", false));
        Assert.False(string.IsNullOrWhiteSpace(configuration["AzureFoundry:Endpoint"]));
        Assert.False(string.IsNullOrWhiteSpace(configuration["AzureFoundry:ModelName"]));
        Assert.False(string.IsNullOrWhiteSpace(configuration["AzureFoundry:ApiKey"]));

        using var services = BuildServices(configuration);
        using var scope = services.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<ICaseV2GeneratorService>();
        var difficulty = Environment.GetEnvironmentVariable("CASEZERO_DIFFICULTY") ?? "Rookie";
        var theme = Environment.GetEnvironmentVariable("CASEZERO_THEME") ?? "office theft";
        var language = Environment.GetEnvironmentVariable("CASEZERO_LANGUAGE") ?? "en-US";
        var caseId = Environment.GetEnvironmentVariable("CASEZERO_CASE_ID")
                     ?? $"case_direct_{difficulty.ToLowerInvariant()}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var seed = int.TryParse(Environment.GetEnvironmentVariable("CASEZERO_SEED"), out var parsedSeed)
            ? parsedSeed
            : 1;
        var reporter = new RecordingJobPhaseReporter(
            caseId,
            Environment.GetEnvironmentVariable("CASEZERO_PHASE_REPORT_FILE"));
        await reporter.ReportAttemptStartedAsync(1, 1);

        GenerateCaseV2Response response;
        try
        {
            response = await generator.GenerateAsync(new GenerateCaseV2Request
            {
                CaseId = caseId,
                Difficulty = difficulty,
                RequiredRank = difficulty,
                Theme = theme,
                Language = language,
                Seed = seed,
                WriteToDisk = true
            }, reporter);
            await reporter.ReportCompletedAsync();
        }
        catch (Exception exception)
        {
            await reporter.ReportFailedAsync(exception.Message);
            throw;
        }

        var result = new DirectGenerationResult
        {
            CaseId = response.CaseId,
            OutputPath = response.OutputPath,
            ValidationErrorCount = response.ValidationErrors.Count,
            FinalValidationPassed = response.FinalValidation?.IsValid == true,
            SolverSucceeded = response.Solver?.Correct == true,
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            PhaseEvents = reporter.PhaseEvents.ToList()
        };
        WriteResult(result);

        Assert.True(
            response.ValidationErrors.Count == 0,
            "Generation validation errors:" + Environment.NewLine
            + string.Join(Environment.NewLine, response.ValidationErrors));
        Assert.False(string.IsNullOrWhiteSpace(response.OutputPath));
        Assert.True(File.Exists(response.OutputPath));
        Assert.True(response.CaseGraphEnabled);
        Assert.True(response.FinalValidation?.IsValid);
        Assert.NotEmpty(reporter.PhaseEvents);

        var artifactReport = CaseV2ArtifactValidationHarness.ValidateDirectory(
            Path.GetDirectoryName(response.OutputPath)
            ?? throw new InvalidOperationException("Generated output directory is missing."));
        Assert.True(artifactReport.IsValid, $"Final artifact validation reported {artifactReport.Issues.Count} issue(s).");
        _output.WriteLine($"DIRECT_GENERATION_RESULT_PATH={response.OutputPath}");
    }

    private static IConfiguration BuildConfiguration(bool useAzureFoundry)
    {
        var root = RepositoryRoot();
        var functionsDirectory = Path.Combine(root, "functions", "CaseGen.Functions");
        return new ConfigurationBuilder()
            .SetBasePath(functionsDirectory)
            .AddCaseGenLocalSettings(functionsDirectory)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM:UseAzureFoundry"] = useAzureFoundry.ToString(),
                ["CaseGenV2:CaseGraphEnabled"] = "true",
                ["CaseGenV2:DisableBlobPublishing"] = "true",
                ["CaseGenV2:CasesBasePath"] = Path.Combine(root, "cases")
            })
            .Build();
    }

    private static ServiceProvider BuildServices(IConfiguration configuration)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton(configuration);
        collection.AddSingleton<IConfiguration>(configuration);
        collection.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });
        collection.AddCaseGenApplicationServices(configuration);
        return collection.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static void WriteResult(DirectGenerationResult result)
    {
        var path = Environment.GetEnvironmentVariable("CASEZERO_DIRECT_RESULT_FILE");
        if (string.IsNullOrWhiteSpace(path))
            return;
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if ((Directory.Exists(gitPath) || File.Exists(gitPath))
                && Directory.Exists(Path.Combine(current.FullName, "functions")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class DirectGenerationResult
    {
        public string CaseId { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public int ValidationErrorCount { get; set; }
        public bool FinalValidationPassed { get; set; }
        public bool SolverSucceeded { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public List<string> PhaseEvents { get; set; } = new();
    }

    private sealed class RecordingJobPhaseReporter : IJobPhaseReporter
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
        private readonly string? _path;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private JobPhaseStatus _status;

        public RecordingJobPhaseReporter(string jobId, string? path)
        {
            _path = path;
            _status = JobPhaseStatus.Started(jobId, DateTimeOffset.UtcNow);
        }

        public bool IsConfigured => true;
        public List<string> PhaseEvents { get; } = new();

        public Task ReportStartedAsync(CancellationToken ct = default) =>
            ReportAttemptStartedAsync(1, 1, ct);

        public async Task ReportAttemptStartedAsync(int attempt, int maxAttempts, CancellationToken ct = default)
        {
            _status.StartAttempt(attempt, maxAttempts, DateTimeOffset.UtcNow);
            PhaseEvents.Add($"attempt:{attempt}:started");
            await WriteAsync(ct);
        }

        public async Task ReportPhaseAsync(string phase, CancellationToken ct = default)
        {
            _status.StartStage(phase, DateTimeOffset.UtcNow, retry: false);
            PhaseEvents.Add($"phase:{phase}");
            await WriteAsync(ct);
        }

        public async Task ReportRetryScheduledAsync(string error, DateTimeOffset retryAt, CancellationToken ct = default)
        {
            _status.ScheduleRetry(error, retryAt, DateTimeOffset.UtcNow);
            PhaseEvents.Add($"attempt:{_status.CurrentAttempt}:retry");
            await WriteAsync(ct);
        }

        public async Task ReportCompletedAsync(CancellationToken ct = default)
        {
            _status.Complete(DateTimeOffset.UtcNow);
            PhaseEvents.Add($"attempt:{_status.CurrentAttempt}:completed");
            await WriteAsync(ct);
        }

        public async Task ReportFailedAsync(string error, CancellationToken ct = default)
        {
            _status.Fail(error, DateTimeOffset.UtcNow);
            PhaseEvents.Add($"attempt:{_status.CurrentAttempt}:failed");
            await WriteAsync(ct);
        }

        public Task<JobPhaseStatus?> GetAsync(CancellationToken ct = default) =>
            Task.FromResult<JobPhaseStatus?>(_status);

        private async Task WriteAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_path))
                return;
            await _lock.WaitAsync(ct);
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(
                    _path,
                    JsonSerializer.Serialize(new { status = _status, events = PhaseEvents }, Options),
                    ct);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
