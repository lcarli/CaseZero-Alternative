namespace CaseZeroApi.Models;

/// <summary>
/// P86: Audit log para rastreamento de ações críticas
/// Registra: asset downloads, email opens, forensics requests, solution submissions
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    
    /// <summary>
    /// Usuário que executou a ação
    /// </summary>
    public required string UserId { get; set; }
    
    /// <summary>
    /// Tipo de ação executada
    /// Exemplos: "asset_download", "email_open", "forensics_request", "solution_submit"
    /// </summary>
    public required string Action { get; set; }
    
    /// <summary>
    /// Recurso afetado pela ação
    /// Formato: "case_id/resource_type/resource_id"
    /// Exemplo: "case_001/asset/asset.evidence_photo"
    /// </summary>
    public required string Resource { get; set; }
    
    /// <summary>
    /// Caso relacionado à ação (para queries rápidas)
    /// </summary>
    public string? CaseId { get; set; }
    
    /// <summary>
    /// Timestamp da ação (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Detalhes adicionais em JSON (opcional)
    /// Exemplos: {"ipAddress": "192.168.1.1", "userAgent": "..."}
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// Resultado da ação: success, failure, unauthorized
    /// </summary>
    public string Result { get; set; } = "success";
    
    /// <summary>
    /// Relacionamento com usuário
    /// </summary>
    public User? User { get; set; }
}
