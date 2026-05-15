namespace CaseZeroApi.Services;

/// <summary>
/// Service for evaluating and applying rules from case.json
/// Implements the Rules Engine v1 for case progression and forensics
/// </summary>
public interface IRulesEngineService
{
    /// <summary>
    /// Task 41: Evaluate rule based on case definition
    /// </summary>
    Task<ForensicRule?> EvaluateForensicRuleAsync(string caseId, string inputAssetId, string analysisType);

    /// <summary>
    /// Task 43: Apply reveal_email action
    /// </summary>
    Task ApplyRevealEmailActionAsync(string userId, string caseId, string emailId);

    /// <summary>
    /// Task 44: Apply reveal_asset action
    /// </summary>
    Task ApplyRevealAssetActionAsync(string userId, string caseId, string assetId);

    /// <summary>
    /// Task 45: Apply add_email_attachment action (optional v1)
    /// </summary>
    Task ApplyAddEmailAttachmentActionAsync(string caseId, string emailId, string assetId);

    /// <summary>
    /// Task 42: Generate fallback "no findings" email when no rule matches
    /// </summary>
    Task<string> GenerateNoFindingsEmailAsync(string caseId, string userId, string inputAssetId, string analysisType);

    /// <summary>
    /// v2: evaluate any trigger and apply matching rules' actions to the user's session.
    /// Idempotent per (ruleId, sessionId). See docs/CASE_JSON_V2_SPEC.md §15.
    /// </summary>
    Task EvaluateAndApplyAsync(string caseId, string userId, RuleTrigger trigger, CancellationToken ct = default);
}

/// <summary>
/// Represents a forensic rule from case.json
/// </summary>
public class ForensicRule
{
    public string InputAssetId { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EmailId { get; set; }
    public string? AssetId { get; set; }
}
