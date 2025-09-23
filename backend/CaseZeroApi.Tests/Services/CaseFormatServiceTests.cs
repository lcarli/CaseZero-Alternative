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
        private readonly CaseFormatService _service;

        public CaseFormatServiceTests()
        {
            _loggerMock = new Mock<ILogger<CaseFormatService>>();
            _service = new CaseFormatService(_loggerMock.Object);
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
            normalizedBundle.Documents.Clear();
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
            normalizedBundle.GatingGraph.HasCycles = true;
            normalizedBundle.GatingGraph.CycleDescription.Add("Circular dependency detected");
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ValidateForGameFormatAsync(json);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Gating graph contains cycles which may cause unlock issues", result.Issues);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithEvidenceLogDocument_CreatesEvidence()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Documents.Add(new NormalizedDocument
            {
                DocId = "evidence_log_001",
                Type = "evidence_log",
                Title = "Crime Scene Evidence Log",
                Sections = new List<string> { "Evidence List", "Chain of Custody" },
                LengthTarget = new List<int> { 200, 400 },
                Gated = false,
                GatingRule = null,
                Content = "Evidence collected at crime scene including fingerprints and DNA samples.",
                Metadata = new Dictionary<string, object>()
            });
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            var evidenceLogEvidence = result.Evidences.FirstOrDefault(e => e.Id == "evidence_log_001");
            Assert.NotNull(evidenceLogEvidence);
            Assert.Equal("Crime Scene Evidence Log", evidenceLogEvidence.Name);
            Assert.Equal("physical", evidenceLogEvidence.Type);
            Assert.Equal("document", evidenceLogEvidence.Category);
            Assert.True(evidenceLogEvidence.IsUnlocked);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithInterviewDocument_CreatesSuspect()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Documents.Add(new NormalizedDocument
            {
                DocId = "interview_001",
                Type = "interview",
                Title = "Interview with John Doe",
                Sections = new List<string> { "Personal Information", "Statement", "Questions" },
                LengthTarget = new List<int> { 300, 500 },
                Gated = false,
                GatingRule = null,
                Content = "Interview with John Doe, age 35, occupation: mechanic. Suspect denies involvement.",
                Metadata = new Dictionary<string, object>()
            });
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.NotEmpty(result.Suspects);
            var suspect = result.Suspects.FirstOrDefault();
            Assert.NotNull(suspect);
            Assert.Contains("John Doe", suspect.Name);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithForensicsReport_CreatesForensicAnalysis()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Documents.Add(new NormalizedDocument
            {
                DocId = "forensics_001",
                Type = "forensics_report",
                Title = "DNA Analysis Report",
                Sections = new List<string> { "Sample Analysis", "Results", "Chain of Custody" },
                LengthTarget = new List<int> { 400, 600 },
                Gated = true,
                GatingRule = new GatingRule { Action = "requires_evidence", EvidenceId = "evidence_001" },
                Content = "DNA analysis shows match with suspect database. Evidence ID: evidence_001",
                Metadata = new Dictionary<string, object>()
            });
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.NotEmpty(result.ForensicAnalyses);
            var analysis = result.ForensicAnalyses.FirstOrDefault();
            Assert.NotNull(analysis);
            Assert.Equal("evidence_001", analysis.EvidenceId);
            Assert.Equal("forensic", analysis.AnalysisType);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithMediaItems_CreatesEvidences()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Media.Add(new NormalizedMedia
            {
                EvidenceId = "photo_001",
                Kind = "photo",
                Title = "Crime Scene Photo",
                Prompt = "Photo showing the crime scene layout and evidence positions",
                Constraints = new Dictionary<string, object> { { "lighting", "natural" } },
                Deferred = false,
                Metadata = new Dictionary<string, object> { { "location", "123 Main St" } }
            });
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            var photoEvidence = result.Evidences.FirstOrDefault(e => e.Id == "photo_001");
            Assert.NotNull(photoEvidence);
            Assert.Equal("Crime Scene Photo", photoEvidence.Name);
            Assert.Equal("photo", photoEvidence.Type);
            Assert.Equal("physical", photoEvidence.Category);
            Assert.True(photoEvidence.IsUnlocked);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithGatedDocuments_CreatesProgressionRules()
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.GatingGraph.Nodes.Add(new GatingNode
            {
                Id = "gated_doc_001",
                Type = "document",
                Gated = true,
                RequiredIds = new List<string> { "evidence_001", "evidence_002" }
            });
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            Assert.NotEmpty(result.UnlockLogic.ProgressionRules);
            var rule = result.UnlockLogic.ProgressionRules.FirstOrDefault();
            Assert.NotNull(rule);
            Assert.Equal("gated_doc_001", rule.Target);
            Assert.Equal("document_reviewed", rule.Condition);
        }

        [Fact]
        public async Task ConvertToGameFormatAsync_WithMultipleDifficulties_SetsDifficultyCorrectly()
        {
            // Arrange
            var testCases = new[]
            {
                ("Rookie", 1, "Rookie"),
                ("Detective", 2, "Detective"),
                ("Commander", 7, "Commander"),
                (null, 2, "Detective")
            };

            foreach (var (difficulty, expectedDifficultyInt, expectedRank) in testCases)
            {
                var normalizedBundle = CreateSampleNormalizedCaseBundle();
                normalizedBundle.Difficulty = difficulty;
                var json = JsonSerializer.Serialize(normalizedBundle);

                // Act
                var result = await _service.ConvertToGameFormatAsync(json);

                // Assert
                Assert.Equal(expectedDifficultyInt, result.Metadata.Difficulty);
                Assert.Equal(expectedRank, result.Metadata.MinRankRequired);
            }
        }

        [Theory]
        [InlineData("evidence_log", true)]
        [InlineData("forensics_report", true)]
        [InlineData("witness_statement", true)]
        [InlineData("police_report", false)]
        [InlineData("interview", false)]
        [InlineData("memo_admin", false)]
        public async Task ConvertToGameFormatAsync_DocumentTypes_ConvertedToEvidenceAppropriately(
            string documentType, bool shouldCreateEvidence)
        {
            // Arrange
            var normalizedBundle = CreateSampleNormalizedCaseBundle();
            normalizedBundle.Documents.Clear();
            normalizedBundle.Documents.Add(new NormalizedDocument
            {
                DocId = $"doc_{documentType}",
                Type = documentType,
                Title = $"Test {documentType}",
                Sections = new List<string> { "Section 1" },
                LengthTarget = new List<int> { 100, 200 },
                Gated = false,
                Content = "Test content",
                Metadata = new Dictionary<string, object>()
            });
            var json = JsonSerializer.Serialize(normalizedBundle);

            // Act
            var result = await _service.ConvertToGameFormatAsync(json);

            // Assert
            var hasEvidence = result.Evidences.Any(e => e.Id == $"doc_{documentType}");
            Assert.Equal(shouldCreateEvidence, hasEvidence);
        }

        private static NormalizedCaseBundle CreateSampleNormalizedCaseBundle()
        {
            return new NormalizedCaseBundle
            {
                CaseId = "CASE-20241001-12345678",
                Version = "1.0",
                CreatedAt = DateTime.UtcNow,
                Timezone = "America/Toronto",
                Difficulty = "Detective",
                Documents = new List<NormalizedDocument>
                {
                    new()
                    {
                        DocId = "police_report_001",
                        Type = "police_report",
                        Title = "Initial Police Report",
                        Sections = new List<string> { "Incident Summary", "Officers", "Evidence" },
                        LengthTarget = new List<int> { 200, 400 },
                        Gated = false,
                        GatingRule = null,
                        Content = "Initial report of the incident at 123 Main Street. Victim: Jane Smith, age 28.",
                        Metadata = new Dictionary<string, object>
                        {
                            { "location", "123 Main Street" },
                            { "reportingOfficer", "Officer Johnson" }
                        }
                    },
                    new()
                    {
                        DocId = "evidence_log_001",
                        Type = "evidence_log",
                        Title = "Crime Scene Evidence Log",
                        Sections = new List<string> { "Evidence List", "Chain of Custody" },
                        LengthTarget = new List<int> { 150, 300 },
                        Gated = false,
                        GatingRule = null,
                        Content = "Evidence collected at scene includes fingerprints and DNA samples.",
                        Metadata = new Dictionary<string, object>()
                    }
                },
                Media = new List<NormalizedMedia>(),
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
                    Pipeline = "v1.0",
                    GeneratedAt = DateTime.UtcNow,
                    ValidationResults = new Dictionary<string, object>(),
                    AppliedRules = new List<string>()
                }
            };
        }
    }
}