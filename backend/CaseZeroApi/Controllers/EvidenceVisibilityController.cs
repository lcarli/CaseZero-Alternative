using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CaseZeroApi.Services;
using CaseZeroApi.Models;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EvidenceVisibilityController : ControllerBase
    {
        private readonly ICaseAccessService _caseAccessService;
        private readonly ILogger<EvidenceVisibilityController> _logger;

        public EvidenceVisibilityController(
            ICaseAccessService caseAccessService,
            ILogger<EvidenceVisibilityController> logger)
        {
            _caseAccessService = caseAccessService;
            _logger = logger;
        }

        /// <summary>
        /// Update evidence visibility status for the current user's case instance
        /// </summary>
        [HttpPost("{caseId}/evidence/{evidenceId}/visibility")]
        public async Task<IActionResult> UpdateEvidenceVisibility(
            string caseId, 
            string evidenceId, 
            [FromBody] UpdateEvidenceVisibilityRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Check if user can access this case
                if (!await _caseAccessService.CanUserAccessCaseAsync(userId, caseId))
                {
                    return Forbid("User does not have access to this case");
                }

                await _caseAccessService.UpdateEvidenceVisibilityAsync(userId, caseId, evidenceId, request.IsVisible);

                _logger.LogInformation("Evidence visibility updated: User {UserId}, Case {CaseId}, Evidence {EvidenceId}, Visible: {IsVisible}", 
                    userId, caseId, evidenceId, request.IsVisible);

                return Ok(new { 
                    success = true, 
                    message = "Evidence visibility updated successfully",
                    caseId = caseId,
                    evidenceId = evidenceId,
                    isVisible = request.IsVisible
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating evidence visibility: User {UserId}, Case {CaseId}, Evidence {EvidenceId}", 
                    userId, caseId, evidenceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get all visible evidences for the current user's case instance
        /// </summary>
        [HttpGet("{caseId}/visible-evidences")]
        public async Task<IActionResult> GetVisibleEvidences(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Check if user can access this case
                if (!await _caseAccessService.CanUserAccessCaseAsync(userId, caseId))
                {
                    return Forbid("User does not have access to this case");
                }

                var visibleEvidences = await _caseAccessService.GetVisibleEvidencesForUserAsync(userId, caseId);

                return Ok(new {
                    caseId = caseId,
                    evidences = visibleEvidences.Select(e => new {
                        id = e.Id,
                        name = e.Name,
                        type = e.Type,
                        fileName = e.FileName,
                        category = e.Category,
                        priority = e.Priority,
                        description = e.Description,
                        location = e.Location,
                        isUnlocked = e.IsUnlocked,
                        requiresAnalysis = e.RequiresAnalysis,
                        dependsOn = e.DependsOn,
                        linkedSuspects = e.LinkedSuspects,
                        analysisRequired = e.AnalysisRequired
                    }).ToList(),
                    totalCount = visibleEvidences.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting visible evidences: User {UserId}, Case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Start a new case instance for the user (creates a copy)
        /// </summary>
        [HttpPost("{caseId}/start")]
        public async Task<IActionResult> StartCaseInstance(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Check if user can access this case
                if (!await _caseAccessService.CanUserAccessCaseAsync(userId, caseId))
                {
                    return Forbid("User does not have access to this case");
                }

                var userCaseInstance = await _caseAccessService.CreateUserCaseInstanceAsync(userId, caseId);

                _logger.LogInformation("Case instance started: User {UserId}, Case {CaseId}", userId, caseId);

                return Ok(new {
                    success = true,
                    message = "Case instance created successfully",
                    caseId = caseId,
                    userId = userId,
                    metadata = new {
                        title = userCaseInstance.Metadata.Title,
                        description = userCaseInstance.Metadata.Description,
                        difficulty = userCaseInstance.Metadata.Difficulty,
                        estimatedDuration = userCaseInstance.Metadata.EstimatedDuration,
                        briefing = userCaseInstance.Metadata.Briefing
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting case instance: User {UserId}, Case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class UpdateEvidenceVisibilityRequest
    {
        public bool IsVisible { get; set; }
        public string? Reason { get; set; }
    }
}