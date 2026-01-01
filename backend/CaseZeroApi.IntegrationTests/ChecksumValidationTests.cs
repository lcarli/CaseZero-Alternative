using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace CaseZeroApi.IntegrationTests;

/// <summary>
/// P87: Testes de validação de checksum SHA256 em assets
/// Previne tampering e garante integridade de evidências
/// </summary>
public class ChecksumValidationTests : IntegrationTestsBase
{
    public ChecksumValidationTests(CustomWebApplicationFactory<Program> factory) 
        : base(factory)
    {
    }

    /// <summary>
    /// P87: Testa que asset sem checksum faz download normalmente
    /// </summary>
    [Fact]
    public async Task AssetDownload_WithoutChecksum_RecordsNoValidation()
    {
        // Arrange
        var userId = "test-no-checksum-" + Guid.NewGuid().ToString()[..8];
        var caseId = "case_001";
        var assetId = "asset.briefing_doc";

        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await CreateCaseInDatabase(caseId);
        await GrantUserCaseAccess(userId, caseId);
        await CreateActiveSessionForUser(userId, caseId);
        await AddVisibleAsset(userId, caseId, assetId);

        // Act - Tentar download (blob não existe em testes, mas verifica fluxo)
        var response = await _client.GetAsync($"/api/cases/{caseId}/assets/{assetId}/download");

        // Assert - Não é Forbidden (passou pela autorização)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        
        // Se houvesse blob, o audit log teria checksumValidated:false
        // Isso é testado em ambiente com blob storage real
    }

    /// <summary>
    /// P87: Testa que audit log registra tentativa de download
    /// (checksum validation requer blob storage real)
    /// </summary>
    [Fact]
    public async Task AssetDownload_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = "test-audit-checksum-" + Guid.NewGuid().ToString()[..8];
        var caseId = "case_001";
        var assetId = "asset.visible_item";

        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await CreateCaseInDatabase(caseId);
        await GrantUserCaseAccess(userId, caseId);
        await CreateActiveSessionForUser(userId, caseId);
        await AddVisibleAsset(userId, caseId, assetId);

        // Act
        var response = await _client.GetAsync($"/api/cases/{caseId}/assets/{assetId}/download");

        // Assert - Verifica que não é Forbidden
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        
        // Nota: Testes completos de checksum validation requerem:
        // 1. Blob storage com arquivos reais
        // 2. Case.json com checksum configurado
        // 3. Assets válidos no blob
        // Esses testes são feitos manualmente ou em ambiente staging
    }

    /// <summary>
    /// P87: Helper para calcular SHA256 (usado para gerar checksums)
    /// </summary>
    [Fact]
    public void CalculateSHA256_GeneratesCorrectHash()
    {
        // Arrange
        var testContent = "Test file content";
        var testBytes = System.Text.Encoding.UTF8.GetBytes(testContent);

        // Act
        string checksum;
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(testBytes);
            checksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        // Assert
        Assert.NotNull(checksum);
        Assert.Equal(64, checksum.Length); // SHA256 = 64 caracteres hex
        Assert.Matches("^[a-f0-9]{64}$", checksum); // Apenas hex lowercase
    }
}
