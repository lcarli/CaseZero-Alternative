using CaseZeroApi.Models.CaseV1;

namespace CaseZeroApi.Services;

/// <summary>
/// Serviço de sanitização de casos v1.0
/// Remove informações sensíveis que NUNCA devem ser expostas ao cliente
/// </summary>
public interface ICaseV1SanitizerService
{
    /// <summary>
    /// Sanitiza um caso removendo dados sensíveis e aplicando filtros de visibilidade
    /// </summary>
    CaseV1 SanitizeCaseForClient(CaseV1 caseData, string userId, string caseSessionId);
}

public class CaseV1SanitizerService : ICaseV1SanitizerService
{
    private readonly ILogger<CaseV1SanitizerService> _logger;

    public CaseV1SanitizerService(ILogger<CaseV1SanitizerService> logger)
    {
        _logger = logger;
    }

    public CaseV1 SanitizeCaseForClient(CaseV1 caseData, string userId, string caseSessionId)
    {
        if (caseData == null)
        {
            throw new ArgumentNullException(nameof(caseData));
        }

        _logger.LogInformation(
            "Sanitizing case {CaseId} for user {UserId} session {SessionId}",
            caseData.CaseId, userId, caseSessionId);

        // Criar cópia para não modificar o original
        var sanitized = new CaseV1
        {
            Version = caseData.Version,
            CaseId = caseData.CaseId,
            Metadata = caseData.Metadata,
            ForensicsDefaults = caseData.ForensicsDefaults
        };

        // 🔒 CRÍTICO: REMOVER RULES (server-side apenas)
        sanitized.Rules = null;

        // TODO: Filtrar assets por visibilidade da sessão
        // Por enquanto, retornar apenas assets com visibility="initial"
        sanitized.Assets = caseData.Assets
            .Where(a => a.Visibility == "initial")
            .ToList();

        // TODO: Filtrar emails por visibilidade da sessão
        // Por enquanto, retornar apenas emails com visibility="initial"
        sanitized.Emails = caseData.Emails
            .Where(e => e.Visibility == "initial")
            .ToList();

        // TODO: Filtrar suspects por visibilidade da sessão
        // Por enquanto, retornar apenas suspects com visibility="initial"
        sanitized.Suspects = caseData.Suspects
            .Where(s => s.Visibility == "initial")
            .ToList();

        _logger.LogInformation(
            "Case sanitized: {AssetsCount} assets, {EmailsCount} emails, {SuspectsCount} suspects visible",
            sanitized.Assets.Count, sanitized.Emails.Count, sanitized.Suspects.Count);

        return sanitized;
    }
}
