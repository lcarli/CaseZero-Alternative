using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;
using CaseGen.Functions.Services;
using CaseGen.Functions.Models;

namespace CaseGen.Functions.Tests;

public class CaseGenerationServiceTests
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IServiceProvider _serviceProvider;

    public CaseGenerationServiceTests()
    {
        // Ensure storage connection is configured via environment for local runs (Azurite)
        Environment.SetEnvironmentVariable("AzureWebJobsStorage", Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true");

        var services = new ServiceCollection();
        
        // Build configuration from environment only (Functions host also uses env at runtime)
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<ICaseGenerationService, CaseGenerationService>();
    services.AddScoped<IStorageService, StorageService>();
    services.AddScoped<ILLMService, LLMService>();

        _serviceProvider = services.BuildServiceProvider();
        _caseGenerationService = _serviceProvider.GetRequiredService<ICaseGenerationService>();
    }

    // No fakes; tests will write to Azurite when running locally via UseDevelopmentStorage=true

    [Fact]
    public async Task PlanCaseAsync_ShouldReturnValidJson()
    {
        // Arrange
        var request = new CaseGenerationRequest
        {
            Title = "Test Case",
            Location = "Test Location",
            Difficulty = "Iniciante",
            TargetDurationMinutes = 60,
            GenerateImages = true,
            Constraints = Array.Empty<string>(),
            Timezone = "America/Sao_Paulo"
        };

        // Act
        var result = await _caseGenerationService.PlanCaseAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("CASE-", result); // Should contain a case ID
    }

    [Fact]
    public async Task ExpandCaseAsync_ShouldReturnValidJson()
    {
        // Arrange
        var planJson = """{"caseId": "TEST-123", "title": "Test Case"}""";

        // Act
        var result = await _caseGenerationService.ExpandCaseAsync(planJson);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GenerateDocumentsAsync_ShouldReturnMultipleDocuments()
    {
        // Arrange
        var designJson = """{"caseId": "TEST-123", "title": "Test Case"}""";

        // Act
        var result = await _caseGenerationService.GenerateDocumentsAsync(designJson);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void CaseGenerationSteps_ShouldContainAllExpectedSteps()
    {
        // Arrange & Act
        var steps = CaseGenerationSteps.AllSteps;

        // Assert
        Assert.Equal(10, steps.Length);
        Assert.Contains(CaseGenerationSteps.Plan, steps);
        Assert.Contains(CaseGenerationSteps.Package, steps);
    }
}