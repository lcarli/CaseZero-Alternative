using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICaseV2StorageService _storage;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        ApplicationDbContext db,
        ICaseV2StorageService storage,
        ILogger<ProfileController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Detailed profile statistics for the current user. Powers the /profile page:
    /// per-case history, score-by-difficulty, full submission timeline, rank-change
    /// history and category breakdown averages.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var userInfo = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Rank, u.LastPromotionDate, u.FirstName, u.LastName, u.BadgeNumber })
            .FirstOrDefaultAsync(ct);
        var currentRank = userInfo?.Rank ?? DetectiveRank.Detective;

        // All submissions for this user, newest first.
        var subs = await _db.CaseSubmissions.AsNoTracking()
            .Where(s => s.SubmittedByUserId == userId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync(ct);

        // Build a caseId -> metadata lookup so we can attach titles / difficulty
        // to each row without re-reading the bundles.
        var allCases = await _storage.ListCasesAsync(ct);
        var caseMetaById = allCases.ToDictionary(
            c => c.CaseId,
            c => new { c.Title, c.Difficulty, c.RequiredRank, c.Category },
            StringComparer.OrdinalIgnoreCase);

        string GetTitle(string id) => caseMetaById.TryGetValue(id, out var m) ? m.Title : id;
        string GetDifficulty(string id) => caseMetaById.TryGetValue(id, out var m) ? (m.Difficulty ?? "Unknown") : "Unknown";

        // ── caseHistory: one row per case the player has touched ──────────
        var caseHistory = subs
            .GroupBy(s => s.CaseId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.SubmittedAt).ToList();
                var resolvedGraded = g.Any(x => x.IsCorrectSuspect && x.Graded);
                var resolvedUngraded = !resolvedGraded && g.Any(x => x.IsCorrectSuspect);
                var bestScore = g.Max(x => x.Score);
                return new
                {
                    caseId = g.Key,
                    title = GetTitle(g.Key),
                    difficulty = GetDifficulty(g.Key),
                    totalAttempts = g.Count(),
                    gradedAttempts = g.Count(x => x.Graded),
                    bestScore = Math.Round(bestScore, 1),
                    isResolved = resolvedGraded || resolvedUngraded,
                    resolvedViaGraded = resolvedGraded,
                    firstAttemptAt = ordered.First().SubmittedAt,
                    lastAttemptAt = ordered.Last().SubmittedAt
                };
            })
            .OrderByDescending(x => x.lastAttemptAt)
            .ToList();

        // ── scoreByDifficulty ──────────────────────────────────────────────
        // Avg over best graded score per case for each difficulty bucket.
        var scoreByDifficulty = subs
            .Where(s => s.Graded)
            .GroupBy(s => s.CaseId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Difficulty = GetDifficulty(g.Key),
                BestScore = g.Max(x => x.Score),
                IsResolved = g.Any(x => x.IsCorrectSuspect)
            })
            .GroupBy(x => x.Difficulty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => DifficultyOrdinal(g.Key))
            .Select(g => new
            {
                difficulty = g.Key,
                casesPlayed = g.Count(),
                casesResolved = g.Count(x => x.IsResolved),
                avgBestScore = Math.Round(g.Average(x => x.BestScore), 1)
            })
            .ToList();

        // ── submissionsTimeline: every submission, no cap ──────────────────
        var submissionsTimeline = subs.Select(s => new
        {
            date = s.SubmittedAt,
            caseId = s.CaseId,
            caseTitle = GetTitle(s.CaseId),
            difficulty = GetDifficulty(s.CaseId),
            type = s.IsCorrectSuspect
                ? (s.Graded ? "resolved" : "resolved_ungraded")
                : (s.Graded ? "attempted" : "attempted_ungraded"),
            score = Math.Round(s.Score, 1),
            graded = s.Graded,
            attemptNumber = s.AttemptNumber
        }).ToList();

        // ── rankHistory ────────────────────────────────────────────────────
        var rankHistory = await _db.UserRankHistories.AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new
            {
                previousRank = h.PreviousRank.HasValue ? h.PreviousRank.Value.ToString() : null,
                newRank = h.NewRank.ToString(),
                changedAt = h.ChangedAt,
                reason = h.Reason
            })
            .ToListAsync(ct);

        // ── categoryBreakdown: averages over submissions that have it stored ──
        // Only graded submissions with breakdown columns populated are aggregated.
        var withBreakdown = subs
            .Where(s => s.Graded && s.CulpritScore.HasValue && s.EvidenceScore.HasValue
                && s.AnalysisScore.HasValue && s.QuestionsScore.HasValue)
            .ToList();

        object categoryBreakdown = withBreakdown.Count == 0
            ? new
            {
                sampleSize = 0,
                culpritAvg = 0.0,
                evidenceAvg = 0.0,
                analysisAvg = 0.0,
                questionsAvg = 0.0,
                culpritWeight = 0.0,
                evidenceWeight = 0.0,
                analysisWeight = 0.0,
                questionsWeight = 0.0
            }
            : new
            {
                sampleSize = withBreakdown.Count,
                culpritAvg = Math.Round(withBreakdown.Average(s => s.CulpritScore!.Value), 4),
                evidenceAvg = Math.Round(withBreakdown.Average(s => s.EvidenceScore!.Value), 4),
                analysisAvg = Math.Round(withBreakdown.Average(s => s.AnalysisScore!.Value), 4),
                questionsAvg = Math.Round(withBreakdown.Average(s => s.QuestionsScore!.Value), 4),
                // Reference weights so the UI can render filled / total bars.
                culpritWeight = 0.4,
                evidenceWeight = 0.2,
                analysisWeight = 0.2,
                questionsWeight = 0.2
            };

        // ── promotion snapshot (also used by dashboard but exposed here too) ─
        var resolvedCaseCount = caseHistory.Count(c => c.resolvedViaGraded);
        var promo = PromotionRules.Compute(currentRank, resolvedCaseCount);

        return Ok(new
        {
            agent = new
            {
                firstName = userInfo?.FirstName,
                lastName = userInfo?.LastName,
                badgeNumber = userInfo?.BadgeNumber,
                currentRank = currentRank.ToString(),
                lastPromotionDate = userInfo?.LastPromotionDate
            },
            promotion = new
            {
                currentRank = promo.CurrentRank.ToString(),
                nextRank = promo.NextRank?.ToString(),
                casesResolved = promo.CasesResolved,
                casesRequiredForCurrent = promo.CasesRequiredForCurrent,
                casesRequiredForNext = promo.CasesRequiredForNext,
                casesRemaining = promo.CasesRemaining,
                progressPct = promo.ProgressPct
            },
            caseHistory,
            scoreByDifficulty,
            submissionsTimeline,
            rankHistory,
            categoryBreakdown
        });
    }

    private static int DifficultyOrdinal(string? difficulty) => difficulty?.Trim().ToLowerInvariant() switch
    {
        "rookie" or "rook" => 0,
        "detective" => 1,
        "detective2" => 2,
        "sergeant" => 3,
        "lieutenant" => 4,
        "captain" => 5,
        "commander" => 6,
        _ => 99
    };
}
