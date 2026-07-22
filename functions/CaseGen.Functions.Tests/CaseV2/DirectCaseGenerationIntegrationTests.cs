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

        var response = await generator.GenerateAsync(new GenerateCaseV2Request
        {
            CaseId = caseId,
            Difficulty = difficulty,
            RequiredRank = difficulty,
            Theme = theme,
            Language = language,
            Seed = seed,
            WriteToDisk = true
        });

        var result = new DirectGenerationResult
        {
            CaseId = response.CaseId,
            OutputPath = response.OutputPath,
            ValidationErrorCount = response.ValidationErrors.Count,
            FinalValidationPassed = response.FinalValidation?.IsValid == true,
            SolverSucceeded = response.Solver?.Correct == true,
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens
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
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
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
    }
}
