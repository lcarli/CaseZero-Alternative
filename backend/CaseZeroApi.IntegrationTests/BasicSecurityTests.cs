using System.Net;
using Xunit;
using System.Net.Http.Json;

namespace CaseZeroApi.IntegrationTests
{
    /// <summary>
    /// P81-P85: Testes de segurança básica
    /// Valida autenticação, visibilidade, sanitização, rate limiting e validação de IDs
    /// </summary>
    public class BasicSecurityTests : IntegrationTestsBase
    {
        public BasicSecurityTests(CustomWebApplicationFactory<Program> factory) : base(factory)
        {
        }

        /// <summary>
        /// P85: Teste validação de IDs malformados
        /// </summary>
        [Theory]
        [InlineData("/api/cases/INVALID_FORMAT/assets")] // Invalid case ID format (must be case_xxx)
        [InlineData("/api/cases/case_001/emails/DROP")] // Invalid email ID format (must be email.xxx or no-findings-xxx)
        public async Task MalformedIds_ReturnBadRequest(string malformedUrl)
        {
            // Arrange
            var token = await CreateAuthenticatedUserAndGetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync(malformedUrl);

            // Assert - Middleware deve rejeitar IDs malformados
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// P85: Teste que IDs válidos passam pela validação
        /// </summary>
        [Theory]
        [InlineData("case_001")]
        [InlineData("case_test_123")]
        [InlineData("case_advanced-case-2025")]
        public async Task ValidCaseIds_PassValidation(string validCaseId)
        {
            // Arrange
            var token = await CreateAuthenticatedUserAndGetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync($"/api/cases/{validCaseId}/assets");

            // Assert - Não deve retornar 400 (pode retornar 404 ou 403, mas não por validação de ID)
            Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// P81: Teste que endpoints protegidos exigem autenticação
        /// </summary>
        [Theory]
        [InlineData("/api/cases/case_001/assets")]
        [InlineData("/api/cases/case_001/emails")]
        [InlineData("/api/cases")]
        public async Task ProtectedEndpoints_RequireAuthentication(string endpoint)
        {
            // Arrange - SEM token de autenticação
            _client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await _client.GetAsync(endpoint);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// P83: Teste que case.json sanitizado não contém dados sensíveis
        /// </summary>
        [Fact]
        public async Task GetCase_SanitizedResponse_NoSensitiveData()
        {
            // Arrange
            var userId = "test-user-sanitization-" + Guid.NewGuid().ToString()[..8];
            var caseId = "case_001";
            
            var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Criar o caso no banco
            await CreateCaseInDatabase(caseId, "Test Case for Sanitization");
            
            // Garantir acesso ao caso e sessão ativa
            await GrantUserCaseAccess(userId, caseId);
            await CreateActiveSessionForUser(userId, caseId);

            // Act
            var response = await _client.GetAsync($"/api/cases/{caseId}");

            // Assert
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            
            // Verificar que campos perigosos NÃO estão presentes
            Assert.DoesNotContain("\"rules\":", content);
            Assert.DoesNotContain("\"solution\":", content);
            Assert.DoesNotContain("\"solutionStub\":", content);
            Assert.DoesNotContain("\"culpritId\":", content);
            Assert.DoesNotContain("\"culprit\":", content);
            Assert.DoesNotContain("\"answer\":", content);
        }

        /// <summary>
        /// P82: Teste que assets invisíveis não são retornados
        /// </summary>
        [Fact]
        public async Task GetAssets_OnlyReturnsVisibleAssets()
        {
            // Arrange
            var userId = "test-user-visibility-" + Guid.NewGuid().ToString()[..8];
            var caseId = "case_001";
            
            var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            await CreateActiveSessionForUser(userId, caseId);
            
            // Adicionar apenas 1 asset visível
            await AddVisibleAsset(userId, caseId, "asset.visible_item");

            // Act
            var response = await _client.GetAsync($"/api/cases/{caseId}/assets");

            // Assert
            response.EnsureSuccessStatusCode();
            
            var assets = await response.Content.ReadFromJsonAsync<List<dynamic>>();
            
            // Deve retornar apenas o asset visível
            Assert.NotNull(assets);
            Assert.Single(assets);
        }
    }
}
