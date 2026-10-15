using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

/// <inheritdoc />
public class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PromotionService> _logger;

    public PromotionService(ApplicationDbContext context, ILogger<PromotionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PromotionOutcome> SyncAsync(string userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            throw new InvalidOperationException($"User {userId} not found");

        // Aggregate only graded submissions; ungraded attempts never affect stats.
        var gradedSubs = await _context.CaseSubmissions
            .Where(s => s.SubmittedByUserId == userId && s.Graded)
            .Select(s => new { s.CaseId, s.IsCorrectSuspect, s.Score })
            .ToListAsync(ct);

        // Best score per graded case and whether it was ever resolved.
        var byCase = gradedSubs
            .GroupBy(s => s.CaseId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Resolved = g.Any(x => x.IsCorrectSuspect),
                BestScore = g.Max(x => x.Score)
            })
            .ToList();

        var casesAttempted = byCase.Count;
        var casesResolved = byCase.Count(c => c.Resolved);
        var casesFailed = casesAttempted - casesResolved;

        user.CasesResolved = casesResolved;
        user.CasesFailed = casesFailed;
        user.SuccessRate = casesAttempted == 0
            ? 0.0
            : Math.Round(100.0 * casesResolved / casesAttempted, 1);
        user.AverageScore = casesAttempted == 0
            ? 0.0
            : Math.Round(byCase.Average(c => c.BestScore), 1);
        // XP is the sum of the best graded score earned on each resolved case.
        user.ExperiencePoints = (int)Math.Round(byCase.Where(c => c.Resolved).Sum(c => c.BestScore));

        var previousRank = user.Rank;
        var targetRank = RankForResolvedCount(casesResolved);

        var promoted = (int)targetRank > (int)previousRank;
        if (promoted)
        {
            // Record one history row per level gained so the timeline is complete.
            for (var r = (int)previousRank + 1; r <= (int)targetRank; r++)
            {
                var newRank = (DetectiveRank)r;
                _context.UserRankHistories.Add(new UserRankHistory
                {
                    UserId = userId,
                    PreviousRank = (DetectiveRank)(r - 1),
                    NewRank = newRank,
                    ChangedAt = DateTime.UtcNow,
                    Reason = $"Promoted to {newRank} ({casesResolved} cases resolved)"
                });
            }

            user.Rank = targetRank;
            user.LastPromotionDate = DateTime.UtcNow;
            user.CanAccessHighPriorityCases = targetRank >= DetectiveRank.Sergeant;

            await DeliverPromotionEmailAsync(user, previousRank, targetRank, ct);

            _logger.LogInformation(
                "User {UserId} promoted {From} -> {To} ({Resolved} cases resolved)",
                userId, previousRank, targetRank, casesResolved);
        }

        await _context.SaveChangesAsync(ct);

        return new PromotionOutcome(promoted, previousRank, targetRank, casesResolved);
    }

    /// <summary>
    /// Highest rank whose cumulative resolved-case threshold is satisfied by
    /// <paramref name="casesResolved"/>. Rank is a pure function of resolved
    /// count, which makes promotion idempotent and demotion impossible.
    /// </summary>
    private static DetectiveRank RankForResolvedCount(int casesResolved)
    {
        var result = DetectiveRank.Rook;
        foreach (var (rank, required) in PromotionRules.ResolvedRequiredFor)
        {
            if (casesResolved >= required && (int)rank > (int)result)
                result = rank;
        }
        return result;
    }

    private async Task DeliverPromotionEmailAsync(
        User recipient, DetectiveRank previousRank, DetectiveRank newRank, CancellationToken ct)
    {
        var systemUser = await GetOrCreateSystemUserAsync(ct);

        var metadata = JsonSerializer.Serialize(new PromotionEmailMetadata
        {
            Kind = "promotion",
            PreviousRank = previousRank.ToString(),
            NewRank = newRank.ToString()
        });

        // Subject/Content are stored in the app default language (pt-BR) as a
        // fallback; the inbox UI localizes PromotionNotice e-mails from MetadataJson.
        _context.Emails.Add(new Email
        {
            ToUserId = recipient.Id,
            FromUserId = systemUser.Id,
            CaseId = null,
            Subject = $"Promoção: você agora é {newRank}",
            Content = $"Parabéns! Por seu desempenho exemplar, você foi promovido de {previousRank} para {newRank}.",
            Preview = $"Você foi promovido para {newRank}.",
            Priority = EmailPriority.High,
            Type = EmailType.PromotionNotice,
            IsSystemGenerated = true,
            MetadataJson = metadata,
            SentAt = DateTime.UtcNow
        });
    }

    private async Task<User> GetOrCreateSystemUserAsync(CancellationToken ct)
    {
        var systemUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == "system", ct);
        if (systemUser is not null) return systemUser;

        systemUser = new User
        {
            UserName = "system",
            Email = "system@fic-police.gov",
            FirstName = "System",
            LastName = "Admin",
            PersonalEmail = "system@example.com",
            Department = "System",
            Position = "System",
            BadgeNumber = "0000",
            EmailVerified = true,
            Rank = DetectiveRank.Commander
        };
        _context.Users.Add(systemUser);
        await _context.SaveChangesAsync(ct);
        return systemUser;
    }

    private sealed class PromotionEmailMetadata
    {
        public string Kind { get; set; } = "promotion";
        public string PreviousRank { get; set; } = string.Empty;
        public string NewRank { get; set; } = string.Empty;
    }
}
