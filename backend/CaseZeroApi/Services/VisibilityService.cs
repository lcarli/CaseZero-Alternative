using CaseZeroApi.Data;
using CaseZeroApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services
{
    public class VisibilityService : IVisibilityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICaseV1StorageService _caseStorageService;
        private readonly ILogger<VisibilityService> _logger;

        public VisibilityService(
            ApplicationDbContext context,
            ICaseV1StorageService caseStorageService,
            ILogger<VisibilityService> logger)
        {
            _context = context;
            _caseStorageService = caseStorageService;
            _logger = logger;
        }

        public async Task<(int assetsUnlocked, int emailsUnlocked)> ApplyInitialRulesAsync(string userId, string caseId)
        {
            _logger.LogInformation("🔓 Applying initial visibility rules for user {UserId} on case {CaseId}", userId, caseId);

            try
            {
                // Load case from blob storage
                var caseData = await _caseStorageService.GetCaseAsync(caseId);
                if (caseData == null)
                {
                    _logger.LogWarning("Case {CaseId} not found in storage", caseId);
                    return (0, 0);
                }

                _logger.LogDebug("Case loaded. Assets count: {AssetsCount}, Emails count: {EmailsCount}",
                    caseData.Assets?.Count ?? 0, caseData.Emails?.Count ?? 0);

                int assetsUnlocked = 0;
                int emailsUnlocked = 0;

                // Find assets with visibility="initial"
                if (caseData.Assets != null)
                {
                    var initialAssets = caseData.Assets
                        .Where(a => a.Visibility != null && a.Visibility.Equals("initial", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var asset in initialAssets)
                    {
                        // Check if already unlocked
                        var exists = await _context.CaseSessionVisibleAssets
                            .AnyAsync(va => va.UserId == userId && va.CaseId == caseId && va.AssetId == asset.AssetId);

                        if (!exists)
                        {
                            var visibleAsset = new CaseSessionVisibleAsset
                            {
                                UserId = userId,
                                CaseId = caseId,
                                AssetId = asset.AssetId,
                                UnlockedAt = DateTime.UtcNow
                            };

                            _context.CaseSessionVisibleAssets.Add(visibleAsset);
                            assetsUnlocked++;
                            _logger.LogDebug("Unlocked asset {AssetId} for user {UserId}", asset.AssetId, userId);
                        }
                    }
                }

                // Find emails with visibility="initial"
                if (caseData.Emails != null)
                {
                    _logger.LogDebug("Checking {Count} emails for initial visibility", caseData.Emails.Count);
                    
                    var initialEmails = caseData.Emails
                        .Where(e => e.Visibility != null && e.Visibility.Equals("initial", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    _logger.LogInformation("Found {Count} emails with visibility='initial'", initialEmails.Count);

                    foreach (var email in initialEmails)
                    {
                        _logger.LogDebug("Processing email {EmailId} with visibility={Visibility}", 
                            email.EmailId, email.Visibility);
                        
                        // Check if already unlocked
                        var exists = await _context.CaseSessionVisibleEmails
                            .AnyAsync(ve => ve.UserId == userId && ve.CaseId == caseId && ve.EmailId == email.EmailId);

                        if (!exists)
                        {
                            var visibleEmail = new CaseSessionVisibleEmail
                            {
                                UserId = userId,
                                CaseId = caseId,
                                EmailId = email.EmailId,
                                UnlockedAt = DateTime.UtcNow
                            };

                            _context.CaseSessionVisibleEmails.Add(visibleEmail);
                            emailsUnlocked++;
                            _logger.LogDebug("Unlocked email {EmailId} for user {UserId}", email.EmailId, userId);
                        }
                        else
                        {
                            _logger.LogDebug("Email {EmailId} already unlocked for user {UserId}", email.EmailId, userId);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Case {CaseId} has no emails", caseId);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Initial visibility applied: {AssetsCount} assets, {EmailsCount} emails unlocked for user {UserId}",
                    assetsUnlocked, emailsUnlocked, userId);

                return (assetsUnlocked, emailsUnlocked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying initial visibility rules for user {UserId} on case {CaseId}", userId, caseId);
                throw;
            }
        }

        public async Task UnlockAssetAsync(string userId, string caseId, string assetId)
        {
            var exists = await _context.CaseSessionVisibleAssets
                .AnyAsync(va => va.UserId == userId && va.CaseId == caseId && va.AssetId == assetId);

            if (!exists)
            {
                var visibleAsset = new CaseSessionVisibleAsset
                {
                    UserId = userId,
                    CaseId = caseId,
                    AssetId = assetId,
                    UnlockedAt = DateTime.UtcNow
                };

                _context.CaseSessionVisibleAssets.Add(visibleAsset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("🔓 Unlocked asset {AssetId} for user {UserId} on case {CaseId}", assetId, userId, caseId);
            }
        }

        public async Task UnlockEmailAsync(string userId, string caseId, string emailId)
        {
            var exists = await _context.CaseSessionVisibleEmails
                .AnyAsync(ve => ve.UserId == userId && ve.CaseId == caseId && ve.EmailId == emailId);

            if (!exists)
            {
                var visibleEmail = new CaseSessionVisibleEmail
                {
                    UserId = userId,
                    CaseId = caseId,
                    EmailId = emailId,
                    UnlockedAt = DateTime.UtcNow
                };

                _context.CaseSessionVisibleEmails.Add(visibleEmail);
                await _context.SaveChangesAsync();

                _logger.LogInformation("🔓 Unlocked email {EmailId} for user {UserId} on case {CaseId}", emailId, userId, caseId);
            }
        }

        public async Task<bool> IsAssetVisibleAsync(string userId, string caseId, string assetId)
        {
            return await _context.CaseSessionVisibleAssets
                .AnyAsync(va => va.UserId == userId && va.CaseId == caseId && va.AssetId == assetId);
        }

        public async Task<bool> IsEmailVisibleAsync(string userId, string caseId, string emailId)
        {
            return await _context.CaseSessionVisibleEmails
                .AnyAsync(ve => ve.UserId == userId && ve.CaseId == caseId && ve.EmailId == emailId);
        }
    }
}
