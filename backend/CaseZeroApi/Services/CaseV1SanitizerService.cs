using CaseZeroApi.Models.CaseV1;
using CaseZeroApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

/// <summary>
/// Serviço de sanitização de casos v1.0
/// Remove informações sensíveis que NUNCA devem ser expostas ao cliente
/// </summary>
public interface ICaseV1SanitizerService
{
    /// <summary>
    /// Sanitiza um caso removendo dados sensíveis (sem session tracking)
    /// </summary>
    CaseV1 Sanitize(CaseV1 caseData);
    
    /// <summary>
    /// Sanitiza um caso removendo dados sensíveis e aplicando filtros de visibilidade
    /// </summary>
    Task<CaseV1> SanitizeCaseForClientAsync(CaseV1 caseData, string userId, string caseId);
}

public class CaseV1SanitizerService : ICaseV1SanitizerService
{
    private readonly ILogger<CaseV1SanitizerService> _logger;
    private readonly ApplicationDbContext _context;

    public CaseV1SanitizerService(
        ILogger<CaseV1SanitizerService> logger,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Sanitização básica - apenas remove rules e filtra por visibility="initial"
    /// </summary>
    public CaseV1 Sanitize(CaseV1 caseData)
    {
        if (caseData == null)
        {
            throw new ArgumentNullException(nameof(caseData));
        }

        return new CaseV1
        {
            Version = caseData.Version,
            CaseId = caseData.CaseId,
            Metadata = caseData.Metadata,
            ForensicsDefaults = caseData.ForensicsDefaults,
            Rules = null, // 🔒 NUNCA expor ao cliente
            Assets = caseData.Assets.Where(a => a.Visibility == "initial").ToList(),
            Emails = caseData.Emails.Where(e => e.Visibility == "initial").ToList(),
            Suspects = caseData.Suspects.Where(s => s.Visibility == "initial").ToList()
        };
    }

    public async Task<CaseV1> SanitizeCaseForClientAsync(CaseV1 caseData, string userId, string caseId)
    {
        if (caseData == null)
        {
            throw new ArgumentNullException(nameof(caseData));
        }

        _logger.LogInformation(
            "Sanitizing case {CaseId} for user {UserId}",
            caseData.CaseId, userId);

        // Buscar assets visíveis do usuário para este caso
        var visibleAssetIds = await _context.CaseSessionVisibleAssets
            .Where(va => va.UserId == userId && va.CaseId == caseId)
            .Select(va => va.AssetId)
            .ToListAsync();

        // Buscar emails visíveis do usuário para este caso
        var visibleEmailIds = await _context.CaseSessionVisibleEmails
            .Where(ve => ve.UserId == userId && ve.CaseId == caseId)
            .Select(ve => ve.EmailId)
            .ToListAsync();

        // Criar cópia sanitizada
        var sanitized = new CaseV1
        {
            Version = caseData.Version,
            CaseId = caseData.CaseId,
            Metadata = caseData.Metadata,
            ForensicsDefaults = caseData.ForensicsDefaults
        };

        // 🔒 CRÍTICO: NUNCA expor rules ao cliente
        sanitized.Rules = null;

        // Filtrar assets: apenas os visíveis na sessão
        sanitized.Assets = caseData.Assets
            .Where(a => visibleAssetIds.Contains(a.AssetId))
            .ToList();

        // Filtrar emails: apenas os visíveis na sessão
        sanitized.Emails = caseData.Emails
            .Where(e => visibleEmailIds.Contains(e.EmailId))
            .ToList();

        // Suspects sempre visíveis (podem ter conditional visibility futuramente)
        sanitized.Suspects = caseData.Suspects.ToList();

        _logger.LogInformation(
            "Case sanitized: {AssetsCount}/{TotalAssets} assets, {EmailsCount}/{TotalEmails} emails visible",
            sanitized.Assets.Count, caseData.Assets.Count,
            sanitized.Emails.Count, caseData.Emails.Count);

        return sanitized;
    }
}
