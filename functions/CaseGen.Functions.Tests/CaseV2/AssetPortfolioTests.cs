using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class AssetPortfolioTests
{
    public static TheoryData<string, int, int, int, int, int, int, int, int> DifficultyBudgets => new()
    {
        { "Rookie", 10, 14, 5, 7, 3, 4, 1, 2 },
        { "Detective", 12, 16, 6, 8, 3, 4, 1, 2 },
        { "Detective2", 13, 17, 7, 9, 4, 5, 1, 2 },
        { "Sergeant", 14, 18, 8, 10, 4, 5, 2, 3 },
        { "Lieutenant", 15, 19, 8, 11, 4, 6, 2, 3 },
        { "Captain", 16, 20, 9, 12, 5, 6, 2, 3 },
        { "Commander", 17, 22, 10, 13, 5, 6, 2, 4 }
    };

    [Theory]
    [MemberData(nameof(DifficultyBudgets))]
    public void Portfolio_RespectsDossierAndInvestigativeBudgets(
        string difficulty,
        int minTotal,
        int maxTotal,
        int minInvestigative,
        int maxInvestigative,
        int minSuspects,
        int maxSuspects,
        int minScenes,
        int maxScenes)
    {
        var observedCounts = new HashSet<int>();
        foreach (var seed in new[] { 0, 1, 7, 19, 41 })
        {
            var suspectCount = minSuspects + seed % (maxSuspects - minSuspects + 1);
            var draft = CreateDraft(difficulty, seed, suspectCount);
            var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);
            observedCounts.Add(portfolio.Count);

            Assert.InRange(portfolio.Count, minTotal, maxTotal);
            Assert.InRange(portfolio.Count(slot => EvidenceRoles.IsInvestigative(slot.EvidenceRole)),
                minInvestigative, maxInvestigative);
            Assert.InRange(portfolio.Count(slot => slot.ImagePurpose == ImagePurposes.Scene),
                minScenes, maxScenes);
            Assert.Equal(portfolio.Count, portfolio.Select(slot => slot.InstanceId).Distinct().Count());
            Assert.Contains(portfolio, slot => slot.InstanceId == "asset.initial_report");
            Assert.True(portfolio.Select(slot => slot.Family).Distinct().Count()
                        >= DifficultyProfileCatalog.Get(difficulty).MinFamilies);

            foreach (var suspect in draft.SuspectStubs)
            {
                Assert.Single(portfolio.Where(slot =>
                    slot.SubjectSuspectId == suspect.Id
                    && slot.ImagePurpose == ImagePurposes.SuspectPortrait));
                Assert.Single(portfolio.Where(slot =>
                    slot.SubjectSuspectId == suspect.Id
                    && slot.Id == "suspect_interview"));
            }
        }

        Assert.True(observedCounts.Count > 1, $"{difficulty} should vary dossier volume across seeds");
    }

    [Fact]
    public void Portfolio_UsesRepeatableArchetypesWithUniqueInstanceIds()
    {
        var draft = CreateDraft("Commander", 23, 6);

        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);

        Assert.Equal(6, portfolio.Count(slot => slot.Id == "suspect_portrait"));
        Assert.Equal(6, portfolio.Count(slot => slot.Id == "suspect_interview"));
        Assert.Equal(portfolio.Count, portfolio.Select(slot => slot.InstanceId).Distinct().Count());
        Assert.Contains(portfolio, slot => slot.InstanceId == "asset.portrait_suspect_1");
        Assert.Contains(portfolio, slot => slot.InstanceId == "asset.interview_suspect_1");
    }

    [Fact]
    public void Portfolio_IncludesDigitalEvidenceWhenBlueprintNeedsIt()
    {
        var draft = CreateDraft("Rookie", 7, 4);
        draft.Blueprint.ClueLadder.Add(new CanonicalClue
        {
            Id = "clue.access",
            SourceType = "digital",
            Discovery = "A WhatsApp message fixed the handoff at 15:14."
        });

        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);

        Assert.Contains(portfolio, slot => slot.Family == "digital");
        Assert.Contains(portfolio, slot => slot.Id == "chat_export");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(19)]
    public void Portfolio_GuaranteesDigitalAssetForGenericDigitalClue(int seed)
    {
        var draft = CreateDraft("Rookie", seed, 4);
        draft.Blueprint.ClueLadder.Add(new CanonicalClue
        {
            Id = "clue.machine_record",
            SourceType = "digital",
            Discovery = "A machine-generated record establishes the relevant timestamp."
        });

        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);

        Assert.Contains(portfolio, slot =>
            slot.Family == "digital" && EvidenceRoles.IsInvestigative(slot.EvidenceRole));
    }

    [Fact]
    public void ContextualAssets_CanContainObjectsWithoutOwningClues()
    {
        var draft = CreateDraft("Rookie", 3, 1);
        draft.AssetStubs = ToStubs(EvidenceArchetypeCatalog.BuildPortfolio(draft));

        EvidenceGraphCompiler.Compile(draft);
        var report = EvidenceContractValidator.ValidatePlan(draft);

        Assert.DoesNotContain(report.Errors, error => error.Contains("contextual asset", StringComparison.Ordinal));
        Assert.All(draft.AssetStubs.Where(asset => asset.EvidenceRole == EvidenceRoles.Contextual), asset =>
        {
            Assert.Empty(asset.SupportsClueIds);
            Assert.NotEmpty(asset.ContainedObjectIds);
        });
    }

    [Fact]
    public void ContextualAssets_CannotBeRequiredEvidence()
    {
        var draft = CreateDraft("Rookie", 3, 1);
        draft.AssetStubs = ToStubs(EvidenceArchetypeCatalog.BuildPortfolio(draft));
        var contextual = draft.AssetStubs.First(asset => asset.EvidenceRole == EvidenceRoles.Contextual);
        draft.RequiredEvidenceIds.Add(contextual.Id);

        EvidenceGraphCompiler.Compile(draft);
        var report = EvidenceContractValidator.ValidatePlan(draft);

        Assert.Contains(report.Errors, error =>
            error.Contains($"contextual asset '{contextual.Id}' cannot be required evidence", StringComparison.Ordinal)
            || error.Contains($"cannot include contextual asset '{contextual.Id}'", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogAndSlots_AlwaysProduceSchemaSafeIds()
    {
        foreach (var archetype in EvidenceArchetypeCatalog.All)
            Assert.Matches("^[a-z0-9_]+$", archetype.Id);

        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(CreateDraft("Commander", 11, 6));
        Assert.All(portfolio, slot => Assert.Matches("^asset\\.[a-z0-9_]+$", slot.InstanceId));
    }

    [Fact]
    public void AssetPlanSchema_AllowsContextualAssetsWithoutClues()
    {
        var schema = (string)typeof(AssetPlanTask)
            .GetField("Schema", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetRawConstantValue()!;
        using var document = System.Text.Json.JsonDocument.Parse(schema);

        var itemProperties = document.RootElement
            .GetProperty("properties")
            .GetProperty("assets")
            .GetProperty("items")
            .GetProperty("properties");

        Assert.Equal(0, itemProperties.GetProperty("supportsClueIds").GetProperty("minItems").GetInt32());
        Assert.Contains(
            itemProperties.GetProperty("evidenceRole").GetProperty("enum").EnumerateArray(),
            value => value.GetString() == EvidenceRoles.Contextual);
    }

    [Fact]
    public void AssetPlanNormalizer_ReassignsIncompatibleDigitalClue()
    {
        var draft = CreateDraft("Rookie", 3, 3);
        draft.Blueprint.ClueLadder.Add(new CanonicalClue
        {
            Id = "clue.digital_record",
            SourceType = "digital",
            Discovery = "A machine record fixes the timestamp."
        });
        var assets = new List<AssetStub>
        {
            new()
            {
                Id = "asset.initial_report",
                ArchetypeId = "incident_report",
                Type = "document",
                EvidenceRole = EvidenceRoles.Primary,
                LayoutHint = "PoliceReport",
                SupportsClueIds = { "clue.digital_record" },
                ContainedObjectIds = { "document.initial_report" }
            },
            new()
            {
                Id = "asset.access_log",
                ArchetypeId = "access_log",
                Type = "digital",
                EvidenceRole = EvidenceRoles.Corroborative,
                LayoutHint = "AccessLog",
                ContainedObjectIds = { "document.access_log" }
            }
        };
        var method = typeof(AssetPlanTask).GetMethod(
            "NormalizeDeclaredOwnership",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method!.Invoke(null, [draft, assets]);

        Assert.DoesNotContain("clue.digital_record", assets[0].SupportsClueIds);
        Assert.Contains("clue.digital_record", assets[1].SupportsClueIds);
    }

    [Fact]
    public void AssetPlanNormalizer_DoesNotUseInterviewAsForensicInput()
    {
        var draft = CreateDraft("Detective", 8, 3);
        draft.Blueprint.ClueLadder.AddRange(
        [
            new CanonicalClue
            {
                Id = "clue.access_record",
                SourceType = "digital",
                Discovery = "An access record identifies the endpoint."
            },
            new CanonicalClue
            {
                Id = "clue.metadata",
                SourceType = "forensic",
                Discovery = "Metadata connects the action to the endpoint."
            }
        ]);
        var assets = new List<AssetStub>
        {
            new()
            {
                Id = "asset.interview_suspect_1",
                ArchetypeId = "suspect_interview",
                Type = "document",
                EvidenceRole = EvidenceRoles.Primary,
                LayoutHint = "InterviewTranscript",
                SupportsClueIds = { "clue.access_record" },
                ForensicInputClueIds = { "clue.metadata" },
                ContainedObjectIds = { "document.interview_suspect_1" }
            },
            new()
            {
                Id = "asset.access_log",
                ArchetypeId = "access_log",
                Type = "digital",
                EvidenceRole = EvidenceRoles.Corroborative,
                LayoutHint = "AccessLog",
                SupportsClueIds = { "clue.access_record" },
                ContainedObjectIds = { "document.access_log" }
            }
        };
        var method = typeof(AssetPlanTask).GetMethod(
            "NormalizeDeclaredOwnership",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method!.Invoke(null, [draft, assets]);

        Assert.Empty(assets[0].ForensicInputClueIds);
        Assert.Contains("clue.metadata", assets[1].ForensicInputClueIds);
    }

    private static CaseDraft CreateDraft(string difficulty, int seed, int suspectCount)
    {
        var suspects = Enumerable.Range(1, suspectCount)
            .Select(index => new SuspectStub
            {
                Id = $"suspect.suspect_{index}",
                Name = $"Suspect {index}",
                IsCulprit = index == 1
            })
            .ToList();
        return new CaseDraft
        {
            CaseId = $"case_{difficulty.ToLowerInvariant()}_{seed}",
            CulpritId = suspects[0].Id,
            Metadata = new PlotMetadata { Difficulty = difficulty, RequiredRank = difficulty },
            Request = new GenerateCaseV2Request
            {
                Seed = seed,
                Difficulty = difficulty,
                RequiredRank = difficulty,
                Language = "en-US"
            },
            SuspectStubs = suspects
        };
    }

    private static List<AssetStub> ToStubs(IEnumerable<EvidenceDossierSlot> slots) =>
        slots.Select(slot => new AssetStub
        {
            Id = slot.InstanceId,
            ArchetypeId = slot.Id,
            Type = slot.Type,
            Title = slot.SuggestedTitle ?? slot.DisplayName,
            Role = slot.Purpose,
            EvidenceRole = slot.EvidenceRole,
            SubjectSuspectId = slot.SubjectSuspectId,
            ImagePurpose = slot.ImagePurpose,
            LayoutHint = slot.Layout,
            ContainedObjectIds = { $"object.{slot.InstanceId["asset.".Length..]}" }
        }).ToList();
}
