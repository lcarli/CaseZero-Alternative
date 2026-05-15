using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

/// <summary>
/// Rules engine for case.json v2. Evaluates triggers, applies matching rules'
/// actions to the user's session. Idempotent per (ruleId, sessionId) — once a
/// rule fires for a session, repeated triggers don't double-apply.
/// See docs/CASE_JSON_V2_SPEC.md §15 for the trigger/action catalogue.
/// </summary>
public class RulesEngineService : IRulesEngineService
{
    private readonly ApplicationDbContext _context;
    private readonly ICaseV2StorageService _v2Storage;
    private readonly ILogger<RulesEngineService> _logger;

    public RulesEngineService(
        ApplicationDbContext context,
        ICaseV2StorageService v2Storage,
        ILogger<RulesEngineService> logger)
    {
        _context = context;
        _v2Storage = v2Storage;
        _logger = logger;
    }

    public async Task EvaluateAndApplyAsync(
        string caseId,
        string userId,
        RuleTrigger trigger,
        CancellationToken ct = default)
    {
        var caseData = await _v2Storage.GetRawAsync(caseId, ct);
        if (caseData?.Rules is null || caseData.Rules.Count == 0) return;

        var session = await _context.CaseSessions
            .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
            .OrderByDescending(cs => cs.SessionStart)
            .FirstOrDefaultAsync(ct);
        if (session is null)
        {
            _logger.LogDebug("No active session for user {UserId} case {CaseId} — rules not applied", userId, caseId);
            return;
        }

        var firedRuleIds = ParseList(session.FiredRuleIds);
        var dirty = false;

        foreach (var rule in caseData.Rules)
        {
            if (firedRuleIds.Contains(rule.RuleId)) continue;
            if (!TriggerMatches(rule.Trigger, trigger)) continue;

            await ApplyActionsAsync(rule.Actions, session, userId, caseId, ct);
            firedRuleIds.Add(rule.RuleId);
            dirty = true;
            _logger.LogInformation("Rule {RuleId} fired for user {UserId} case {CaseId}", rule.RuleId, userId, caseId);
        }

        if (dirty)
        {
            session.FiredRuleIds = JsonSerializer.Serialize(firedRuleIds);
            await _context.SaveChangesAsync(ct);
        }
    }

    private static bool TriggerMatches(Models.CaseV2.CaseV2Trigger def, RuleTrigger fired)
    {
        return def.Type switch
        {
            "forensics_complete" when fired is ForensicsCompleteTrigger f =>
                def.InputAssetId == f.InputAssetId &&
                string.Equals(def.AnalysisType, f.AnalysisType, StringComparison.OrdinalIgnoreCase),

            "attachment_download" when fired is AttachmentDownloadTrigger a =>
                (def.EmailId is null || def.EmailId == a.EmailId) &&
                (def.AssetId is null || def.AssetId == a.AssetId),

            "asset_viewed" when fired is AssetViewedTrigger v =>
                def.AssetId == v.AssetId || def.InputAssetId == v.AssetId,

            "email_opened" when fired is EmailOpenedTrigger e =>
                def.EmailId == e.EmailId,

            "suspect_viewed" when fired is SuspectViewedTrigger s =>
                def.SuspectId == s.SuspectId,

            "time_elapsed" when fired is TimeElapsedTrigger t =>
                def.AtMinutes.HasValue && t.GameTimeMinutes >= def.AtMinutes.Value,

            "multiple_conditions" =>
                EvaluateMultiple(def, fired),

            _ => false
        };
    }

    private static bool EvaluateMultiple(Models.CaseV2.CaseV2Trigger def, RuleTrigger fired)
    {
        if (def.Conditions is null || def.Conditions.Count == 0) return false;
        if (string.Equals(def.Operator, "OR", StringComparison.OrdinalIgnoreCase))
            return def.Conditions.Any(c => TriggerMatches(c, fired));
        return def.Conditions.All(c => TriggerMatches(c, fired));
    }

    private async Task ApplyActionsAsync(
        IEnumerable<Models.CaseV2.CaseV2Action> actions,
        CaseSession session,
        string userId,
        string caseId,
        CancellationToken ct)
    {
        foreach (var action in actions)
        {
            switch (action.Type)
            {
                case "reveal_email" when !string.IsNullOrEmpty(action.EmailId):
                    {
                        var exists = await _context.CaseSessionVisibleEmails
                            .AnyAsync(v => v.UserId == userId && v.CaseId == caseId && v.EmailId == action.EmailId, ct);
                        if (!exists)
                            _context.CaseSessionVisibleEmails.Add(new CaseSessionVisibleEmail
                            {
                                UserId = userId,
                                CaseId = caseId,
                                EmailId = action.EmailId!,
                                UnlockedAt = DateTime.UtcNow
                            });
                        break;
                    }
                case "reveal_asset" when !string.IsNullOrEmpty(action.AssetId):
                    {
                        var exists = await _context.CaseSessionVisibleAssets
                            .AnyAsync(v => v.UserId == userId && v.CaseId == caseId && v.AssetId == action.AssetId, ct);
                        if (!exists)
                            _context.CaseSessionVisibleAssets.Add(new CaseSessionVisibleAsset
                            {
                                UserId = userId,
                                CaseId = caseId,
                                AssetId = action.AssetId!,
                                UnlockedAt = DateTime.UtcNow
                            });
                        break;
                    }
                case "reveal_suspect" when !string.IsNullOrEmpty(action.SuspectId):
                    {
                        var revealed = ParseList(session.RevealedSuspectIds);
                        if (!revealed.Contains(action.SuspectId!))
                        {
                            revealed.Add(action.SuspectId!);
                            session.RevealedSuspectIds = JsonSerializer.Serialize(revealed);
                        }
                        break;
                    }
                case "add_email_attachment" when !string.IsNullOrEmpty(action.EmailId) && !string.IsNullOrEmpty(action.AssetId):
                    {
                        var overrides = ParseStringListDict(session.EmailAttachmentOverrides);
                        if (!overrides.TryGetValue(action.EmailId!, out var list)) list = new();
                        if (!list.Contains(action.AssetId!)) list.Add(action.AssetId!);
                        overrides[action.EmailId!] = list;
                        session.EmailAttachmentOverrides = JsonSerializer.Serialize(overrides);
                        break;
                    }
                case "send_notification" when !string.IsNullOrEmpty(action.Message):
                    {
                        var list = ParseNotifications(session.Notifications);
                        list.Add(new SessionNotification(action.Level ?? "info", action.Message!, DateTime.UtcNow));
                        session.Notifications = JsonSerializer.Serialize(list);
                        break;
                    }
                case "update_suspect_status" when !string.IsNullOrEmpty(action.SuspectId) && !string.IsNullOrEmpty(action.Status):
                    {
                        var dict = ParseDict(session.SuspectStatusOverrides);
                        dict[action.SuspectId!] = action.Status!;
                        session.SuspectStatusOverrides = JsonSerializer.Serialize(dict);
                        break;
                    }
                case "mark_alibi_verified" when !string.IsNullOrEmpty(action.SuspectId):
                    {
                        var dict = ParseBoolDict(session.SuspectAlibiVerified);
                        dict[action.SuspectId!] = action.Verified ?? true;
                        session.SuspectAlibiVerified = JsonSerializer.Serialize(dict);
                        break;
                    }
                default:
                    _logger.LogWarning("Unknown or malformed action type {Type}", action.Type);
                    break;
            }
        }
    }

    private static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); }
    }

    private static Dictionary<string, string> ParseDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); } catch { return new(); }
    }

    private static Dictionary<string, bool> ParseBoolDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new(); } catch { return new(); }
    }

    private static Dictionary<string, List<string>> ParseStringListDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? new(); } catch { return new(); }
    }

    private static List<SessionNotification> ParseNotifications(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<SessionNotification>>(json) ?? new(); } catch { return new(); }
    }

    public record SessionNotification(string Level, string Message, DateTime At);
}
