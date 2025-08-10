using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using CaseZeroApi.Services;
using System.Net.Http;
using System.Text;

namespace CaseZeroApi.Tests.Services
{
    /// <summary>
    /// Integration tests for the complete case generation workflow.
    /// These tests demonstrate the full process but use mocked HTTP responses.
    /// To test with real Azure OpenAI, update the configuration and remove mocks.
    /// </summary>
    public class CaseGenerationIntegrationTests
    {
        [Fact]
        public async Task GenerateCaseJsonAsync_WithValidSeed_ShouldReturnValidJson()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CaseGenerationService>>();
            var configuration = CreateTestConfiguration();
            
            // Mock the HTTP response for Azure OpenAI
            var mockResponse = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"caseId\":\"CASE-2024-TEST\",\"title\":\"Test Case\",\"description\":\"A test case for unit testing\",\"suspects\":[{\"id\":\"SUS-001\",\"name\":\"Test Suspect\"}],\"evidences\":[{\"id\":\"EVD-001\",\"name\":\"Test Evidence\"}]}"
                  }
                }
              ]
            }
            """;

            var httpClient = CreateMockHttpClient(mockResponse);
            var llmOptions = new LlmOptions
            {
                Endpoint = "https://test.openai.azure.com",
                Deployment = "gpt-4",
                ApiKey = "test-key"
            };
            var llmClient = new LlmClient(httpClient, llmOptions);
            var service = new CaseGenerationService(llmClient, mockLogger.Object, configuration);

            var seed = new CaseSeed(
                Title: "Test Case",
                Location: "Test Location",
                IncidentDateTime: DateTimeOffset.Now,
                Pitch: "Test investigation case",
                Twist: "Unexpected revelation"
            );

            // Act
            var result = await service.GenerateCaseJsonAsync(seed);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("CASE-2024-TEST", result);
            Assert.Contains("Test Case", result);
        }

        [Fact]
        public void PromptLibrary_GeneratesCompletePrompts()
        {
            // Arrange
            var seed = new CaseSeed(
                Title: "Corporate Theft Investigation",
                Location: "Downtown Office Building",
                IncidentDateTime: DateTimeOffset.Parse("2024-01-15T22:30:00-05:00"),
                Pitch: "Executive found dead in locked office during business hours",
                Twist: "Security badge used while victim was supposedly alone"
            );

            var caseJson = """
            {
              "caseId": "CASE-2024-001",
              "suspects": [{"id": "SUS-001", "name": "John Suspect"}],
              "evidences": [{"id": "EVD-001", "name": "Security footage"}]
            }
            """;
            var context = new CaseContext(seed, caseJson);

            // Act & Assert
            var systemArchitect = PromptLibrary.SystemArchitect(seed);
            Assert.Contains("arquiteto narrativo forense", systemArchitect);
            Assert.Contains("JSON VÁLIDO", systemArchitect);

            var userArchitect = PromptLibrary.UserArchitect(seed);
            Assert.Contains("Corporate Theft Investigation", userArchitect);
            Assert.Contains("Downtown Office Building", userArchitect);

            var systemForense = PromptLibrary.SystemForense();
            Assert.Contains("redator forense oficial", systemForense);

            var userInterrogatorio = PromptLibrary.UserInterrogatorio(context, "SUS-001");
            Assert.Contains("INTERROGATÓRIO", userInterrogatorio);
            Assert.Contains("SUS-001", userInterrogatorio);

            var userRelatorio = PromptLibrary.UserRelatorio(context);
            Assert.Contains("RELATÓRIO INVESTIGATIVO", userRelatorio);
            Assert.Contains("CASE-2024-001", userRelatorio);

            var userLaudo = PromptLibrary.UserLaudo(context, "ANL-001", "Análise de CFTV");
            Assert.Contains("LAUDO PERICIAL", userLaudo);
            Assert.Contains("ANL-001", userLaudo);

            var userEvidences = PromptLibrary.UserEvidences(context);
            Assert.Contains("TABELA-MESTRA DE EVIDÊNCIAS", userEvidences);

            var userImagePrompts = PromptLibrary.UserImagePrompts(context);
            Assert.Contains("prompts de imagem RICOS", userImagePrompts);
        }

        [Fact]
        public void DocumentGeneration_CreatesProperStructure()
        {
            // Arrange
            var doc = new GeneratedDoc
            {
                Id = "DOC-INT-SUS-001",
                FileName = "03_interrogatorios/INT-SUS-001-sessao-01.md",
                Content = "# Interrogatório\n\nContent here...",
                Kind = DocumentKind.Interrogatorio
            };

            var imagePrompt = new ImagePrompt
            {
                EvidenceId = "EVD-001",
                Title = "Security Camera Frame",
                IntendedUse = "frame_CFTV",
                Prompt = "Detailed description of security camera footage...",
                NegativePrompt = "clear faces, identifiable features",
                Constraints = new Dictionary<string, object>
                {
                    ["lighting"] = "low-light security camera",
                    ["camera"] = "CMOS sensor, 1080p",
                    ["style"] = "security footage grain"
                }
            };

            // Act & Assert
            Assert.Equal("DOC-INT-SUS-001", doc.Id);
            Assert.Equal("03_interrogatorios/INT-SUS-001-sessao-01.md", doc.FileName);
            Assert.Equal(DocumentKind.Interrogatorio, doc.Kind);

            Assert.Equal("EVD-001", imagePrompt.EvidenceId);
            Assert.Equal("frame_CFTV", imagePrompt.IntendedUse);
            Assert.Contains("security camera", imagePrompt.Prompt);
            Assert.Contains("clear faces", imagePrompt.NegativePrompt);
            Assert.True(imagePrompt.Constraints.ContainsKey("lighting"));
        }

        private static IConfiguration CreateTestConfiguration()
        {
            var configurationData = new Dictionary<string, string?>
            {
                ["CasesBasePath"] = "/tmp/test-cases",
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com",
                ["AzureOpenAI:Deployment"] = "gpt-4",
                ["AzureOpenAI:ApiKey"] = "test-key"
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData)
                .Build();
        }

        private static HttpClient CreateMockHttpClient(string responseContent)
        {
            var mockHandler = new MockHttpMessageHandler(responseContent);
            return new HttpClient(mockHandler);
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _responseContent;

            public MockHttpMessageHandler(string responseContent)
            {
                _responseContent = responseContent;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }
    }
}