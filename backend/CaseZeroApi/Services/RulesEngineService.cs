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
    private readonly ILogger<RulesEngineService> _logger;

    public RulesEngineService(
        ApplicationDbContext context,
        ICaseV1StorageService storageService,
        ILogger<RulesEngineService> logger)
    {
        _context = context;
        _storageService = storageService;
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
}
