using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CaseSessionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CaseSessionController> _logger;

        public CaseSessionController(ApplicationDbContext context, ILogger<CaseSessionController> logger)
        {
            _context = context;
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
                // End any active session for this user/case
                var existingSession = await _context.CaseSessions
                    .FirstOrDefaultAsync(cs => cs.UserId == userId && cs.CaseId == request.CaseId && cs.IsActive);

                if (existingSession != null)
                {
                    existingSession.IsActive = false;
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
                    IsActive = true
                };

                _context.CaseSessions.Add(newSession);
                await _context.SaveChangesAsync();

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
                    IsActive = newSession.IsActive
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
                    .FirstOrDefaultAsync(cs => cs.UserId == userId && cs.CaseId == caseId && cs.IsActive);

                if (activeSession == null)
                {
                    _logger.LogWarning("❌ No active session found for userId: {UserId}, caseId: {CaseId}", userId, caseId);
                    return NotFound("No active session found for this case");
                }

                _logger.LogInformation("✅ Active session found: {SessionId}", activeSession.Id);
                
                activeSession.IsActive = false;
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
                    IsActive = activeSession.IsActive
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
                    IsActive = lastSession.IsActive
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
                        IsActive = cs.IsActive
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
    }
}