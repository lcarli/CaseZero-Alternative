using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CaseZeroApi.Controllers;

[Route("api/cases/{caseId}")]
[ApiController]
[Authorize]
public class CaseTriggersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IRulesEngineService _rulesEngine;
    private readonly ICaseV2StorageService _storage;
    private readonly ILogger<CaseTriggersController> _logger;
    private readonly CaseGameClock _clock;
    private readonly ICaseV2SanitizerService _sanitizer;

    public CaseTriggersController(
        ApplicationDbContext context,
        IRulesEngineService rulesEngine,
        ICaseV2StorageService storage,
        ILogger<CaseTriggersController> logger,
        CaseGameClock clock,
        ICaseV2SanitizerService sanitizer)
    {
        _context = context;
        _rulesEngine = rulesEngine;
        _storage = storage;
        _logger = logger;
        _clock = clock;
        _sanitizer = sanitizer;
    }

    private string? UserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("assets/{assetId}/view")]
    public async Task<IActionResult> ViewAsset(string caseId, string assetId, CancellationToken ct)
    {
        var userId = UserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var data = await _storage.GetRawAsync(caseId, ct);
        if (data is null) return NotFound(new { error = "case_not_found" });
        var visible = await _sanitizer.SanitizeForUserAsync(data, userId, caseId, ct);
        if (!visible.Assets.Any(a => a.Id == assetId))
            return StatusCode(403, new { error = "asset_not_visible" });
        await _rulesEngine.EvaluateAndApplyAsync(caseId, userId, new AssetViewedTrigger(assetId), ct);
        return Ok(new { caseId, assetId, trigger = "asset_viewed" });
    }

    [HttpPost("suspects/{suspectId}/view")]
    public async Task<IActionResult> ViewSuspect(string caseId, string suspectId, CancellationToken ct)
    {
        var userId = UserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var data = await _storage.GetRawAsync(caseId, ct);
        if (data is null) return NotFound(new { error = "case_not_found" });
        var visible = await _sanitizer.SanitizeForUserAsync(data, userId, caseId, ct);
        if (!visible.Suspects.Any(s => s.Id == suspectId))
            return StatusCode(403, new { error = "suspect_not_visible" });
        await _rulesEngine.EvaluateAndApplyAsync(caseId, userId, new SuspectViewedTrigger(suspectId), ct);
        return Ok(new { caseId, suspectId, trigger = "suspect_viewed" });
    }

    public record TimeRequest(int GameTimeMinutes);

    [HttpPost("time")]
    public async Task<IActionResult> AdvanceTime(string caseId, [FromBody] TimeRequest body, CancellationToken ct)
    {
        var userId = UserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (body is null) return BadRequest(new { error = "Body required" });

        if (body.GameTimeMinutes < 0) return BadRequest(new { error = "invalid_game_time" });
        var session = await _context.CaseSessions
            .Where(s => s.UserId == userId && s.CaseId == caseId)
            .OrderByDescending(s => s.SessionStart).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
        if (session is null) return NotFound(new { error = "session_not_found" });
        var caseData = await _storage.GetRawAsync(caseId, ct);
        if (caseData is null) return NotFound(new { error = "case_not_found" });
        _clock.Initialize(session, CaseGameClock.Parse(caseData.Metadata.OpenedAt));
        var clock = _clock.Capture(session);
        await _rulesEngine.EvaluateAndApplyAsync(caseId, userId,
            new TimeElapsedTrigger((int)Math.Floor(clock.ElapsedMinutes)), ct);
        return Ok(new { caseId, gameTimeMinutes = clock.ElapsedMinutes, gameClock = clock });
    }
}
