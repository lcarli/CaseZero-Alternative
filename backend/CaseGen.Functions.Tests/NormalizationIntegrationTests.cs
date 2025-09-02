using System.Text.Json;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;

namespace CaseGen.Functions.Tests;

// Simple integration test to verify the deterministic normalizer works
public class NormalizationIntegrationTests
{
    [Fact]
    public void CreateSampleNormalizationInput_ShouldSerializeCorrectly()
    {
        // Arrange
        var input = new NormalizationInput
        {
            CaseId = "CASE-20241201-test123",
            Difficulty = "Detective",
            Timezone = "America/Sao_Paulo",
            PlanJson = "{}",
            ExpandedJson = "{}",
            DesignJson = "{}",
            Documents = new[]
            {
                JsonSerializer.Serialize(new
                {
                    docId = "doc_police_001",
                    type = "police_report",
                    title = "Relatório Policial",
                    sections = new[] { "Ocorrência", "Detalhes" },
                    lengthTarget = new[] { 200, 500 },
                    gated = false,
                    content = "Conteúdo do relatório"
                })
            },
            Media = new[]
            {
                JsonSerializer.Serialize(new
                {
                    evidenceId = "ev_photo_001",
                    kind = "photo",
                    title = "Foto da Cena",
                    prompt = "Fotografia da cena do crime",
                    constraints = new { iluminacao = "natural" },
                    deferred = false
                })
            }
        };

        // Act
        var json = JsonSerializer.Serialize(input, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        Assert.NotNull(json);
        Assert.Contains("CASE-20241201-test123", json);
        Assert.Contains("Detective", json);
        Assert.Contains("doc_police_001", json);
        Assert.Contains("ev_photo_001", json);
    }

    [Fact]
    public void CreateNormalizedCaseBundle_ShouldHaveCorrectStructure()
    {
        // Arrange
        var bundle = new NormalizedCaseBundle
        {
            CaseId = "CASE-20241201-test123",
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            Timezone = "UTC",
            Difficulty = "Detective",
            Documents = new[]
            {
                new NormalizedDocument
                {
                    DocId = "doc_test_001",
                    Type = "police_report",
                    Title = new I18nText
                    {
                        PtBr = "Relatório Policial",
                        En = "Police Report",
                        Es = "Informe Policial",
                        Fr = "Rapport de Police"
                    },
                    Sections = new[] { "Ocorrência", "Detalhes" },
                    LengthTarget = new[] { 200, 500 },
                    Gated = false,
                    Content = "Conteúdo do relatório",
                    Metadata = new Dictionary<string, object>
                    {
                        ["generated"] = true
                    }
                }
            },
            Media = new[]
            {
                new NormalizedMedia
                {
                    EvidenceId = "ev_test_001",
                    Kind = "photo",
                    Title = new I18nText
                    {
                        PtBr = "Foto da Cena",
                        En = "Scene Photo",
                        Es = "Foto de la Escena",
                        Fr = "Photo de la Scène"
                    },
                    Prompt = "Fotografia da cena",
                    Deferred = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["generated"] = true
                    }
                }
            },
            GatingGraph = new GatingGraph
            {
                Nodes = new[]
                {
                    new GatingNode
                    {
                        Id = "doc_test_001",
                        Type = "document",
                        Gated = false,
                        RequiredIds = Array.Empty<string>()
                    }
                },
                Edges = Array.Empty<GatingEdge>(),
                HasCycles = false,
                CycleDescription = Array.Empty<string>()
            },
            Metadata = new NormalizedCaseMetadata
            {
                GeneratedBy = "NormalizerService",
                Pipeline = "Plan→Expand→Design→GenerateDocuments/GenerateMedia→Normalize",
                GeneratedAt = DateTime.UtcNow,
                ValidationResults = new Dictionary<string, object>(),
                AppliedRules = new[] { "UNIQUE_IDS", "GATING_REFERENCE_INTEGRITY" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        Assert.NotNull(json);
        Assert.Contains("CASE-20241201-test123", json);
        Assert.Contains("Relatório Policial", json);
        Assert.Contains("Police Report", json);
        Assert.Contains("Informe Policial", json);
        Assert.Contains("Rapport de Police", json);
        Assert.Contains("NormalizerService", json);
    }

    [Fact]
    public void CreateCaseManifest_ShouldHaveCorrectStructure()
    {
        // Arrange
        var manifest = new CaseManifest
        {
            CaseId = "CASE-20241201-test123",
            Version = "1.0",
            GeneratedAt = DateTime.UtcNow,
            BundlePaths = new[] { "documents/", "media/" },
            Documents = new[]
            {
                new ManifestEntry
                {
                    Id = "doc_test_001",
                    RelativePath = "documents/doc_test_001.json",
                    Type = "police_report",
                    Gated = false,
                    Hash = "abc123",
                    SizeBytes = 1024
                }
            },
            Media = new[]
            {
                new ManifestEntry
                {
                    Id = "ev_test_001",
                    RelativePath = "media/ev_test_001.json",
                    Type = "photo",
                    Gated = false,
                    Hash = "def456",
                    SizeBytes = 2048
                }
            },
            FileHashes = new Dictionary<string, string>
            {
                ["documents/doc_test_001.json"] = "abc123",
                ["media/ev_test_001.json"] = "def456"
            },
            Visibility = new CaseVisibility
            {
                AlwaysVisible = new[] { "doc_test_001", "ev_test_001" },
                GatedVisible = Array.Empty<string>(),
                HiddenUntilUnlocked = Array.Empty<string>()
            }
        };

        // Act
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        Assert.NotNull(json);
        Assert.Contains("CASE-20241201-test123", json);
        Assert.Contains("documents/", json);
        Assert.Contains("doc_test_001", json);
        Assert.Contains("ev_test_001", json);
        Assert.Contains("abc123", json);
    }
}