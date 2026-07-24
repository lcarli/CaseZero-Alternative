using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class ForensicEvidenceContractTests
{
    [Fact]
    public void ForensicCatalog_IsImmutableCompleteAndLocalized()
    {
        var languages = new[] { "pt-BR", "en-US", "es-ES", "fr-FR" };

        Assert.NotEmpty(ForensicMethodCatalog.All);
        foreach (var method in ForensicMethodCatalog.All)
        {
            Assert.NotEmpty(method.AcceptedInputObjectTypes);
            Assert.NotEmpty(method.AcceptedAssetTypes);
            Assert.NotEmpty(method.ProducibleProperties);
            Assert.NotEmpty(method.LimitationKeys);
            Assert.NotEmpty(method.ValidResultLayouts);
            foreach (var language in languages)
            {
                var localized = method.Localize(language);
                Assert.False(string.IsNullOrWhiteSpace(localized.Name));
                Assert.False(string.IsNullOrWhiteSpace(localized.Description));
                Assert.Equal(method.LimitationKeys.Length, localized.Limitations.Count);
                Assert.Equal(7, localized.Labels.Count);
            }
        }

        var metadata = ForensicMethodCatalog.Get("MetadataAnalysis");
        var changed = metadata.AcceptedAssetTypes.Add("invented");
        Assert.DoesNotContain("invented", metadata.AcceptedAssetTypes);
        Assert.Contains("invented", changed);
    }

    [Fact]
    public void ResultIdNormalizer_RenamesDuplicateAssetsEmailsAndAttachments()
    {
        var first = ForensicDetail("asset.report_duplicate", "email.lab_duplicate");
        var second = ForensicDetail("asset.report_duplicate", "email.lab_duplicate");

        ForensicResultIdNormalizer.EnsureUnique(
            new[] { first, second },
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.Equal("asset.report_duplicate", first.asset!.Id);
        Assert.Equal("asset.report_duplicate_2", second.asset!.Id);
        Assert.Equal(second.asset.Id, second.full.ResultAssetId);
        Assert.Contains(second.asset.Id, second.email!.Attachments);
        Assert.Equal("email.lab_duplicate_2", second.email.Id);
        Assert.Equal(second.email.Id, second.full.ResultEmailId);

        static (ForensicsOutcome full, EvidenceAsset? asset, EvidenceEmail? email) ForensicDetail(
            string assetId,
            string emailId)
        {
            var asset = new EvidenceAsset { Id = assetId };
            var email = new EvidenceEmail { Id = emailId, Attachments = { assetId } };
            return (
                new ForensicsOutcome { ResultAssetId = assetId, ResultEmailId = emailId },
                asset,
                email);
        }
    }

    [Fact]
    public void MaterializeAnalysisTypes_DiscardsLlmAvailableForExtensions()
    {
        var proposed = new[]
        {
            new ForensicsAnalysisType
            {
                Type = "AccountAttribution",
                DurationMinutes = 75,
                AvailableFor = { "digital", "photo", "invented" }
            }
        };
        var outcomes = new[]
        {
            new ForensicOutcomeStub { AnalysisType = "AccountAttribution" }
        };

        var normalized = Assert.Single(ForensicMethodCatalog.MaterializeAnalysisTypes(proposed, outcomes));
        var expected = ForensicMethodCatalog.Get("AccountAttribution").AcceptedAssetTypes
            .Order(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expected, normalized.AvailableFor.Order(StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain("photo", normalized.AvailableFor);
        Assert.Equal(75, normalized.DurationMinutes);
    }

    [Fact]
    public void ForensicValidator_AcceptsExactContainedObjectProducedPropertyAndLimitations()
    {
        var draft = ValidForensicDraft();

        var report = ForensicContractValidator.Validate(draft);

        Assert.Empty(report.Errors);
    }

    [Fact]
    public void ForensicValidator_RejectsMentionedButUncontainedObjectAndUnsupportedProperty()
    {
        var draft = ValidForensicDraft();
        var transform = Assert.Single(draft.CaseGraph.ForensicTransforms);
        transform.InputObjectId = "document.mentioned_attachment";
        transform.ProducedProperties.Clear();
        transform.ProducedProperties.Add(ForensicObservationProperty.HandwritingSimilarity);
        draft.CaseGraph.Observations.Single(observation => observation.Id == "observation.metadata")
            .ForensicProperty = ForensicObservationProperty.HandwritingSimilarity;

        var report = ForensicContractValidator.Validate(draft);

        Assert.Contains(report.Errors, error => error.Contains("not contained", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("cannot be produced", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("unsupported property", StringComparison.Ordinal));
    }

    [Fact]
    public void ForensicValidator_RequiresReferenceCustodyLayoutItemsAndLimitations()
    {
        var draft = ValidForensicDraft("HandwritingComparison");
        var transform = Assert.Single(draft.CaseGraph.ForensicTransforms);
        transform.ReferenceSampleObjectId = null;
        transform.ChainOfCustodyObservationId = null;
        transform.ResultLayoutId = "MedicalReport";
        transform.LimitationKeys.Clear();
        draft.ResultAssets.Single().BodyDoc!.Sections.Clear();

        var report = ForensicContractValidator.Validate(draft);

        Assert.Contains(report.Errors, error => error.Contains("requires a reference sample", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("requires chain-of-custody", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("result layout", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("no limitation statement", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("Items Submitted", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("Limitations label", StringComparison.Ordinal));
    }

    [Fact]
    public void ForensicValidator_RejectsForensicOnlyCulpritOverclaim()
    {
        var draft = ValidForensicDraft();
        draft.CaseGraph.Facts.Add(new CanonicalFact
        {
            Id = "fact.culprit",
            SubjectId = "person.culprit",
            Predicate = "isCulprit",
            LiteralValue = "true",
            LiteralType = LiteralValueType.Boolean
        });
        draft.CaseGraph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.overclaim",
            PremiseIds = { "observation.metadata" },
            Rule = DerivationRule.CrossSourceCorroboration,
            ConclusionFactId = "fact.culprit",
            SupportsSuspectId = "suspect.culprit",
            IsCulpritConclusion = true
        });

        var report = ForensicContractValidator.Validate(draft);

        Assert.Contains(report.Errors, error => error.Contains("forensic observations alone", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingDecisiveAttribution_IsValidationFailureAndDoesNotFabricateMatch()
    {
        var draft = new CaseDraft
        {
            CulpritId = "suspect.culprit",
            Metadata = new PlotMetadata { Difficulty = "Detective" },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue
                    {
                        Id = "clue.decisive",
                        SourceType = "forensic",
                        Strength = "decisive",
                        SupportsSuspectId = "suspect.culprit"
                    }
                }
            },
            ForensicStubs =
            {
                new ForensicOutcomeStub
                {
                    InputAssetId = "asset.input",
                    AnalysisType = "MetadataAnalysis",
                    Findings = true,
                    MatchedSuspectId = null,
                    SupportsClueIds = { "clue.decisive" }
                }
            }
        };

        var report = ForensicContractValidator.Validate(draft);

        Assert.Contains(report.Errors, error => error.Contains("decisive culprit attribution", StringComparison.Ordinal));
        Assert.Null(draft.ForensicStubs[0].MatchedSuspectId);
    }

    [Fact]
    public void MissingClueOwnership_RemainsUnassignedAndProducesRepeatableRepairError()
    {
        var draft = new CaseDraft
        {
            Metadata = new PlotMetadata { Difficulty = "Rookie" },
            Request = new GenerateCaseV2Request { Difficulty = "Rookie" },
            Blueprint = new InvestigationBlueprint
            {
                ClueLadder =
                {
                    new CanonicalClue
                    {
                        Id = "clue.unowned",
                        SourceType = "document",
                        Discovery = "A specific record exists."
                    }
                }
            },
            AssetStubs =
            {
                Stub("asset.one"), Stub("asset.two"), Stub("asset.three")
            }
        };

        var first = PipelineStageValidator.ValidateEvidencePlan(draft);
        var second = PipelineStageValidator.ValidateEvidencePlan(draft);

        Assert.Contains(first.Errors, error => error.Contains("clue 'clue.unowned' has no evidence asset", StringComparison.Ordinal));
        Assert.Contains(second.Errors, error => error.Contains("clue 'clue.unowned' has no evidence asset", StringComparison.Ordinal));
        Assert.All(draft.AssetStubs, asset => Assert.Empty(asset.SupportsClueIds));
    }

    [Fact]
    public void AssetSpecValidator_RejectsPlaceholderMissingObjectsAndBadLayout()
    {
        var draft = EvidenceDraft();
        var spec = Assert.Single(draft.CaseGraph.AssetSpecs);
        spec.ObservationIds.Clear();
        spec.ContainedObjectIds.Clear();
        spec.ContainedObjectTypes.Clear();
        spec.LayoutId = "Photo";

        var report = EvidenceContractValidator.ValidatePlan(draft);

        Assert.Contains(report.Errors, error => error.Contains("empty placeholder", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("incompatible", StringComparison.Ordinal));
    }

    [Fact]
    public void FidelityValidator_RequiresCanonicalValuesAndRejectsPrivateLeakageOverclaimAndEmptyTables()
    {
        var draft = EvidenceDraft();
        var asset = Assert.Single(draft.AssetFull);
        asset.Description = "The culprit committed the crime.";
        asset.BodyDoc!.Sections[0].Text = "Secret operator was suspect.culprit.";
        asset.BodyDoc.Sections.Add(new EvidenceSection
        {
            Kind = "table",
            Table = new EvidenceTable { Columns = { "Time", "Event" } }
        });

        var report = EvidenceContractValidator.ValidateContent(draft);

        Assert.Contains(report.Errors, error => error.Contains("does not materialize observation", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("leaks unassigned private fact", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("stronger conclusion", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("placeholder content", StringComparison.Ordinal));
    }

    [Fact]
    public void SemanticReviewerContract_IsNarrowAndTyped()
    {
        var draft = EvidenceDraft();

        var report = EvidenceContractValidator.ValidateContent(draft, new RejectingSemanticReviewer());

        Assert.Contains(report.Errors, error => error.Contains("semantic overstatement", StringComparison.Ordinal));
    }

    [Fact]
    public void DecoyArc_RequiresSuspicionPositiveVerificationReachabilityAndNeutralWording()
    {
        var draft = DecoyDraft();
        var valid = EvidenceContractValidator.Validate(draft);
        Assert.Empty(valid.Errors);

        var arc = Assert.Single(draft.CaseGraph.DecoyArcs);
        arc.SuspicionObservationIds.Clear();
        draft.CaseGraph.Derivations.Single().PremiseIds.Clear();
        draft.AssetFull.Single(asset => asset.Id == "asset.verification").BodyDoc!.Sections[0].Text =
            "The suspect is innocent.";

        var invalid = EvidenceContractValidator.Validate(draft);

        Assert.Contains(invalid.Errors, error => error.Contains("no plausible suspicion", StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.Contains("without positive verification", StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.Contains("explicitly labels", StringComparison.Ordinal));
    }

    private static CaseDraft ValidForensicDraft(string methodId = "MetadataAnalysis")
    {
        var method = ForensicMethodCatalog.Get(methodId);
        var property = method.ProducibleProperties.OrderBy(value => value).First();
        var limitation = method.LimitationKeys[0];
        var localization = method.Localize("en-US");
        var inputObjectType = method.AcceptedInputObjectTypes.OrderBy(value => value).First();
        var inputAssetType = method.AcceptedAssetTypes.Order(StringComparer.Ordinal).First();
        var inputObjectId = inputObjectType == ForensicInputObjectType.Image
            ? "document.image"
            : "document.input";
        var graph = new CaseGraph
        {
            Entities =
            {
                new CaseEntity { Id = "person.culprit", Kind = EntityKind.Person, DisplayName = "Culprit" },
                new CaseEntity { Id = inputObjectId, Kind = EntityKind.Document, DisplayName = "Submitted object" },
                new CaseEntity { Id = "document.reference", Kind = EntityKind.Document, DisplayName = "Reference sample" }
            },
            SuspectReferences =
            {
                new SuspectEntityReference { SuspectId = "suspect.culprit", PersonEntityId = "person.culprit" }
            },
            Facts =
            {
                new CanonicalFact
                {
                    Id = "fact.custody",
                    SubjectId = inputObjectId,
                    Predicate = "custodyRecorded",
                    LiteralValue = "Seal C-41",
                    LiteralType = LiteralValueType.String
                },
                new CanonicalFact
                {
                    Id = "fact.metadata",
                    SubjectId = inputObjectId,
                    Predicate = "forensicResult",
                    LiteralValue = "Author tag CBR-04",
                    LiteralType = LiteralValueType.String
                }
            },
            Sources =
            {
                new EvidenceSourceNode
                {
                    Id = "asset.input",
                    ObservationIds = { "observation.custody" }
                },
                new EvidenceSourceNode
                {
                    Id = "asset.report",
                    AvailableAt = ReachabilityState.AfterForensic,
                    ObservationIds = { "observation.metadata" }
                }
            },
            Observations =
            {
                new EvidenceObservation
                {
                    Id = "observation.custody",
                    FactId = "fact.custody",
                    SourceAssetId = "asset.input"
                },
                new EvidenceObservation
                {
                    Id = "observation.metadata",
                    FactId = "fact.metadata",
                    SourceAssetId = "asset.report",
                    ForensicProperty = property
                }
            },
            AssetSpecs =
            {
                new EvidenceAssetSpec
                {
                    Id = "asset.input",
                    ArchetypeId = "input_record",
                    AssetType = inputAssetType,
                    LayoutId = inputAssetType == "photo" ? "Photo" : inputAssetType == "digital" ? "FileListing" : "PoliceReport",
                    ObservationIds = { "observation.custody" },
                    ContainedObjectIds = { inputObjectId },
                    ContainedObjectTypes = { [inputObjectId] = inputObjectType }
                }
            },
            ForensicTransforms =
            {
                new ForensicTransform
                {
                    Id = "forensic.test",
                    MethodId = methodId,
                    InputAssetId = "asset.input",
                    InputObjectId = inputObjectId,
                    ReferenceSampleObjectId = method.RequiresReferenceSample ? "document.reference" : null,
                    ChainOfCustodyObservationId = method.RequiresChainOfCustody ? "observation.custody" : null,
                    ProducedObservationIds = { "observation.metadata" },
                    ProducedProperties = { property },
                    LimitationKeys = { limitation },
                    ResultAssetId = "asset.report",
                    ResultLayoutId = "ForensicReport"
                }
            }
        };
        return new CaseDraft
        {
            Request = new GenerateCaseV2Request { Language = "en-US" },
            CaseGraph = graph,
            AnalysisTypes = { ForensicMethodCatalog.CreateAnalysisType(methodId, 60) },
            ResultAssets =
            {
                new EvidenceAsset
                {
                    Id = "asset.report",
                    Type = "pdf",
                    BodyDoc = new EvidenceDocument
                    {
                        Layout = "ForensicReport",
                        Sections =
                        {
                            new EvidenceSection
                            {
                                Kind = "keyValue",
                                Heading = localization.Labels["itemsSubmitted"],
                                Text = inputObjectId
                            },
                            new EvidenceSection
                            {
                                Kind = "narrative",
                                Heading = localization.Labels["findings"],
                                Text = "Author tag CBR-04"
                            },
                            new EvidenceSection
                            {
                                Kind = "narrative",
                                Heading = localization.Labels["limitations"],
                                Text = localization.Limitations[limitation]
                            }
                        }
                    }
                }
            }
        };
    }

    private static CaseDraft EvidenceDraft()
    {
        var graph = new CaseGraph
        {
            Entities =
            {
                new CaseEntity { Id = "person.culprit", Kind = EntityKind.Person, DisplayName = "Culprit" },
                new CaseEntity { Id = "document.record", Kind = EntityKind.Document, DisplayName = "Access record" }
            },
            SuspectReferences =
            {
                new SuspectEntityReference { SuspectId = "suspect.culprit", PersonEntityId = "person.culprit" }
            },
            Facts =
            {
                new CanonicalFact
                {
                    Id = "fact.public",
                    SubjectId = "document.record",
                    Predicate = "recordsAccess",
                    LiteralValue = "Badge 41 opened the archive at 21:10.",
                    LiteralType = LiteralValueType.String
                },
                new CanonicalFact
                {
                    Id = "fact.private",
                    SubjectId = "person.culprit",
                    Predicate = "privateOperator",
                    LiteralValue = "Secret operator was suspect.culprit.",
                    LiteralType = LiteralValueType.String,
                    Visibility = FactVisibility.Private
                }
            },
            Sources =
            {
                new EvidenceSourceNode { Id = "asset.record", ObservationIds = { "observation.public" } }
            },
            Observations =
            {
                new EvidenceObservation
                {
                    Id = "observation.public",
                    FactId = "fact.public",
                    SourceAssetId = "asset.record"
                }
            },
            AssetSpecs =
            {
                new EvidenceAssetSpec
                {
                    Id = "asset.record",
                    ArchetypeId = "access_log",
                    AssetType = "digital",
                    LayoutId = "AccessLog",
                    ObservationIds = { "observation.public" },
                    ContainedObjectIds = { "document.record" },
                    ContainedObjectTypes = { ["document.record"] = ForensicInputObjectType.AccessRecord }
                }
            }
        };
        return new CaseDraft
        {
            CulpritId = "suspect.culprit",
            CaseGraph = graph,
            AssetFull =
            {
                new EvidenceAsset
                {
                    Id = "asset.record",
                    Type = "digital",
                    Description = "Access record.",
                    BodyDoc = new EvidenceDocument
                    {
                        Layout = "AccessLog",
                        Sections =
                        {
                            new EvidenceSection
                            {
                                Kind = "narrative",
                                Text = "Badge 41 opened the archive at 21:10."
                            }
                        }
                    }
                }
            }
        };
    }

    private static CaseDraft DecoyDraft()
    {
        var graph = new CaseGraph();
        AddPerson(graph, "suspect.culprit", "person.culprit");
        AddPerson(graph, "suspect.decoy", "person.decoy");
        graph.Entities.Add(new CaseEntity { Id = "document.suspicion", Kind = EntityKind.Document, DisplayName = "Suspicion record" });
        graph.Entities.Add(new CaseEntity { Id = "document.verification", Kind = EntityKind.Document, DisplayName = "Verification record" });
        graph.Facts.Add(Fact("fact.suspicion", "person.decoy", "suspicion", "The decoy argued with the victim."));
        graph.Facts.Add(Fact("fact.verification", "person.decoy", "verification", "A call record places the decoy elsewhere."));
        graph.Facts.Add(Fact("fact.excluded", "person.decoy", "excludedFromCrime", "true"));
        graph.Sources.Add(new EvidenceSourceNode { Id = "asset.suspicion", ObservationIds = { "observation.suspicion" } });
        graph.Sources.Add(new EvidenceSourceNode { Id = "asset.verification", ObservationIds = { "observation.verification" } });
        graph.Observations.Add(new EvidenceObservation { Id = "observation.suspicion", FactId = "fact.suspicion", SourceAssetId = "asset.suspicion" });
        graph.Observations.Add(new EvidenceObservation { Id = "observation.verification", FactId = "fact.verification", SourceAssetId = "asset.verification" });
        graph.Derivations.Add(new ProofDerivation
        {
            Id = "derivation.exclude",
            PremiseIds = { "observation.verification" },
            Rule = DerivationRule.Exclusion,
            ConclusionFactId = "fact.excluded",
            SupportsSuspectId = "suspect.decoy"
        });
        graph.DecoyArcs.Add(new DecoyArc
        {
            SuspectId = "suspect.decoy",
            SuspicionObservationIds = { "observation.suspicion" },
            VerificationObservationIds = { "observation.verification" },
            ResolutionDerivationId = "derivation.exclude"
        });
        graph.AssetSpecs.Add(Spec("asset.suspicion", "document.suspicion", "observation.suspicion"));
        graph.AssetSpecs.Add(Spec("asset.verification", "document.verification", "observation.verification"));

        return new CaseDraft
        {
            CulpritId = "suspect.culprit",
            CaseGraph = graph,
            AssetFull =
            {
                Asset("asset.suspicion", "The decoy argued with the victim."),
                Asset("asset.verification", "A call record places the decoy elsewhere.")
            }
        };
    }

    private static EvidenceAssetSpec Spec(string assetId, string objectId, string observationId) => new()
    {
        Id = assetId,
        ArchetypeId = "record",
        AssetType = "document",
        LayoutId = "PoliceReport",
        ObservationIds = { observationId },
        ContainedObjectIds = { objectId },
        ContainedObjectTypes = { [objectId] = ForensicInputObjectType.Document }
    };

    private static EvidenceAsset Asset(string id, string text) => new()
    {
        Id = id,
        Type = "document",
        BodyDoc = new EvidenceDocument
        {
            Layout = "PoliceReport",
            Sections = { new EvidenceSection { Kind = "narrative", Text = text } }
        }
    };

    private static CanonicalFact Fact(string id, string subjectId, string predicate, string value) => new()
    {
        Id = id,
        SubjectId = subjectId,
        Predicate = predicate,
        LiteralValue = value,
        LiteralType = LiteralValueType.String
    };

    private static void AddPerson(CaseGraph graph, string suspectId, string personId)
    {
        graph.Entities.Add(new CaseEntity { Id = personId, Kind = EntityKind.Person, DisplayName = suspectId });
        graph.SuspectReferences.Add(new SuspectEntityReference { SuspectId = suspectId, PersonEntityId = personId });
    }

    private static AssetStub Stub(string id) => new()
    {
        Id = id,
        ArchetypeId = "incident_report",
        Type = "document",
        LayoutHint = "PoliceReport",
        ContainedObjectIds = { $"document.{id["asset.".Length..]}" }
    };

    private sealed class RejectingSemanticReviewer : IEvidenceSemanticReviewer
    {
        public IReadOnlyList<EvidenceSemanticReviewFinding> Review(EvidenceSemanticReviewRequest request) =>
        [
            new EvidenceSemanticReviewFinding("overstatement", request.AssetId, "description exceeds assigned observations")
        ];
    }
}
