using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;

namespace CaseGen.Functions.Tests;

public class NormalizerServiceTests
{
    private readonly NormalizerService _normalizerService;
    private readonly IServiceProvider _serviceProvider;

    public NormalizerServiceTests()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SCHEMAS_BASE_PATH"] = "Schemas/v1"
            })
            .Build();

        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddScoped<IJsonSchemaProvider, FileJsonSchemaProvider>();
        services.AddScoped<ISchemaValidationService, SchemaValidationService>();
        services.AddScoped<ICaseLoggingService, TestCaseLoggingService>();
        services.AddScoped<INormalizerService, NormalizerService>();

        _serviceProvider = services.BuildServiceProvider();
        _normalizerService = (NormalizerService)_serviceProvider.GetRequiredService<INormalizerService>();
    }

    [Fact]
    public async Task NormalizeCaseAsync_WithValidInput_ShouldReturnNormalizedResult()
    {
        // Arrange
        var caseId = "CASE-20241201-abc12345";
        var documents = new[]
        {
            JsonSerializer.Serialize(new
            {
                docId = "doc_police_001",
                type = "police_report",
                title = "Relatório Policial Inicial",
                sections = new[] { "Ocorrência", "Detalhes", "Conclusão" },
                lengthTarget = new[] { 200, 500 },
                gated = false,
                content = "Conteúdo do relatório policial"
            })
        };

        var media = new[]
        {
            JsonSerializer.Serialize(new
            {
                evidenceId = "ev_photo_001",
                kind = "photo",
                title = "Foto da Cena do Crime",
                prompt = "Fotografia da cena do crime mostrando...",
                constraints = new { iluminacao = "natural", angulo = "frontal" },
                deferred = false
            })
        };

        var input = new NormalizationInput
        {
            CaseId = caseId,
            Difficulty = "Detective",
            Timezone = "America/Sao_Paulo",
            PlanJson = "{}",
            ExpandedJson = "{}",
            DesignJson = "{}",
            Documents = documents,
            Media = media
        };

        // Act
        var result = await _normalizerService.NormalizeCaseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.NormalizedJson);
        Assert.NotNull(result.Manifest);
        Assert.NotNull(result.Log);

        // Verify normalized bundle structure
        Assert.Equal(caseId, result.NormalizedJson.CaseId);
        Assert.Equal("Detective", result.NormalizedJson.Difficulty);
        Assert.Equal("America/Sao_Paulo", result.NormalizedJson.Timezone);
        Assert.Single(result.NormalizedJson.Documents);
        Assert.Single(result.NormalizedJson.Media);

        // Verify document normalization
        var normalizedDoc = result.NormalizedJson.Documents[0];
        Assert.Equal("doc_police_001", normalizedDoc.DocId);
        Assert.Equal("police_report", normalizedDoc.Type);
        Assert.False(normalizedDoc.Gated);
        Assert.NotNull(normalizedDoc.Title);

        // Verify media normalization
        var normalizedMedia = result.NormalizedJson.Media[0];
        Assert.Equal("ev_photo_001", normalizedMedia.EvidenceId);
        Assert.Equal("photo", normalizedMedia.Kind);
        Assert.False(normalizedMedia.Deferred);

        // Verify gating graph
        Assert.NotNull(result.NormalizedJson.GatingGraph);
        Assert.False(result.NormalizedJson.GatingGraph.HasCycles);

        // Verify manifest
        Assert.Equal(caseId, result.Manifest.CaseId);
        Assert.Single(result.Manifest.Documents);
        Assert.Single(result.Manifest.Media);

        // Verify log
        Assert.Equal("normalize", result.Log.Step);
        Assert.True(result.Log.Entries.Any());
        Assert.True(result.Log.ValidationResults.Any());
    }

    [Fact]
    public async Task NormalizeCaseAsync_WithGatedDocument_ShouldValidateGatingRules()
    {
        // Arrange
        var caseId = "CASE-20241201-def67890";
        var documents = new[]
        {
            JsonSerializer.Serialize(new
            {
                docId = "doc_forensics_001",
                type = "forensics_report",
                title = "Laudo Pericial",
                sections = new[] { "Análise", "Cadeia de Custódia", "Conclusão" },
                lengthTarget = new[] { 300, 800 },
                gated = true,
                gatingRule = new
                {
                    action = "submit_evidence",
                    evidenceId = "ev_sample_001",
                    notes = "Enviar amostra para análise"
                },
                content = "Conteúdo do laudo pericial"
            })
        };

        var media = new[]
        {
            JsonSerializer.Serialize(new
            {
                evidenceId = "ev_sample_001",
                kind = "photo",
                title = "Amostra Coletada",
                prompt = "Fotografia da amostra coletada",
                constraints = new { tipo = "macro", resolucao = "alta" },
                deferred = false
            })
        };

        var input = new NormalizationInput
        {
            CaseId = caseId,
            Difficulty = "Detective",
            Timezone = "UTC",
            Documents = documents,
            Media = media
        };

        // Act
        var result = await _normalizerService.NormalizeCaseAsync(input);

        // Assert
        Assert.NotNull(result);
        
        // Verify gated document
        var gatedDoc = result.NormalizedJson.Documents[0];
        Assert.True(gatedDoc.Gated);
        Assert.NotNull(gatedDoc.GatingRule);
        Assert.Equal("submit_evidence", gatedDoc.GatingRule.Action);
        Assert.Equal("ev_sample_001", gatedDoc.GatingRule.EvidenceId);

        // Verify gating graph includes relationship
        var gatingGraph = result.NormalizedJson.GatingGraph;
        Assert.Contains(gatingGraph.Edges, e => 
            e.From == "ev_sample_001" && 
            e.To == "doc_forensics_001" && 
            e.Relationship == "unlocks");

        // Verify forensics report has custody chain
        Assert.Contains(gatedDoc.Sections, s => 
            s.ToLowerInvariant().Contains("cadeia") && 
            s.ToLowerInvariant().Contains("custódia"));

        // Verify validation passed
        Assert.Contains(result.Log.ValidationResults, v => 
            v.Rule == "GATING_REFERENCE_INTEGRITY" && v.Status == "PASS");
        Assert.Contains(result.Log.ValidationResults, v => 
            v.Rule == "FORENSICS_CUSTODY_CHAIN" && v.Status == "PASS");
    }

    [Fact]
    public async Task NormalizeCaseAsync_WithInvalidGatingReference_ShouldFail()
    {
        // Arrange
        var caseId = "CASE-20241201-ghi78901";
        var documents = new[]
        {
            JsonSerializer.Serialize(new
            {
                docId = "doc_forensics_001",
                type = "forensics_report",
                title = "Laudo Pericial",
                sections = new[] { "Análise", "Cadeia de Custódia", "Conclusão" },
                lengthTarget = new[] { 300, 800 },
                gated = true,
                gatingRule = new
                {
                    action = "submit_evidence",
                    evidenceId = "ev_nonexistent_001", // Non-existent evidence
                    notes = "Enviar amostra inexistente"
                },
                content = "Conteúdo do laudo pericial"
            })
        };

        var media = new[]
        {
            JsonSerializer.Serialize(new
            {
                evidenceId = "ev_photo_001",
                kind = "photo",
                title = "Foto da Cena",
                prompt = "Fotografia da cena",
                constraints = new { tipo = "geral" },
                deferred = false
            })
        };

        var input = new NormalizationInput
        {
            CaseId = caseId,
            Difficulty = "Detective",
            Timezone = "UTC",
            Documents = documents,
            Media = media
        };

        // Act
        var result = await _normalizerService.NormalizeCaseAsync(input);

        // Assert
        Assert.NotNull(result);
        
        // Verify validation failed for gating reference
        Assert.Contains(result.Log.ValidationResults, v => 
            v.Rule == "GATING_REFERENCE_INTEGRITY" && 
            v.Status == "FAIL" &&
            v.Description.Contains("ev_nonexistent_001"));
    }

    [Fact]
    public async Task NormalizeCaseAsync_WithDuplicateIds_ShouldFail()
    {
        // Arrange
        var caseId = "CASE-20241201-jkl01234";
        var documents = new[]
        {
            JsonSerializer.Serialize(new
            {
                docId = "doc_duplicate_001",
                type = "police_report",
                title = "Primeiro Relatório",
                sections = new[] { "Seção 1" },
                lengthTarget = new[] { 100, 300 },
                gated = false,
                content = "Primeiro conteúdo"
            }),
            JsonSerializer.Serialize(new
            {
                docId = "doc_duplicate_001", // Duplicate ID
                type = "interview",
                title = "Segundo Relatório",
                sections = new[] { "Seção 1" },
                lengthTarget = new[] { 100, 300 },
                gated = false,
                content = "Segundo conteúdo"
            })
        };

        var input = new NormalizationInput
        {
            CaseId = caseId,
            Difficulty = "Detective",
            Documents = documents,
            Media = Array.Empty<string>()
        };

        // Act
        var result = await _normalizerService.NormalizeCaseAsync(input);

        // Assert
        Assert.NotNull(result);
        
        // Verify validation failed for duplicate IDs
        Assert.Contains(result.Log.ValidationResults, v => 
            v.Rule == "UNIQUE_DOCUMENT_IDS" && 
            v.Status == "FAIL" &&
            v.Description.Contains("doc_duplicate_001"));
    }
}

// Test helper classes
public class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "CaseGen.Functions.Tests";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class TestCaseLoggingService : ICaseLoggingService
{
    public void LogOrchestratorStep(string caseId, string step, string details = "")
    {
        // No-op for tests
    }

    public void LogOrchestratorProgress(string caseId, int currentStep, int totalSteps, string stepName)
    {
        // No-op for tests
    }

    public Task LogDetailedAsync(string caseId, string source, string level, string message, object? data = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task LogLLMInteractionAsync(string caseId, string provider, string promptType, string prompt, string response, int? tokenCount = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<string> GetDetailedLogAsync(string caseId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("{}");
    }

    public Task LogStepResponseAsync(string caseId, string stepName, string jsonResponse, CancellationToken cancellationToken = default)
    {
        // For tests, just return completed task
        return Task.CompletedTask;
    }

    public Task<string> GetStepLogAsync(string caseId, string step, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("{}");
    }

    public Task LogStepMetadataAsync(string caseId, string stepName, object metadata, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}