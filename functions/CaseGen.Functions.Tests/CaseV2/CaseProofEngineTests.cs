using CaseGen.Functions.Services.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseProofEngineTests
{
    [Fact]
    public void DetectiveMilestone_HasOneCulpritTwoIndependentReachablePathsAndReachableDecoyRefutations()
    {
        var graph = DetectiveGraph();

        var wellFormed = CaseGraphValidator.Validate(graph);
        var reachability = ReachabilityClosureEngine.Calculate(graph);
        var closure = DerivationClosureEngine.Calculate(graph, reachability.FinalObservationIds);
        var uniqueness = CulpritUniquenessGate.Validate(graph, "suspect.culprit", "Detective");
        var independence = IndependentProofPathGate.Validate(graph, "suspect.culprit", "Detective");
        var reachable = ReachabilityGate.Validate(graph, "suspect.culprit");
        var leakage = PrematureSolutionLeakageGate.Validate(graph, "suspect.culprit", "Detective");

        Assert.True(wellFormed.IsValid, string.Join(Environment.NewLine, wellFormed.Errors.Select(error => error.Message)));
        Assert.Empty(reachability.UnreachableRequiredSourceIds);
        Assert.Empty(reachability.UnreachableRequiredAnalysisIds);
        Assert.True(closure.CanDerive("fact.decoy_one_excluded"));
        Assert.True(closure.CanDerive("fact.decoy_two_excluded"));
        Assert.True(uniqueness.IsValid, string.Join(Environment.NewLine, uniqueness.Failures.Select(error => error.Message)));
        Assert.True(independence.IsValid, string.Join(Environment.NewLine, independence.Failures.Select(error => error.Message)));
        Assert.Equal(2, independence.Paths.Count);
        Assert.True(reachable.IsValid, string.Join(Environment.NewLine, reachable.Failures.Select(error => error.Message)));
        Assert.True(leakage.IsValid, string.Join(Environment.NewLine, leakage.Failures.Select(error => error.Message)));
        Assert.Null(leakage.LeakingPath);
    }

    [Fact]
    public void Closure_IsDeterministicAndReturnsMeasuredPaths()
    {
        var graph = DetectiveGraph();
        var observations = ReachabilityClosureEngine.Calculate(graph).FinalObservationIds;

        var first = DerivationClosureEngine.Calculate(graph, observations);
        var second = DerivationClosureEngine.Calculate(graph, observations.Reverse());
        var firstPaths = first.GetPaths("fact.culprit");
        var secondPaths = second.GetPaths("fact.culprit");

        Assert.Equal(firstPaths.Select(path => path.Signature), secondPaths.Select(path => path.Signature));
        Assert.All(firstPaths, path =>
        {
            Assert.True(path.Depth >= 1);
            Assert.NotEmpty(path.ObservationIds);
            Assert.NotEmpty(path.DerivationIds);
        });
    }

    [Fact]
    public void CulpritGate_RejectsTwoCulprits()
    {
        var graph = DetectiveGraph();
        AddObservedCulpritPath(
            graph,
            "suspect.decoy_one",
            "person.decoy_one",
            "asset.decoy_accusation",
            "origin.decoy_accusation");

        var result = CulpritUniquenessGate.Validate(graph, "suspect.culprit", "Detective");

        Assert.Contains(result.Failures, failure => failure.Code == "multiple_valid_culprits"
                                                   && failure.NodeId == "suspect.decoy_one");
    }

    [Fact]
    public void CulpritGate_RejectsPrivateOnlyTruth()
    {
        var graph = BasicSuspectGraph();
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.private_identity",
            Predicate = "identity",
            SubjectId = "person.culprit",
            LiteralValue = "culprit",
            LiteralType = LiteralValueType.String,
            Visibility = FactVisibility.Private
        });
        graph.Facts.Add(CulpritFact("fact.culprit", "person.culprit"));
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.private",
            PremiseIds = { "fact.private_identity" },
            Rule = DerivationRule.IdentityMatch,
            ConclusionFactId = "fact.culprit",
            SupportsSuspectId = "suspect.culprit",
            IsCulpritConclusion = true
        });

        var result = CulpritUniquenessGate.Validate(graph, "suspect.culprit", "Detective");

        Assert.Contains(result.Failures, failure => failure.Code == "private_only_culprit");
    }

    [Fact]
    public void CulpritGate_RejectsMotiveOnlyConclusion()
    {
        var graph = BasicSuspectGraph();
        graph.Facts.Add(Fact("fact.motive", "person.culprit", "motiveBenefit", "debt relief"));
        graph.Facts.Add(CulpritFact("fact.culprit", "person.culprit"));
        AddSourceObservation(graph, "asset.motive", "origin.motive", "observation.motive", "fact.motive");
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.motive",
            PremiseIds = { "observation.motive" },
            Rule = DerivationRule.BenefitAttribution,
            ConclusionFactId = "fact.culprit",
            SupportsSuspectId = "suspect.culprit",
            IsCulpritConclusion = true
        });

        var result = CulpritUniquenessGate.Validate(graph, "suspect.culprit", "Detective");

        Assert.Contains(result.Failures, failure => failure.Code == "motive_only_culprit");
    }

    [Fact]
    public void IndependentGate_GroupsReportAndSummaryEmailAsOneOrigin()
    {
        var graph = BasicSuspectGraph();
        graph.Facts.Add(Fact("fact.report", "person.culprit", "forensicIdentity", "match"));
        graph.Facts.Add(Fact("fact.email", "person.culprit", "forensicSummary", "match"));
        graph.Facts.Add(CulpritFact("fact.culprit", "person.culprit"));
        AddSourceObservation(graph, "asset.report", "origin.analysis_1", "observation.report", "fact.report");
        AddSourceObservation(
            graph,
            "email.report_summary",
            "origin.analysis_1",
            "observation.email",
            "fact.email",
            EvidenceSourceKind.Email);
        AddCulpritDerivation(graph, "derivation.report", "observation.report", DerivationRule.IdentityMatch);
        AddCulpritDerivation(graph, "derivation.email", "observation.email", DerivationRule.IdentityMatch);

        var result = IndependentProofPathGate.Validate(graph, "suspect.culprit", "Detective");

        Assert.Contains(result.Failures, failure => failure.Code == "insufficient_independent_paths");
        Assert.Single(result.Paths);
    }

    [Fact]
    public void HigherDifficulty_RequiresAdditionalAndDeeperProof()
    {
        var graph = DetectiveGraph();

        var uniqueness = CulpritUniquenessGate.Validate(graph, "suspect.culprit", "Lieutenant");
        var independence = IndependentProofPathGate.Validate(graph, "suspect.culprit", "Lieutenant");

        Assert.Contains(uniqueness.Failures, failure => failure.Code == "insufficient_proof_depth");
        Assert.Contains(independence.Failures, failure => failure.Code == "insufficient_independent_paths");
    }

    [Fact]
    public void Reachability_HidesEvidenceUntilActionsAndRejectsCircularReveals()
    {
        var graph = DetectiveGraph();
        var reachability = ReachabilityClosureEngine.Calculate(graph);

        Assert.DoesNotContain("observation.forensic", reachability.InitialObservationIds);
        Assert.Contains("observation.forensic", reachability.FinalObservationIds);
        Assert.Contains("asset.forensic_report", reachability.FinalSourceIds);

        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = "asset.circular",
            AvailableAt = ReachabilityState.AfterAction,
            RevealPrerequisiteIds = { "action.circular" }
        });
        graph.Actions.Add(new InvestigationAction
        {
            Id = "action.circular",
            PrerequisiteSourceIds = { "asset.circular" },
            RevealsSourceIds = { "asset.circular" }
        });
        graph.RequiredSourceIds.Add("asset.circular");

        var validation = CaseGraphValidator.Validate(graph);
        var gate = ReachabilityGate.Validate(graph, "suspect.culprit");

        Assert.Contains(validation.Errors, error => error.Code == "circular_reveal_dependency");
        Assert.Contains(gate.Failures, failure => failure.Code == "circular_reveal_dependency");
        Assert.Contains(gate.Failures, failure => failure.Code == "unreachable_required_source");
    }

    [Fact]
    public void LeakageGate_RequiresRookieInitialSolutionAndReportsDetectiveLeakPath()
    {
        var rookie = BasicSuspectGraph();
        rookie.Facts.Add(Fact("fact.initial_identity", "person.culprit", "identity", "match"));
        rookie.Facts.Add(CulpritFact("fact.culprit", "person.culprit"));
        AddSourceObservation(rookie, "asset.initial", "origin.initial", "observation.initial", "fact.initial_identity");
        AddCulpritDerivation(rookie, "derivation.initial", "observation.initial", DerivationRule.IdentityMatch);

        var rookieResult = PrematureSolutionLeakageGate.Validate(rookie, "suspect.culprit", "Rookie");
        var detectiveResult = PrematureSolutionLeakageGate.Validate(rookie, "suspect.culprit", "Detective");
        var unsolvedRookie = PrematureSolutionLeakageGate.Validate(BasicSuspectGraph(), "suspect.culprit", "Rookie");

        Assert.True(rookieResult.IsValid);
        Assert.Contains(detectiveResult.Failures, failure => failure.Code == "premature_culprit_leak");
        Assert.Equal("derivation.initial", detectiveResult.LeakingPath?.FinalDerivationId);
        Assert.Contains(unsolvedRookie.Failures, failure => failure.Code == "rookie_not_initially_solvable");
    }

    [Fact]
    public void TransitionalProjection_IncludesCompleteGraphInDraftSummary()
    {
        var draft = new CaseDraft
        {
            CulpritId = "suspect.culprit",
            SuspectStubs = { new SuspectStub { Id = "suspect.culprit", Name = "Culprit" } },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue
                    {
                        Id = "clue.identity",
                        Discovery = "Badge 41 belongs to the culprit.",
                        Inference = "The badge identifies the culprit.",
                        SupportsSuspectId = "suspect.culprit",
                        Role = "identity"
                    }
                }
            },
            AssetStubs =
            {
                new AssetStub { Id = "asset.badge", SupportsClueIds = { "clue.identity" } }
            }
        };

        EvidenceGraphCompiler.Compile(draft);
        var summary = draft.ToSummaryJson();
        var snapshotJson = System.Text.Json.JsonSerializer.Serialize(draft);
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<CaseDraft>(snapshotJson);

        Assert.Equal(CaseGraphOrigin.TransitionalClueLadder, draft.CaseGraph.Origin);
        Assert.NotEmpty(draft.CaseGraph.Facts);
        Assert.Contains("\"caseGraph\"", summary);
        Assert.Contains("\"observation.identity_badge\"", summary);
        Assert.NotNull(snapshot);
        Assert.Equal(draft.CaseGraph.Facts.Count, snapshot.CaseGraph.Facts.Count);
    }

    private static CaseGraph DetectiveGraph()
    {
        var graph = BasicSuspectGraph(includeDecoys: true);
        graph.Facts.AddRange(
        [
            Fact("fact.forensic_identity", "person.culprit", "forensicIdentity", "device match"),
            Fact("fact.witness_action", "person.culprit", "witnessAction", "handled package"),
            Fact("fact.decoy_one_alibi", "person.decoy_one", "alibi", "on call"),
            Fact("fact.decoy_two_alibi", "person.decoy_two", "alibi", "camera exit"),
            Fact("fact.decoy_one_excluded", "person.decoy_one", "excludedFromCrime", "true"),
            Fact("fact.decoy_two_excluded", "person.decoy_two", "excludedFromCrime", "true"),
            CulpritFact("fact.culprit", "person.culprit")
        ]);

        AddSourceObservation(graph, "asset.input", "origin.input", null, null);
        AddSourceObservation(graph, "asset.badge", "origin.badge", "observation.decoy_one", "fact.decoy_one_alibi");
        AddSourceObservation(
            graph,
            "asset.followup",
            "origin.witness",
            "observation.witness",
            "fact.witness_action",
            availableAt: ReachabilityState.AfterAction,
            prerequisiteId: "action.followup");
        AddObservationToExistingSource(graph, "asset.followup", "observation.decoy_two", "fact.decoy_two_alibi");
        AddSourceObservation(
            graph,
            "asset.forensic_report",
            "origin.forensic_analysis",
            "observation.forensic",
            "fact.forensic_identity",
            availableAt: ReachabilityState.AfterForensic,
            prerequisiteId: "action.forensic");

        graph.Actions.Add(new InvestigationAction
        {
            Id = "action.followup",
            Kind = InvestigationActionKind.CompareRecords,
            CompletesAt = ReachabilityState.AfterAction,
            PrerequisiteSourceIds = { "asset.badge" },
            RevealsSourceIds = { "asset.followup" }
        });
        graph.Actions.Add(new InvestigationAction
        {
            Id = "action.forensic",
            Kind = InvestigationActionKind.RequestAnalysis,
            CompletesAt = ReachabilityState.AfterForensic,
            PrerequisiteSourceIds = { "asset.input" },
            RevealsSourceIds = { "asset.forensic_report" },
            AnalysisId = "asset.input:DeviceCorrelation",
            Required = true
        });
        graph.RequiredSourceIds.AddRange(["asset.followup", "asset.forensic_report"]);
        graph.RequiredAnalysisIds.Add("asset.input:DeviceCorrelation");

        AddCulpritDerivation(graph, "derivation.culprit_forensic", "observation.forensic", DerivationRule.IdentityMatch);
        AddCulpritDerivation(graph, "derivation.culprit_witness", "observation.witness", DerivationRule.ActionAttribution);
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.exclude_one",
            PremiseIds = { "observation.decoy_one" },
            Rule = DerivationRule.Exclusion,
            ConclusionFactId = "fact.decoy_one_excluded",
            SupportsSuspectId = "suspect.decoy_one"
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.exclude_two",
            PremiseIds = { "observation.decoy_two" },
            Rule = DerivationRule.Exclusion,
            ConclusionFactId = "fact.decoy_two_excluded",
            SupportsSuspectId = "suspect.decoy_two"
        });
        return graph;
    }

    private static CaseGraph BasicSuspectGraph(bool includeDecoys = false)
    {
        var graph = new CaseGraph();
        AddSuspect(graph, "suspect.culprit", "person.culprit", "Culprit");
        if (includeDecoys)
        {
            AddSuspect(graph, "suspect.decoy_one", "person.decoy_one", "Decoy One");
            AddSuspect(graph, "suspect.decoy_two", "person.decoy_two", "Decoy Two");
        }
        return graph;
    }

    private static void AddSuspect(CaseGraph graph, string suspectId, string personId, string name)
    {
        graph.Entities.Add(new CaseEntity { Id = personId, Kind = EntityKind.Person, DisplayName = name });
        graph.SuspectReferences.Add(new SuspectEntityReference
        {
            SuspectId = suspectId,
            PersonEntityId = personId
        });
    }

    private static CanonicalFact Fact(string id, string subjectId, string predicate, string value) => new()
    {
        Id = id,
        SubjectId = subjectId,
        Predicate = predicate,
        LiteralValue = value,
        LiteralType = LiteralValueType.String
    };

    private static CanonicalFact CulpritFact(string id, string subjectId) => new()
    {
        Id = id,
        SubjectId = subjectId,
        Predicate = "isCulprit",
        LiteralValue = "true",
        LiteralType = LiteralValueType.Boolean
    };

    private static void AddSourceObservation(
        CaseGraph graph,
        string sourceId,
        string originId,
        string? observationId,
        string? factId,
        EvidenceSourceKind kind = EvidenceSourceKind.Asset,
        ReachabilityState availableAt = ReachabilityState.Initial,
        string? prerequisiteId = null)
    {
        var source = new EvidenceSourceNode
        {
            Id = sourceId,
            Kind = kind,
            EvidentiaryOriginId = originId,
            AvailableAt = availableAt
        };
        if (!string.IsNullOrWhiteSpace(prerequisiteId))
            source.RevealPrerequisiteIds.Add(prerequisiteId);
        if (!string.IsNullOrWhiteSpace(observationId) && !string.IsNullOrWhiteSpace(factId))
        {
            source.ObservationIds.Add(observationId);
            graph.Observations.Add(new EvidenceObservation
            {
                Id = observationId,
                FactId = factId,
                SourceAssetId = sourceId,
                EvidentiaryOriginId = originId
            });
        }
        graph.Sources.Add(source);
    }

    private static void AddObservationToExistingSource(
        CaseGraph graph,
        string sourceId,
        string observationId,
        string factId)
    {
        var source = graph.Sources.First(item => item.Id == sourceId);
        source.ObservationIds.Add(observationId);
        graph.Observations.Add(new EvidenceObservation
        {
            Id = observationId,
            FactId = factId,
            SourceAssetId = sourceId,
            EvidentiaryOriginId = source.EvidentiaryOriginId
        });
    }

    private static void AddCulpritDerivation(
        CaseGraph graph,
        string derivationId,
        string premiseId,
        DerivationRule rule)
    {
        graph.Derivations.Add(new ProofDerivation
        {
            Id = derivationId,
            PremiseIds = { premiseId },
            Rule = rule,
            ConclusionFactId = "fact.culprit",
            SupportsSuspectId = "suspect.culprit",
            IsCulpritConclusion = true
        });
    }

    private static void AddObservedCulpritPath(
        CaseGraph graph,
        string suspectId,
        string personId,
        string sourceId,
        string originId)
    {
        var factId = $"fact.accusation_{personId.Split('.')[1]}";
        var culpritFactId = $"fact.culprit_{personId.Split('.')[1]}";
        var observationId = $"observation.accusation_{personId.Split('.')[1]}";
        graph.Facts.Add(Fact(factId, personId, "identity", "match"));
        graph.Facts.Add(CulpritFact(culpritFactId, personId));
        AddSourceObservation(graph, sourceId, originId, observationId, factId);
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.accusation_{personId.Split('.')[1]}",
            PremiseIds = { observationId },
            Rule = DerivationRule.IdentityMatch,
            ConclusionFactId = culpritFactId,
            SupportsSuspectId = suspectId,
            IsCulpritConclusion = true
        });
    }
}
