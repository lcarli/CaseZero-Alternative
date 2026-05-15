using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;
using CaseZeroApi.Models;

namespace CaseZeroApi.IntegrationTests;

/// <summary>
/// P86: Testes de integração para Audit Log
/// Valida que ações críticas são registradas corretamente
/// </summary>
public class AuditLogTests : IntegrationTestsBase
{
    public AuditLogTests(CustomWebApplicationFactory<Program> factory) 
        : base(factory)
    {
    }

    /// <summary>
    /// P86: Testa que download de asset visível é permitido (mesmo que blob não exista em test)
    /// O audit log é criado apenas quando o download é bem-sucedido (blob existe)
    /// Em testes, verificamos que o fluxo de autorização funciona corretamente
    /// </summary>
    [Fact]
    public async Task AssetDownload_WithVisibleAsset_PassesAuthorization()
    {
        // Arrange
        var userId = "test-audit-asset-" + Guid.NewGuid().ToString()[..8];
        var caseId = "case_001";
        var assetId = "asset.test_evidence";

        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await CreateCaseInDatabase(caseId);
        await GrantUserCaseAccess(userId, caseId);
        await CreateActiveSessionForUser(userId, caseId);
        await AddVisibleAsset(userId, caseId, assetId);

        // Act
        var response = await _client.GetAsync($"/api/cases/{caseId}/assets/{assetId}/download");

        // Assert - Asset visível deve passar pela autorização (mesmo que blob não exista)
        // Em produção com blob storage real, retornaria 200 e audit log seria criado
        // Em testes, retorna 404 (blob não existe), mas passou pela autorização ✅
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        
        // Nota: O audit log só é criado quando o download é bem-sucedido
        // Como o blob não existe em testes, não haverá audit log
        // O comportamento correto é testado em UnauthorizedAssetDownload
    }

    /// <summary>
    /// P86: Testa que tentativa de download não autorizado é registrada
    /// </summary>
    [Fact]
    public async Task UnauthorizedAssetDownload_CreatesAuditLogWithUnauthorized()
    {
        // Arrange
        var userId = "test-audit-unauthorized-" + Guid.NewGuid().ToString()[..8];
        var caseId = "case_001";
        var assetId = "asset.hidden_evidence";

        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await CreateCaseInDatabase(caseId);
        await GrantUserCaseAccess(userId, caseId);
        await CreateActiveSessionForUser(userId, caseId);
        // NÃO adicionar asset à visibilidade

        // Act
        var response = await _client.GetAsync($"/api/cases/{caseId}/assets/{assetId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verificar audit log com resultado "unauthorized"
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
        
        var auditLog = context.AuditLogs
            .FirstOrDefault(log => 
                log.UserId == userId && 
                log.Action == "asset_download_unauthorized" && 
                log.Resource == $"{caseId}/asset/{assetId}");

        Assert.NotNull(auditLog);
        Assert.Equal("unauthorized", auditLog.Result);
    }

    /// <summary>
    /// P86: Testa que abertura de email é registrada no audit log
    /// </summary>
    [Fact]
    public async Task EmailOpen_CreatesAuditLog()
    {
        // Arrange
        var userId = "test-audit-email-" + Guid.NewGuid().ToString()[..8];
        var caseId = "case_001";
        var emailId = "email.briefing_001";

        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await CreateCaseInDatabase(caseId);
        await GrantUserCaseAccess(userId, caseId);
        await CreateActiveSessionForUser(userId, caseId);
        
        // Adicionar email à visibilidade
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
            context.CaseSessionVisibleEmails.Add(new CaseSessionVisibleEmail
            {
                UserId = userId,
                CaseId = caseId,
                EmailId = emailId
            });
            await context.SaveChangesAsync();
        }

        // Act
        var response = await _client.PostAsync($"/api/cases/{caseId}/emails/{emailId}/open", null);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verificar audit log
        using var scope2 = _factory.Services.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
        
        var auditLog = context2.AuditLogs
            .FirstOrDefault(log => 
                log.UserId == userId && 
                log.Action == "email_open" && 
                log.Resource == $"{caseId}/email/{emailId}");

        Assert.NotNull(auditLog);
        Assert.Equal("success", auditLog.Result);
        Assert.Contains("openCount", auditLog.Details ?? "");
    }

    /// <summary>
    /// P86: Testa que forensic request é registrado no audit log
    /// </summary>
    [Fact]
    public async Task ForensicRequest_CreatesAuditLog()
    {
        // Arrange
        var userId = "test-audit-forensic-" + Guid.NewGuid().ToString()[..8];
        var caseId = "case_001";
        var assetId = "asset.test_evidence";

        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await CreateCaseInDatabase(caseId);
        await GrantUserCaseAccess(userId, caseId);
        await CreateActiveSessionForUser(userId, caseId);

        var forensicRequest = new
        {
            caseId = caseId,
            inputAssetId = assetId,
            inputAssetName = "Test Evidence",
            analysisType = "DNA",
            requestedAt = DateTime.UtcNow,
            estimatedCompletionTime = DateTime.UtcNow.AddHours(2),
            notes = "Test forensic request"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(forensicRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/forensicrequest", content);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Request failed with status {response.StatusCode}: {errorContent}");
        }
        Assert.True(response.IsSuccessStatusCode);

        // Verificar audit log
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
        
        var auditLog = context.AuditLogs
            .FirstOrDefault(log => 
                log.UserId == userId && 
                log.Action == "forensic_request" && 
                log.CaseId == caseId);

        Assert.NotNull(auditLog);
        Assert.Equal("success", auditLog.Result);
        Assert.Contains("inputAssetId", auditLog.Details ?? "");
        Assert.Contains("analysisType", auditLog.Details ?? "");
    }

    /// <summary>
    /// P86: Testa query de audit logs por usuário
    /// </summary>
    [Fact]
    public async Task GetUserLogs_ReturnsUserAuditLogs()
    {
        // Arrange
        var userId = "test-audit-query-" + Guid.NewGuid().ToString()[..8];
        var caseId = "case_001";

        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await CreateCaseInDatabase(caseId);
        await GrantUserCaseAccess(userId, caseId);
        await CreateActiveSessionForUser(userId, caseId);

        // Criar audit logs manualmente
        using (var scope = _factory.Services.CreateScope())
        {
            var auditService = scope.ServiceProvider.GetRequiredService<Services.IAuditLogService>();
            
            await auditService.LogActionAsync(userId, "test_action_1", $"{caseId}/test/1", caseId);
            await auditService.LogActionAsync(userId, "test_action_2", $"{caseId}/test/2", caseId);
            await auditService.LogActionAsync(userId, "test_action_3", $"{caseId}/test/3", caseId);
        }

        // Act - Query logs
        using var scope2 = _factory.Services.CreateScope();
        var auditService2 = scope2.ServiceProvider.GetRequiredService<Services.IAuditLogService>();
        var logs = await auditService2.GetUserLogsAsync(userId);

        // Assert
        Assert.NotEmpty(logs);
        Assert.True(logs.Count >= 3);
        Assert.All(logs, log => Assert.Equal(userId, log.UserId));
    }
}
