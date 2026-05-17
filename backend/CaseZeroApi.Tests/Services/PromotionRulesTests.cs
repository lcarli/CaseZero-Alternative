using CaseZeroApi.Models;
using CaseZeroApi.Services;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class PromotionRulesTests
{
    [Fact]
    public void NextRank_FromRook_IsDetective()
    {
        Assert.Equal(DetectiveRank.Detective, PromotionRules.NextRank(DetectiveRank.Rook));
    }

    [Fact]
    public void NextRank_FromCommander_IsNull()
    {
        Assert.Null(PromotionRules.NextRank(DetectiveRank.Commander));
    }

    [Fact]
    public void Compute_AtCommander_PinsProgressTo100()
    {
        var progress = PromotionRules.Compute(DetectiveRank.Commander, 100);
        Assert.Equal(100.0, progress.ProgressPct);
        Assert.Null(progress.NextRank);
        Assert.Null(progress.CasesRemaining);
    }

    [Fact]
    public void Compute_AtRookWithZeroResolves_ProgressIsZero()
    {
        var progress = PromotionRules.Compute(DetectiveRank.Rook, 0);
        Assert.Equal(DetectiveRank.Detective, progress.NextRank);
        Assert.Equal(0.0, progress.ProgressPct);
        Assert.Equal(3, progress.CasesRequiredForNext);
        Assert.Equal(3, progress.CasesRemaining);
    }

    [Fact]
    public void Compute_AtRookWithHalfwayResolves_RoundsProgress()
    {
        // Rook->Detective needs 3 resolves; 2 resolves = 66.7%
        var progress = PromotionRules.Compute(DetectiveRank.Rook, 2);
        Assert.Equal(66.7, progress.ProgressPct);
        Assert.Equal(1, progress.CasesRemaining);
    }

    [Fact]
    public void Compute_OvershootsNextThreshold_ClampedTo100Until_NextRankApplied()
    {
        // Player has 10 resolves but is still ranked Detective (8 needed for Det2).
        // Progress measures Detective -> Detective2 span: floor 3, ceiling 8 (delta=5).
        // 10-3 = 7 into a 5-wide bucket → clamped to 100.
        var progress = PromotionRules.Compute(DetectiveRank.Detective, 10);
        Assert.Equal(100.0, progress.ProgressPct);
        Assert.Equal(0, progress.CasesRemaining);
    }

    [Fact]
    public void ResolvedRequiredFor_IsMonotonicallyIncreasing()
    {
        var ranks = Enum.GetValues<DetectiveRank>().OrderBy(r => (int)r).ToList();
        for (int i = 1; i < ranks.Count; i++)
        {
            Assert.True(
                PromotionRules.ResolvedRequiredFor[ranks[i]] > PromotionRules.ResolvedRequiredFor[ranks[i - 1]],
                $"Threshold for {ranks[i]} must be greater than {ranks[i - 1]}");
        }
    }
}
