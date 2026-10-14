using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Services; // P86
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ForensicRequestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ForensicRequestController> _logger;
        private readonly IAuditLogService _auditLogService; // P86
        private readonly ICaseV2StorageService _storage;
        private readonly ICaseV2SanitizerService _sanitizer;
        private readonly CaseGameClock _clock;

        public ForensicRequestController(
            ApplicationDbContext context, 
            ILogger<ForensicRequestController> logger,
            IAuditLogService auditLogService, // P86
            ICaseV2StorageService storage,
            ICaseV2SanitizerService sanitizer,
            CaseGameClock clock)
        {
            _context = context;
            _logger = logger;
            _auditLogService = auditLogService; // P86
            _storage = storage;
            _sanitizer = sanitizer;
            _clock = clock;
        }

        // GET: api/forensicrequest/{caseId}
        [HttpGet("{caseId}")]
        public async Task<ActionResult<IEnumerable<ForensicRequest>>> GetForensicRequests(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var requests = await _context.ForensicRequests
                .Where(fr => fr.CaseId == caseId && fr.UserId == userId)
                .OrderByDescending(fr => fr.RequestedAt)
                .ToListAsync();

            return Ok(requests);
        }

        // GET: api/forensicrequest/{caseId}/pending
        [HttpGet("{caseId}/pending")]
        public async Task<ActionResult<IEnumerable<ForensicRequest>>> GetPendingForensicRequests(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var requests = await _context.ForensicRequests
                .Where(fr => fr.CaseId == caseId && fr.UserId == userId && 
                            (fr.Status == "pending" || fr.Status == "in-progress"))
                .OrderBy(fr => fr.EstimatedCompletionTime)
                .ToListAsync();

            return Ok(requests);
        }

        // GET: api/forensicrequest/{caseId}/{id}
        [HttpGet("{caseId}/{id}")]
        public async Task<ActionResult<ForensicRequest>> GetForensicRequest(string caseId, int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var request = await _context.ForensicRequests
                .FirstOrDefaultAsync(fr => fr.Id == id && fr.CaseId == caseId && fr.UserId == userId);

            if (request == null)
            {
                return NotFound();
            }

            return Ok(request);
        }

        public sealed class CreateRequest
        {
            [Required, MaxLength(128)] public string CaseId { get; set; } = string.Empty;
            [Required, MaxLength(160)] public string InputAssetId { get; set; } = string.Empty;
            [Required, MaxLength(100)] public string AnalysisType { get; set; } = string.Empty;
            [MaxLength(2000)] public string? Notes { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult<ForensicRequest>> CreateForensicRequest(CreateRequest input, CancellationToken ct = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var caseData = await _storage.GetRawAsync(input.CaseId, ct);
            if (caseData is null) return NotFound(new { error = "case_not_found" });
            var visible = await _sanitizer.SanitizeForUserAsync(caseData, userId, input.CaseId, ct);
            var asset = visible.Assets.FirstOrDefault(a => a.Id == input.InputAssetId);
            if (asset is null)
            {
                _logger.LogWarning("Rejected forensic request for unavailable asset {AssetId}", input.InputAssetId);
                return BadRequest(new { error = "asset_not_visible" });
            }
            var analyses = caseData.ForensicsDefaults?.AnalysisTypes.Where(a =>
                string.Equals(a.Type, input.AnalysisType, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
            if (analyses?.Count > 1)
            {
                _logger.LogError("Ambiguous forensic analysis {AnalysisType} for case {CaseId}", input.AnalysisType, input.CaseId);
                return StatusCode(500, new { error = "forensic_configuration_invalid" });
            }
            var analysis = analyses?.SingleOrDefault();
            if (analysis is null || analysis.DurationMinutes <= 0 || !analysis.Supports(asset))
            {
                _logger.LogWarning("Rejected incompatible analysis {AnalysisType} for {AssetId}", input.AnalysisType, input.InputAssetId);
                return BadRequest(new { error = "analysis_not_compatible" });
            }
            var session = await _context.CaseSessions
                .Where(s => s.UserId == userId && s.CaseId == input.CaseId)
                .OrderByDescending(s => s.SessionStart).ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync(ct);
            if (session is null || session.Status != SessionStatus.Active)
                return Conflict(new { error = "active_session_required" });
            var existing = await _context.ForensicRequests.FirstOrDefaultAsync(r =>
                r.UserId == userId && r.CaseId == input.CaseId && r.InputAssetId == asset.Id &&
                r.AnalysisType == analysis.Type && r.Status != "cancelled" && r.Status != "failed", ct);
            if (existing is not null) return Ok(existing);
            _clock.Initialize(session, CaseGameClock.Parse(caseData.Metadata.OpenedAt));
            var clock = _clock.Capture(session);
            var request = new ForensicRequest
            {
                UserId = userId, CaseId = input.CaseId, InputAssetId = asset.Id,
                InputAssetName = asset.Title, AnalysisType = analysis.Type,
                RequestedAt = clock.GameTime,
                EstimatedCompletionTime = clock.GameTime.AddMinutes(analysis.DurationMinutes),
                Notes = input.Notes
            };

            _context.ForensicRequests.Add(request);
            _context.Entry(session).Property(s => s.StateVersion).IsModified = true;
            try { await _context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrent forensic request/session update for {CaseId}", input.CaseId);
                return Conflict(new { error = "session_changed" });
            }

            // P86: Audit log - forensic request criado
            await _auditLogService.LogActionAsync(
                userId,
                "forensic_request",
                $"{request.CaseId}/forensic/{request.Id}",
                request.CaseId,
                "success",
                JsonSerializer.Serialize(new { request.InputAssetId, request.AnalysisType }));

            return CreatedAtAction(nameof(GetForensicRequest), 
                new { caseId = request.CaseId, id = request.Id }, request);
        }

        // PUT: api/forensicrequest/{caseId}/{id}
        [HttpPut("{caseId}/{id}")]
        public async Task<IActionResult> UpdateForensicRequest(string caseId, int id, ForensicRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (id != request.Id || caseId != request.CaseId)
            {
                return BadRequest();
            }

            var existingRequest = await _context.ForensicRequests
                .FirstOrDefaultAsync(fr => fr.Id == id && fr.CaseId == caseId && fr.UserId == userId);

            if (existingRequest == null)
            {
                return NotFound();
            }

            if (request.Status != existingRequest.Status || request.CompletedAt != existingRequest.CompletedAt ||
                request.ResultDocumentId != existingRequest.ResultDocumentId || request.ResultEmailId != existingRequest.ResultEmailId)
                return BadRequest(new { error = "forensic_result_is_server_owned" });
            if (request.Notes?.Length > 2000) return BadRequest(new { error = "notes_too_long" });
            existingRequest.Notes = request.Notes;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrent update of forensic request {RequestId}", id);
                return Conflict(new { error = "forensic_request_changed" });
            }

            return NoContent();
        }

        // DELETE: api/forensicrequest/{caseId}/{id}
        [HttpDelete("{caseId}/{id}")]
        public async Task<IActionResult> DeleteForensicRequest(string caseId, int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var request = await _context.ForensicRequests
                .FirstOrDefaultAsync(fr => fr.Id == id && fr.CaseId == caseId && fr.UserId == userId);

            if (request == null)
            {
                return NotFound();
            }

            if (request.Status == "completed") return Conflict(new { error = "analysis_already_completed" });
            request.Status = "cancelled";
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrent cancellation of forensic request {RequestId}", id);
                return Conflict(new { error = "forensic_request_changed" });
            }

            return NoContent();
        }
    }
}
