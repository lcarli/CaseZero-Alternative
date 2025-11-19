using System.Text.Json;
using CaseZeroApi.Models;
using CaseZeroApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services
{
    public class CaseFormatServiceTests
    {
        private readonly Mock<ILogger<CaseFormatService>> _loggerMock;
        private readonly Mock<IBlobStorageService> _blobStorageMock;
        private readonly CaseFormatService _service;

        public CaseFormatServiceTests()
        {
            _loggerMock = new Mock<ILogger<CaseFormatService>>();
            _blobStorageMock = new Mock<IBlobStorageService>();
            _service = new CaseFormatService(_loggerMock.Object, _blobStorageMock.Object);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithValidNormalizedBundle_ReturnsGameFormat()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(normalizedBundle.CaseId, result.CaseId);
            Assert.NotEmpty(result.Evidences);
            Assert.NotNull(result.Metadata);
            Assert.NotNull(result.UnlockLogic);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithInvalidJson_ThrowsArgumentException()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.ConvertToGameFormatAsync(invalidJson));
        }

        [Fact]
        public async Task ValidateForGameFormatAsync_WithValidBundle_ReturnsValidResult()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ValidateForGameFormatAsync(json);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public async Task ValidateForGameFormatAsync_WithMissingCaseId_ReturnsInvalidResult()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.CaseId = string.Empty;
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ValidateForGameFormatAsync(json);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Missing caseId", result.Issues);
        }

        [Fact]
        public async Task ValidateForGameFormatAsync_WithNoDocuments_ReturnsInvalidResult()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Documents!.Items.Clear();
            normalizedBundle.Documents.Total = 0;
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ValidateForGameFormatAsync(json);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("No documents found in bundle", result.Issues);
        }

        [Fact]
        public async Task ValidateForGameFormatAsync_WithCircularDependencies_ReturnsInvalidResult()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.GatingGraph!.HasCycles = true;
            normalizedBundle.GatingGraph.CycleDescription.Add("Circular dependency detected");
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ValidateForGameFormatAsync(json);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Gating graph contains cycles which may cause unlock issues", result.Issues);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithEvidenceEntities_CreatesEvidences()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Entities!.Evidence.Add("EV001");
            normalizedBundle.Entities.Total++;
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.Contains(result.Evidences, e => e.Id == "EV001");
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithSuspectEntities_CreatesSuspects()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Entities!.Suspects.Add("SU123");
            normalizedBundle.Entities.Total++;
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.Contains(result.Suspects, s => s.Id == "SU123");
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithGatedNodes_CreatesProgressionRule()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.GatingGraph!.Nodes.Add(new GatingNode
            {
                Id = "DOC777",
                Type = "document",
                Gated = true,
                RequiredIds = new List<string> { "EV001", "EV002" }
            });
            normalizedBundle.Entities!.Evidence.AddRange(new[] { "EV001", "EV002" });
            normalizedBundle.Entities.Total += 2;
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.Contains(result.UnlockLogic.ProgressionRules, rule => rule.Target == "DOC777");
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_SetsMetadataDifficultyFromBundle()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Difficulty = "hard";
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.Equal("Inspector", result.Metadata.MinRankRequired);
            Assert.Equal(3, result.Metadata.Difficulty);
        }

        private static NormalizedCaseBundle CreateSampleNormalizedCaseBundle()
        {
            return new NormalizedCaseBundle
            {
                CaseId = "CASE-20241001-12345678",
                Version = "v2-hierarchical",
                GeneratedAt = DateTime.UtcNow,
                Timezone = "America/Toronto",
                Difficulty = "medium",
                Entities = new CaseEntities
                {
                    Suspects = new List<string>(),
                    Evidence = new List<string>(),
                    Witnesses = new List<string>(),
                    Total = 0
                },
                Documents = new DocumentsCollection
                {
                    Items = new List<string> { "@documents/police_report_001" },
                    Total = 1
                },
                GatingGraph = new GatingGraph
                {
                    Nodes = new List<GatingNode>(),
                    Edges = new List<GatingEdge>(),
                    HasCycles = false,
                    CycleDescription = new List<string>()
                },
                Metadata = new NormalizedCaseMetadata
                {
                    GeneratedBy = "CaseGen",
                    Pipeline = "v2",
                    GeneratedAt = DateTime.UtcNow,
                    ValidationResults = new Dictionary<string, object>(),
                    AppliedRules = new List<string>()
                }
            };
        }
    }
}