using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using CaseZeroApi.Services;
using System.Net.Http;

namespace CaseZeroApi.Tests.Services
{
    public class CaseGenerationServiceTests
    {
        [Fact]
        public void CaseSeed_Constructor_ShouldCreateValidSeed()
        {
            // Arrange & Act
            var seed = new CaseSeed(
                Title: "Test Case",
                Location: "Test Location",
                IncidentDateTime: DateTimeOffset.Now,
                Pitch: "Test pitch",
                Twist: "Test twist"
            );

            // Assert
            Assert.Equal("Test Case", seed.Title);
            Assert.Equal("Test Location", seed.Location);
            Assert.Equal("Test pitch", seed.Pitch);
            Assert.Equal("Test twist", seed.Twist);
            Assert.Equal("Médio", seed.Difficulty);
            Assert.Equal(60, seed.TargetDurationMinutes);
            Assert.Equal("America/Toronto", seed.Timezone);
        }

        [Fact]
        public void CasePackage_ShouldInitializeWithEmptyCollections()
        {
            // Arrange & Act
            var package = new CasePackage();

            // Assert
            Assert.Empty(package.Interrogatorios);
            Assert.Empty(package.Relatorios);
            Assert.Empty(package.Laudos);
            Assert.Empty(package.ImagePrompts);
            Assert.Null(package.EvidenceManifest);
        }

        [Fact]
        public void GenerationOptions_Default_ShouldHaveCorrectValues()
        {
            // Arrange & Act
            var options = GenerationOptions.Default;

            // Assert
            Assert.True(options.GenerateImages);
        }

        [Fact]
        public void CaseContext_Constructor_ShouldExtractCaseId()
        {
            // Arrange
            var seed = new CaseSeed("Test", "Location", DateTimeOffset.Now, "Pitch", "Twist");
            var caseJson = """{"caseId": "CASE-2024-TEST", "title": "Test Case"}""";

            // Act
            var context = new CaseContext(seed, caseJson);

            // Assert
            Assert.Equal("CASE-2024-TEST", context.CaseId);
            Assert.Equal(caseJson, context.CaseJson);
        }

        [Fact]
        public void CaseContext_Constructor_WithInvalidJson_ShouldUseFallback()
        {
            // Arrange
            var seed = new CaseSeed("Test", "Location", DateTimeOffset.Now, "Pitch", "Twist");
            var caseJson = """{"title": "Test Case"}"""; // No caseId

            // Act
            var context = new CaseContext(seed, caseJson);

            // Assert
            Assert.Equal("CASE-UNDEFINED", context.CaseId);
        }

        [Fact]
        public void LlmOptions_BuildChatUrl_ShouldGenerateCorrectUrl()
        {
            // Arrange
            var options = new LlmOptions
            {
                Endpoint = "https://test.openai.azure.com",
                Deployment = "gpt-4",
                ApiVersion = "2024-02-15-preview"
            };

            // Act
            var url = options.BuildChatUrl();

            // Assert
            Assert.Equal("https://test.openai.azure.com/openai/deployments/gpt-4/chat/completions?api-version=2024-02-15-preview", url.ToString());
        }

        [Fact]
        public void ChatMsg_StaticMethods_ShouldCreateCorrectMessages()
        {
            // Arrange & Act
            var systemMsg = ChatMsg.System("System message");
            var userMsg = ChatMsg.User("User message");
            var assistantMsg = ChatMsg.Assistant("Assistant message");

            // Assert
            Assert.Equal("system", systemMsg.Role);
            Assert.Equal("System message", systemMsg.Content);
            Assert.Equal("user", userMsg.Role);
            Assert.Equal("User message", userMsg.Content);
            Assert.Equal("assistant", assistantMsg.Role);
            Assert.Equal("Assistant message", assistantMsg.Content);
        }
    }
}