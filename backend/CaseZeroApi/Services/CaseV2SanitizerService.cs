using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

public class CaseV2SanitizerService : ICaseV2SanitizerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CaseV2SanitizerService> _logger;

    public CaseV2SanitizerService(ApplicationDbContext context, ILogger<CaseV2SanitizerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public CaseV2Sanitized Sanitize(CaseV2 caseData)
    {
        ArgumentNullException.ThrowIfNull(caseData);

        return new CaseV2Sanitized
        {
            Version = caseData.Version,
            CaseId = caseData.CaseId,
            Metadata = caseData.Metadata,
            Assets = caseData.Assets.Where(a => caseData.Metadata.IsAllInitial || a.Visibility == "initial").ToList(),
            Emails = caseData.Emails.Where(e => caseData.Metadata.IsAllInitial || e.Visibility == "initial").ToList(),
            Suspects = caseData.Suspects.Where(s => caseData.Metadata.IsAllInitial || s.Visibility == "initial").ToList(),
            Timeline = caseData.Timeline,
            ForensicsDefaults = caseData.ForensicsDefaults is null
                ? null
                : new CaseV2ForensicsDefaultsClient { AnalysisTypes = caseData.ForensicsDefaults.AnalysisTypes },
            Solution = caseData.Solution is null ? null : ToSanitizedSolution(caseData.Solution),
            GameMetadata = caseData.GameMetadata is null ? null : ToClientGameMetadata(caseData.GameMetadata)
        };
    }

    public async Task<CaseV2Sanitized> SanitizeForUserAsync(CaseV2 caseData, string userId, string caseId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caseData);

        var visibleAssetIds = await _context.CaseSessionVisibleAssets
            .Where(va => va.UserId == userId && va.CaseId == caseId)
            .Select(va => va.AssetId)
            .ToListAsync(ct);

        var visibleEmailIds = await _context.CaseSessionVisibleEmails
            .Where(ve => ve.UserId == userId && ve.CaseId == caseId)
            .Select(ve => ve.EmailId)
            .ToListAsync(ct);

        var session = await _context.CaseSessions
            .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
            .OrderByDescending(cs => cs.SessionStart)
            .ThenByDescending(cs => cs.Id)
            .FirstOrDefaultAsync(ct);

        var revealedSuspectIds = ParseStringList(session?.RevealedSuspectIds);
        var attachmentOverrides = ParseDict<List<string>>(session?.EmailAttachmentOverrides);
        var statusOverrides = ParseDict<string>(session?.SuspectStatusOverrides);
        var alibiOverrides = ParseDict<bool>(session?.SuspectAlibiVerified);
        var syntheticEmails = ParseList<CaseV2Email>(session?.SyntheticEmails);
        var emailStates = await _context.CaseSessionEmailStates
            .Where(s => s.UserId == userId && s.CaseId == caseId)
            .ToDictionaryAsync(s => s.EmailId, s => new CaseZeroApi.DTOs.EmailStateDto
            {
                EmailId = s.EmailId, ReadAt = s.ReadAt, OpenCount = s.OpenCount
            }, ct);

        // Assets: visible = those flagged 'initial' OR present in session visible list
        var assets = caseData.Assets
            .Where(a => caseData.Metadata.IsAllInitial || a.Visibility == "initial" || visibleAssetIds.Contains(a.Id))
            .ToList();

        // Emails: same logic + apply attachment overrides + add synthetic emails
        var emails = caseData.Emails
            .Where(e => caseData.Metadata.IsAllInitial || e.Visibility == "initial" || visibleEmailIds.Contains(e.Id))
            .Select(e =>
            {
                if (attachmentOverrides.TryGetValue(e.Id, out var extra) && extra is not null && extra.Count > 0)
                {
                    var attachments = e.Attachments is null ? new List<string>() : new List<string>(e.Attachments);
                    foreach (var a in extra) if (!attachments.Contains(a)) attachments.Add(a);
                    return new CaseV2Email
                    {
                        Id = e.Id,
                        From = e.From,
                        To = e.To,
                        Subject = e.Subject,
                        Body = e.Body,
                        SentAt = e.SentAt,
                        Priority = e.Priority,
                        Attachments = attachments,
                        Visibility = e.Visibility,
                        Metadata = e.Metadata
                    };
                }
                return e;
            })
            .ToList();

        if (syntheticEmails is not null) emails.AddRange(syntheticEmails);

        // Suspects: initial + revealed; apply status & alibi overrides
        var suspects = caseData.Suspects
            .Where(s => caseData.Metadata.IsAllInitial || s.Visibility == "initial" || revealedSuspectIds.Contains(s.Id))
            .Select(s => ApplyOverrides(s, statusOverrides, alibiOverrides))
            .ToList();

        return new CaseV2Sanitized
        {
            Version = caseData.Version,
            CaseId = caseData.CaseId,
            Metadata = caseData.Metadata,
            Assets = assets,
            Emails = emails,
            Suspects = suspects,
            Notifications = ParseList<RulesEngineService.SessionNotification>(session?.Notifications) ?? new(),
            EmailStates = emailStates,
            Timeline = caseData.Timeline,
            ForensicsDefaults = caseData.ForensicsDefaults is null
                ? null
                : new CaseV2ForensicsDefaultsClient { AnalysisTypes = caseData.ForensicsDefaults.AnalysisTypes },
            Solution = caseData.Solution is null ? null : ToSanitizedSolution(caseData.Solution),
            GameMetadata = caseData.GameMetadata is null ? null : ToClientGameMetadata(caseData.GameMetadata)
        };
    }

    private static CaseV2Suspect ApplyOverrides(CaseV2Suspect s, Dictionary<string, string> status, Dictionary<string, bool> alibi)
    {
        var clone = new CaseV2Suspect
        {
            Id = s.Id,
            Name = s.Name,
            Alias = s.Alias,
            Age = s.Age,
            Occupation = s.Occupation,
            Relationship = s.Relationship,
            Description = s.Description,
            Motive = s.Motive,
            Alibi = s.Alibi,
            AlibiVerified = s.AlibiVerified,
            Status = s.Status,
            Background = s.Background,
            RelatedAssets = s.RelatedAssets,
            Photo = s.Photo,
            Visibility = s.Visibility
        };
        if (status.TryGetValue(s.Id, out var st)) clone.Status = st;
        if (alibi.TryGetValue(s.Id, out var av)) clone.AlibiVerified = av;
        return clone;
    }

    private static SanitizedSolution ToSanitizedSolution(CaseV2Solution sol) => new()
    {
        MinimumScore = sol.MinimumScore,
        MaxAttempts = sol.MaxAttempts,
        Questions = sol.Questions.Select(q => new SanitizedQuestion
        {
            Id = q.Id,
            Prompt = q.Prompt,
            Options = q.Options,
            Weight = q.Weight
        }).ToList()
    };

    private static CaseV2GameMetadataClient ToClientGameMetadata(CaseV2GameMetadata g) => new()
    {
        SchemaVersion = g.SchemaVersion,
        CreatedAt = g.CreatedAt,
        Tags = g.Tags,
        ContentWarnings = g.ContentWarnings,
        Localizations = g.Localizations
    };

    private List<string> ParseStringList(string? json)
    {
        return Parse<List<string>>(json);
    }

    private List<T> ParseList<T>(string? json)
    {
        return Parse<List<T>>(json);
    }

    private Dictionary<string, T> ParseDict<T>(string? json)
    {
        return Parse<Dictionary<string, T>>(json);
    }

    private T Parse<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidDataException("Session progression state is null."); }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid persisted session progression state");
            throw;
        }
    }
}
