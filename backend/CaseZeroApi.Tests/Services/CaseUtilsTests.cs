using Xunit;
using CaseZeroApi.Services;

namespace CaseZeroApi.Tests.Services
{
    public class CaseUtilsTests
    {
        [Fact]
        public void ExtractCaseId_ValidJson_ShouldReturnCaseId()
        {
            // Arrange
            var json = """{"caseId": "CASE-2024-001", "title": "Test Case"}""";

            // Act
            var result = CaseUtils.ExtractCaseId(json);

            // Assert
            Assert.Equal("CASE-2024-001", result);
        }

        [Fact]
        public void ExtractCaseId_MissingCaseId_ShouldReturnNull()
        {
            // Arrange
            var json = """{"title": "Test Case"}""";

            // Act
            var result = CaseUtils.ExtractCaseId(json);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractCaseId_InvalidJson_ShouldReturnNull()
        {
            // Arrange
            var json = "invalid json";

            // Act
            var result = CaseUtils.ExtractCaseId(json);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractIds_ValidArrayWithIds_ShouldReturnIds()
        {
            // Arrange
            var json = """
            {
                "suspects": [
                    {"id": "SUS-001", "name": "John Doe"},
                    {"id": "SUS-002", "name": "Jane Smith"}
                ]
            }
            """;

            // Act
            var result = CaseUtils.ExtractIds(json, "suspects");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains("SUS-001", result);
            Assert.Contains("SUS-002", result);
        }

        [Fact]
        public void ExtractIds_EmptyArray_ShouldReturnEmptyList()
        {
            // Arrange
            var json = """{"suspects": []}""";

            // Act
            var result = CaseUtils.ExtractIds(json, "suspects");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractIds_MissingArray_ShouldReturnEmptyList()
        {
            // Arrange
            var json = """{"title": "Test Case"}""";

            // Act
            var result = CaseUtils.ExtractIds(json, "suspects");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractIds_ArrayWithoutIds_ShouldReturnEmptyList()
        {
            // Arrange
            var json = """
            {
                "suspects": [
                    {"name": "John Doe"},
                    {"name": "Jane Smith"}
                ]
            }
            """;

            // Act
            var result = CaseUtils.ExtractIds(json, "suspects");

            // Assert
            Assert.Empty(result);
        }
    }
}