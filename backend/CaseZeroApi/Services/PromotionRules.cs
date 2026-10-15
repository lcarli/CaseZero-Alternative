using CaseZeroApi.Models;

namespace CaseZeroApi.Services;

/// <summary>
/// Computes promotion thresholds and progress for the current player. The rules
/// are deliberately data-light — they live as a single source of truth here so
/// the dashboard and (future) automated promotion job can consult them.
///
/// Thresholds are cumulative: to reach rank N the player needs that many
/// graded-correct case resolves total since account creation.
/// </summary>
public static class PromotionRules
{
    /// <summary>Number of graded-correct resolves required to reach each rank.</summary>
    public static readonly IReadOnlyDictionary<DetectiveRank, int> ResolvedRequiredFor = new Dictionary<DetectiveRank, int>
    {
        [DetectiveRank.Rook]        = 0,
        [DetectiveRank.Detective]   = 3,
        [DetectiveRank.Detective2]  = 8,
        [DetectiveRank.Sergeant]    = 16,
        [DetectiveRank.Lieutenant]  = 28,
        [DetectiveRank.Captain]     = 44,
        [DetectiveRank.Commander]   = 65,
    };

    /// <summary>
    /// Returns the next rank above <paramref name="current"/>, or null if the
    /// player is already at Commander.
    /// </summary>
    public static DetectiveRank? NextRank(DetectiveRank current)
    {
        var next = (int)current + 1;
        return Enum.IsDefined(typeof(DetectiveRank), next) ? (DetectiveRank?)next : null;
    }

    public record PromotionProgress(
        DetectiveRank CurrentRank,
        DetectiveRank? NextRank,
        int CasesResolved,
        int CasesRequiredForCurrent,
        int? CasesRequiredForNext,
        int? CasesRemaining,
        double ProgressPct
    );

    /// <summary>
    /// Builds a snapshot of where the player sits in the promotion ladder.
    /// Progress percent is computed against the *delta* between the current
    /// and next thresholds, so the bar always fills 0..100 between adjacent
    /// ranks rather than against the absolute total.
    /// </summary>
    public static PromotionProgress Compute(DetectiveRank current, int casesResolved)
    {
        var next = NextRank(current);
        var floor = ResolvedRequiredFor[current];
        int? ceiling = next.HasValue ? ResolvedRequiredFor[next.Value] : null;

        if (!next.HasValue || !ceiling.HasValue)
        {
            return new PromotionProgress(
                current, null, casesResolved, floor, null, null, 100.0);
        }

        var span = Math.Max(1, ceiling.Value - floor);
        var into = Math.Max(0, casesResolved - floor);
        var pct = Math.Min(100.0, Math.Round(100.0 * into / span, 1));
        var remaining = Math.Max(0, ceiling.Value - casesResolved);

        return new PromotionProgress(
            current, next, casesResolved, floor, ceiling, remaining, pct);
    }
}
