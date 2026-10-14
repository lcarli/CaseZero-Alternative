using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using Microsoft.EntityFrameworkCore;
using CaseZero.Progression;
using CaseZeroApi.Models.CaseV2;

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
        if (caseData is null) throw new InvalidDataException($"Case '{caseId}' was not found.");

        var session = await _context.CaseSessions
            .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
            .OrderByDescending(cs => cs.SessionStart)
            .ThenByDescending(cs => cs.Id)
            .FirstOrDefaultAsync(ct);
        if (session is null)
        {
            _logger.LogDebug("No active session for user {UserId} case {CaseId} — rules not applied", userId, caseId);
            return;
        }

        var firedRuleIds = ParseList(session.FiredRuleIds);
        var observed = Parse<List<ObservedCaseTrigger>>(session.ObservedTriggers);
        var fact = ObservedCaseTrigger.From(trigger);
        if (fact is not null && !observed.Contains(fact)) observed.Add(fact);
        session.ObservedTriggers = JsonSerializer.Serialize(observed);
        if (trigger is AttachmentDownloadTrigger attachment)
            await ApplyActionsAsync(new[] { new CaseV2Action { Type = "reveal_asset", AssetId = attachment.AssetId } },
                session, userId, caseId, ct);
        if (trigger is TimeElapsedTrigger elapsed)
        {
            session.GameTimeMinutes = Math.Max(session.GameTimeMinutes, elapsed.GameTimeMinutes);
            await ApplyTemporalEventsAsync(caseData, session, userId, caseId, ct);
        }

        foreach (var rule in caseData.Rules ?? new())
        {
            if (firedRuleIds.Contains(rule.RuleId)) continue;
            if (!RuleConditions.Satisfied(rule.Trigger, c => LeafMatches(c, observed, session.GameTimeMinutes))) continue;

            await ApplyActionsAsync(rule.Actions, session, userId, caseId, ct);
            firedRuleIds.Add(rule.RuleId);
            _logger.LogInformation("Rule {RuleId} fired for user {UserId} case {CaseId}", rule.RuleId, userId, caseId);
        }

        session.FiredRuleIds = JsonSerializer.Serialize(firedRuleIds);
        await _context.SaveChangesAsync(ct);
    }

    private static bool LeafMatches(CaseV2Trigger def, List<ObservedCaseTrigger> observed, double elapsed)
    {
        if (def.Type == "time_elapsed") return def.AtMinutes.HasValue && elapsed >= def.AtMinutes.Value;
        return observed.Any(f => def.Type switch
        {
            "forensics_complete" when f.Type == def.Type =>
                def.InputAssetId == f.AssetId &&
                string.Equals(def.AnalysisType, f.AnalysisType, StringComparison.OrdinalIgnoreCase),
            "attachment_download" when f.Type == def.Type =>
                (def.EmailId is null || def.EmailId == f.EmailId) &&
                (def.AssetId is null || def.AssetId == f.AssetId),
            "asset_viewed" when f.Type == def.Type => def.AssetId == f.AssetId || def.InputAssetId == f.AssetId,
            "email_opened" when f.Type == def.Type => def.EmailId == f.EmailId,
            "suspect_viewed" when f.Type == def.Type => def.SuspectId == f.SuspectId,
            _ => false
        });
    }

    private async Task ApplyTemporalEventsAsync(CaseV2 caseData, CaseSession session, string userId, string caseId, CancellationToken ct)
    {
        var fired = ParseList(session.FiredTemporalEventIds);
        foreach (var item in caseData.TemporalEvents ?? new())
        {
            if (item.TriggerAtMinutes > session.GameTimeMinutes || fired.Contains(item.Id)) continue;
            var actions = new List<CaseV2Action>();
            if (!string.IsNullOrEmpty(item.Payload?.EmailId))
                actions.Add(new() { Type = "reveal_email", EmailId = item.Payload.EmailId });
            if (!string.IsNullOrEmpty(item.Payload?.AssetId))
                actions.Add(new() { Type = "reveal_asset", AssetId = item.Payload.AssetId });
            if (!string.IsNullOrEmpty(item.Payload?.Message))
                actions.Add(new() { Type = "send_notification", Message = item.Payload.Message, Level = "info" });
            await ApplyActionsAsync(actions, session, userId, caseId, ct);
            fired.Add(item.Id);
        }
        session.FiredTemporalEventIds = JsonSerializer.Serialize(fired);
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
                        var exists = _context.CaseSessionVisibleEmails.Local.Any(v =>
                            v.UserId == userId && v.CaseId == caseId && v.EmailId == action.EmailId) ||
                            await _context.CaseSessionVisibleEmails
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
                        var exists = _context.CaseSessionVisibleAssets.Local.Any(v =>
                            v.UserId == userId && v.CaseId == caseId && v.AssetId == action.AssetId) ||
                            await _context.CaseSessionVisibleAssets
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
                    throw new InvalidDataException($"Unknown or malformed rule action '{action.Type}'.");
            }
        }
    }

    private static List<string> ParseList(string? json)
    {
        return Parse<List<string>>(json);
    }

    private static Dictionary<string, string> ParseDict(string? json)
    {
        return Parse<Dictionary<string, string>>(json);
    }

    private static Dictionary<string, bool> ParseBoolDict(string? json)
    {
        return Parse<Dictionary<string, bool>>(json);
    }

    private static Dictionary<string, List<string>> ParseStringListDict(string? json)
    {
        return Parse<Dictionary<string, List<string>>>(json);
    }

    private static List<SessionNotification> ParseNotifications(string? json)
    {
        return Parse<List<SessionNotification>>(json);
    }

    private static T Parse<T>(string? json) where T : new() =>
        string.IsNullOrWhiteSpace(json) ? new() :
            JsonSerializer.Deserialize<T>(json) ?? throw new InvalidDataException("Session progression state is null.");

    public record SessionNotification(string Level, string Message, DateTime At);
}
