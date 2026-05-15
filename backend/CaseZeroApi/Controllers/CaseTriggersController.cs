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

    public CaseTriggersController(
        ApplicationDbContext context,
        IRulesEngineService rulesEngine,
        ICaseV2StorageService storage,
        ILogger<CaseTriggersController> logger)
    {
        _context = context;
        _rulesEngine = rulesEngine;
        _storage = storage;
        _logger = logger;
    }

    private string? UserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("assets/{assetId}/view")]
    public async Task<IActionResult> ViewAsset(string caseId, string assetId, CancellationToken ct)
    {
        var userId = UserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        await _rulesEngine.EvaluateAndApplyAsync(caseId, userId, new AssetViewedTrigger(assetId), ct);
        return Ok(new { caseId, assetId, trigger = "asset_viewed" });
    }

    [HttpPost("suspects/{suspectId}/view")]
    public async Task<IActionResult> ViewSuspect(string caseId, string suspectId, CancellationToken ct)
    {
        var userId = UserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
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

        var gameTime = body.GameTimeMinutes;

        // Fire rules with time_elapsed trigger
        await _rulesEngine.EvaluateAndApplyAsync(caseId, userId, new TimeElapsedTrigger(gameTime), ct);

        // Process temporalEvents (idempotent via FiredTemporalEventIds)
        var caseData = await _storage.GetRawAsync(caseId, ct);
        var firedTemporalIds = new List<string>();
        if (caseData?.TemporalEvents is { Count: > 0 })
        {
            var session = await _context.CaseSessions
                .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                .OrderByDescending(cs => cs.SessionStart)
                .FirstOrDefaultAsync(ct);

            if (session is not null)
            {
                var fired = ParseList(session.FiredTemporalEventIds);
                var pending = caseData.TemporalEvents
                    .Where(te => te.TriggerAtMinutes <= gameTime && !fired.Contains(te.Id))
                    .ToList();

                foreach (var te in pending)
                {
                    fired.Add(te.Id);
                    firedTemporalIds.Add(te.Id);
                    // Reveal email / asset payloads, or just record a memo
                    if (!string.IsNullOrEmpty(te.Payload?.EmailId))
                    {
                        var existsE = await _context.CaseSessionVisibleEmails
                            .AnyAsync(v => v.UserId == userId && v.CaseId == caseId && v.EmailId == te.Payload.EmailId, ct);
                        if (!existsE)
                            _context.CaseSessionVisibleEmails.Add(new CaseSessionVisibleEmail
                            {
                                UserId = userId,
                                CaseId = caseId,
                                EmailId = te.Payload.EmailId,
                                UnlockedAt = DateTime.UtcNow
                            });
                    }
                    if (!string.IsNullOrEmpty(te.Payload?.AssetId))
                    {
                        var existsA = await _context.CaseSessionVisibleAssets
                            .AnyAsync(v => v.UserId == userId && v.CaseId == caseId && v.AssetId == te.Payload.AssetId, ct);
                        if (!existsA)
                            _context.CaseSessionVisibleAssets.Add(new CaseSessionVisibleAsset
                            {
                                UserId = userId,
                                CaseId = caseId,
                                AssetId = te.Payload.AssetId,
                                UnlockedAt = DateTime.UtcNow
                            });
                    }
                }
                session.FiredTemporalEventIds = JsonSerializer.Serialize(fired);
                await _context.SaveChangesAsync(ct);
            }
        }

        return Ok(new { caseId, gameTimeMinutes = gameTime, firedTemporalEventIds = firedTemporalIds });
    }

    private static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); }
    }
}
