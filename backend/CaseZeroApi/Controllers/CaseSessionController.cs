using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CaseSessionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IVisibilityService _visibilityService;
        private readonly ILogger<CaseSessionController> _logger;

        public CaseSessionController(
            ApplicationDbContext context, 
            IVisibilityService visibilityService,
            ILogger<CaseSessionController> logger)
        {
            _context = context;
            _visibilityService = visibilityService;
            _logger = logger;
        }

        /// <summary>
        /// Start a new case session
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartSession([FromBody] StartCaseSessionRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Ensure user has access to this case (auto-assign if not exists)
                var userCase = await _context.UserCases
                    .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == request.CaseId);

                if (userCase == null)
                {
                    _logger.LogInformation("Auto-assigning case {CaseId} to user {UserId}", request.CaseId, userId);
                    _context.UserCases.Add(new UserCase
                    {
                        UserId = userId,
                        CaseId = request.CaseId,
                        AssignedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }

                // End any active session for this user/case
                var existingSession = await _context.CaseSessions
                    .FirstOrDefaultAsync(cs => cs.UserId == userId && cs.CaseId == request.CaseId && cs.Status == SessionStatus.Active);

                if (existingSession != null)
                {
                    existingSession.Status = SessionStatus.Paused;
                    existingSession.SessionEnd = DateTime.UtcNow;
                    existingSession.SessionDurationMinutes = 
                        (int)(existingSession.SessionEnd.Value - existingSession.SessionStart).TotalMinutes;
                }

                // Create new session
                var newSession = new CaseSession
                {
                    UserId = userId,
                    CaseId = request.CaseId,
                    SessionStart = DateTime.UtcNow,
                    GameTimeAtStart = request.GameTimeAtStart,
                    Status = SessionStatus.Active
                };

                _context.CaseSessions.Add(newSession);
                await _context.SaveChangesAsync();

                // Apply initial visibility rules (unlock initial assets/emails)
                try
                {
                    var (assetsUnlocked, emailsUnlocked) = await _visibilityService.ApplyInitialRulesAsync(userId, request.CaseId);
                    _logger.LogInformation("Initial visibility applied: {Assets} assets, {Emails} emails unlocked", assetsUnlocked, emailsUnlocked);
                }
                catch (Exception visEx)
                {
                    _logger.LogError(visEx, "Failed to apply initial visibility rules, but session was created");
                    // Continue - session is valid even if visibility fails
                }

                var sessionDto = new CaseSessionDto
                {
                    Id = newSession.Id,
                    UserId = newSession.UserId,
                    CaseId = newSession.CaseId,
                    SessionStart = newSession.SessionStart,
                    SessionEnd = newSession.SessionEnd,
                    SessionDurationMinutes = newSession.SessionDurationMinutes,
                    GameTimeAtStart = newSession.GameTimeAtStart,
                    GameTimeAtEnd = newSession.GameTimeAtEnd,
                    Status = newSession.Status
                };

                return Ok(sessionDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting case session for user {UserId} and case {CaseId}", userId, request.CaseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// End the current active case session
        /// </summary>
        [HttpPost("end/{caseId}")]
        public async Task<IActionResult> EndSession(string caseId, [FromBody] EndCaseSessionRequest request)
        {
            _logger.LogInformation("🚪 EndSession called for caseId: {CaseId}", caseId);
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("👤 UserId from token: {UserId}", userId);
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("⚠️ Unauthorized - no userId in token");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("🔍 Looking for active session for userId: {UserId}, caseId: {CaseId}", userId, caseId);
                
                var activeSession = await _context.CaseSessions
                    .FirstOrDefaultAsync(cs => cs.UserId == userId && cs.CaseId == caseId && cs.Status == SessionStatus.Active);

                if (activeSession == null)
                {
                    _logger.LogWarning("❌ No active session found for userId: {UserId}, caseId: {CaseId}", userId, caseId);
                    return NotFound("No active session found for this case");
                }

                _logger.LogInformation("✅ Active session found: {SessionId}", activeSession.Id);
                
                activeSession.Status = SessionStatus.Paused;
                activeSession.SessionEnd = DateTime.UtcNow;
                activeSession.GameTimeAtEnd = request.GameTimeAtEnd;
                activeSession.SessionDurationMinutes = 
                    (int)(activeSession.SessionEnd.Value - activeSession.SessionStart).TotalMinutes;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("💾 Session saved successfully: {SessionId}", activeSession.Id);

                var sessionDto = new CaseSessionDto
                {
                    Id = activeSession.Id,
                    UserId = activeSession.UserId,
                    CaseId = activeSession.CaseId,
                    SessionStart = activeSession.SessionStart,
                    SessionEnd = activeSession.SessionEnd,
                    SessionDurationMinutes = activeSession.SessionDurationMinutes,
                    GameTimeAtStart = activeSession.GameTimeAtStart,
                    GameTimeAtEnd = activeSession.GameTimeAtEnd,
                    Status = activeSession.Status
                };

                return Ok(sessionDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending case session for user {UserId} and case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get last session for a specific case
        /// </summary>
        [HttpGet("last/{caseId}")]
        public async Task<IActionResult> GetLastSession(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var lastSession = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                    .OrderByDescending(cs => cs.SessionStart)
                    .FirstOrDefaultAsync();

                if (lastSession == null)
                {
                    return NotFound("No session found for this case");
                }

                var sessionDto = new CaseSessionDto
                {
                    Id = lastSession.Id,
                    UserId = lastSession.UserId,
                    CaseId = lastSession.CaseId,
                    SessionStart = lastSession.SessionStart,
                    SessionEnd = lastSession.SessionEnd,
                    SessionDurationMinutes = lastSession.SessionDurationMinutes,
                    GameTimeAtStart = lastSession.GameTimeAtStart,
                    GameTimeAtEnd = lastSession.GameTimeAtEnd,
                    Status = lastSession.Status
                };

                return Ok(sessionDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving last session for user {UserId} and case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get all sessions for a specific case
        /// </summary>
        [HttpGet("{caseId}")]
        public async Task<IActionResult> GetCaseSessions(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var sessions = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                    .OrderByDescending(cs => cs.SessionStart)
                    .Select(cs => new CaseSessionDto
                    {
                        Id = cs.Id,
                        UserId = cs.UserId,
                        CaseId = cs.CaseId,
                        SessionStart = cs.SessionStart,
                        SessionEnd = cs.SessionEnd,
                        SessionDurationMinutes = cs.SessionDurationMinutes,
                        GameTimeAtStart = cs.GameTimeAtStart,
                        GameTimeAtEnd = cs.GameTimeAtEnd,
                        Status = cs.Status
                    })
                    .ToListAsync();

                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions for user {UserId} and case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get complete session state for a case including visible assets/emails
        /// </summary>
        [HttpGet("/api/cases/{caseId}/session")]
        public async Task<IActionResult> GetCaseSessionState(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Get active or most recent session
                var session = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                    .OrderByDescending(cs => cs.SessionStart)
                    .FirstOrDefaultAsync();

                CaseSessionDto? sessionDto = null;
                if (session != null)
                {
                    sessionDto = new CaseSessionDto
                    {
                        Id = session.Id,
                        UserId = session.UserId,
                        CaseId = session.CaseId,
                        SessionStart = session.SessionStart,
                        SessionEnd = session.SessionEnd,
                        SessionDurationMinutes = session.SessionDurationMinutes,
                        GameTimeAtStart = session.GameTimeAtStart,
                        GameTimeAtEnd = session.GameTimeAtEnd,
                        Status = session.Status
                    };
                }

                // Get visible assets
                var visibleAssets = await _context.CaseSessionVisibleAssets
                    .Where(va => va.UserId == userId && va.CaseId == caseId)
                    .Select(va => va.AssetId)
                    .ToListAsync();

                // Get visible emails
                var visibleEmails = await _context.CaseSessionVisibleEmails
                    .Where(ve => ve.UserId == userId && ve.CaseId == caseId)
                    .Select(ve => ve.EmailId)
                    .ToListAsync();

                // Get email states
                var emailStates = await _context.CaseSessionEmailStates
                    .Where(es => es.UserId == userId && es.CaseId == caseId)
                    .ToDictionaryAsync(
                        es => es.EmailId,
                        es => new EmailStateDto
                        {
                            EmailId = es.EmailId,
                            ReadAt = es.ReadAt,
                            OpenCount = es.OpenCount
                        });

                var stateDto = new CaseSessionStateDto
                {
                    Session = sessionDto,
                    VisibleAssetIds = visibleAssets,
                    VisibleEmailIds = visibleEmails,
                    EmailStates = emailStates
                };

                return Ok(stateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session state for user {UserId} and case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Resume a paused case session
        /// </summary>
        [HttpPost("/api/cases/{caseId}/resume")]
        public async Task<IActionResult> ResumeSession(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Find the most recent paused session
                var pausedSession = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && cs.CaseId == caseId && cs.Status == SessionStatus.Paused)
                    .OrderByDescending(cs => cs.SessionStart)
                    .FirstOrDefaultAsync();

                if (pausedSession == null)
                {
                    return NotFound("No paused session found for this case");
                }

                // Mark as active
                pausedSession.Status = SessionStatus.Active;
                pausedSession.SessionEnd = null; // Clear end time since we're resuming
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Resumed session {SessionId} for user {UserId} on case {CaseId}", 
                    pausedSession.Id, userId, caseId);

                var sessionDto = new CaseSessionDto
                {
                    Id = pausedSession.Id,
                    UserId = pausedSession.UserId,
                    CaseId = pausedSession.CaseId,
                    SessionStart = pausedSession.SessionStart,
                    SessionEnd = pausedSession.SessionEnd,
                    SessionDurationMinutes = pausedSession.SessionDurationMinutes,
                    GameTimeAtStart = pausedSession.GameTimeAtStart,
                    GameTimeAtEnd = pausedSession.GameTimeAtEnd,
                    Status = pausedSession.Status
                };

                return Ok(sessionDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming session for user {UserId} and case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}