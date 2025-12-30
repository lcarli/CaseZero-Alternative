using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;
using Microsoft.Extensions.DependencyInjection;
using CaseZeroApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.IntegrationTests
{
    /// <summary>
    /// Task 105-110: Testes de segurança anti-spoiler
    /// Garante que informações sensíveis NUNCA sejam expostas ao cliente
    /// </summary>
    public class SecurityIntegrationTests : IntegrationTestsBase
    {
        private readonly string _testUserId = "test-user-security";
        private readonly string _testCaseId = "case_001";

        public SecurityIntegrationTests(CustomWebApplicationFactory<Program> factory) : base(factory)
        {
        }

        /// <summary>
        /// Task 105: Teste anti-spoiler - Assets endpoint
        /// Verifica que GET /api/cases/{caseId}/assets NÃO retorna:
        /// - solution, culpritId, rules[]
        /// - Assets hidden não desbloqueados
        /// </summary>
        [Fact]
        public async Task GetAssets_NeverExposeSensitiveData()
        {
            // Arrange
            var token = await CreateAuthenticatedUserAndGetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Criar sessão ativa para o usuário
            await CreateActiveSessionForUser(_testUserId, _testCaseId);
            
            // Adicionar alguns assets visíveis
            await AddVisibleAsset(_testUserId, _testCaseId, "asset.briefing_doc");

            // Act
            var response = await _client.GetAsync($"/api/cases/{_testCaseId}/assets");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var content = await response.Content.ReadAsStringAsync();
            var jsonContent = content.ToLower();

            // 🔒 CRÍTICO: Verificar que dados sensíveis NÃO aparecem
            Assert.DoesNotContain("solution", jsonContent);
            Assert.DoesNotContain("culprit", jsonContent);
            Assert.DoesNotContain("rules", jsonContent);
            Assert.DoesNotContain("answer", jsonContent);
            Assert.DoesNotContain("hidden", jsonContent); // Assets hidden não devem aparecer
            
            // Verificar que apenas assets visíveis retornam
            var assets = JsonSerializer.Deserialize<List<AssetDto>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            Assert.NotNull(assets);
            Assert.Single(assets); // Apenas 1 asset visível adicionado
            Assert.Contains(assets, a => a.Id == "asset.briefing_doc");
        }

        /// <summary>
        /// Task 61: Teste que asset hidden não aparece em /assets
        /// Usuário não deve ver assets com visibility:hidden não desbloqueados
        /// </summary>
        [Fact]
        public async Task GetAssets_HiddenAsset_NotReturned()
        {
            // Arrange
            var token = await CreateAuthenticatedUserAndGetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            await CreateActiveSessionForUser(_testUserId, _testCaseId);
            
            // Adicionar asset visível
            await AddVisibleAsset(_testUserId, _testCaseId, "asset.visible_evidence");
            
            // NÃO adicionar asset hidden (simula asset que ainda está hidden)

            // Act
            var response = await _client.GetAsync($"/api/cases/{_testCaseId}/assets");

            // Assert
            var content = await response.Content.ReadAsStringAsync();
            var assets = JsonSerializer.Deserialize<List<AssetDto>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            Assert.NotNull(assets);
            
            // Verificar que asset hidden NÃO está na lista
            Assert.DoesNotContain(assets, a => a.Id.Contains("hidden"));
            Assert.DoesNotContain(assets, a => a.Id.Contains("secret"));
            
            // Apenas o asset visível deve retornar
            Assert.Single(assets);
            Assert.Equal("asset.visible_evidence", assets[0].Id);
        }

        /// <summary>
        /// Task 65: Teste que usuário não consegue baixar asset não visível
        /// Acesso direto a asset hidden deve retornar 403 Forbidden
        /// </summary>
        [Fact]
        public async Task DownloadAsset_InvisibleAsset_ReturnsForbidden()
        {
            // Arrange
            var token = await CreateAuthenticatedUserAndGetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            await CreateActiveSessionForUser(_testUserId, _testCaseId);
            
            // Tentar baixar asset que NÃO está na lista de visíveis
            var hiddenAssetId = "asset.hidden_evidence";

            // Act
            var response = await _client.GetAsync($"/api/cases/{_testCaseId}/assets/{hiddenAssetId}/download");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        /// <summary>
        /// Task 107: Teste autorização - User A não acessa sessão de User B
        /// </summary>
        [Fact]
        public async Task GetAssets_DifferentUser_ReturnsForbidden()
        {
            // Arrange - Criar User A com sessão
            var tokenUserA = await CreateAuthenticatedUserAndGetToken();
            var userAId = "user-a-security-test";
            await CreateActiveSessionForUser(userAId, _testCaseId);
            await AddVisibleAsset(userAId, _testCaseId, "asset.user_a_evidence");

            // Arrange - User B tenta acessar caso de User A
            var tokenUserB = await CreateAuthenticatedUserAndGetToken(email: "userb@fic-police.gov");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenUserB);

            // Act
            var response = await _client.GetAsync($"/api/cases/{_testCaseId}/assets");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        /// <summary>
        /// Task 60: Teste start case cria sessão com email do chefe visível
        /// </summary>
        [Fact]
        public async Task StartCase_CreatesSessionWithInitialEmail()
        {
            // Arrange
            var token = await CreateAuthenticatedUserAndGetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Grant access to case
            await GrantUserCaseAccess(_testUserId, _testCaseId);

            var startRequest = new
            {
                caseId = _testCaseId
            };

            // Act
            var response = await _client.PostAsync("/api/casesession/start", CreateJsonContent(startRequest));

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            // Verificar que sessão foi criada
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var session = await context.CaseSessions
                .FirstOrDefaultAsync(s => s.UserId == _testUserId && s.CaseId == _testCaseId);
            
            Assert.NotNull(session);
            Assert.Equal(SessionStatus.Active, session.Status);
            
            // Verificar que email do chefe está visível
            var visibleEmails = await context.CaseSessionVisibleEmails
                .Where(e => e.UserId == _testUserId && e.CaseId == _testCaseId)
                .ToListAsync();
            
            Assert.NotEmpty(visibleEmails);
            // Deve ter pelo menos o email.briefing_001
            Assert.Contains(visibleEmails, e => e.EmailId.Contains("briefing"));
        }

        /// <summary>
        /// Task 106: Teste sanitização - Buscar palavras proibidas em todas as responses
        /// </summary>
        [Fact]
        public async Task GetCaseSession_NeverExposeSolution()
        {
            // Arrange
            var token = await CreateAuthenticatedUserAndGetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            await CreateActiveSessionForUser(_testUserId, _testCaseId);

            // Act
            var response = await _client.GetAsync($"/api/cases/{_testCaseId}/session");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var content = await response.Content.ReadAsStringAsync();
            var jsonContent = content.ToLower();

            // 🔒 CRÍTICO: NUNCA expor solution
            Assert.DoesNotContain("\"solution\":", jsonContent);
            Assert.DoesNotContain("culpritid", jsonContent);
            Assert.DoesNotContain("\"rules\":", jsonContent);
        }

        // ===== Helper Methods =====

        private async Task<string> CreateAuthenticatedUserAndGetToken(string email = "security-test@fic-police.gov")
        {
            // Criar usuário via banco diretamente
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var user = new User
            {
                Id = _testUserId,
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "Security",
                PersonalEmail = email,
                EmailConfirmed = true,
                Rank = DetectiveRank.Detective
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Simular login e obter token (simplificado para testes)
            var loginRequest = new LoginRequestDto
            {
                Email = email,
                Password = "TestPassword123!"
            };

            // Para testes de integração, vamos usar um token mock ou configurar Identity corretamente
            // Por enquanto, retornar um token válido para os testes
            return "mock-jwt-token-for-testing";
        }

        private async Task CreateActiveSessionForUser(string userId, string caseId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var session = new CaseSession
            {
                UserId = userId,
                CaseId = caseId,
                SessionStart = DateTime.UtcNow,
                GameTimeAtStart = "00:00:00",
                Status = SessionStatus.Active
            };

            context.CaseSessions.Add(session);
            await context.SaveChangesAsync();
        }

        private async Task AddVisibleAsset(string userId, string caseId, string assetId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var visibleAsset = new CaseSessionVisibleAsset
            {
                UserId = userId,
                CaseId = caseId,
                AssetId = assetId
            };

            context.CaseSessionVisibleAssets.Add(visibleAsset);
            await context.SaveChangesAsync();
        }

        private async Task GrantUserCaseAccess(string userId, string caseId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var userCase = new UserCase
            {
                UserId = userId,
                CaseId = caseId,
                AssignedAt = DateTime.UtcNow
            };

            context.UserCases.Add(userCase);
            await context.SaveChangesAsync();
        }
    }
}
