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
        services.AddScoped<ILLMProvider, MockLLMProvider>();
        services.AddScoped<ISchemaValidationService, SchemaValidationService>();
        services.AddScoped<IJsonSchemaProvider, FileJsonSchemaProvider>();
        services.AddScoped<ICaseLoggingService, CaseLoggingService>();
        services.AddScoped<INormalizerService, NormalizerService>();
        services.AddScoped<IPdfRenderingService, PdfRenderingService>();

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
        var result = await _caseGenerationService.PlanCaseAsync(request, "TEST-CASE-001");

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
        var result = await _caseGenerationService.ExpandCaseAsync(planJson, "TEST-CASE-002");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task DesignCaseAsync_ShouldReturnValidJson()
    {
        // Arrange
        var expandedJson = """{"caseId": "TEST-123", "title": "Test Case"}""";

        // Act
        var result = await _caseGenerationService.DesignCaseAsync(expandedJson, "TEST-CASE-003");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void CaseGenerationSteps_ShouldContainAllExpectedSteps()
    {
        // Arrange & Act
        var steps = CaseGenerationSteps.AllSteps;

        // Assert
        Assert.Equal(12, steps.Length); // Updated from 10 to 12 due to new RenderImages step
        Assert.Contains(CaseGenerationSteps.Plan, steps);
        Assert.Contains(CaseGenerationSteps.RenderImages, steps); // Verify new step is included
        Assert.Contains(CaseGenerationSteps.Package, steps);
    }

    [Fact]
    public async Task RenderDocumentFromJsonAsync_ShouldGenerateMarkdownAndPdf()
    {
        // Arrange
        var testDocumentJson = """
        {
            "docId": "test_police_001",
            "type": "police_report",
            "title": "Test Police Report",
            "words": 120,
            "sections": [
                {
                    "title": "Summary",
                    "content": "This is a test police report summary for rendering validation."
                },
                {
                    "title": "Evidence",
                    "content": "Test evidence description with sample content."
                }
            ]
        }
        """;
        var caseId = "TEST-CASE-001";
        var docId = "test_police_001";

        // Act
        var result = await _caseGenerationService.RenderDocumentFromJsonAsync(docId, testDocumentJson, caseId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        // Verify the result contains expected information
        var resultObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result);
        Assert.True(resultObj.TryGetProperty("docId", out var resultDocId));
        Assert.Equal("test_police_001", resultDocId.GetString());
        Assert.True(resultObj.TryGetProperty("status", out var status));
        Assert.Equal("rendered", status.GetString());
        Assert.True(resultObj.TryGetProperty("files", out var files));
        Assert.True(files.TryGetProperty("markdown", out var mdPath));
        Assert.True(files.TryGetProperty("pdf", out var pdfPath));
        Assert.Contains(".md", mdPath.GetString());
        Assert.Contains(".pdf", pdfPath.GetString());
    }

    [Fact]
    public async Task RenderDocumentFromJsonAsync_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = """{"incomplete": "json"}""";
        var caseId = "TEST-CASE-002";
        var docId = "invalid_doc";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _caseGenerationService.RenderDocumentFromJsonAsync(docId, invalidJson, caseId));
        
        Assert.Contains("Invalid document JSON structure", exception.Message);
    }
}