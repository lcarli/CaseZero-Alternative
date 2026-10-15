using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class SolverReviewRepairDifficultyTests
{
    [Fact]
    public void PlayerProjection_ContainsCompleteVisibleContentWithoutPrivateOrFutureData()
    {
        var draft = SimulationDraft();
        draft.CulpritId = "suspect.culprit";
        draft.RequiredEvidenceIds.Add("asset.hidden");
        draft.Explanation = "PRIVATE SOLUTION";
        var simulator = new SequentialPlayerSimulator(draft);

        var projection = PlayerVisibleProjectionBuilder.Build(draft, simulator);
        var json = JsonSerializer.Serialize(projection);

        var asset = Assert.Single(projection.Assets);
        Assert.Equal("asset.initial", asset.Id);
        Assert.Equal("Complete body", asset.Body);
        Assert.NotNull(asset.BodyDoc);
        Assert.Contains("Visible section", EvidenceContentText.ExtractBody(draft.AssetFull[0]));
        var email = Assert.Single(projection.Emails.Where(item => item.Id == "email.initial"));
        Assert.Contains("asset.initial", email.AttachmentIds);
        Assert.DoesNotContain("asset.hidden", json);
        Assert.DoesNotContain("email.future", json);
        Assert.DoesNotContain("PRIVATE SOLUTION", json);
        Assert.DoesNotContain("fact.private", json);
        Assert.DoesNotContain("RevealsSourceIds", json);
        Assert.Contains(projection.AvailableActions, action => action.Kind == PlayerActionKind.InspectAsset);
        Assert.Contains(projection.Suspects, suspect => suspect.Id == "suspect.culprit");
        Assert.Single(projection.Timeline);
    }
    [Fact]
    public void SequentialSimulation_EnforcesAvailabilityRevealsAndObservationCitations()
    {
        var draft = SimulationDraft();
        var simulator = new SequentialPlayerSimulator(draft);

        var unavailable = simulator.Apply(new PlayerActionRequest
        {
            Kind = PlayerActionKind.RequestAnalysis,
            ActionId = "action.analysis",
            AnalysisId = "asset.initial:Invented"
        });
        Assert.False(unavailable.Accepted);
        Assert.Equal("analysis_unavailable", unavailable.Code);
        Assert.DoesNotContain("asset.hidden", simulator.ReachableSourceIds);

        Assert.True(simulator.Apply(new PlayerActionRequest
        {
            Kind = PlayerActionKind.InspectAsset,
            ActionId = "inspect:asset.initial",
            TargetId = "asset.initial"
        }).Accepted);
        var analysis = simulator.Apply(new PlayerActionRequest
        {
            Kind = PlayerActionKind.RequestAnalysis,
            ActionId = "action.analysis",
            AnalysisId = "asset.initial:MetadataAnalysis"
        });
        Assert.True(analysis.Accepted);
        Assert.Contains("asset.hidden", analysis.RevealedSourceIds);

        var uncited = simulator.Apply(new PlayerActionRequest
        {
            Kind = PlayerActionKind.AccuseSuspect,
            ActionId = "accuse:suspect.culprit",
            SuspectId = "suspect.culprit"
        });
        Assert.False(uncited.Accepted);
        Assert.Equal("citation_required", uncited.Code);

        simulator.Apply(new PlayerActionRequest
        {
            Kind = PlayerActionKind.InspectAsset,
            ActionId = "inspect:asset.hidden",
            TargetId = "asset.hidden"
        });
        var accusation = simulator.Apply(new PlayerActionRequest
        {
            Kind = PlayerActionKind.AccuseSuspect,
            ActionId = "accuse:suspect.culprit",
            SuspectId = "suspect.culprit",
            ObservationIds = { "observation.hidden" }
        });

        Assert.True(accusation.Accepted);
        Assert.Equal("suspect.culprit", simulator.AccusedSuspectId);
        Assert.Equal(6, simulator.Trace.Count);
        Assert.Contains("observation.hidden", simulator.Snapshot(true, true).FinalReachableObservationIds);
    }

    [Theory]
    [InlineData(true, true, 0.7, true)]
    [InlineData(false, true, 1.0, false)]
    [InlineData(true, false, 1.0, false)]
    [InlineData(true, true, 0.69, false)]
    public void LogicalAndNarrativeSolvability_AreSeparateRequiredGates(
        bool logical,
        bool narrative,
        double score,
        bool expected) =>
        Assert.Equal(expected, SolvabilityDecision.BothPass(logical, narrative, score));

    [Fact]
    public async Task Solver_ReplaysDeterministicGraphWitnessWithoutLlmJudgment()
    {
        var draft = RookieTopologyDraft();

        var result = await new SolverTask(
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).RunAsync(
            draft,
            CancellationToken.None);

        Assert.True(result.LogicalProofPassed);
        Assert.True(result.NarrativeSimulationPassed);
        Assert.True(result.Correct);
        Assert.Equal("suspect.culprit", result.Attempt.SuspectId);
        Assert.NotEmpty(result.Attempt.ObservationIds);
        Assert.Contains(result.Trace.Entries, entry =>
            entry.Accepted && entry.Kind == PlayerActionKind.AccuseSuspect);
    }

    [Fact]
    public void LocaleProfiles_CoverAllLanguagesWithLocalizedLabelsAndValidation()
    {
        foreach (var language in new[] { "pt-BR", "en-US", "es-ES", "fr-FR" })
        {
            var profile = LocaleProfileCatalog.Get(language);
            Assert.NotEmpty(profile.TimeZoneIds);
            Assert.NotEmpty(profile.Terminology);
            Assert.NotEmpty(profile.ReportHeadings);
            Assert.Equal(6, profile.ActionLabels.Count);
            Assert.False(string.IsNullOrWhiteSpace(LocaleProfileCatalog.ActionLabel(language, PlayerActionKind.RequestAnalysis)));
        }
        Assert.NotEqual(
            PlayerMessageCatalog.Get("en-US", "citationRequired"),
            PlayerMessageCatalog.Get("pt-BR", "citationRequired"));

        var valid = new CaseDraft
        {
            Request = new GenerateCaseV2Request { Language = "pt-BR" },
            Blueprint = new InvestigationBlueprint
            {
                Locale = new CaseLocaleContext
                {
                    TimeZoneId = "America/Sao_Paulo",
                    UtcOffset = "-03:00",
                    PoliceAgency = "Polícia Civil de São Paulo",
                    InvestigatorName = "Ana Costa",
                    InvestigatorEmail = "ana@casezero.local"
                }
            },
            Metadata = new PlotMetadata
            {
                IncidentDate = "2026-07-20T20:00:00-03:00",
                OpenedAt = "2026-07-21T09:00:00-03:00"
            }
        };
        Assert.Empty(LocaleProfileCatalog.Validate(valid).Errors);
        valid.Blueprint.Locale.TimeZoneId = "Invalid/Zone";
        Assert.Contains(LocaleProfileCatalog.Validate(valid).Errors, error => error.Contains("locale.timeZoneId", StringComparison.Ordinal));
    }

    [Fact]
    public void ObjectiveChecksProduceTypedOwnedDeterministicFindings()
    {
        var draft = SimulationDraft();
        draft.Request.Language = "fr-FR";
        draft.Blueprint.Locale.TimeZoneId = "America/Sao_Paulo";

        var findings = ObjectiveQualityValidator.Validate(draft);

        var locale = Assert.Single(findings.Where(finding =>
            finding.ConstraintId.StartsWith("locale.", StringComparison.Ordinal)
            && finding.Message.Contains("timeZoneId", StringComparison.Ordinal)));
        Assert.Equal(SpecialistReviewKind.LocaleProceduralRealism, locale.Reviewer);
        Assert.True(locale.Deterministic);
        Assert.False(string.IsNullOrWhiteSpace(locale.OwningNodeId));
        Assert.Equal(
            SpecialistSeverityPolicy.For(locale.Reviewer, locale.ConstraintId),
            locale.Severity);
    }

    [Fact]
    public void SpecialistSeverity_IsDeterministic()
    {
        Assert.Equal(
            ReviewSeverity.High,
            SpecialistSeverityPolicy.For(SpecialistReviewKind.ScientificRealism, "forensic.overclaim"));
        Assert.Equal(
            ReviewSeverity.Medium,
            SpecialistSeverityPolicy.For(SpecialistReviewKind.NarrativeHumanBehavior, "motive.proportionality"));
    }

    [Fact]
    public void DependencyIndex_InvalidatesOnlyTargetAndCalculatedDependents()
    {
        var draft = RepairDraft();
        var index = NodeDependencyIndex.Build(draft);

        var invalidated = index.CalculateInvalidation("asset.one");

        Assert.Contains("asset.one", invalidated);
        Assert.Contains("question.one", invalidated);
        Assert.Contains("explanation.solution", invalidated);
        Assert.DoesNotContain("asset.two", invalidated);
    }

    [Fact]
    public void NodeHashes_AreByteStableAndUnaffectedNodesRemainStable()
    {
        var draft = RepairDraft();
        var first = NodeDependencyIndex.Build(draft);
        var second = NodeDependencyIndex.Build(draft);
        Assert.Equal(first.Find("asset.one")!.ContentHash, second.Find("asset.one")!.ContentHash);
        Assert.Equal(first.Find("asset.two")!.ContentHash, second.Find("asset.two")!.ContentHash);

        draft.AssetFull.Single(asset => asset.Id == "asset.one").Body = "changed";
        var changed = NodeDependencyIndex.Build(draft);

        Assert.NotEqual(first.Find("asset.one")!.ContentHash, changed.Find("asset.one")!.ContentHash);
        Assert.Equal(first.Find("asset.two")!.ContentHash, changed.Find("asset.two")!.ContentHash);
    }

    [Fact]
    public void RepairPlanner_UsesTypedSmallestNodeOperation()
    {
        var draft = RepairDraft();
        var issue = new RepairIssue("evidenceContent", "asset body is incomplete")
        {
            NodeId = "asset.one",
            ConstraintId = "document.missing_observation"
        };

        var operation = Assert.Single(RepairCoordinator.BuildOperations(
            draft,
            Array.Empty<RedTeamTask.Finding>(),
            new[] { issue }));

        Assert.Equal(RepairOperationKind.RewriteAsset, operation.Operation);
        Assert.Equal("asset.one", operation.TargetNodeId);
        Assert.DoesNotContain("asset.two", operation.InvalidatedNodeIds);
    }

    [Fact]
    public void RepairBudget_StopsExactConstraintAndFreezesValidNodes()
    {
        var tracker = new RepairBudgetTracker();
        var operation = new RepairOperation
        {
            TargetNodeId = "asset.one",
            ConstraintId = "document.missing_observation",
            Operation = RepairOperationKind.RewriteAsset
        };

        Assert.True(tracker.TryBegin(operation, 2, "hash-one", out _));
        Assert.True(tracker.TryBegin(operation, 2, "hash-two", out _));
        Assert.False(tracker.TryBegin(operation, 2, "hash-three", out var plateau));
        Assert.Equal("asset.one", plateau!.NodeId);
        Assert.Equal("document.missing_observation", plateau.ConstraintId);
        Assert.Equal(2, plateau.Attempts);

        tracker.RecordValid("asset.two", "stable-hash");
        Assert.Contains("asset.two", tracker.FrozenNodeIds);
    }

    [Fact]
    public void DifficultyCatalog_DefinesTopologyForEverySupportedDifficulty()
    {
        var names = new[] { "Rookie", "Detective", "Detective2", "Sergeant", "Lieutenant", "Captain", "Commander" };
        foreach (var name in names)
        {
            var topology = DifficultyProfileCatalog.Get(name).Topology;
            Assert.True(topology.MinDerivationDepth > 0);
            Assert.True(topology.MaxDerivationDepth >= topology.MinDerivationDepth);
            Assert.True(topology.MinIndependentProofPaths > 0);
            Assert.True(topology.RequiredDecoyArcs > 0);
        }

        Assert.True(DifficultyProfileCatalog.Get("Rookie").Topology.InitialSolutionAllowed);
        Assert.Equal(0, DifficultyProfileCatalog.Get("Rookie").Topology.MinForensicHops);
        Assert.Equal(3, DifficultyProfileCatalog.Get("Detective").Topology.MinDerivationDepth);
        Assert.True(DifficultyProfileCatalog.Get("Detective2").Topology.RequiresPartiallyOverlappingProofPaths);
        Assert.True(DifficultyProfileCatalog.Get("Commander").Topology.MinForensicHops >= 3);
    }

    [Fact]
    public void RookieTopology_EnforcesInitialDirectNoForensicsContract()
    {
        var draft = RookieTopologyDraft();
        Assert.Empty(DifficultyTopologyValidator.Validate(draft).Errors);

        draft.CaseGraph.Sources[0].AvailableAt = ReachabilityState.AfterAction;
        draft.CaseGraph.ForensicTransforms.Add(new ForensicTransform { Id = "forensic.invalid" });
        var invalid = DifficultyTopologyValidator.Validate(draft);

        Assert.Contains(invalid.Errors, error => error.Contains("all evidence to be initial", StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.Contains("zero forensic", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectiveTopology_RequiresThreeHopGatedIndependentProof()
    {
        var draft = DetectiveTopologyDraft();

        var report = DifficultyTopologyValidator.Validate(draft);

        Assert.Empty(report.Errors);
    }

    [Fact]
    public void SeniorTopology_EnforcesConflictsReliabilityOptionalAnalysisAndDepth()
    {
        var draft = DetectiveTopologyDraft();
        draft.Metadata.Difficulty = "Commander";
        draft.Metadata.RequiredRank = "Commander";

        var report = DifficultyTopologyValidator.Validate(draft);

        Assert.Contains(report.Errors, error => error.Contains("proof depth", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("conflicting", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("optional analyses", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("reliability levels", StringComparison.Ordinal));
    }

    [Fact]
    public void Detective2Topology_RequiresPartiallyOverlappingPaths()
    {
        var draft = DetectiveTopologyDraft();
        draft.Metadata.Difficulty = "Detective2";
        draft.Metadata.RequiredRank = "Detective2";

        var report = DifficultyTopologyValidator.Validate(draft);

        Assert.Contains(report.Errors, error => error.Contains("partially overlapping proof paths", StringComparison.Ordinal));
    }

    private static CaseDraft SimulationDraft()
    {
        var graph = new CaseGraph
        {
            Entities =
            {
                new CaseEntity { Id = "person.culprit", Kind = EntityKind.Person, DisplayName = "Culprit" },
                new CaseEntity { Id = "document.initial", Kind = EntityKind.Document, DisplayName = "Initial" },
                new CaseEntity { Id = "document.hidden", Kind = EntityKind.Document, DisplayName = "Hidden" }
            },
            SuspectReferences =
            {
                new SuspectEntityReference { SuspectId = "suspect.culprit", PersonEntityId = "person.culprit" }
            },
            Facts =
            {
                Fact("fact.initial", "document.initial", "records", "Initial observation"),
                Fact("fact.hidden", "document.hidden", "records", "Hidden observation"),
                new CanonicalFact
                {
                    Id = "fact.private",
                    SubjectId = "person.culprit",
                    Predicate = "secret",
                    LiteralValue = "PRIVATE FACT",
                    LiteralType = LiteralValueType.String,
                    Visibility = FactVisibility.Private
                }
            },
            Sources =
            {
                new EvidenceSourceNode { Id = "asset.initial", ObservationIds = { "observation.initial" } },
                new EvidenceSourceNode { Id = "email.initial", Kind = EvidenceSourceKind.Email },
                new EvidenceSourceNode
                {
                    Id = "asset.hidden",
                    AvailableAt = ReachabilityState.AfterForensic,
                    RevealPrerequisiteIds = { "action.analysis" },
                    ObservationIds = { "observation.hidden" }
                },
                new EvidenceSourceNode
                {
                    Id = "email.future",
                    Kind = EvidenceSourceKind.Email,
                    AvailableAt = ReachabilityState.AfterForensic,
                    RevealPrerequisiteIds = { "action.analysis" }
                }
            },
            Observations =
            {
                new EvidenceObservation { Id = "observation.initial", FactId = "fact.initial", SourceAssetId = "asset.initial" },
                new EvidenceObservation { Id = "observation.hidden", FactId = "fact.hidden", SourceAssetId = "asset.hidden" }
            },
            Actions =
            {
                new InvestigationAction
                {
                    Id = "action.analysis",
                    Kind = InvestigationActionKind.RequestAnalysis,
                    CompletesAt = ReachabilityState.AfterForensic,
                    PrerequisiteSourceIds = { "asset.initial" },
                    RevealsSourceIds = { "asset.hidden", "email.future" },
                    AnalysisId = "asset.initial:MetadataAnalysis"
                }
            }
        };
        return new CaseDraft
        {
            Request = new GenerateCaseV2Request { Language = "en-US" },
            CaseGraph = graph,
            Metadata = new PlotMetadata { Title = "Case", Location = "Boston", Difficulty = "Detective" },
            SuspectFull =
            {
                new PlotSuspect { Id = "suspect.culprit", Name = "Alex", Occupation = "Clerk", Relationship = "Coworker" }
            },
            AssetFull =
            {
                new EvidenceAsset
                {
                    Id = "asset.initial",
                    Type = "document",
                    Title = "Initial asset",
                    Body = "Complete body",
                    BodyDoc = new EvidenceDocument
                    {
                        Sections = { new EvidenceSection { Kind = "narrative", Text = "Visible section" } }
                    }
                }
            },
            ResultAssets =
            {
                new EvidenceAsset { Id = "asset.hidden", Type = "document", Title = "Hidden result", Body = "Future result" }
            },
            FollowUpEmails =
            {
                new EvidenceEmail
                {
                    Id = "email.initial", From = "unit@casezero.local", Subject = "Initial",
                    Body = "Visible email", Visibility = "initial", Attachments = { "asset.initial", "asset.hidden" }
                }
            },
            ResultEmails =
            {
                new EvidenceEmail { Id = "email.future", Subject = "Future", Body = "Future email", Visibility = "hidden" }
            },
            Timeline =
            {
                new EvidenceTimelineEntry { Time = "20:00", Event = "Incident reported" }
            }
        };
    }

    private static CaseDraft RepairDraft()
    {
        var draft = new CaseDraft
        {
            CaseGraph = new CaseGraph
            {
                Sources =
                {
                    new EvidenceSourceNode { Id = "asset.one" },
                    new EvidenceSourceNode { Id = "asset.two" }
                }
            },
            AssetFull =
            {
                new EvidenceAsset { Id = "asset.one", Body = "one" },
                new EvidenceAsset { Id = "asset.two", Body = "two" }
            },
            RequiredEvidenceIds = { "asset.one" },
            QuestionTopics =
            {
                new QuestionTopic { Id = "question.one", SupportingEvidenceIds = { "asset.one" } }
            },
            Questions =
            {
                new SolutionQuestion { Id = "question.one", Prompt = "Question?" }
            },
            Explanation = "Uses asset one."
        };
        return draft;
    }

    private static CaseDraft RookieTopologyDraft()
    {
        var draft = TopologyBase("Rookie", 2);
        AddCulpritDirectPath(draft.CaseGraph, "a");
        AddCulpritDirectPath(draft.CaseGraph, "b");
        AddDecoy(draft.CaseGraph, "one");
        AddDecoy(draft.CaseGraph, "two");
        return draft;
    }

    private static CaseDraft DetectiveTopologyDraft()
    {
        var draft = TopologyBase("Detective", 2);
        AddDetectiveBranch(draft.CaseGraph, "a");
        AddDetectiveBranch(draft.CaseGraph, "b");
        AddDecoy(draft.CaseGraph, "one");
        AddDecoy(draft.CaseGraph, "two");
        return draft;
    }

    private static CaseDraft TopologyBase(string difficulty, int decoys)
    {
        var graph = new CaseGraph();
        AddPerson(graph, "suspect.culprit", "person.culprit");
        for (var index = 1; index <= decoys; index++)
            AddPerson(graph, $"suspect.decoy_{Number(index)}", $"person.decoy_{Number(index)}");
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.culprit",
            SubjectId = "person.culprit",
            Predicate = "isCulprit",
            LiteralValue = "true",
            LiteralType = LiteralValueType.Boolean
        });
        return new CaseDraft
        {
            CulpritId = "suspect.culprit",
            Metadata = new PlotMetadata { Difficulty = difficulty, RequiredRank = difficulty },
            Request = new GenerateCaseV2Request { Difficulty = difficulty, RequiredRank = difficulty },
            CaseGraph = graph,
            SuspectFull =
            {
                new PlotSuspect { Id = "suspect.culprit", Name = "Culprit" }
            }
        };
    }

    private static void AddCulpritDirectPath(CaseGraph graph, string suffix)
    {
        var factId = $"fact.clue_{suffix}";
        var observationId = $"observation.clue_{suffix}";
        var assetId = $"asset.clue_{suffix}";
        graph.Facts.Add(Fact(factId, "person.culprit", $"identity_{suffix}", $"Identity {suffix}"));
        graph.Sources.Add(new EvidenceSourceNode { Id = assetId, EvidentiaryOriginId = $"origin.{suffix}", ObservationIds = { observationId } });
        graph.Observations.Add(new EvidenceObservation { Id = observationId, FactId = factId, SourceAssetId = assetId });
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
        var initialFact = $"fact.initial_{suffix}";
        var bridgeFact = $"fact.bridge_{suffix}";
        var joinedFact = $"fact.joined_{suffix}";
        var initialObservation = $"observation.initial_{suffix}";
        var forensicObservation = $"observation.forensic_{suffix}";
        var initialAsset = $"asset.initial_{suffix}";
        var resultAsset = $"asset.result_{suffix}";
        var actionId = $"action.analysis_{suffix}";
        graph.Facts.Add(Fact(initialFact, "person.culprit", "identity", $"Initial {suffix}"));
        graph.Facts.Add(Fact(bridgeFact, "person.culprit", "identityBridge", $"Bridge {suffix}"));
        graph.Facts.Add(Fact(joinedFact, "person.culprit", "actionJoin", $"Join {suffix}"));
        graph.Facts.Add(Fact($"fact.forensic_{suffix}", "person.culprit", "forensic", $"Forensic {suffix}"));
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = initialAsset, EvidentiaryOriginId = $"origin.initial_{suffix}", ObservationIds = { initialObservation }
        });
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = resultAsset, EvidentiaryOriginId = $"origin.forensic_{suffix}",
            AvailableAt = ReachabilityState.AfterForensic,
            RevealPrerequisiteIds = { actionId },
            ObservationIds = { forensicObservation }
        });
        graph.Observations.Add(new EvidenceObservation { Id = initialObservation, FactId = initialFact, SourceAssetId = initialAsset });
        graph.Observations.Add(new EvidenceObservation
        {
            Id = forensicObservation, FactId = $"fact.forensic_{suffix}", SourceAssetId = resultAsset,
            ForensicProperty = ForensicObservationProperty.DeviceCorrelation
        });
        graph.Actions.Add(new InvestigationAction
        {
            Id = actionId,
            Kind = InvestigationActionKind.RequestAnalysis,
            PrerequisiteSourceIds = { initialAsset },
            RevealsSourceIds = { resultAsset },
            CompletesAt = ReachabilityState.AfterForensic,
            AnalysisId = $"{initialAsset}:MetadataAnalysis",
            Required = true
        });
        graph.ForensicTransforms.Add(new ForensicTransform
        {
            Id = $"forensic.{suffix}",
            MethodId = "MetadataAnalysis",
            InputAssetId = initialAsset,
            ProducedObservationIds = { forensicObservation },
            ProducedProperties = { ForensicObservationProperty.DeviceCorrelation },
            ResultAssetId = resultAsset
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.identity_{suffix}",
            PremiseIds = { initialObservation },
            Rule = DerivationRule.IdentityMatch,
            ConclusionFactId = bridgeFact
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.join_{suffix}",
            PremiseIds = { $"derivation.identity_{suffix}", forensicObservation },
            Rule = DerivationRule.CrossSourceCorroboration,
            ConclusionFactId = joinedFact
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = $"derivation.culprit_{suffix}",
            PremiseIds = { $"derivation.join_{suffix}" },
            Rule = DerivationRule.ActionAttribution,
            ConclusionFactId = "fact.culprit",
            SupportsSuspectId = "suspect.culprit",
            IsCulpritConclusion = true
        });
    }

    private static void AddDecoy(CaseGraph graph, string suffix)
    {
        var suspectId = $"suspect.decoy_{suffix}";
        var personId = $"person.decoy_{suffix}";
        var factId = $"fact.decoy_{suffix}";
        var excludedId = $"fact.excluded_{suffix}";
        var observationId = $"observation.decoy_{suffix}";
        var sourceId = $"asset.decoy_{suffix}";
        var derivationId = $"derivation.exclude_{suffix}";
        graph.Facts.Add(Fact(factId, personId, "verification", $"Verified {suffix}"));
        graph.Facts.Add(Fact(excludedId, personId, "excludedFromCrime", "true"));
        graph.Sources.Add(new EvidenceSourceNode { Id = sourceId, ObservationIds = { observationId } });
        graph.Observations.Add(new EvidenceObservation { Id = observationId, FactId = factId, SourceAssetId = sourceId });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = derivationId,
            PremiseIds = { observationId },
            Rule = DerivationRule.Exclusion,
            ConclusionFactId = excludedId,
            SupportsSuspectId = suspectId
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

    private static CanonicalFact Fact(string id, string subject, string predicate, string value) => new()
    {
        Id = id,
        SubjectId = subject,
        Predicate = predicate,
        LiteralValue = value,
        LiteralType = LiteralValueType.String
    };

    private static string Number(int value) => value switch
    {
        1 => "one",
        2 => "two",
        3 => "three",
        4 => "four",
        _ => value.ToString()
    };
}
