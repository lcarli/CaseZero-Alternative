using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class Phase9MigrationTests
{
    [Fact]
    public void FeatureFlag_DefaultsOffAndCanEnableGraphCanonicalMode()
    {
        var disabled = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var enabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CaseGenV2:CaseGraphEnabled"] = "true"
            })
            .Build();

        Assert.False(CaseGraphFeatureOptions.From(disabled).Enabled);
        Assert.True(CaseGraphFeatureOptions.From(enabled).Enabled);
    }

    [Fact]
    public void GraphCompiler_UsesGraphSurfaceIdsAndReportsExactParity()
    {
        var draft = MigrationDraft();
        var legacy = LegacyContract();
        CaseGraphGenerationCoordinator.PrepareCanonicalGraph(draft, graphModeEnabled: true);

        var compiled = CaseGraphPublicContractCompiler.Compile(draft, legacy);
        var parity = CaseGraphPublicContractCompiler.Compare(legacy, compiled);

        Assert.Equal(CaseGraphOrigin.Generated, draft.CaseGraph.Origin);
        EvidenceGraphCompiler.Compile(draft);
        Assert.Equal(CaseGraphOrigin.Generated, draft.CaseGraph.Origin);
        Assert.True(parity.IsEquivalent);
        Assert.Equal(legacy.ToJsonString(), compiled.ToJsonString());

        draft.CaseGraph.Sources.RemoveAll(source => source.Id == "asset.initial");
        var missing = CaseGraphPublicContractCompiler.Compile(draft, legacy);
        var mismatch = CaseGraphPublicContractCompiler.Compare(legacy, missing);
        Assert.Contains(mismatch.Issues, issue => issue.Surface == "assets");
    }

    [Fact]
    public void RookieRegression_EnforcesExactNoForensicsAndScoringContract()
    {
        var draft = ValidRookieDraft();
        var json = PublicRookieJson();

        Assert.Empty(RookieRegressionValidator.Validate(draft, json).Errors);

        draft.AnalysisTypes.Add(ForensicMethodCatalog.CreateAnalysisType("MetadataAnalysis", 60));
        var root = JsonNode.Parse(json)!.AsObject();
        root["solution"]!["partialCreditRules"]!["culpritWeight"] = 0.5;
        var invalid = RookieRegressionValidator.Validate(draft, root.ToJsonString());

        Assert.Contains(invalid.Errors, error => error.Contains("zero analysis types", StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.Contains("scoring behavior", StringComparison.Ordinal));
    }

    [Fact]
    public void FinalValidator_ReportsTypedBlockingGatesAndPrivateArtifactsPersist()
    {
        var draft = ValidRookieDraft();
        var solver = SuccessfulSolver(draft);
        var report = CaseV2FinalValidator.CreateDefault().Validate(draft, PublicRookieJson(), solver);

        Assert.DoesNotContain(report.Issues, issue => issue.Gate == FinalValidationGate.Schema);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues.Select(issue => issue.Message)));

        var scratch = Path.Combine(AppContext.BaseDirectory, "casev2-private-artifact-scratch");
        try
        {
            PrivateCaseArtifactPersistence.Write(scratch, draft.CaseGraph, report);
            Assert.True(File.Exists(Path.Combine(scratch, "case.graph.private.json")));
            Assert.True(File.Exists(Path.Combine(scratch, "validation-report.private.json")));
            Assert.False(File.Exists(Path.Combine(scratch, "case.json")));
        }
        finally
        {
            if (Directory.Exists(scratch))
                Directory.Delete(scratch, recursive: true);
        }
    }

    [Fact]
    public void FinalValidator_BlocksSolverScoresBelowMvpThreshold()
    {
        var draft = ValidRookieDraft();
        var solver = SuccessfulSolver(draft);
        solver.Score = 0.89;

        var report = CaseV2FinalValidator.CreateDefault().Validate(draft, PublicRookieJson(), solver);

        Assert.Contains(report.Issues, issue =>
            issue.Gate == FinalValidationGate.Solver
            && issue.Code == "solver.quality_threshold"
            && issue.Blocking);
    }

    [Fact]
    public void Mutation_RemoveDecisiveObservation_FailsMissingPremiseGate()
    {
        var draft = ValidDetectiveDraft();
        draft.CaseGraph.Observations.RemoveAll(observation => observation.Id == "observation.forensic_a");

        var report = CaseGraphValidator.Validate(draft.CaseGraph);

        Assert.Contains(report.Errors, error =>
            error.Code == "missing_source_observation" && error.NodeId == "asset.result_a");
        Assert.Contains(report.Errors, error =>
            error.Code == "missing_derivation_premise" && error.NodeId == "derivation.join_a");
    }

    [Fact]
    public void Mutation_ReplaceTimestamp_FailsExactOrderingGate()
    {
        var graph = ValidDetectiveDraft().CaseGraph;
        graph.Events.Add(new CanonicalEvent
        {
            Id = "event.before",
            Kind = EventKind.Entered,
            Timestamp = DateTimeOffset.Parse("2026-07-21T22:00:00Z")
        });
        graph.Events.Add(new CanonicalEvent
        {
            Id = "event.after",
            Kind = EventKind.Exited,
            Timestamp = DateTimeOffset.Parse("2026-07-21T21:00:00Z")
        });
        graph.EventOrderConstraints.Add(new EventOrderConstraint
        {
            Id = "order.mutated",
            BeforeEventId = "event.before",
            AfterEventId = "event.after"
        });

        Assert.Contains(
            CaseGraphValidator.Validate(graph).Errors,
            error => error.Code == "impossible_event_order" && error.NodeId == "order.mutated");
    }

    [Fact]
    public void Mutation_ChangeAccountOwner_FailsExactConflictGate()
    {
        var graph = ValidDetectiveDraft().CaseGraph;
        graph.Entities.Add(new CaseEntity { Id = "account.one", Kind = EntityKind.Account, DisplayName = "One" });
        graph.Entities.Add(new CaseEntity { Id = "account.two", Kind = EntityKind.Account, DisplayName = "Two" });
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.owner_one", SubjectId = "person.culprit", Predicate = "ownsAccount", ObjectId = "account.one"
        });
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.owner_two", SubjectId = "person.culprit", Predicate = "ownsAccount", ObjectId = "account.two"
        });

        Assert.Contains(
            CaseGraphValidator.Validate(graph).Errors,
            error => error.Code == "conflicting_canonical_fact");
    }

    [Fact]
    public void Mutation_ExposePrivateFact_FailsExactLeakageGate()
    {
        var graph = ValidDetectiveDraft().CaseGraph;
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.private_mutation",
            SubjectId = "person.culprit",
            Predicate = "secret",
            LiteralValue = "private",
            LiteralType = LiteralValueType.String,
            Visibility = FactVisibility.Private
        });
        graph.Observations.Add(new EvidenceObservation
        {
            Id = "observation.private_mutation",
            FactId = "fact.private_mutation",
            SourceAssetId = "asset.initial_a",
            Visibility = FactVisibility.Public
        });
        graph.Sources.Single(source => source.Id == "asset.initial_a")
            .ObservationIds.Add("observation.private_mutation");

        var errors = CaseGraphValidator.Validate(graph).Errors;
        Assert.Contains(errors, error => error.Code == "private_fact_in_initial_evidence");
        Assert.Contains(errors, error => error.Code == "invalid_visibility_transition");
    }

    [Fact]
    public void Mutation_SelectIncompatibleAnalysis_FailsForensicCompatibilityGate()
    {
        var draft = ValidDetectiveDraft();
        draft.CaseGraph.Entities.Add(new CaseEntity { Id = "document.photo", Kind = EntityKind.Document, DisplayName = "Photo" });
        draft.CaseGraph.AssetSpecs.Add(new EvidenceAssetSpec
        {
            Id = "asset.initial_a",
            ArchetypeId = "photo",
            AssetType = "photo",
            LayoutId = "Photo",
            ContainedObjectIds = { "document.photo" },
            ContainedObjectTypes = { ["document.photo"] = ForensicInputObjectType.Image }
        });
        draft.CaseGraph.ForensicTransforms.Clear();
        draft.CaseGraph.ForensicTransforms.Add(new ForensicTransform
        {
            Id = "forensic.incompatible",
            MethodId = "AccountAttribution",
            InputAssetId = "asset.initial_a",
            InputObjectId = "document.photo"
        });

        var errors = ForensicContractValidator.Validate(draft).Errors;
        Assert.Contains(errors, error => error.Contains("rejects asset type 'photo'", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("rejects object type 'Image'", StringComparison.Ordinal));
    }

    [Fact]
    public void Mutation_RemoveRevealRule_FailsExactReachabilityGate()
    {
        var draft = ValidDetectiveDraft();
        draft.CaseGraph.Actions.RemoveAll(action => action.Id == "action.analysis_a");

        var errors = CaseGraphValidator.Validate(draft.CaseGraph).Errors;
        var reachability = ReachabilityGate.Validate(draft.CaseGraph, draft.CulpritId);

        Assert.Contains(errors, error => error.Code == "missing_reveal_prerequisite" && error.NodeId == "asset.result_a");
        Assert.Contains(reachability.Failures, failure =>
            failure.Code == "unreachable_required_source" && failure.NodeId == "asset.result_a");
    }

    [Fact]
    public void Mutation_RemoveDecoyVerification_FailsExactDecoyGate()
    {
        var draft = ValidDetectiveDraft();
        draft.CaseGraph.DecoyArcs[0].VerificationObservationIds.Clear();

        Assert.Contains(
            EvidenceContractValidator.ValidatePlan(draft).Errors,
            error => error.Contains("no verification observation", StringComparison.Ordinal));
    }

    [Fact]
    public void Mutation_DuplicateEvidenceOrigin_FailsIndependentPathGate()
    {
        var draft = ValidDetectiveDraft();
        draft.CaseGraph.Sources.Single(source => source.Id == "asset.initial_b").EvidentiaryOriginId = "origin.initial_a";
        draft.CaseGraph.Sources.Single(source => source.Id == "asset.result_b").EvidentiaryOriginId = "origin.forensic_a";

        var result = IndependentProofPathGate.Validate(draft.CaseGraph, draft.CulpritId, "Detective");

        Assert.Contains(result.Failures, failure => failure.Code == "insufficient_independent_paths");
    }

    [Fact]
    public void Mutation_SecondCulprit_FailsExactUniquenessGate()
    {
        var draft = ValidDetectiveDraft();
        draft.CaseGraph.Facts.Add(new CanonicalFact
        {
            Id = "fact.culprit_decoy",
            SubjectId = "person.decoy_one",
            Predicate = "isCulprit",
            LiteralValue = "true",
            LiteralType = LiteralValueType.Boolean
        });
        draft.CaseGraph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.second_culprit",
            PremiseIds = { "observation.initial_a" },
            Rule = DerivationRule.IdentityMatch,
            ConclusionFactId = "fact.culprit_decoy",
            SupportsSuspectId = "suspect.decoy_one",
            IsCulpritConclusion = true
        });

        var result = CulpritUniquenessGate.Validate(draft.CaseGraph, draft.CulpritId, "Detective");

        Assert.Contains(result.Failures, failure =>
            failure.Code == "multiple_valid_culprits" && failure.NodeId == "suspect.decoy_one");
    }

    [Fact]
    public void BatchHarness_ReportsEveryRequiredMetricDeterministically()
    {
        var samples = new[]
        {
            Sample(1, true, true, 0, false, 100, 10, 5, ["PoliceReport", "AccessLog"]),
            Sample(2, true, false, 2, true, 300, 20, 8, ["AccessLog", "ForensicReport"])
        };

        var report = CaseV2BatchHarness.Summarize(samples);

        Assert.Equal(2, report.TotalCases);
        Assert.Equal(1, report.GraphValidationPassRate);
        Assert.Equal(0.5, report.FirstPassSuccessRate);
        Assert.Equal(1, report.AverageRepairOperations);
        Assert.Equal(0.5, report.RepairPlateauRate);
        Assert.Equal(0.5, report.SolverSuccessRate);
        Assert.Equal(200, report.AverageLatencyMs);
        Assert.Equal(30, report.TotalInputTokens);
        Assert.Equal(13, report.TotalOutputTokens);
        Assert.Equal(3, report.EvidenceLayoutDiversity);
        Assert.Contains(SpecialistReviewKind.DocumentFidelity.ToString(), report.SpecialistFindingsByCategory.Keys);
    }

    [Fact]
    public async Task BatchHarness_IntegratesGeneratorResponsesAcrossSeedsAndThemes()
    {
        var requests = new[]
        {
            new BatchGenerationRequest("Rookie", 1, "office theft"),
            new BatchGenerationRequest("Rookie", 2, "financial fraud")
        };

        var report = await new CaseV2BatchHarness().RunAsync(requests, new FakeGenerator());

        Assert.Equal(2, report.TotalCases);
        Assert.Equal(1, report.GraphValidationPassRate);
        Assert.Equal(1, report.FirstPassSuccessRate);
        Assert.Equal(1, report.SolverSuccessRate);
        Assert.Equal(1, report.AverageRepairOperations);
        Assert.Equal(30, report.TotalInputTokens);
        Assert.Equal(14, report.TotalOutputTokens);
        Assert.Equal(2, report.EvidenceLayoutDiversity);
    }

    [Fact]
    public void DetectiveApprovalInfrastructure_RemainsBlockedWithoutRealGeneratedPassingCase()
    {
        var batch = CaseV2BatchHarness.Summarize(
        [
            Sample(1, true, true, 0, false, 100, 0, 0, ["PoliceReport"])
        ]);

        var report = DifficultyApprovalGate.Evaluate(
            "Detective",
            batch,
            hasPersistedGolden: true,
            hasRealGeneratedGolden: false);

        Assert.Equal(DifficultyApprovalStatus.Blocked, report.Status);
        Assert.Contains(report.BlockingReasons, reason => reason.Contains("real generated case", StringComparison.Ordinal));
    }

    [Fact]
    public void BlockingFinalGate_PreventsPersistence()
    {
        var report = new FinalValidationReport
        {
            Issues =
            {
                new FinalValidationIssue(FinalValidationGate.Proof, "proof.failed", "fact.culprit", "failed", true)
            }
        };

        Assert.False(GenerationPersistenceGate.CanPersist(true, Array.Empty<string>(), report));
        Assert.False(GenerationPersistenceGate.CanPersist(false, Array.Empty<string>(), new FinalValidationReport()));
        Assert.True(GenerationPersistenceGate.CanPersist(true, Array.Empty<string>(), new FinalValidationReport()));
    }

    [Fact]
    public void QuestionProvenance_IsAnObjectiveDeterministicGate()
    {
        var draft = ValidRookieDraft();
        draft.QuestionTopics.Add(new QuestionTopic { Id = "question.provenance" });

        var finding = Assert.Single(ObjectiveQualityValidator.Validate(draft).Where(item =>
            item.ConstraintId == "question_provenance_missing"));

        Assert.Equal("question.provenance", finding.OwningNodeId);
        Assert.True(finding.Deterministic);
    }

    private static BatchGenerationSample Sample(
        int seed,
        bool graphPass,
        bool success,
        int repairs,
        bool plateau,
        double latency,
        long inputTokens,
        long outputTokens,
        IReadOnlyList<string> layouts)
    {
        var validation = new FinalValidationReport();
        if (!graphPass)
            validation.Issues.Add(new FinalValidationIssue(FinalValidationGate.Graph, "graph.invalid", "graph", "invalid", true));
        return new BatchGenerationSample
        {
            Request = new BatchGenerationRequest("Detective", seed, "theme"),
            Validation = validation,
            FirstPassSuccess = success,
            RepairOperations = repairs,
            RepairPlateaued = plateau,
            SolverSucceeded = success,
            LatencyMs = latency,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EvidenceLayouts = layouts,
            SpecialistFindings =
            [
                new SpecialistFinding(
                    SpecialistReviewKind.DocumentFidelity,
                    "document.semantic",
                    "asset.one",
                    ReviewSeverity.Low,
                    "minor",
                    null,
                    false)
            ]
        };
    }

    private static CaseDraft MigrationDraft()
    {
        var draft = ValidRookieDraft();
        draft.CaseGraph.Origin = CaseGraphOrigin.TransitionalClueLadder;
        draft.AssetFull =
        [
            new EvidenceAsset { Id = "asset.initial", Type = "document", Title = "Asset", Body = "Identity A" }
        ];
        draft.FollowUpEmails =
        [
            new EvidenceEmail
            {
                Id = "email.initial", From = "unit@casezero.local", Subject = "Email", Body = "Body",
                SentAt = "2026-07-21T10:00:00Z", Visibility = "initial"
            }
        ];
        draft.SuspectFull =
        [
            new PlotSuspect { Id = "suspect.culprit", Name = "Culprit" },
            new PlotSuspect { Id = "suspect.decoy_one", Name = "Decoy One" },
            new PlotSuspect { Id = "suspect.decoy_two", Name = "Decoy Two" }
        ];
        draft.CaseGraph.Sources.Add(new EvidenceSourceNode { Id = "email.initial", Kind = EvidenceSourceKind.Email });
        return draft;
    }

    private static JsonObject LegacyContract()
    {
        var root = JsonNode.Parse(PublicRookieJson())!.AsObject();
        root["assets"] = new JsonArray
        {
            new JsonObject { ["id"] = "asset.initial", ["type"] = "document", ["title"] = "Asset", ["uri"] = "case://case_test/assets/initial.pdf", ["visibility"] = "initial" }
        };
        root["emails"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "email.briefing", ["from"] = "unit@casezero.local", ["to"] = "detective@casezero.local",
                ["subject"] = "Briefing", ["body"] = "Briefing body", ["sentAt"] = "2026-07-21T10:00:00Z", ["visibility"] = "initial"
            },
            new JsonObject
            {
                ["id"] = "email.initial", ["from"] = "unit@casezero.local", ["to"] = "detective@casezero.local",
                ["subject"] = "Email", ["body"] = "Body", ["sentAt"] = "2026-07-21T10:00:00Z", ["visibility"] = "initial"
            }
        };
        return root;
    }

    private static CaseDraft ValidRookieDraft()
    {
        var graph = BaseGraph("Rookie");
        AddDirectCulpritPath(graph, "a", "asset.initial", "Identity A");
        AddDirectCulpritPath(graph, "b", "asset.second", "Identity B");
        AddDecoy(graph, "one", "asset.decoy_one");
        AddDecoy(graph, "two", "asset.decoy_two");
        graph.Sources.Add(new EvidenceSourceNode { Id = "asset.extra" });
        return new CaseDraft
        {
            CaseId = "case_test",
            CulpritId = "suspect.culprit",
            Metadata = Metadata("Rookie"),
            Request = new GenerateCaseV2Request { Difficulty = "Rookie", RequiredRank = "Rookie", Language = "en-US" },
            Blueprint = Blueprint(),
            CaseGraph = graph,
            RequiredEvidenceIds = { "asset.initial", "asset.second" }
        };
    }

    private static CaseDraft ValidDetectiveDraft()
    {
        var graph = BaseGraph("Detective");
        AddDetectiveBranch(graph, "a");
        AddDetectiveBranch(graph, "b");
        AddDecoy(graph, "one", "asset.decoy_one");
        AddDecoy(graph, "two", "asset.decoy_two");
        return new CaseDraft
        {
            CaseId = "case_detective_test",
            CulpritId = "suspect.culprit",
            Metadata = Metadata("Detective"),
            Request = new GenerateCaseV2Request { Difficulty = "Detective", RequiredRank = "Detective", Language = "en-US" },
            Blueprint = Blueprint(),
            CaseGraph = graph
        };
    }

    private static CaseGraph BaseGraph(string difficulty)
    {
        var graph = new CaseGraph();
        AddPerson(graph, "suspect.culprit", "person.culprit");
        AddPerson(graph, "suspect.decoy_one", "person.decoy_one");
        AddPerson(graph, "suspect.decoy_two", "person.decoy_two");
        graph.Facts.Add(Fact("fact.culprit", "person.culprit", "isCulprit", "true", LiteralValueType.Boolean));
        return graph;
    }

    private static void AddDirectCulpritPath(CaseGraph graph, string suffix, string sourceId, string value)
    {
        var factId = $"fact.identity_{suffix}";
        var observationId = $"observation.identity_{suffix}";
        graph.Facts.Add(Fact(factId, "person.culprit", $"identity{suffix}", value));
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = sourceId,
            EvidentiaryOriginId = $"origin.{suffix}",
            ObservationIds = { observationId }
        });
        graph.Observations.Add(new EvidenceObservation { Id = observationId, FactId = factId, SourceAssetId = sourceId });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.culprit_{suffix}",
            PremiseIds = { observationId },
            Rule = DerivationRule.IdentityMatch,
            ConclusionFactId = "fact.culprit",
            SupportsSuspectId = "suspect.culprit",
            IsCulpritConclusion = true
        });
    }

    private static void AddDetectiveBranch(CaseGraph graph, string suffix)
    {
        var initialSource = $"asset.initial_{suffix}";
        var resultSource = $"asset.result_{suffix}";
        var initialObservation = $"observation.initial_{suffix}";
        var forensicObservation = $"observation.forensic_{suffix}";
        var actionId = $"action.analysis_{suffix}";
        graph.Facts.Add(Fact($"fact.initial_{suffix}", "person.culprit", "identity", $"Initial {suffix}"));
        graph.Facts.Add(Fact($"fact.forensic_{suffix}", "person.culprit", "forensic", $"Forensic {suffix}"));
        graph.Facts.Add(Fact($"fact.bridge_{suffix}", "person.culprit", "bridge", $"Bridge {suffix}"));
        graph.Facts.Add(Fact($"fact.join_{suffix}", "person.culprit", "join", $"Join {suffix}"));
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = initialSource, EvidentiaryOriginId = $"origin.initial_{suffix}", ObservationIds = { initialObservation }
        });
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = resultSource, EvidentiaryOriginId = $"origin.forensic_{suffix}",
            AvailableAt = ReachabilityState.AfterForensic,
            RevealPrerequisiteIds = { actionId },
            ObservationIds = { forensicObservation },
            Required = true
        });
        graph.Observations.Add(new EvidenceObservation
        {
            Id = initialObservation, FactId = $"fact.initial_{suffix}", SourceAssetId = initialSource
        });
        graph.Observations.Add(new EvidenceObservation
        {
            Id = forensicObservation, FactId = $"fact.forensic_{suffix}", SourceAssetId = resultSource,
            ForensicProperty = ForensicObservationProperty.DeviceCorrelation
        });
        graph.Actions.Add(new InvestigationAction
        {
            Id = actionId, Kind = InvestigationActionKind.RequestAnalysis,
            PrerequisiteSourceIds = { initialSource }, RevealsSourceIds = { resultSource },
            AnalysisId = $"{initialSource}:MetadataAnalysis", Required = true,
            CompletesAt = ReachabilityState.AfterForensic
        });
        graph.ForensicTransforms.Add(new ForensicTransform
        {
            Id = $"forensic.{suffix}", MethodId = "MetadataAnalysis", InputAssetId = initialSource,
            ProducedObservationIds = { forensicObservation },
            ProducedProperties = { ForensicObservationProperty.DeviceCorrelation },
            ResultAssetId = resultSource
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.identity_{suffix}", PremiseIds = { initialObservation },
            Rule = DerivationRule.IdentityMatch, ConclusionFactId = $"fact.bridge_{suffix}"
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.join_{suffix}", PremiseIds = { $"derivation.identity_{suffix}", forensicObservation },
            Rule = DerivationRule.CrossSourceCorroboration, ConclusionFactId = $"fact.join_{suffix}"
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.culprit_{suffix}", PremiseIds = { $"derivation.join_{suffix}" },
            Rule = DerivationRule.ActionAttribution, ConclusionFactId = "fact.culprit",
            SupportsSuspectId = "suspect.culprit", IsCulpritConclusion = true
        });
    }

    private static void AddDecoy(CaseGraph graph, string suffix, string sourceId)
    {
        var suspectId = $"suspect.decoy_{suffix}";
        var personId = $"person.decoy_{suffix}";
        var observationId = $"observation.decoy_{suffix}";
        var factId = $"fact.decoy_{suffix}";
        var excludedId = $"fact.excluded_{suffix}";
        var derivationId = $"derivation.exclude_{suffix}";
        graph.Facts.Add(Fact(factId, personId, "verification", $"Verified {suffix}"));
        graph.Facts.Add(Fact(excludedId, personId, "excludedFromCrime", "true", LiteralValueType.Boolean));
        graph.Sources.Add(new EvidenceSourceNode { Id = sourceId, ObservationIds = { observationId } });
        graph.Observations.Add(new EvidenceObservation { Id = observationId, FactId = factId, SourceAssetId = sourceId });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = derivationId, PremiseIds = { observationId }, Rule = DerivationRule.Exclusion,
            ConclusionFactId = excludedId, SupportsSuspectId = suspectId
        });
        graph.DecoyArcs.Add(new DecoyArc
        {
            SuspectId = suspectId,
            SuspicionObservationIds = { observationId },
            VerificationObservationIds = { observationId },
            ResolutionDerivationId = derivationId
        });
    }

    private static void AddPerson(CaseGraph graph, string suspectId, string personId)
    {
        graph.Entities.Add(new CaseEntity { Id = personId, Kind = EntityKind.Person, DisplayName = suspectId });
        graph.SuspectReferences.Add(new SuspectEntityReference { SuspectId = suspectId, PersonEntityId = personId });
    }

    private static CanonicalFact Fact(
        string id,
        string subject,
        string predicate,
        string value,
        LiteralValueType type = LiteralValueType.String) => new()
    {
        Id = id, SubjectId = subject, Predicate = predicate, LiteralValue = value, LiteralType = type
    };

    private static PlotMetadata Metadata(string difficulty) => new()
    {
        Title = $"{difficulty} Test Case",
        Description = "A complete deterministic validation case.",
        Location = "Boston, Massachusetts",
        IncidentDate = "2026-07-20T20:00:00-04:00",
        OpenedAt = "2026-07-21T09:00:00-04:00",
        Difficulty = difficulty,
        RequiredRank = difficulty
    };

    private static InvestigationBlueprint Blueprint() => new()
    {
        Locale = new CaseLocaleContext
        {
            TimeZoneId = "America/New_York",
            UtcOffset = "-04:00",
            PoliceAgency = "Boston Police Department",
            InvestigatorName = "Alex Morgan",
            InvestigatorEmail = "alex@casezero.local"
        }
    };

    private static SolverTask.SolverResult SuccessfulSolver(CaseDraft draft) => new()
    {
        Correct = true,
        Score = 1,
        LogicalProofPassed = true,
        NarrativeSimulationPassed = true,
        Attempt = new SolverTask.Attempt
        {
            SuspectId = draft.CulpritId,
            EvidenceIds = { "asset.initial", "asset.second" },
            ObservationIds = { "observation.identity_a", "observation.identity_b" }
        },
        Trace = new SolverDiagnosticTrace
        {
            LogicalProofPassed = true,
            NarrativeSimulationPassed = true,
            Entries =
            {
                new PlayerActionTraceEntry(
                    1, PlayerActionKind.AccuseSuspect, "accuse:suspect.culprit", "suspect.culprit",
                    true, "accepted", "Action accepted.", Array.Empty<string>(),
                    new[] { "observation.identity_a", "observation.identity_b" })
            }
        }
    };

    private static string PublicRookieJson() => """
    {
      "version":"2.0",
      "caseId":"case_test",
      "metadata":{
        "title":"Rookie Test Case","description":"A complete deterministic validation case.",
        "location":"Boston, Massachusetts","incidentDate":"2026-07-20T20:00:00-04:00",
        "openedAt":"2026-07-21T09:00:00-04:00","difficulty":"Rookie","requiredRank":"Rookie",
        "unlockMode":"all_initial","estimatedDurationMinutes":45
      },
      "assets":[
        {"id":"asset.initial","type":"document","title":"Initial","uri":"case://case_test/assets/initial.pdf","visibility":"initial"},
        {"id":"asset.second","type":"document","title":"Second","uri":"case://case_test/assets/second.pdf","visibility":"initial"},
        {"id":"asset.decoy_one","type":"document","title":"Decoy One","uri":"case://case_test/assets/decoy_one.pdf","visibility":"initial"},
        {"id":"asset.decoy_two","type":"document","title":"Decoy Two","uri":"case://case_test/assets/decoy_two.pdf","visibility":"initial"},
        {"id":"asset.extra","type":"document","title":"Context","uri":"case://case_test/assets/extra.pdf","visibility":"initial"}
      ],
      "emails":[
        {"id":"email.briefing","from":"unit@casezero.local","to":"detective@casezero.local",
         "subject":"Briefing","body":"Review the evidence.","sentAt":"2026-07-21T09:00:00-04:00","visibility":"initial"}
      ],
      "suspects":[
        {"id":"suspect.culprit","name":"Culprit","visibility":"initial"},
        {"id":"suspect.decoy_one","name":"Decoy One","visibility":"initial"},
        {"id":"suspect.decoy_two","name":"Decoy Two","visibility":"initial"}
      ],
      "timeline":[],
      "temporalEvents":[],
      "rules":[],
      "forensicsDefaults":{"analysisTypes":[],"noFindingsEmail":{"template":"none","from":"lab@casezero.local","subject":"No findings"}},
      "forensicOutcomes":[],
      "solution":{
        "culpritId":"suspect.culprit","requiredEvidenceIds":["asset.initial","asset.second"],
        "requiredAnalysisIds":[],"questions":[],"explanation":"Two records identify the culprit.",
        "minimumScore":0.7,"maxAttempts":3,
        "partialCreditRules":{"culpritWeight":0.4,"evidenceWeight":0.2,"analysisWeight":0.2,"questionsWeight":0.2}
      },
      "gameMetadata":{"schemaVersion":"2.0","createdAt":"2026-07-21T15:00:00Z"}
    }
    """;

    private static string PublicDetectiveJson() => """
    {
      "version":"2.0",
      "caseId":"case_detective_test",
      "metadata":{
        "title":"Detective Test Case","description":"A complete deterministic validation case.",
        "location":"Boston, Massachusetts","incidentDate":"2026-07-20T20:00:00-04:00",
        "openedAt":"2026-07-21T09:00:00-04:00","difficulty":"Detective","requiredRank":"Detective",
        "unlockMode":"gated","estimatedDurationMinutes":90
      },
      "assets":[
        {"id":"asset.initial_a","type":"digital","title":"Initial A","uri":"case://case_detective_test/assets/initial_a.pdf","visibility":"initial"},
        {"id":"asset.result_a","type":"pdf","title":"Result A","uri":"case://case_detective_test/assets/result_a.pdf","visibility":"hidden"},
        {"id":"asset.initial_b","type":"digital","title":"Initial B","uri":"case://case_detective_test/assets/initial_b.pdf","visibility":"initial"},
        {"id":"asset.result_b","type":"pdf","title":"Result B","uri":"case://case_detective_test/assets/result_b.pdf","visibility":"hidden"},
        {"id":"asset.decoy_one","type":"document","title":"Decoy One","uri":"case://case_detective_test/assets/decoy_one.pdf","visibility":"initial"},
        {"id":"asset.decoy_two","type":"document","title":"Decoy Two","uri":"case://case_detective_test/assets/decoy_two.pdf","visibility":"initial"}
      ],
      "emails":[
        {"id":"email.briefing","from":"unit@casezero.local","to":"detective@casezero.local",
         "subject":"Briefing","body":"Review the evidence.","sentAt":"2026-07-21T09:00:00-04:00","visibility":"initial"}
      ],
      "suspects":[
        {"id":"suspect.culprit","name":"Culprit","visibility":"initial"},
        {"id":"suspect.decoy_one","name":"Decoy One","visibility":"initial"},
        {"id":"suspect.decoy_two","name":"Decoy Two","visibility":"initial"}
      ],
      "timeline":[],
      "temporalEvents":[],
      "rules":[
        {"ruleId":"rule.analysis_a","trigger":{"type":"forensics_complete","inputAssetId":"asset.initial_a","analysisType":"MetadataAnalysis"},"actions":[{"type":"reveal_asset","assetId":"asset.result_a"}]},
        {"ruleId":"rule.analysis_b","trigger":{"type":"forensics_complete","inputAssetId":"asset.initial_b","analysisType":"MetadataAnalysis"},"actions":[{"type":"reveal_asset","assetId":"asset.result_b"}]}
      ],
      "forensicsDefaults":{
        "analysisTypes":[{"type":"MetadataAnalysis","durationMinutes":60,"availableFor":["digital","document","pdf","photo"]}],
        "noFindingsEmail":{"template":"none","from":"lab@casezero.local","subject":"No findings"}
      },
      "forensicOutcomes":[
        {"inputAssetId":"asset.initial_a","analysisType":"MetadataAnalysis","findings":true,"resultAssetId":"asset.result_a"},
        {"inputAssetId":"asset.initial_b","analysisType":"MetadataAnalysis","findings":true,"resultAssetId":"asset.result_b"}
      ],
      "solution":{
        "culpritId":"suspect.culprit","requiredEvidenceIds":["asset.result_a","asset.result_b"],
        "requiredAnalysisIds":["asset.initial_a:MetadataAnalysis","asset.initial_b:MetadataAnalysis"],
        "questions":[],"explanation":"Two gated correlations identify the culprit.",
        "minimumScore":0.7,"maxAttempts":3,
        "partialCreditRules":{"culpritWeight":0.4,"evidenceWeight":0.2,"analysisWeight":0.2,"questionsWeight":0.2}
      },
      "gameMetadata":{"schemaVersion":"2.0","createdAt":"2026-07-21T15:00:00Z"}
    }
    """;

    private sealed class FakeGenerator : ICaseV2GeneratorService
    {
        public Task<GenerateCaseV2Response> GenerateAsync(
            GenerateCaseV2Request request,
            CancellationToken ct = default) =>
            Task.FromResult(Response(request));

        public Task<GenerateCaseV2Response> GenerateAsync(
            GenerateCaseV2Request request,
            IJobPhaseReporter reporter,
            CancellationToken ct = default) =>
            Task.FromResult(Response(request));

        private static GenerateCaseV2Response Response(GenerateCaseV2Request request) => new()
        {
            CaseId = request.CaseId ?? "case.batch",
            FinalValidation = new FinalValidationReport
            {
                Difficulty = request.Difficulty ?? "Rookie"
            },
            Solver = new SolverTask.SolverResult
            {
                Correct = true,
                LogicalProofPassed = true,
                NarrativeSimulationPassed = true
            },
            InputTokens = request.Seed == 1 ? 10 : 20,
            OutputTokens = request.Seed == 1 ? 5 : 9,
            EvidenceLayouts = request.Seed == 1 ? ["PoliceReport"] : ["AccessLog"],
            EvidenceLayoutDiversity = 1,
            RepairOperationCount = request.Seed == 1 ? 0 : 2,
            StageLatencyMs = new Dictionary<string, double> { ["generation"] = 100 },
            CaseJson = PublicRookieJson()
        };
    }
}
