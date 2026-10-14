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
        private readonly ICaseV2StorageService _storage;
        private readonly CaseGameClock _clock;

        public CaseSessionController(
            ApplicationDbContext context, 
            IVisibilityService visibilityService,
            ILogger<CaseSessionController> logger,
            ICaseV2StorageService storage,
            CaseGameClock clock)
        {
            _context = context;
            _visibilityService = visibilityService;
            _logger = logger;
            _storage = storage;
            _clock = clock;
        }

        /// <summary>
        /// Start or restore the user's case session.
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
                // For V1.0 cases (from blob), we don't enforce UserCases table
                // These cases exist in blob storage, not in the Cases table
                var isV1Case = request.CaseId.StartsWith("case_") || request.CaseId.StartsWith("CASE-");
                
                if (!isV1Case)
                {
                    // Only check UserCases for legacy cases (stored in SQL)
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
                }

                var caseData = await _storage.GetRawAsync(request.CaseId);
                if (isV1Case && caseData is null) return NotFound(new { error = "case_not_found" });
                var newSession = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && cs.CaseId == request.CaseId)
                    .OrderByDescending(cs => cs.SessionStart).ThenByDescending(cs => cs.Id)
                    .FirstOrDefaultAsync();
                if (newSession is null)
                {
                    newSession = new CaseSession { UserId = userId, CaseId = request.CaseId };
                    _context.CaseSessions.Add(newSession);
                }
                _clock.Initialize(newSession, CaseGameClock.Parse(caseData?.Metadata.OpenedAt ??
                    request.GameTimeAtStart ?? DateTime.UtcNow.ToString("o")));
                if (newSession.Status == SessionStatus.Paused) _clock.Resume(newSession);
                _clock.Capture(newSession);
                await _context.SaveChangesAsync();
                await _visibilityService.ApplyInitialRulesAsync(userId, request.CaseId);

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
                    Status = newSession.Status,
                    GameClock = _clock.Read(newSession)
                };

                return Ok(sessionDto);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return SessionChanged(ex, userId, request.CaseId);
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
                    .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                    .OrderByDescending(cs => cs.SessionStart).ThenByDescending(cs => cs.Id)
                    .FirstOrDefaultAsync();

                if (activeSession == null)
                {
                    _logger.LogWarning("❌ No active session found for userId: {UserId}, caseId: {CaseId}", userId, caseId);
                    return NotFound("No active session found for this case");
                }

                _logger.LogInformation("✅ Active session found: {SessionId}", activeSession.Id);
                
                await InitializeClockAsync(activeSession);
                _clock.Capture(activeSession);
                if (activeSession.Status != SessionStatus.Completed) activeSession.Status = SessionStatus.Paused;
                activeSession.IsActive = false;
                activeSession.SessionEnd = DateTime.UtcNow;
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
                    Status = activeSession.Status,
                    GameClock = _clock.Read(activeSession)
                };

                return Ok(sessionDto);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return SessionChanged(ex, userId, caseId);
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
                    .ThenByDescending(cs => cs.Id)
                    .FirstOrDefaultAsync();

                if (lastSession == null)
                {
                    return NotFound("No session found for this case");
                }
                await InitializeClockAsync(lastSession);
                await _context.SaveChangesAsync();

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
                    Status = lastSession.Status,
                    GameClock = _clock.Read(lastSession)
                };

                return Ok(sessionDto);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return SessionChanged(ex, userId, caseId);
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
        /// Resume the latest case session without resetting an already active clock.
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
                var session = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                    .OrderByDescending(cs => cs.SessionStart).ThenByDescending(cs => cs.Id)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return NotFound(new { error = "session_not_found" });
                }
                if (session.Status == SessionStatus.Completed)
                    return Conflict(new { error = "session_completed" });

                await InitializeClockAsync(session);
                _clock.Resume(session);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Resumed session {SessionId} for user {UserId} on case {CaseId}", 
                    session.Id, userId, caseId);

                var sessionDto = new CaseSessionDto
                {
                    Id = session.Id,
                    UserId = session.UserId,
                    CaseId = session.CaseId,
                    SessionStart = session.SessionStart,
                    SessionEnd = session.SessionEnd,
                    SessionDurationMinutes = session.SessionDurationMinutes,
                    GameTimeAtStart = session.GameTimeAtStart,
                    GameTimeAtEnd = session.GameTimeAtEnd,
                    Status = session.Status,
                    GameClock = _clock.Read(session)
                };

                return Ok(sessionDto);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return SessionChanged(ex, userId, caseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming session for user {UserId} and case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("clock/{caseId}")]
        public async Task<IActionResult> GetClock(string caseId, CancellationToken ct)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            try
            {
                var session = await _context.CaseSessions
                    .Where(s => s.UserId == userId && s.CaseId == caseId)
                    .OrderByDescending(s => s.SessionStart).ThenByDescending(s => s.Id)
                    .FirstOrDefaultAsync(ct);
                if (session is null) return NotFound(new { error = "session_not_found" });
                await InitializeClockAsync(session, ct);
                await _context.SaveChangesAsync(ct);
                return Ok(_clock.Read(session));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return SessionChanged(ex, userId, caseId);
            }
        }

        [HttpPost("clock/{caseId}/pause")]
        public Task<IActionResult> PauseClock(string caseId) => EndSession(caseId, new EndCaseSessionRequest());

        private async Task InitializeClockAsync(CaseSession session, CancellationToken ct = default)
        {
            if (session.GameClockUpdatedAt is not null) return;
            var caseData = await _storage.GetRawAsync(session.CaseId, ct);
            _clock.Initialize(session, CaseGameClock.Parse(caseData?.Metadata.OpenedAt ?? session.GameTimeAtStart));
        }

        private ConflictObjectResult SessionChanged(DbUpdateConcurrencyException ex, string userId, string caseId)
        {
            _logger.LogWarning(ex, "Concurrent session update for user {UserId} and case {CaseId}", userId, caseId);
            return Conflict(new { error = "session_changed" });
        }

        /// <summary>
        /// Reset visibility for a case (useful for testing)
        /// DELETE /api/CaseSession/reset-visibility/{caseId}
        /// </summary>
        [HttpDelete("reset-visibility/{caseId}")]
        public async Task<IActionResult> ResetVisibility(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Remove all visible assets for this user/case
                var visibleAssets = await _context.CaseSessionVisibleAssets
                    .Where(va => va.UserId == userId && va.CaseId == caseId)
                    .ToListAsync();
                
                _context.CaseSessionVisibleAssets.RemoveRange(visibleAssets);

                // Remove all visible emails for this user/case
                var visibleEmails = await _context.CaseSessionVisibleEmails
                    .Where(ve => ve.UserId == userId && ve.CaseId == caseId)
                    .ToListAsync();
                
                _context.CaseSessionVisibleEmails.RemoveRange(visibleEmails);

                // Remove attachment downloads
                var downloads = await _context.EmailAttachmentsDownloaded
                    .Where(d => d.UserId == userId && d.CaseId == caseId)
                    .ToListAsync();
                
                _context.EmailAttachmentsDownloaded.RemoveRange(downloads);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Reset visibility for user {UserId} on case {CaseId}. Removed {Assets} assets, {Emails} emails, {Downloads} downloads", 
                    userId, caseId, visibleAssets.Count, visibleEmails.Count, downloads.Count);

                return Ok(new { 
                    message = "Visibility reset successfully",
                    assetsRemoved = visibleAssets.Count,
                    emailsRemoved = visibleEmails.Count,
                    downloadsRemoved = downloads.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting visibility for user {UserId} and case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}