using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly ICaseV2StorageService _storage;
    private readonly ISolutionService _solutionService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CasesController> _logger;

    public CasesController(
        ICaseV2StorageService storage,
        ISolutionService solutionService,
        ApplicationDbContext db,
        ILogger<CasesController> logger)
    {
        _storage = storage;
        _solutionService = solutionService;
        _db = db;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var allCases = await _storage.ListCasesAsync(ct);

        // Resolve current user's rank from the DB so we can filter cases the player
        // isn't ranked for. Falls back to Detective if anything is off.
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRank = 1; // Detective default
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Rank })
                .FirstOrDefaultAsync(ct);
            if (user is not null) userRank = (int)user.Rank;
        }

        // Filter: keep cases whose requiredRank ordinal <= player's rank.
        var cases = allCases.Where(c => RankOrdinal(c.RequiredRank) <= userRank).ToList();
        var locked = allCases.Count - cases.Count;
        if (locked > 0)
            _logger.LogInformation("Dashboard hid {N} case(s) above user rank {Rank}", locked, userRank);

        return Ok(new
        {
            stats = new
            {
                casesResolved = 0,
                casesActive = cases.Count,
                successRate = 0.0,
                averageRating = 0.0
            },
            cases = cases.Select(c => new
            {
                caseId = c.CaseId,
                title = c.Title,
                description = c.Description,
                difficulty = c.Difficulty,
                category = c.Category,
                estimatedDurationMinutes = c.EstimatedDurationMinutes,
                briefing = c.Briefing,
                tags = c.Tags,
                requiredRank = c.RequiredRank
            }).ToList(),
            recentActivities = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Maps a v2 requiredRank string to the ordinal in <see cref="Models.DetectiveRank"/>.
    /// Treats "Rookie" and "Rook" as equivalent (the seeded users use "Rook").
    /// </summary>
    private static int RankOrdinal(string? rankName) => rankName?.Trim().ToLowerInvariant() switch
    {
        "rookie" or "rook" => 0,
        "detective" => 1,
        "detective2" => 2,
        "sergeant" => 3,
        "lieutenant" => 4,
        "captain" => 5,
        "commander" => 6,
        _ => 1 // unknown → assume Detective entry-level
    };

    [HttpGet]
    public async Task<ActionResult<List<CaseV2Metadata2>>> ListCases(CancellationToken ct)
    {
        var cases = await _storage.ListCasesAsync(ct);
        return Ok(cases);
    }

    [HttpGet("{caseId}")]
    public async Task<ActionResult<CaseV2Sanitized>> GetCase(string caseId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Seed initial visibility + session on first access so the downstream
        // controllers that gate on CaseSessionVisible*/CaseSession can serve
        // initial entities without an explicit "start session" call.
        await EnsureSessionAndInitialVisibilityAsync(caseId, userId, ct);

        var data = await _storage.GetForUserAsync(caseId, userId, ct);
        return data is null ? NotFound(new { error = $"Case not found: {caseId}" }) : Ok(data);
    }

    private async Task EnsureSessionAndInitialVisibilityAsync(string caseId, string userId, CancellationToken ct)
    {
        var raw = await _storage.GetRawAsync(caseId, ct);
        if (raw is null) return;

        var unlockAll =
            string.Equals(raw.Metadata.UnlockMode, "all_initial", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrEmpty(raw.Metadata.UnlockMode) &&
             string.Equals(raw.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase));

        // 1) Ensure there is a CaseSession for this (user, case).
        var hasSession = await _db.CaseSessions
            .AnyAsync(cs => cs.UserId == userId && cs.CaseId == caseId, ct);
        if (!hasSession)
        {
            _db.CaseSessions.Add(new Models.CaseSession
            {
                UserId = userId,
                CaseId = caseId,
                SessionStart = DateTime.UtcNow,
                IsActive = true,
                Status = Models.SessionStatus.Active
            });
        }

        // 2) Seed visible assets.
        var existingAssetIds = await _db.CaseSessionVisibleAssets
            .Where(va => va.UserId == userId && va.CaseId == caseId)
            .Select(va => va.AssetId)
            .ToListAsync(ct);
        foreach (var asset in raw.Assets)
        {
            var visible = unlockAll ||
                          string.Equals(asset.Visibility, "initial", StringComparison.OrdinalIgnoreCase);
            if (!visible) continue;
            if (existingAssetIds.Contains(asset.Id)) continue;
            _db.CaseSessionVisibleAssets.Add(new Models.CaseSessionVisibleAsset
            {
                UserId = userId,
                CaseId = caseId,
                AssetId = asset.Id,
                UnlockedAt = DateTime.UtcNow
            });
        }

        // 3) Seed visible emails.
        var existingEmailIds = await _db.CaseSessionVisibleEmails
            .Where(ve => ve.UserId == userId && ve.CaseId == caseId)
            .Select(ve => ve.EmailId)
            .ToListAsync(ct);
        foreach (var email in raw.Emails)
        {
            var visible = unlockAll ||
                          string.Equals(email.Visibility, "initial", StringComparison.OrdinalIgnoreCase);
            if (!visible) continue;
            if (existingEmailIds.Contains(email.Id)) continue;
            _db.CaseSessionVisibleEmails.Add(new Models.CaseSessionVisibleEmail
            {
                UserId = userId,
                CaseId = caseId,
                EmailId = email.Id,
                UnlockedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    [HttpGet("{caseId}/raw")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CaseV2>> GetCaseRaw(string caseId, CancellationToken ct)
    {
        var data = await _storage.GetRawAsync(caseId, ct);
        return data is null ? NotFound(new { error = $"Case not found: {caseId}" }) : Ok(data);
    }

    [HttpHead("{caseId}")]
    public async Task<IActionResult> CaseExists(string caseId, CancellationToken ct)
    {
        return await _storage.CaseExistsAsync(caseId, ct) ? Ok() : NotFound();
    }

    [HttpPost("{caseId}/submit")]
    public async Task<IActionResult> Submit(string caseId, [FromBody] SubmitCaseRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Request body required" });
        try
        {
            var result = await _solutionService.SubmitAsync(caseId, userId, request, ct);
            return Ok(result);
        }
        catch (MaxAttemptsExceededException ex)
        {
            return Conflict(new { error = ex.Message, maxAttempts = ex.MaxAttempts });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
