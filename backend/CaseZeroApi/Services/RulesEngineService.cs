using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

/// <summary>
/// Implementation of Rules Engine v1
/// Evaluates rules from case.json and applies actions to reveal content
/// </summary>
public class RulesEngineService : IRulesEngineService
{
    private readonly ApplicationDbContext _context;
    private readonly ICaseV1StorageService _storageService;
    private readonly ICaseV2StorageService _v2Storage;
    private readonly ILogger<RulesEngineService> _logger;

    public RulesEngineService(
        ApplicationDbContext context,
        ICaseV1StorageService storageService,
        ICaseV2StorageService v2Storage,
        ILogger<RulesEngineService> logger)
    {
        _context = context;
        _storageService = storageService;
        _v2Storage = v2Storage;
        _logger = logger;
    }

    /// <summary>
    /// Task 41: Evaluate forensic rule by loading case.json and finding matching rule
    /// </summary>
    public async Task<ForensicRule?> EvaluateForensicRuleAsync(string caseId, string inputAssetId, string analysisType)
    {
        try
        {
            // Load case.json from blob storage (uses cache from D25)
            var caseJsonContent = await _storageService.GetCaseJsonAsync(caseId);
            if (string.IsNullOrEmpty(caseJsonContent))
            {
                _logger.LogWarning("case.json not found for caseId={CaseId}", caseId);
                return null;
            }

            var caseJson = JsonSerializer.Deserialize<CaseJsonV1>(caseJsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (caseJson?.Rules?.Forensics == null || caseJson.Rules.Forensics.Count == 0)
            {
                _logger.LogWarning("No forensic rules defined in case.json for caseId={CaseId}", caseId);
                return null;
            }

            // Find matching rule
            var matchingRule = caseJson.Rules.Forensics.FirstOrDefault(r =>
                r.InputAssetId == inputAssetId &&
                string.Equals(r.AnalysisType, analysisType, StringComparison.OrdinalIgnoreCase));

            if (matchingRule != null)
            {
                _logger.LogInformation(
                    "Rule matched: InputAssetId={InputAssetId}, AnalysisType={AnalysisType}, Action={Action}",
                    matchingRule.InputAssetId, matchingRule.AnalysisType, matchingRule.Action);
            }

            return matchingRule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating forensic rule for caseId={CaseId}", caseId);
            return null;
        }
    }

    /// <summary>
    /// Task 43: Apply reveal_email action - insert into CaseSessionVisibleEmail
    /// </summary>
    public async Task ApplyRevealEmailActionAsync(string userId, string caseId, string emailId)
    {
        try
        {
            // Check if already visible
            var exists = await _context.CaseSessionVisibleEmails
                .AnyAsync(e => e.UserId == userId && e.CaseId == caseId && e.EmailId == emailId);

            if (exists)
            {
                _logger.LogInformation(
                    "Email already visible: UserId={UserId}, CaseId={CaseId}, EmailId={EmailId}",
                    userId, caseId, emailId);
                return;
            }

            // Insert new visible email
            var visibleEmail = new CaseSessionVisibleEmail
            {
                UserId = userId,
                CaseId = caseId,
                EmailId = emailId,
                UnlockedAt = DateTime.UtcNow
            };

            _context.CaseSessionVisibleEmails.Add(visibleEmail);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Email revealed: UserId={UserId}, CaseId={CaseId}, EmailId={EmailId}",
                userId, caseId, emailId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to reveal email: UserId={UserId}, CaseId={CaseId}, EmailId={EmailId}",
                userId, caseId, emailId);
            throw;
        }
    }

    /// <summary>
    /// Task 44: Apply reveal_asset action - insert into CaseSessionVisibleAsset
    /// </summary>
    public async Task ApplyRevealAssetActionAsync(string userId, string caseId, string assetId)
    {
        try
        {
            // Check if already visible
            var exists = await _context.CaseSessionVisibleAssets
                .AnyAsync(a => a.UserId == userId && a.CaseId == caseId && a.AssetId == assetId);

            if (exists)
            {
                _logger.LogInformation(
                    "Asset already visible: UserId={UserId}, CaseId={CaseId}, AssetId={AssetId}",
                    userId, caseId, assetId);
                return;
            }

            // Insert new visible asset
            var visibleAsset = new CaseSessionVisibleAsset
            {
                UserId = userId,
                CaseId = caseId,
                AssetId = assetId,
                UnlockedAt = DateTime.UtcNow
            };

            _context.CaseSessionVisibleAssets.Add(visibleAsset);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Asset revealed: UserId={UserId}, CaseId={CaseId}, AssetId={AssetId}",
                userId, caseId, assetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to reveal asset: UserId={UserId}, CaseId={CaseId}, AssetId={AssetId}",
                userId, caseId, assetId);
            throw;
        }
    }

    /// <summary>
    /// Task 45: Apply add_email_attachment action - update email JSON with new attachment
    /// </summary>
    public async Task ApplyAddEmailAttachmentActionAsync(string caseId, string emailId, string assetId)
    {
        try
        {
            // Load email from blob storage
            var emailContent = await _storageService.GetEmailAsync(caseId, emailId);
            if (string.IsNullOrEmpty(emailContent))
            {
                _logger.LogWarning("Email not found: CaseId={CaseId}, EmailId={EmailId}", caseId, emailId);
                return;
            }

            var email = JsonSerializer.Deserialize<Dictionary<string, object>>(emailContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (email == null)
            {
                _logger.LogWarning("Failed to deserialize email: CaseId={CaseId}, EmailId={EmailId}", caseId, emailId);
                return;
            }

            // Add or update attachments array
            if (!email.ContainsKey("attachments"))
            {
                email["attachments"] = new List<string>();
            }

            var attachments = email["attachments"] as List<string> ?? new List<string>();
            if (!attachments.Contains(assetId))
            {
                attachments.Add(assetId);
                email["attachments"] = attachments;

                // Save updated email back to blob storage
                var updatedContent = JsonSerializer.Serialize(email);
                await _storageService.SaveEmailAsync(caseId, emailId, updatedContent);

                _logger.LogInformation(
                    "Attachment added to email: CaseId={CaseId}, EmailId={EmailId}, AssetId={AssetId}",
                    caseId, emailId, assetId);
            }
            else
            {
                _logger.LogInformation(
                    "Attachment already exists: CaseId={CaseId}, EmailId={EmailId}, AssetId={AssetId}",
                    caseId, emailId, assetId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to add attachment: CaseId={CaseId}, EmailId={EmailId}, AssetId={AssetId}",
                caseId, emailId, assetId);
            throw;
        }
    }

    /// <summary>
    /// Task 42: Generate fallback "no findings" email when no rule matches
    /// Returns the emailId of the generated email
    /// </summary>
    public async Task<string> GenerateNoFindingsEmailAsync(string caseId, string userId, string inputAssetId, string analysisType)
    {
        try
        {
            var emailId = $"no-findings-{Guid.NewGuid()}";
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            var noFindingsEmail = new
            {
                id = emailId,
                from = "forensics@casezero.system",
                to = userId,
                subject = $"Forensic Analysis Complete - No Findings",
                body = $@"
<div style='font-family: Arial, sans-serif;'>
    <h2>Forensic Analysis Complete</h2>
    <p><strong>Status:</strong> No findings</p>
    <p><strong>Asset:</strong> {inputAssetId}</p>
    <p><strong>Analysis Type:</strong> {analysisType}</p>
    <p><strong>Timestamp:</strong> {timestamp}</p>
    <hr/>
    <p>The forensic analysis did not reveal any additional information.</p>
    <p>This asset has been thoroughly examined with the selected analysis method.</p>
    <p style='color: #666; font-size: 0.9em; margin-top: 30px;'>
        This is an automated message from the CaseZero Forensics System.
    </p>
</div>
                ",
                timestamp = timestamp,
                attachments = new List<string>(),
                metadata = new
                {
                    forensic = true,
                    inputAssetId,
                    analysisType,
                    result = "no_findings"
                }
            };

            var emailContent = JsonSerializer.Serialize(noFindingsEmail, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await _storageService.SaveEmailAsync(caseId, emailId, emailContent);

            _logger.LogInformation(
                "No findings email generated: CaseId={CaseId}, EmailId={EmailId}, InputAssetId={InputAssetId}",
                caseId, emailId, inputAssetId);

            return emailId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate no findings email: CaseId={CaseId}, InputAssetId={InputAssetId}",
                caseId, inputAssetId);
            throw;
        }
    }

    #region DTOs

    private class CaseJsonV1
    {
        public RulesSection? Rules { get; set; }
    }

    private class RulesSection
    {
        public List<ForensicRule>? Forensics { get; set; }
    }

    #endregion

    #region v2 Evaluate-And-Apply

    /// <summary>
    /// v2 entry point: evaluate every rule of the case against <paramref name="trigger"/>,
    /// and for each match apply all actions. Idempotent per (ruleId, sessionId).
    /// </summary>
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
        // default AND — at least one condition must match the current trigger.
        // The remaining sub-conditions still need to be satisfied later.
        // For idempotent first-match firing we approximate: all conditions must match
        // *some* trigger this engine has seen. Since we don't persist sub-condition history
        // here, we conservatively only fire when every condition matches the current trigger.
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

    #endregion
}
