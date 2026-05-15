using System.Net;
using Xunit;

namespace CaseZeroApi.IntegrationTests;

/// <summary>
/// P90: Testes de validação de Security Headers (CSP, HSTS, etc)
/// Garante que todos os endpoints retornam headers de segurança adequados
/// </summary>
public class SecurityHeadersTests : IntegrationTestsBase
{
    public SecurityHeadersTests(CustomWebApplicationFactory<Program> factory) 
        : base(factory)
    {
    }

    /// <summary>
    /// P90: Valida que Content-Security-Policy está configurado corretamente
    /// </summary>
    [Fact]
    public async Task PublicEndpoint_ReturnsCSPHeader()
    {
        // Arrange - Endpoint público não precisa autenticação
        
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").First();
        
        // Validar diretivas críticas
        Assert.Contains("default-src 'self'", cspHeader);
        Assert.Contains("script-src 'self'", cspHeader);
        Assert.Contains("object-src 'none'", cspHeader);
        Assert.Contains("frame-ancestors 'none'", cspHeader);
        Assert.Contains("base-uri 'self'", cspHeader);
    }

    /// <summary>
    /// P90: Valida que X-Frame-Options previne clickjacking
    /// </summary>
    [Fact]
    public async Task AllEndpoints_ReturnXFrameOptions()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    /// <summary>
    /// P90: Valida que X-Content-Type-Options previne MIME sniffing
    /// </summary>
    [Fact]
    public async Task AllEndpoints_ReturnXContentTypeOptions()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    /// <summary>
    /// P90: Valida que Referrer-Policy está configurado
    /// </summary>
    [Fact]
    public async Task AllEndpoints_ReturnReferrerPolicy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", 
            response.Headers.GetValues("Referrer-Policy").First());
    }

    /// <summary>
    /// P90: Valida que X-XSS-Protection está habilitado
    /// </summary>
    [Fact]
    public async Task AllEndpoints_ReturnXXSSProtection()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.True(response.Headers.Contains("X-XSS-Protection"));
        Assert.Equal("1; mode=block", response.Headers.GetValues("X-XSS-Protection").First());
    }

    /// <summary>
    /// P90: Valida que Permissions-Policy bloqueia features perigosas
    /// </summary>
    [Fact]
    public async Task AllEndpoints_ReturnPermissionsPolicy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        
        var permissionsPolicy = response.Headers.GetValues("Permissions-Policy").First();
        
        // Validar que features perigosas estão bloqueadas
        Assert.Contains("geolocation=()", permissionsPolicy);
        Assert.Contains("microphone=()", permissionsPolicy);
        Assert.Contains("camera=()", permissionsPolicy);
        Assert.Contains("payment=()", permissionsPolicy);
    }

    /// <summary>
    /// P90: Valida CSP em endpoint autenticado
    /// </summary>
    [Fact]
    public async Task AuthenticatedEndpoint_ReturnsSecurityHeaders()
    {
        // Arrange
        var userId = "test-security-headers-" + Guid.NewGuid().ToString()[..8];
        var token = await CreateAuthenticatedUserAndGetToken(userId: userId);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/cases");

        // Assert - Mesmo endpoints autenticados devem ter headers de segurança
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    /// <summary>
    /// P90: Valida que CSP permite recursos necessários para a aplicação
    /// </summary>
    [Fact]
    public async Task CSPPolicy_AllowsNecessaryResources()
    {
        // Act
        var response = await _client.GetAsync("/api/health");
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").First();

        // Assert - Verificar que permite recursos necessários
        Assert.Contains("style-src 'self' 'unsafe-inline'", cspHeader); // Para componentes inline
        Assert.Contains("img-src 'self' data: blob:", cspHeader);       // Para data URLs e blobs
        Assert.Contains("font-src 'self' data:", cspHeader);            // Para fontes data URLs
        Assert.Contains("media-src 'self' blob:", cspHeader);           // Para mídia blob
    }

    /// <summary>
    /// P90: Valida que CSP bloqueia recursos perigosos
    /// </summary>
    [Fact]
    public async Task CSPPolicy_BlocksDangerousResources()
    {
        // Act
        var response = await _client.GetAsync("/api/health");
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").First();

        // Assert - Verificar bloqueios críticos
        Assert.Contains("object-src 'none'", cspHeader);           // Bloqueia <object>, <embed>
        Assert.Contains("frame-ancestors 'none'", cspHeader);      // Previne embedding
        Assert.DoesNotContain("'unsafe-eval'", cspHeader);         // Não permite eval()
        
        // Script deve ser restrito (não deve ter unsafe-inline ou unsafe-eval)
        var scriptDirective = cspHeader.Split(';')
            .FirstOrDefault(d => d.Trim().StartsWith("script-src"));
        Assert.NotNull(scriptDirective);
        Assert.DoesNotContain("'unsafe-inline'", scriptDirective);
        Assert.DoesNotContain("'unsafe-eval'", scriptDirective);
    }
}
