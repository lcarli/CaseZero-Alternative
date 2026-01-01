using CaseZeroApi.Data;
using CaseZeroApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

/// <summary>
/// P86: Implementação do serviço de audit log
/// Registra todas as ações críticas para compliance e debugging
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        ApplicationDbContext context, 
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogActionAsync(
        string userId, 
        string action, 
        string resource,
        string? caseId = null,
        string result = "success",
        string? details = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                Resource = resource,
                CaseId = caseId,
                Result = result,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "📝 Audit: {Action} by {UserId} on {Resource} - {Result}",
                action, userId, resource, result);
        }
        catch (Exception ex)
        {
            // Não falhar a operação principal se audit log falhar
            _logger.LogError(ex, 
                "Failed to write audit log: {Action} by {UserId} on {Resource}",
                action, userId, resource);
        }
    }

    public async Task<List<AuditLog>> GetUserLogsAsync(string userId, int limit = 100)
    {
        return await _context.AuditLogs
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetCaseLogsAsync(string caseId, int limit = 100)
    {
        return await _context.AuditLogs
            .Where(log => log.CaseId == caseId)
            .OrderByDescending(log => log.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetActionLogsAsync(string action, int limit = 100)
    {
        return await _context.AuditLogs
            .Where(log => log.Action == action)
            .OrderByDescending(log => log.Timestamp)
            .Take(limit)
            .ToListAsync();
    }
}
