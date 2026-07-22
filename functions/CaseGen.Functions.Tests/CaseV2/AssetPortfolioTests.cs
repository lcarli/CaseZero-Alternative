using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class AssetPortfolioTests
{
    [Fact]
    public void Portfolio_ChangesWithSeedAndMaintainsInvestigativeDiversity()
    {
        var first = new CaseDraft
        {
            CaseId = "case_a",
            Request = new GenerateCaseV2Request { Seed = 1 }
        };
        var second = new CaseDraft
        {
            CaseId = "case_b",
            Request = new GenerateCaseV2Request { Seed = 19 }
        };

        var firstPortfolio = EvidenceArchetypeCatalog.BuildPortfolio(first);
        var secondPortfolio = EvidenceArchetypeCatalog.BuildPortfolio(second);

        var budget = EvidenceArchetypeCatalog.GetBudget(first);
        Assert.InRange(firstPortfolio.Count, budget.MinAssets, budget.MaxAssets);
        Assert.Equal(firstPortfolio.Count, firstPortfolio.Select(a => a.Id).Distinct().Count());
        Assert.False(firstPortfolio.Select(a => a.Id).SequenceEqual(secondPortfolio.Select(a => a.Id)));
        Assert.Contains(firstPortfolio, a => a.Family == "testimonial");
        Assert.Contains(firstPortfolio, a => a.Family == "official");
        Assert.True(firstPortfolio.Select(a => a.Family).Distinct().Count() >= budget.MinFamilies);
    }

    [Fact]
    public void Portfolio_IncludesDigitalEvidenceWhenBlueprintNeedsIt()
    {
        var draft = new CaseDraft
        {
            CaseId = "case_digital",
            Request = new GenerateCaseV2Request { Seed = 7 },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue { Id = "clue.access", SourceType = "digital" }
                }
            }
        };

        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);

        Assert.Contains(portfolio, a => a.Family == "digital");
    }

    [Fact]
    public void Portfolio_SelectsChatExportForMessageClues()
    {
        var draft = new CaseDraft
        {
            CaseId = "case_chat",
            Metadata = new PlotMetadata { Difficulty = "Rookie" },
            Request = new GenerateCaseV2Request { Seed = 4, Difficulty = "Rookie" },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue
                    {
                        Id = "clue.message",
                        SourceType = "digital",
                        Discovery = "A WhatsApp message offered the missing item at 15:14."
                    }
                }
            }
        };

        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);

        Assert.Contains(portfolio, archetype => archetype.Id == "chat_export");
    }

    [Fact]
    public void Portfolio_RemainsDiverseAcrossManySeeds()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var draft = new CaseDraft
            {
                CaseId = $"case_{seed}",
                Request = new GenerateCaseV2Request { Seed = seed }
            };

            var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);

            var budget = EvidenceArchetypeCatalog.GetBudget(draft);
            Assert.InRange(portfolio.Count, budget.MinAssets, budget.MaxAssets);
            Assert.True(portfolio.Select(a => a.Family).Distinct().Count() >= budget.MinFamilies);
            Assert.True(portfolio.Select(a => a.Layout).Distinct().Count() >= Math.Min(4, budget.MinFamilies));
        }
    }

    [Fact]
    public void CatalogArchetypes_AlwaysProduceSchemaSafeCanonicalAssetIds()
    {
        foreach (var archetype in EvidenceArchetypeCatalog.All)
        {
            var id = $"asset.{archetype.Id}";
            Assert.Matches("^asset\\.[a-z0-9_]+$", id);
        }
    }

    [Theory]
    [InlineData("Rookie", 3, 5, 3)]
    [InlineData("Detective", 6, 8, 5)]
    [InlineData("Detective2", 7, 9, 5)]
    [InlineData("Sergeant", 8, 10, 6)]
    [InlineData("Lieutenant", 8, 10, 6)]
    [InlineData("Captain", 9, 10, 6)]
    [InlineData("Commander", 9, 10, 6)]
    public void Portfolio_RespectsDifficultyBudget(string difficulty, int min, int max, int minFamilies)
    {
        var observedCounts = new HashSet<int>();
        for (var seed = 0; seed < 50; seed++)
        {
            var draft = new CaseDraft
            {
                CaseId = $"case_{difficulty}_{seed}",
                Metadata = new PlotMetadata { Difficulty = difficulty },
                Request = new GenerateCaseV2Request { Seed = seed }
            };

            var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);
            observedCounts.Add(portfolio.Count);

            Assert.InRange(portfolio.Count, min, max);
            Assert.True(portfolio.Select(a => a.Family).Distinct().Count() >= minFamilies);
        }

        Assert.True(observedCounts.Count > 1, $"{difficulty} should vary evidence count across seeds");
    }
}
