using CaseZeroApi.Models;

namespace CaseZeroApi.Services;

/// <summary>
/// Owns the career-progression side effects of solving cases. Recomputes a
/// player's aggregate stats from their graded submissions and derives the rank
/// purely from the number of cases resolved, so the operation is idempotent and
/// never demotes. When the derived rank is higher than the persisted one, the
/// player is promoted: <see cref="User.Rank"/> and <see cref="User.LastPromotionDate"/>
/// are updated, one <see cref="UserRankHistory"/> row is written per level gained,
/// and an in-game promotion e-mail is delivered.
/// </summary>
public interface IPromotionService
{
    /// <summary>
    /// Recomputes <paramref name="userId"/>'s stats and rank and applies any
    /// promotion. Safe to call repeatedly. Returns a snapshot describing whether
    /// a promotion happened during this call.
    /// </summary>
    Task<PromotionOutcome> SyncAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Result of a <see cref="IPromotionService.SyncAsync"/> call.
/// </summary>
public record PromotionOutcome(
    bool Promoted,
    DetectiveRank PreviousRank,
    DetectiveRank NewRank,
    int CasesResolved);
