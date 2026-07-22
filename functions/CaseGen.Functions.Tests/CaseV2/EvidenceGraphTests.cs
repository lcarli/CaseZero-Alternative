using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using CaseGen.Functions.Models.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class EvidenceGraphTests
{
    [Fact]
    public void DifficultyProfiles_ProgressEvidenceAndForensics()
    {
        var rookie = DifficultyProfileCatalog.Get("Rookie");
        var detective = DifficultyProfileCatalog.Get("Detective");
        var commander = DifficultyProfileCatalog.Get("Commander");

        Assert.True(rookie.AllEvidenceInitial);
        Assert.Equal(0, rookie.MaxAnalyses);
        Assert.True(detective.MinAssets > rookie.MinAssets);
        Assert.True(commander.MinAssets > detective.MinAssets);
        Assert.True(commander.MinIndependentCulpritSources > detective.MinIndependentCulpritSources);
    }

    [Fact]
    public void EvidenceGraph_MapsClaimsAndRedHerringResolutions()
    {
        var draft = new CaseDraft
        {
            CulpritId = "suspect.culprit",
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue
                    {
                        Id = "clue.access",
                        Discovery = "The access log records credential 41 at 21:10.",
                        Inference = "The culprit had access.",
                        SupportsSuspectId = "suspect.culprit",
                        Strength = "supporting",
                        SourceType = "digital"
                    }
                },
                RedHerrings =
                {
                    new CanonicalRedHerring
                    {
                        SuspectId = "suspect.decoy",
                        Suspicion = "The decoy argued with the victim.",
                        Verification = "A timestamped call lasted from 21:02 to 21:19.",
                        Resolution = "A timestamped call places the decoy elsewhere."
                    }
                }
            },
            AssetStubs =
            {
                new AssetStub
                {
                    Id = "asset.access",
                    SupportsClueIds = { "clue.access" },
                    ResolvesRedHerringSuspectIds = { "suspect.decoy" }
                }
            }
        };

        var graph = EvidenceGraphCompiler.Compile(draft);

        Assert.Equal("asset.access", Assert.Single(graph.Claims).AssetIds.Single());
        Assert.Equal("asset.access", Assert.Single(graph.RedHerrings).AssetIds.Single());
    }

    [Fact]
    public void BlueprintValidator_RejectsCorrelatedCulpritSources()
    {
        var draft = new CaseDraft
        {
            Metadata = new() { Difficulty = "Detective", RequiredRank = "Detective" },
            CulpritId = "suspect.culprit",
            SuspectStubs =
            {
                new() { Id = "suspect.culprit" },
                new() { Id = "suspect.decoy_one" },
                new() { Id = "suspect.decoy_two" }
            },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new() { Id = "clue.one", SupportsSuspectId = "suspect.culprit", Strength = "supporting", SourceType = "digital" },
                    new() { Id = "clue.two", SupportsSuspectId = "suspect.culprit", Strength = "decisive", SourceType = "digital" }
                }
            }
        };

        var report = PipelineStageValidator.ValidateBlueprint(draft);

        Assert.Contains(report.Errors, error => error.Contains("independent source types", StringComparison.Ordinal));
    }

    [Fact]
    public void BlueprintValidator_RequiresIdentityActionAndForensicAttributionRoles()
    {
        var draft = new CaseDraft
        {
            Metadata = new() { Difficulty = "Detective", RequiredRank = "Detective" },
            CulpritId = "suspect.culprit",
            SuspectStubs =
            {
                new() { Id = "suspect.culprit" },
                new() { Id = "suspect.decoy_one" },
                new() { Id = "suspect.decoy_two" }
            },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new() { Id = "clue.identity", SupportsSuspectId = "suspect.culprit", Strength = "supporting", SourceType = "document" },
                    new() { Id = "clue.action", SupportsSuspectId = "suspect.culprit", Strength = "supporting", SourceType = "digital" },
                    new() { Id = "clue.forensic", SupportsSuspectId = "suspect.culprit", Strength = "decisive", SourceType = "forensic" }
                }
            }
        };

        var missingRoles = PipelineStageValidator.ValidateBlueprint(draft);

        Assert.Contains(missingRoles.Errors, error => error.Contains("identity-bridge", StringComparison.Ordinal));
        Assert.Contains(missingRoles.Errors, error => error.Contains("independent action", StringComparison.Ordinal));
        Assert.Contains(missingRoles.Errors, error => error.Contains("forensic-attribution", StringComparison.Ordinal));

        draft.Blueprint.ClueLadder[0].Role = "identity";
        draft.Blueprint.ClueLadder[1].Role = "action";
        draft.Blueprint.ClueLadder[2].Role = "forensicAttribution";
        var completeRoles = PipelineStageValidator.ValidateBlueprint(draft);

        Assert.DoesNotContain(completeRoles.Errors, error => error.Contains("identity-bridge", StringComparison.Ordinal));
        Assert.DoesNotContain(completeRoles.Errors, error => error.Contains("independent action", StringComparison.Ordinal));
        Assert.DoesNotContain(completeRoles.Errors, error => error.Contains("forensic-attribution", StringComparison.Ordinal));
    }

    [Fact]
    public void RepairPlan_BlueprintFailureInvalidatesAllDownstreamStages()
    {
        var draft = DraftFor("Detective");

        var plan = RepairCoordinator.BuildPlan(
            draft,
            Array.Empty<RedTeamTask.Finding>(),
            new[] { new RepairIssue("blueprint", "canonical chronology is contradictory") },
            solverFailed: false);

        Assert.True(plan.Blueprint);
        Assert.True(plan.Suspects);
        Assert.True(plan.AssetPlan);
        Assert.True(plan.Assets);
        Assert.True(plan.Timeline);
        Assert.True(plan.Forensics);
        Assert.True(plan.Rules);
        Assert.True(plan.Solution);
    }

    [Fact]
    public void RepairPlan_EvidencePlanFailureRebuildsPlanAndConsumers()
    {
        var plan = RepairCoordinator.BuildPlan(
            DraftFor("Detective"),
            Array.Empty<RedTeamTask.Finding>(),
            new[] { new RepairIssue("evidencePlan", "clue has no evidence asset") },
            solverFailed: false);

        Assert.False(plan.Blueprint);
        Assert.True(plan.AssetPlan);
        Assert.True(plan.Assets);
        Assert.True(plan.Forensics);
        Assert.True(plan.Solution);
    }

    [Fact]
    public void RepairPlan_RookieNeverIntroducesForensics()
    {
        var finding = new RedTeamTask.Finding
        {
            Severity = "high",
            OwnerStage = "forensics",
            Issue = "forensic dependency"
        };

        var plan = RepairCoordinator.BuildPlan(
            DraftFor("Rookie"),
            new[] { finding },
            new[] { new RepairIssue("forensics", "Rookie cannot contain a forensic workflow") },
            solverFailed: true);

        Assert.False(plan.Forensics);
        Assert.True(plan.Assets);
        Assert.True(plan.Solution);
    }

    [Fact]
    public void EvidenceContentValidatorReadsBodyAndBodyDocTogether()
    {
        var draft = DraftFor("Detective");
        draft.Blueprint.ClueLadder.Add(new CanonicalClue
        {
            Id = "clue.access",
            Discovery = "Credential 41 opened the archive at 21:10."
        });
        draft.AssetStubs.Add(new AssetStub
        {
            Id = "asset.access",
            SupportsClueIds = { "clue.access" }
        });
        draft.AssetFull.Add(new EvidenceAsset
        {
            Id = "asset.access",
            Body = "Short stray body.",
            BodyDoc = new EvidenceDocument
            {
                Sections =
                {
                    new EvidenceSection
                    {
                        Kind = "narrative",
                        Text = "Credential 41 opened the archive at 21:10."
                    }
                }
            }
        });
        EvidenceGraphCompiler.Compile(draft);

        var report = PipelineStageValidator.ValidateEvidenceContent(draft);

        Assert.Empty(report.Errors);
    }

    [Fact]
    public void ForensicClue_IsMappedToAndValidatedInResultAsset()
    {
        var draft = DraftFor("Detective");
        draft.Blueprint.ClueLadder.Add(new CanonicalClue
        {
            Id = "clue.account_owner",
            SourceType = "forensic",
            Discovery = "The verified account owner is R. Monteiro."
        });
        draft.Blueprint.ClueLadder.Add(new CanonicalClue
        {
            Id = "clue.custody",
            SourceType = "document",
            Discovery = "The bank export was collected under seal C-41."
        });
        draft.AssetStubs.Add(new AssetStub
        {
            Id = "asset.bank_export",
            ArchetypeId = "bank_statement",
            Type = "digital",
            LayoutHint = "BankStatement",
            SupportsClueIds = { "clue.custody" },
            ContainedObjectIds = { "account.fm_8821" },
            ForensicInputClueIds = { "clue.account_owner" }
        });
        draft.AssetFull.Add(new EvidenceAsset { Id = "asset.bank_export", Type = "digital" });
        draft.AnalysisTypes.Add(ForensicMethodCatalog.CreateAnalysisType("AccountAttribution", 60));
        draft.ForensicStubs.Add(new ForensicOutcomeStub
        {
            InputAssetId = "asset.bank_export",
            InputObjectId = "account.fm_8821",
            AnalysisType = "AccountAttribution",
            Findings = true,
            SupportsClueIds = { "clue.account_owner" },
            ProducedProperties = { ForensicObservationProperty.AccountOwnership },
            LimitationKeys = { "ownership_not_intent" },
            ChainOfCustodyObservationId = "observation.custody_bank_export",
            ResultLayoutId = "ForensicReport"
        });
        draft.ForensicFull.Add(new ForensicsOutcome
        {
            InputAssetId = "asset.bank_export",
            AnalysisType = "AccountAttribution",
            Findings = true,
            ResultAssetId = "asset.report_account_owner"
        });
        draft.ResultAssets.Add(new EvidenceAsset
        {
            Id = "asset.report_account_owner",
            BodyDoc = new EvidenceDocument
            {
                Layout = "ForensicReport",
                Sections =
                {
                    new EvidenceSection
                    {
                        Kind = "keyValue",
                        Heading = "Items Submitted",
                        Text = "account.fm_8821"
                    },
                    new EvidenceSection
                    {
                        Kind = "narrative",
                        Text = "The verified account owner is R. Monteiro."
                    },
                    new EvidenceSection
                    {
                        Kind = "narrative",
                        Heading = "Limitations",
                        Text = "Recorded ownership does not establish intent or personal use."
                    }
                }
            }
        });

        var graph = EvidenceGraphCompiler.Compile(draft);
        var report = PipelineStageValidator.ValidateForensics(draft);

        Assert.Equal(
            "asset.report_account_owner",
            graph.Claims.Single(claim => claim.ClueId == "clue.account_owner").AssetIds.Single());
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void ForensicValidator_RejectsOutcomeThatAnalyzesTheWrongInputAsset()
    {
        var draft = DraftFor("Detective");
        draft.Blueprint.ClueLadder.Add(new CanonicalClue
        {
            Id = "clue.metadata",
            SourceType = "forensic",
            Discovery = "The receipt PDF metadata records author tag CBR-04."
        });
        draft.AssetStubs.Add(new AssetStub
        {
            Id = "asset.receipt",
            Type = "document",
            ForensicInputClueIds = { "clue.metadata" }
        });
        draft.AssetFull.Add(new EvidenceAsset { Id = "asset.receipt", Type = "document" });
        draft.AssetFull.Add(new EvidenceAsset { Id = "asset.email", Type = "digital" });
        draft.AnalysisTypes.Add(new ForensicsAnalysisType
        {
            Type = "MetadataAnalysis",
            AvailableFor = { "digital", "document" }
        });
        draft.ForensicStubs.Add(new ForensicOutcomeStub
        {
            InputAssetId = "asset.email",
            AnalysisType = "MetadataAnalysis",
            Findings = true,
            SupportsClueIds = { "clue.metadata" }
        });

        var report = PipelineStageValidator.ValidateForensics(draft);

        Assert.Contains(report.Errors, error => error.Contains("owned input asset", StringComparison.Ordinal));
    }

    private static CaseDraft DraftFor(string difficulty) => new()
    {
        Metadata = new() { Difficulty = difficulty, RequiredRank = difficulty },
        Request = new() { Difficulty = difficulty, RequiredRank = difficulty }
    };
}
