namespace CaseZeroApi.Services;

/// <summary>
/// Service for evaluating and applying rules from case.json (v2).
/// Applies rules when triggers fire (asset open, forensic completed, etc),
/// unlocking emails/assets per session. Idempotent per (ruleId, sessionId).
/// See docs/CASE_JSON_V2_SPEC.md §15.
/// </summary>
public interface IRulesEngineService
{
    /// <summary>
    /// Evaluate any trigger and apply matching rules' actions to the user's session.
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
