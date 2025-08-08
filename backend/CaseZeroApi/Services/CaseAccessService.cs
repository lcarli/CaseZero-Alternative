using CaseZeroApi.Models;
using CaseZeroApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CaseZeroApi.Services
{
    public interface ICaseAccessService
    {
        Task<List<string>> GetAvailableCasesForUserAsync(string userId);
        Task<bool> CanUserAccessCaseAsync(string userId, string caseId);
        Task<CaseObject?> GetUserCaseInstanceAsync(string userId, string caseId);
        Task<CaseObject> CreateUserCaseInstanceAsync(string userId, string caseId);
        Task UpdateEvidenceVisibilityAsync(string userId, string caseId, string evidenceId, bool isVisible);
        Task<List<CaseEvidence>> GetVisibleEvidencesForUserAsync(string userId, string caseId);
    }

    public class CaseAccessService : ICaseAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICaseObjectService _caseObjectService;
        private readonly ILogger<CaseAccessService> _logger;

        public CaseAccessService(
            ApplicationDbContext context, 
            ICaseObjectService caseObjectService,
            ILogger<CaseAccessService> logger)
        {
            _context = context;
            _caseObjectService = caseObjectService;
            _logger = logger;
        }

        public async Task<List<string>> GetAvailableCasesForUserAsync(string userId)
        {
            try
            {
                // Get user rank
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    return new List<string>();
                }

                // Get all available cases from filesystem
                var allCases = await _caseObjectService.GetAvailableCasesAsync();
                var availableCases = new List<string>();

                // Filter cases based on user rank
                foreach (var caseId in allCases)
                {
                    var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                    if (caseObject?.Metadata != null)
                    {
                        if (CanUserAccessCase(user.Rank, caseObject.Metadata.MinRankRequired))
                        {
                            availableCases.Add(caseId);
                        }
                    }
                }

                _logger.LogInformation("Found {Count} available cases for user {UserId} with rank {Rank}", 
                    availableCases.Count, userId, user.Rank);

                return availableCases;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available cases for user {UserId}", userId);
                return new List<string>();
            }
        }

        public async Task<bool> CanUserAccessCaseAsync(string userId, string caseId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                if (caseObject?.Metadata == null) return false;

                return CanUserAccessCase(user.Rank, caseObject.Metadata.MinRankRequired);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking case access for user {UserId}, case {CaseId}", userId, caseId);
                return false;
            }
        }

        public async Task<CaseObject?> GetUserCaseInstanceAsync(string userId, string caseId)
        {
            try
            {
                // For now, return the master case - in the future this would return user's copy
                // TODO: Implement user-specific case instances stored in document database
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                
                if (caseObject == null || !await CanUserAccessCaseAsync(userId, caseId))
                {
                    return null;
                }

                // TODO: Apply user-specific evidence visibility rules
                // For now, return the case as-is
                return caseObject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user case instance for user {UserId}, case {CaseId}", userId, caseId);
                return null;
            }
        }

        public async Task<CaseObject> CreateUserCaseInstanceAsync(string userId, string caseId)
        {
            try
            {
                // Load master case
                var masterCase = await _caseObjectService.LoadCaseObjectAsync(caseId);
                if (masterCase == null)
                {
                    throw new InvalidOperationException($"Master case {caseId} not found");
                }

                // TODO: In future, create a copy in document database for the user
                // For now, return master case - this is where we would implement:
                // 1. Copy case to user's document in CosmosDB/MongoDB
                // 2. Set initial evidence visibility based on unlock conditions
                // 3. Create user case session tracking

                _logger.LogInformation("Created case instance for user {UserId}, case {CaseId}", userId, caseId);
                return masterCase;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user case instance for user {UserId}, case {CaseId}", userId, caseId);
                throw;
            }
        }

        public Task UpdateEvidenceVisibilityAsync(string userId, string caseId, string evidenceId, bool isVisible)
        {
            try
            {
                // TODO: Implement evidence visibility update in user's case instance
                // This would update the document database with user-specific evidence visibility
                
                _logger.LogInformation("Updated evidence visibility for user {UserId}, case {CaseId}, evidence {EvidenceId} to {IsVisible}", 
                    userId, caseId, evidenceId, isVisible);

                // For now, log the action - in future this would:
                // 1. Update user's case document in CosmosDB/MongoDB
                // 2. Track visibility change with timestamp
                // 3. Trigger any dependent evidence unlocks
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating evidence visibility for user {UserId}, case {CaseId}, evidence {EvidenceId}", 
                    userId, caseId, evidenceId);
                throw;
            }
        }

        public async Task<List<CaseEvidence>> GetVisibleEvidencesForUserAsync(string userId, string caseId)
        {
            try
            {
                var userCase = await GetUserCaseInstanceAsync(userId, caseId);
                if (userCase == null)
                {
                    return new List<CaseEvidence>();
                }

                // TODO: Filter based on user-specific visibility rules
                // For now, return evidences that are marked as unlocked in master case
                var visibleEvidences = userCase.Evidences.Where(e => e.IsUnlocked).ToList();

                _logger.LogInformation("Found {Count} visible evidences for user {UserId}, case {CaseId}", 
                    visibleEvidences.Count, userId, caseId);

                return visibleEvidences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting visible evidences for user {UserId}, case {CaseId}", userId, caseId);
                return new List<CaseEvidence>();
            }
        }

        private static bool CanUserAccessCase(DetectiveRank userRank, string requiredRankString)
        {
            // Convert string rank to enum
            if (!Enum.TryParse<DetectiveRank>(requiredRankString, true, out var requiredRank))
            {
                // If can't parse, allow access (backward compatibility)
                return true;
            }

            // User can access cases that require their rank or lower
            return userRank >= requiredRank;
        }
    }
}