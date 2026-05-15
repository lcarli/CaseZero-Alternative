using CaseZeroApi.Models.CaseV1;
using CaseZeroApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

/// <summary>
/// Serviço de sanitização de casos v1.0
/// 🔒 CRITICAL: Remove informações sensíveis que NUNCA devem ser expostas ao cliente
/// Remove: solution, solutionStub, culpritId, rules[], e filtra assets/emails hidden não desbloqueados
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
    /// Sanitização básica - remove rules e filtra por visibility="initial"
    /// Também sanitiza metadata perigosa
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
            Metadata = SanitizeMetadata(caseData.Metadata),
            ForensicsDefaults = caseData.ForensicsDefaults,
            Rules = null, // 🔒 NUNCA expor ao cliente
            Assets = caseData.Assets
                .Where(a => a.Visibility == "initial")
                .Select(a => SanitizeAsset(a))
                .ToList(),
            Emails = caseData.Emails
                .Where(e => e.Visibility == "initial")
                .Select(e => SanitizeEmail(e))
                .ToList(),
            Suspects = caseData.Suspects
                .Where(s => s.Visibility == "initial")
                .Select(s => SanitizeSuspect(s))
                .ToList()
        };
    }

    public async Task<CaseV1> SanitizeCaseForClientAsync(CaseV1 caseData, string userId, string caseId)
    {
        if (caseData == null)
        {
            throw new ArgumentNullException(nameof(caseData));
        }

        _logger.LogInformation(
            "🔒 Sanitizing case {CaseId} for user {UserId}",
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

        _logger.LogInformation(
            "📊 User {UserId} has access to {AssetCount} assets and {EmailCount} emails in case {CaseId}",
            userId, visibleAssetIds.Count, visibleEmailIds.Count, caseId);

        // Criar cópia sanitizada
        var sanitized = new CaseV1
        {
            Version = caseData.Version,
            CaseId = caseData.CaseId,
            Metadata = SanitizeMetadata(caseData.Metadata),
            ForensicsDefaults = caseData.ForensicsDefaults
        };

        // 🔒 CRÍTICO: NUNCA expor rules ao cliente
        sanitized.Rules = null;

        // Filtrar assets: apenas os visíveis na sessão + sanitização
        sanitized.Assets = caseData.Assets
            .Where(a => visibleAssetIds.Contains(a.AssetId))
            .Select(a => SanitizeAsset(a))
            .ToList();

        // Filtrar emails: apenas os visíveis na sessão + sanitização
        sanitized.Emails = caseData.Emails
            .Where(e => visibleEmailIds.Contains(e.EmailId))
            .Select(e => SanitizeEmail(e))
            .ToList();

        // Filtrar suspects: manter todos + sanitização
        sanitized.Suspects = caseData.Suspects
            .Select(s => SanitizeSuspect(s))
            .ToList();

        _logger.LogInformation(
            "✅ Case sanitized - Removed rules, filtered to {AssetCount} visible assets and {EmailCount} visible emails",
            sanitized.Assets.Count, sanitized.Emails.Count);

        return sanitized;
    }

    /// <summary>
    /// Sanitize metadata - remove any fields containing "solution", "answer", "culprit"
    /// </summary>
    private CaseMetadata SanitizeMetadata(CaseMetadata metadata)
    {
        // Keep all safe metadata fields
        // CaseMetadata doesn't contain solution/culprit fields in current schema
        return new CaseMetadata
        {
            Title = metadata.Title,
            Description = metadata.Description,
            Difficulty = metadata.Difficulty,
            EstimatedTimeMinutes = metadata.EstimatedTimeMinutes,
            RequiredRank = metadata.RequiredRank,
            Location = metadata.Location,
            IncidentDate = metadata.IncidentDate,
            Category = metadata.Category,
            Briefing = metadata.Briefing,
            Victim = metadata.Victim
        };
    }

    /// <summary>
    /// Sanitize asset - remove any dangerous metadata
    /// </summary>
    private Asset SanitizeAsset(Asset asset)
    {
        return new Asset
        {
            AssetId = asset.AssetId,
            Name = asset.Name,
            Type = asset.Type,
            Category = asset.Category,
            Description = asset.Description,
            FilePath = asset.FilePath,
            Visibility = asset.Visibility,
            // Filter metadata to remove any "solution" or "answer" fields
            Metadata = SanitizeDictionaryMetadata(asset.Metadata)
        };
    }

    /// <summary>
    /// Sanitize email - remove any dangerous metadata
    /// </summary>
    private Email SanitizeEmail(Email email)
    {
        return new Email
        {
            EmailId = email.EmailId,
            From = email.From,
            To = email.To,
            Subject = email.Subject,
            SentAt = email.SentAt,
            Priority = email.Priority,
            Visibility = email.Visibility,
            Content = email.Content,
            Attachments = email.Attachments,
            // Filter metadata to remove any "solution" or "answer" fields
            Metadata = SanitizeDictionaryMetadata(email.Metadata)
        };
    }

    /// <summary>
    /// Sanitize suspect - remove any spoiler fields
    /// </summary>
    private Suspect SanitizeSuspect(Suspect suspect)
    {
        return new Suspect
        {
            SuspectId = suspect.SuspectId,
            Name = suspect.Name,
            Age = suspect.Age,
            Occupation = suspect.Occupation,
            Relationship = suspect.Relationship,
            Motive = suspect.Motive,
            Alibi = suspect.Alibi,
            AlibiVerified = suspect.AlibiVerified,
            Background = suspect.Background,
            LinkedAssets = suspect.LinkedAssets,
            Visibility = suspect.Visibility
        };
    }

    /// <summary>
    /// Sanitize generic metadata dictionary - remove dangerous keys
    /// 🔒 CRITICAL: Blocks keys containing: solution, answer, culprit, correct
    /// </summary>
    private Dictionary<string, object>? SanitizeDictionaryMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata == null) return null;

        var dangerousKeys = new[] { "solution", "solutionStub", "answer", "culprit", "culpritId", "correct", "isCorrect" };
        
        var sanitized = new Dictionary<string, object>();
        foreach (var kvp in metadata)
        {
            // Skip keys that might contain spoilers
            if (dangerousKeys.Any(dk => kvp.Key.Contains(dk, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("🚨 BLOCKED dangerous metadata key: {Key}", kvp.Key);
                continue;
            }
            
            sanitized[kvp.Key] = kvp.Value;
        }

        return sanitized;
    }
}
