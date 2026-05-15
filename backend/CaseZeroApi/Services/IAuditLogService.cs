namespace CaseZeroApi.Services;

/// <summary>
/// P86: Serviço para registrar ações críticas no audit log
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Registra uma ação no audit log
    /// </summary>
    /// <param name="userId">ID do usuário que executou a ação</param>
    /// <param name="action">Tipo de ação (asset_download, email_open, etc.)</param>
    /// <param name="resource">Recurso afetado (formato: case_id/type/id)</param>
    /// <param name="caseId">ID do caso (opcional, para queries rápidas)</param>
    /// <param name="result">Resultado da ação (success, failure, unauthorized)</param>
    /// <param name="details">Detalhes adicionais em JSON (opcional)</param>
    Task LogActionAsync(
        string userId, 
        string action, 
        string resource,
        string? caseId = null,
        string result = "success",
        string? details = null);

    /// <summary>
    /// Busca logs de auditoria por usuário
    /// </summary>
    Task<List<Models.AuditLog>> GetUserLogsAsync(string userId, int limit = 100);

    /// <summary>
    /// Busca logs de auditoria por caso
    /// </summary>
    Task<List<Models.AuditLog>> GetCaseLogsAsync(string caseId, int limit = 100);

    /// <summary>
    /// Busca logs de auditoria por tipo de ação
    /// </summary>
    Task<List<Models.AuditLog>> GetActionLogsAsync(string action, int limit = 100);
}
