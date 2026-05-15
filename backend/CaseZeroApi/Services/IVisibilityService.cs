using CaseZeroApi.Models;

namespace CaseZeroApi.Services
{
    /// <summary>
    /// Service for managing case visibility rules and unlocking content for user sessions
    /// </summary>
    public interface IVisibilityService
    {
        /// <summary>
        /// Apply initial visibility rules when starting a new case session.
        /// Unlocks assets and emails marked with visibility="initial" in the case.json
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="caseId">Case ID</param>
        /// <returns>Number of assets and emails unlocked</returns>
        Task<(int assetsUnlocked, int emailsUnlocked)> ApplyInitialRulesAsync(string userId, string caseId);

        /// <summary>
        /// Unlock a specific asset for a user's case session
        /// </summary>
        Task UnlockAssetAsync(string userId, string caseId, string assetId);

        /// <summary>
        /// Unlock a specific email for a user's case session
        /// </summary>
        Task UnlockEmailAsync(string userId, string caseId, string emailId);

        /// <summary>
        /// Check if an asset is visible to the user
        /// </summary>
        Task<bool> IsAssetVisibleAsync(string userId, string caseId, string assetId);

        /// <summary>
        /// Check if an email is visible to the user
        /// </summary>
        Task<bool> IsEmailVisibleAsync(string userId, string caseId, string emailId);
    }
}
