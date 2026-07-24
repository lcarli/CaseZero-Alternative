using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseGraphKernelTests
{
    [Fact]
    public void Validator_ReportsTypedRepairableErrorsForInvalidGraphShapes()
    {
        var graph = BaseGraph();
        graph.Facts.AddRange(
        [
            Fact("fact.invalid_value", "person.culprit", "assignedTo", literal: null),
            Fact("fact.missing_subject", "person.missing", "status", "active"),
            new CanonicalFact
            {
                Id = "fact.missing_object",
                Predicate = "assignedTo",
                SubjectId = "person.culprit",
                ObjectId = "device.missing"
            },
            new CanonicalFact
            {
                Id = "fact.duration",
                Predicate = "duration",
                SubjectId = "person.culprit",
                LiteralValue = "12",
                LiteralType = LiteralValueType.Duration
            },
            Fact("fact.conflict_one", "person.culprit", "badgeNumber", "41"),
            Fact("fact.conflict_two", "person.culprit", "badgeNumber", "42")
        ]);
        graph.Events.Add(new CanonicalEvent
        {
            Id = "event.window",
            Kind = EventKind.Accessed,
            Timestamp = DateTimeOffset.Parse("2026-07-21T20:00:00Z"),
            WindowEnd = DateTimeOffset.Parse("2026-07-21T19:00:00Z"),
            Precision = TemporalPrecision.Window,
            ParticipantIds = { "person.missing" },
            FactIds = { "fact.missing" }
        });
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = "asset.initial",
            ObservationIds = { "observation.private" }
        });
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.private",
            Predicate = "isCulprit",
            SubjectId = "person.culprit",
            LiteralValue = "true",
            LiteralType = LiteralValueType.Boolean,
            Visibility = FactVisibility.Private
        });
        graph.Observations.Add(new EvidenceObservation
        {
            Id = "observation.private",
            FactId = "fact.private",
            SourceAssetId = "asset.initial",
            Visibility = FactVisibility.Public
        });
        graph.Observations.Add(new EvidenceObservation
        {
            Id = "observation.orphan",
            FactId = "fact.missing",
            SourceAssetId = "asset.missing"
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.orphan",
            PremiseIds = { "observation.missing" },
            ConclusionFactId = "fact.missing"
        });

        var report = CaseGraphValidator.Validate(graph);

        Assert.Contains(report.Errors, error => error.Code == "invalid_canonical_value");
        Assert.Contains(report.Errors, error => error.Code == "missing_fact_subject");
        Assert.Contains(report.Errors, error => error.Code == "missing_fact_object");
        Assert.Contains(report.Errors, error => error.Code == "missing_literal_unit");
        Assert.Contains(report.Errors, error => error.Code == "conflicting_canonical_fact");
        Assert.Contains(report.Errors, error => error.Code == "invalid_event_window");
        Assert.Contains(report.Errors, error => error.Code == "missing_event_participant");
        Assert.Contains(report.Errors, error => error.Code == "missing_observation_source");
        Assert.Contains(report.Errors, error => error.Code == "private_fact_in_initial_evidence");
        Assert.Contains(report.Errors, error => error.Code == "invalid_visibility_transition");
        Assert.Contains(report.Errors, error => error.Code == "missing_derivation_premise");
        Assert.All(report.Errors, error =>
        {
            Assert.False(string.IsNullOrWhiteSpace(error.NodeId));
            Assert.False(string.IsNullOrWhiteSpace(error.OwningStage));
            Assert.Equal(error.NodeId, error.RepairTarget.NodeId);
        });
    }

    [Fact]
    public void Validator_DetectsDuplicateFactsEventsAndObservationOwnership()
    {
        var graph = BaseGraph();
        graph.Facts.Add(Fact("fact.one", "person.culprit", "status", "active"));
        graph.Facts.Add(Fact("fact.two", "person.culprit", "status", "active"));
        var timestamp = DateTimeOffset.Parse("2026-07-21T20:00:00Z");
        graph.Events.Add(Event("event.one", timestamp));
        graph.Events.Add(Event("event.two", timestamp));
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = "asset.record",
            ClaimedFactIds = { "fact.unobserved" }
        });
        graph.Observations.Add(new EvidenceObservation
        {
            Id = "observation.record",
            FactId = "fact.one",
            SourceAssetId = "asset.record"
        });

        var report = CaseGraphValidator.Validate(graph);

        Assert.Contains(report.Errors, error => error.Code == "duplicate_canonical_fact");
        Assert.Contains(report.Errors, error => error.Code == "duplicate_canonical_event");
        Assert.Contains(report.Errors, error => error.Code == "observation_not_owned_by_source");
        Assert.Contains(report.Errors, error => error.Code == "unobserved_source_claim");
    }

    [Fact]
    public void Validator_AllowsFactsInOneDeclaredIntentionalConflict()
    {
        var graph = BaseGraph();
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.reported_one",
            Predicate = "reportedTime",
            SubjectId = "person.culprit",
            LiteralValue = "20:10",
            LiteralType = LiteralValueType.String,
            IntentionalConflictId = "conflict.reported_time"
        });
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.reported_two",
            Predicate = "reportedTime",
            SubjectId = "person.culprit",
            LiteralValue = "20:15",
            LiteralType = LiteralValueType.String,
            IntentionalConflictId = "conflict.reported_time"
        });

        var report = CaseGraphValidator.Validate(graph);

        Assert.DoesNotContain(report.Errors, error => error.Code == "conflicting_canonical_fact");
    }

    [Fact]
    public void Validator_AllowsMultipleAccessEventsForOneSystem()
    {
        var graph = BaseGraph();
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.badge_event_one",
            Predicate = "access.badgeEvent",
            SubjectId = "person.culprit",
            LiteralValue = "18:57",
            LiteralType = LiteralValueType.String
        });
        graph.Facts.Add(new CanonicalFact
        {
            Id = "fact.badge_event_two",
            Predicate = "access.badgeEvent",
            SubjectId = "person.culprit",
            LiteralValue = "19:16",
            LiteralType = LiteralValueType.String
        });

        var report = CaseGraphValidator.Validate(graph);

        Assert.DoesNotContain(report.Errors, error => error.Code == "conflicting_canonical_fact");
    }

    [Fact]
    public void Validator_RejectsImpossibleOrderingAndTravelWindows()
    {
        var graph = BaseGraph();
        graph.Events.Add(Event("event.late", DateTimeOffset.Parse("2026-07-21T21:00:00Z")));
        graph.Events.Add(Event("event.early", DateTimeOffset.Parse("2026-07-21T20:00:00Z")));
        graph.EventOrderConstraints.Add(new EventOrderConstraint
        {
            Id = "order.invalid",
            BeforeEventId = "event.late",
            AfterEventId = "event.early"
        });
        graph.TravelWindows.Add(new TravelWindowConstraint
        {
            Id = "travel.invalid",
            FromEventId = "event.early",
            ToEventId = "event.late",
            MinimumTravelTime = TimeSpan.FromHours(2)
        });

        var report = CaseGraphValidator.Validate(graph);

        Assert.Contains(report.Errors, error => error.Code == "impossible_event_order");
        Assert.Contains(report.Errors, error => error.Code == "impossible_travel_window");
    }

    [Fact]
    public void PlayerTimeline_ContainsOnlyPublicEventsInDeterministicOrder()
    {
        var graph = BaseGraph();
        graph.Events.Add(Event("event.second", DateTimeOffset.Parse("2026-07-21T21:00:00Z")));
        graph.Events.Add(Event("event.private", DateTimeOffset.Parse("2026-07-21T19:00:00Z"), FactVisibility.Private));
        graph.Events.Add(Event("event.first", DateTimeOffset.Parse("2026-07-21T20:00:00Z")));

        var timeline = CaseGraphTimelineCompiler.CompilePlayerTimeline(graph);

        Assert.Equal(["event.first", "event.second"], timeline.Select(item => item.Id));
    }

    [Fact]
    public void Validator_RejectsCircularDerivations()
    {
        var graph = BaseGraph();
        graph.Facts.Add(Fact("fact.a", "person.culprit", "a", "true"));
        graph.Facts.Add(Fact("fact.b", "person.culprit", "b", "true"));
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.a",
            PremiseIds = { "fact.b" },
            Rule = DerivationRule.IdentityMatch,
            ConclusionFactId = "fact.a"
        });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.b",
            PremiseIds = { "fact.a" },
            Rule = DerivationRule.ActionAttribution,
            ConclusionFactId = "fact.b"
        });

        var report = CaseGraphValidator.Validate(graph);
        var closure = DerivationClosureEngine.Calculate(graph, Array.Empty<string>());

        Assert.Equal(2, report.Errors.Count(error => error.Code == "circular_derivation"));
        Assert.False(closure.CanDerive("fact.a"));
        Assert.False(closure.CanDerive("fact.b"));
    }

    [Fact]
    public void CaseGraph_RoundTripsAndDeepCloneIsIndependent()
    {
        var graph = BaseGraph();
        graph.Facts.Add(Fact("fact.status", "person.culprit", "status", "active"));

        var json = JsonSerializer.Serialize(graph);
        var restored = JsonSerializer.Deserialize<CaseGraph>(json);
        var clone = graph.DeepClone();
        graph.Entities[0].DisplayName = "Changed";

        Assert.NotNull(restored);
        Assert.Equal("fact.status", Assert.Single(restored.Facts).Id);
        Assert.Equal("Culprit", clone.Entities[0].DisplayName);
        Assert.Contains("\"Origin\":\"HandAuthored\"", json);
    }

    [Fact]
    public void TransitionalProjection_DoesNotOverwriteHandAuthoredGraph()
    {
        var draft = new CaseDraft
        {
            CaseGraph = BaseGraph(),
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue { Id = "clue.legacy", Discovery = "Legacy clue" }
                }
            }
        };

        var projected = CaseGraphProjection.ProjectTransitionalClueLadder(draft);

        Assert.Same(draft.CaseGraph, projected);
        Assert.Equal(CaseGraphOrigin.HandAuthored, projected.Origin);
        Assert.Empty(projected.Facts);
    }

    [Fact]
    public void TransitionalProjection_BuildsTwoDepthThreeDetectiveProofBranches()
    {
        var draft = new CaseDraft
        {
            CulpritId = "suspect.culprit",
            Request = new GenerateCaseV2Request { Difficulty = "Detective" },
            Metadata = new PlotMetadata { Difficulty = "Detective" },
            SuspectStubs =
            {
                new SuspectStub { Id = "suspect.culprit", Name = "Culprit", IsCulprit = true }
            },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue
                    {
                        Id = "clue.identity", Role = "identity", SourceType = "document",
                        Discovery = "Endpoint A is assigned to the suspect.", SupportsSuspectId = "suspect.culprit"
                    },
                    new CanonicalClue
                    {
                        Id = "clue.action", Role = "action", SourceType = "digital",
                        Discovery = "Session B performed the action.", SupportsSuspectId = "suspect.culprit"
                    },
                    new CanonicalClue
                    {
                        Id = "clue.forensic", Role = "forensicAttribution", SourceType = "forensic",
                        Discovery = "Forensic analysis maps session B to endpoint A.", SupportsSuspectId = "suspect.culprit"
                    }
                }
            },
            AssetStubs =
            {
                new AssetStub
                {
                    Id = "asset.identity", Type = "document", SupportsClueIds = { "clue.identity" },
                    ForensicInputClueIds = { "clue.forensic" }, ContainedObjectIds = { "document.identity" }
                },
                new AssetStub
                {
                    Id = "asset.action", Type = "digital", SupportsClueIds = { "clue.action" },
                    ContainedObjectIds = { "session.action" }
                }
            },
            ForensicStubs =
            {
                new ForensicOutcomeStub
                {
                    InputAssetId = "asset.identity",
                    InputObjectId = "document.identity",
                    AnalysisType = "MetadataAnalysis",
                    Findings = true,
                    SupportsClueIds = { "clue.forensic" }
                }
            },
            ForensicFull =
            {
                new ForensicsOutcome
                {
                    InputAssetId = "asset.identity",
                    AnalysisType = "MetadataAnalysis",
                    Findings = true,
                    ResultAssetId = "asset.forensic"
                }
            },
            ResultAssets =
            {
                new EvidenceAsset { Id = "asset.forensic", Type = "document", Title = "Forensic result" }
            }
        };

        EvidenceGraphCompiler.Compile(draft);
        var reachability = ReachabilityClosureEngine.Calculate(draft.CaseGraph);
        var proof = CulpritUniquenessGate.Validate(
            draft.CaseGraph,
            draft.CulpritId,
            "Detective",
            reachability.FinalObservationIds);
        var independent = IndependentProofPathGate.Validate(
            draft.CaseGraph,
            draft.CulpritId,
            "Detective",
            reachability.FinalObservationIds);

        Assert.True(proof.Paths.Count >= 2);
        Assert.All(proof.Paths, path => Assert.True(path.Depth >= 3));
        Assert.True(independent.Paths.Count >= 2);
    }

    [Fact]
    public void TransitionalProjection_MaterializesDetective2AdvancedTopology()
    {
        var draft = TransitionalTopologyDraft("Detective2", initialClues: 2, forensicClues: 1, decoys: 3);

        EvidenceGraphCompiler.Compile(draft);
        var report = DifficultyTopologyValidator.Validate(draft);
        var evidenceReport = EvidenceContractValidator.ValidatePlan(draft);

        Assert.Empty(report.Errors);
        Assert.DoesNotContain(evidenceReport.Errors, error =>
            error.Contains("excluded without positive verification evidence", StringComparison.Ordinal));
    }

    [Fact]
    public void TransitionalProjection_MaterializesCommanderSeniorTopology()
    {
        var draft = TransitionalTopologyDraft("Commander", initialClues: 3, forensicClues: 3, decoys: 4);

        EvidenceGraphCompiler.Compile(draft);
        var report = DifficultyTopologyValidator.Validate(draft);

        Assert.Empty(report.Errors);
    }

    [Fact]
    public void CaseBibleNormalization_SortsTruthTimelineChronologically()
    {
        var bible = new CaseBible
        {
            TruthTimeline =
            {
                new CaseBibleTruthBeat { Id = "event.third", Time = "2026-07-21T22:00:00Z" },
                new CaseBibleTruthBeat { Id = "event.first", Time = "2026-07-21T20:00:00Z" },
                new CaseBibleTruthBeat { Id = "event.second", Time = "2026-07-21T21:00:00Z" }
            }
        };

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal(
            ["event.first", "event.second", "event.third"],
            bible.TruthTimeline.Select(beat => beat.Id));
    }

    private static CaseGraph BaseGraph() => new()
    {
        Entities =
        {
            new CaseEntity { Id = "person.culprit", Kind = EntityKind.Person, DisplayName = "Culprit" }
        },
        SuspectReferences =
        {
            new SuspectEntityReference { SuspectId = "suspect.culprit", PersonEntityId = "person.culprit" }
        }
    };

    private static CaseDraft TransitionalTopologyDraft(
        string difficulty,
        int initialClues,
        int forensicClues,
        int decoys)
    {
        var draft = new CaseDraft
        {
            CulpritId = "suspect.culprit",
            Request = new GenerateCaseV2Request { Difficulty = difficulty, RequiredRank = difficulty },
            Metadata = new PlotMetadata { Difficulty = difficulty, RequiredRank = difficulty }
        };
        draft.SuspectStubs.Add(new SuspectStub
        {
            Id = draft.CulpritId,
            Name = "Culprit",
            IsCulprit = true
        });

        var initialRoles = new[] { "identity", "action", "benefit" };
        var initialSources = new[] { "document", "digital", "witness" };
        for (var index = 0; index < initialClues; index++)
        {
            var suffix = index + 1;
            var clueId = $"clue.initial_{suffix}";
            draft.Blueprint.ClueLadder.Add(new CanonicalClue
            {
                Id = clueId,
                Role = initialRoles[Math.Min(index, initialRoles.Length - 1)],
                SourceType = initialSources[Math.Min(index, initialSources.Length - 1)],
                Strength = "supporting",
                Discovery = $"Initial culprit observation {suffix}.",
                SupportsSuspectId = draft.CulpritId
            });
            draft.AssetStubs.Add(new AssetStub
            {
                Id = $"asset.initial_{suffix}",
                Type = initialSources[Math.Min(index, initialSources.Length - 1)] == "digital"
                    ? "digital"
                    : "document",
                SupportsClueIds = { clueId },
                ContainedObjectIds = { $"document.initial_{suffix}" }
            });
        }

        for (var index = 0; index < forensicClues; index++)
        {
            var suffix = index + 1;
            var clueId = $"clue.forensic_{suffix}";
            var inputAssetId = $"asset.forensic_input_{suffix}";
            var resultAssetId = $"asset.forensic_result_{suffix}";
            var analysisType = $"MetadataAnalysis{suffix}";
            draft.Blueprint.ClueLadder.Add(new CanonicalClue
            {
                Id = clueId,
                Role = "forensicAttribution",
                SourceType = "forensic",
                Strength = index == forensicClues - 1 ? "decisive" : "supporting",
                Discovery = $"Forensic culprit observation {suffix}.",
                SupportsSuspectId = draft.CulpritId
            });
            draft.AssetStubs.Add(new AssetStub
            {
                Id = inputAssetId,
                Type = "document",
                ForensicInputClueIds = { clueId },
                ContainedObjectIds = { $"document.forensic_input_{suffix}" }
            });
            draft.ForensicStubs.Add(new ForensicOutcomeStub
            {
                InputAssetId = inputAssetId,
                InputObjectId = $"document.forensic_input_{suffix}",
                AnalysisType = analysisType,
                Findings = true,
                SupportsClueIds = { clueId }
            });
            draft.ForensicFull.Add(new ForensicsOutcome
            {
                InputAssetId = inputAssetId,
                AnalysisType = analysisType,
                Findings = true,
                ResultAssetId = resultAssetId
            });
            draft.ResultAssets.Add(new EvidenceAsset
            {
                Id = resultAssetId,
                Type = "document",
                Title = $"Forensic result {suffix}"
            });
        }

        for (var index = 0; index < decoys; index++)
        {
            var suffix = index + 1;
            var suspectId = $"suspect.decoy_{suffix}";
            draft.SuspectStubs.Add(new SuspectStub { Id = suspectId, Name = $"Decoy {suffix}" });
            draft.Blueprint.RedHerrings.Add(new CanonicalRedHerring
            {
                SuspectId = suspectId,
                Suspicion = $"Suspicious observation {suffix}.",
                Verification = $"Verification record {suffix}.",
                Resolution = $"Explanation {suffix}."
            });
            draft.AssetStubs.Add(new AssetStub
            {
                Id = $"asset.decoy_{suffix}",
                Type = "document",
                IntroducesRedHerringSuspectIds = { suspectId },
                ResolvesRedHerringSuspectIds = { suspectId },
                ContainedObjectIds = { $"document.decoy_{suffix}" }
            });
        }

        return draft;
    }

    private static CanonicalFact Fact(
        string id,
        string subjectId,
        string predicate,
        string? literal) => new()
    {
        Id = id,
        SubjectId = subjectId,
        Predicate = predicate,
        LiteralValue = literal,
        LiteralType = literal is null ? null : LiteralValueType.String
    };

    private static CanonicalEvent Event(
        string id,
        DateTimeOffset timestamp,
        FactVisibility visibility = FactVisibility.Public) => new()
    {
        Id = id,
        Kind = EventKind.Accessed,
        Timestamp = timestamp,
        Precision = TemporalPrecision.Exact,
        ParticipantIds = { "person.culprit" },
        Visibility = visibility
    };
}
