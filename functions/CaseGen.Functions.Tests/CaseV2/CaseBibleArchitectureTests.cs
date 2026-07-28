using System.Text.Json;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseBibleArchitectureTests
{
    [Fact]
    public void Validator_AcceptsCompleteCanonicalBible()
    {
        var report = CaseBibleValidator.Validate(
            CaseBibleTestData.ValidRookieBible(),
            "Rookie",
            "en-US",
            required: true);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Errors));
    }

    [Fact]
    public void Validator_AcceptsNonRookieForensicAttributionIntent()
    {
        var report = CaseBibleValidator.Validate(
            CaseBibleTestData.ValidDetectiveBible(),
            "Detective",
            "en-US",
            required: true);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Errors));
    }

    [Fact]
    public void CaseBibleTaskSchema_IsValidJson()
    {
        var schema = (string)typeof(CaseBibleTask)
            .GetField("Schema", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetRawConstantValue()!;

        using var document = JsonDocument.Parse(schema);

        Assert.Equal("object", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void Validator_ReportsIdsReferencesChronologySchedulesAndCulprit()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        bible.People[1].Id = bible.People[0].Id;
        bible.Accounts[0].DeviceIds.Add("device.missing");
        bible.TruthTimeline.Reverse();
        bible.WorkSchedules[0].End = "2026-07-21T07:00:00-04:00";
        bible.Incident.CulpritPersonId = "person.victim";

        var report = CaseBibleValidator.Validate(bible, "Rookie", "en-US", required: true);

        Assert.Contains(report.Issues, issue => issue.Code is "duplicate_id" or "duplicate_global_id");
        Assert.Contains(report.Issues, issue => issue.Code == "broken_reference");
        Assert.Contains(report.Issues, issue => issue.Code == "truth_timeline_nonchronological");
        Assert.Contains(report.Issues, issue => issue.Code == "schedule_range_invalid");
        Assert.Contains(report.Issues, issue => issue.Code == "culprit_not_suspect");
    }

    [Fact]
    public void Validator_ReportsInsufficientIntentAndUnmarkedContradictions()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        bible.DifficultyIntent.ProofPaths.RemoveAt(0);
        bible.DifficultyIntent.DecoyArcs.RemoveAt(0);
        bible.DifficultyIntent.ForensicOpportunities.Add(new CaseBibleForensicOpportunity
        {
            Id = "forensic.forbidden",
            InputEntityId = "device.laptop",
            InputSourceId = "source.endpoint_record",
            ResultObservationId = "observation.endpoint_owner",
            MethodHint = "MetadataAnalysis",
            Purpose = "Not allowed for Rookie"
        });
        bible.Facts.Add(new CaseBibleFact
        {
            Id = "fact.last_seen_conflict",
            Predicate = "lastSeenAt",
            SubjectId = "person.victim",
            LiteralValue = "2026-07-21T18:45:00-04:00",
            LiteralType = LiteralValueType.DateTime,
            Visibility = FactVisibility.Public
        });

        var report = CaseBibleValidator.Validate(bible, "Rookie", "en-US", required: true);

        Assert.Contains(report.Issues, issue => issue.Code == "proof_path_count_insufficient");
        Assert.Contains(report.Issues, issue => issue.Code == "decoy_intent_insufficient");
        Assert.Contains(report.Issues, issue => issue.Code == "rookie_forensic_opportunity_forbidden");
        Assert.Contains(report.Issues, issue => issue.Code == "unmarked_fact_contradiction");
    }

    [Fact]
    public void Normalizer_DeclaresResolvableObservationContradiction()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var canonical = bible.Observations.First(observation =>
            !string.IsNullOrWhiteSpace(bible.Facts.Single(fact => fact.Id == observation.FactId).CanonicalValue));
        var fact = bible.Facts.Single(value => value.Id == canonical.FactId);
        canonical.ObservedValue = fact.CanonicalValue;
        canonical.Reliability = ObservationReliability.Verified;
        bible.Observations.Add(new CaseBibleObservation
        {
            Id = $"{canonical.Id}_conflicting",
            FactId = canonical.FactId,
            SourceId = canonical.SourceId,
            Statement = "A conflicting source records a different value.",
            ObservedValue = $"not-{fact.CanonicalValue}",
            Reliability = ObservationReliability.Disputed,
            Visibility = canonical.Visibility
        });

        CaseBibleNormalizer.Normalize(bible);

        var conflict = Assert.Single(bible.DifficultyIntent.IntentionalConflicts.Where(value =>
            value.ObservationIds.Contains(canonical.Id, StringComparer.Ordinal)));
        Assert.Contains(canonical.Id, conflict.ResolutionObservationIds);
        Assert.DoesNotContain(
            CaseBibleValidator.Validate(bible, "Rookie", "en-US", required: true).Errors,
            error => error.Contains("unmarked_observation_contradiction", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalizer_FlattensNestedClueIdsAndReferences()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var clue = bible.DifficultyIntent.Clues[0];
        var originalId = clue.Id;
        clue.Id = "clue.action.contractor_badge";
        foreach (var proofPath in bible.DifficultyIntent.ProofPaths)
        {
            proofPath.ClueIds = proofPath.ClueIds
                .Select(id => id == originalId ? clue.Id : id)
                .ToList();
        }

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal("clue.action_contractor_badge", clue.Id);
        Assert.DoesNotContain(
            bible.DifficultyIntent.ProofPaths.SelectMany(path => path.ClueIds),
            id => id == "clue.action.contractor_badge");
    }

    [Fact]
    public void Normalizer_ProjectsMissingForensicOpportunitiesIntoCulpritClues()
    {
        var bible = CaseBibleTestData.ValidDetectiveBible();
        bible.DifficultyIntent.Difficulty = "Commander";
        foreach (var clue in bible.DifficultyIntent.Clues)
            clue.SourceType = "document";
        bible.DifficultyIntent.ForensicOpportunities.Clear();
        foreach (var (observation, index) in bible.Observations.Take(3).Select((value, index) => (value, index)))
        {
            bible.DifficultyIntent.ForensicOpportunities.Add(new CaseBibleForensicOpportunity
            {
                Id = $"forensic.coverage_{index}",
                InputEntityId = "device.laptop",
                InputSourceId = observation.SourceId,
                ResultObservationId = observation.Id,
                MethodHint = "MetadataAnalysis",
                Purpose = $"Corroborate forensic hop {index + 1}."
            });
        }

        CaseBibleNormalizer.Normalize(bible);

        var projected = bible.DifficultyIntent.Clues
            .Where(clue => clue.SourceType == "forensic"
                           && clue.SupportsPersonId == bible.Incident.CulpritPersonId)
            .ToArray();
        Assert.Equal(3, projected.Length);
        Assert.All(projected, clue => Assert.Contains(
            bible.DifficultyIntent.ProofPaths,
            path => path.ClueIds.Contains(clue.Id, StringComparer.Ordinal)));
    }

    [Fact]
    public void Normalizer_MergesDuplicateFactThatOnlyDiffersByConflictMetadata()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var original = bible.Facts[0];
        var duplicateId = $"{original.Id}_duplicate";
        bible.Facts.Add(new CaseBibleFact
        {
            Id = duplicateId,
            Predicate = original.Predicate,
            SubjectId = original.SubjectId,
            ObjectId = original.ObjectId,
            LiteralValue = original.LiteralValue,
            LiteralType = original.LiteralType,
            LiteralUnit = original.LiteralUnit,
            EventId = original.EventId,
            Visibility = FactVisibility.Public,
            TruthStatus = FactTruthStatus.Confirmed,
            IntentionalConflictId = "conflict.duplicate_value"
        });
        var observationId = bible.Observations[0].Id;
        bible.Observations[0].FactId = duplicateId;

        CaseBibleNormalizer.Normalize(bible);

        Assert.DoesNotContain(bible.Facts, fact => fact.Id == duplicateId);
        Assert.Equal(original.Id, bible.Observations.Single(value => value.Id == observationId).FactId);
        Assert.Equal("conflict.duplicate_value", original.IntentionalConflictId);
        Assert.Equal(FactVisibility.Public, original.Visibility);
    }

    [Fact]
    public void Normalizer_DerivesClueTypeFromCanonicalSourceAndReliabilityFromDecoys()
    {
        var bible = CaseBibleTestData.ValidDetectiveBible();
        bible.DifficultyIntent.Difficulty = "Commander";
        var forensicOpportunity = bible.DifficultyIntent.ForensicOpportunities[0];
        var forensicObservation = bible.Observations.Single(value =>
            value.Id == forensicOpportunity.ResultObservationId);
        var mislabeled = bible.DifficultyIntent.Clues.First(clue =>
            clue.ObservationIds.Contains(forensicObservation.Id, StringComparer.Ordinal));
        mislabeled.SourceType = "document";
        foreach (var observation in bible.Observations)
            observation.Reliability = ObservationReliability.Verified;

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal("forensic", mislabeled.SourceType);
        Assert.True(
            bible.Observations.Select(observation => observation.Reliability).Distinct().Count() >= 3);
    }

    [Fact]
    public async Task TaskRunner_RetriesMalformedJsonWithCompactCorrection()
    {
        var provider = new MalformedThenValidProvider();

        var bible = await TaskRunner.RunStructuredAsync<CaseBible>(
            provider,
            NullLogger.Instance,
            "CaseBible",
            "system",
            "original request",
            "{}",
            CancellationToken.None);

        Assert.Equal("1.0", bible.Version);
        Assert.Equal(2, provider.UserPrompts.Count);
        Assert.Contains("RETRY CORRECTION", provider.UserPrompts[1], StringComparison.Ordinal);
        Assert.Contains("compact values", provider.UserPrompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_AllowsExplicitlyDeclaredIntentionalConflict()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var original = bible.Facts.Single(fact => fact.Id == "fact.last_seen");
        original.IntentionalConflictId = "conflict.last_seen";
        bible.Facts.Add(new CaseBibleFact
        {
            Id = "fact.last_seen_reported",
            Predicate = original.Predicate,
            SubjectId = original.SubjectId,
            LiteralValue = "2026-07-21T19:42:00-04:00",
            LiteralType = LiteralValueType.DateTime,
            Visibility = FactVisibility.Public,
            TruthStatus = FactTruthStatus.Disputed,
            IntentionalConflictId = "conflict.last_seen"
        });
        bible.DifficultyIntent.IntentionalConflicts.Add(new CaseBibleIntentionalConflict
        {
            Id = "conflict.last_seen",
            FactIds = { "fact.last_seen", "fact.last_seen_reported" },
            ResolutionObservationIds = { "observation.last_seen" },
            Purpose = "A witness recollection is less precise than the audit lock record."
        });

        var report = CaseBibleValidator.Validate(bible, "Rookie", "en-US", required: true);

        Assert.DoesNotContain(report.Issues, issue => issue.Code == "unmarked_fact_contradiction");
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Errors));
    }

    [Fact]
    public void MissingBible_IsBackwardCompatibleUnlessGeneratedGraphRequiresIt()
    {
        var legacyDraft = new CaseDraft
        {
            CaseGraph = new CaseGraph { Origin = CaseGraphOrigin.HandAuthored }
        };
        var generatedDraft = new CaseDraft
        {
            CaseGraph = new CaseGraph { Origin = CaseGraphOrigin.Generated }
        };

        Assert.True(CaseBibleValidator.Validate(legacyDraft).IsValid);
        Assert.Contains(
            CaseBibleValidator.Validate(generatedDraft, required: true).Issues,
            issue => issue.Code == "case_bible_missing");
    }

    [Fact]
    public void Normalizer_SortsChronologyAndProjectsCanonicalValues()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        bible.TruthTimeline.Reverse();
        var draft = new CaseDraft
        {
            CaseId = "case_bible_projection",
            Request = new GenerateCaseV2Request
            {
                Difficulty = "Rookie",
                RequiredRank = "Rookie",
                Language = "en-US"
            },
            CaseBible = bible,
            Metadata = new PlotMetadata
            {
                Title = "Presentation title",
                Location = "Wrong place",
                IncidentDate = "2000-01-01T00:00:00Z",
                OpenedAt = "2000-01-01T01:00:00Z"
            }
        };

        CaseBibleNormalizer.Normalize(bible);
        CaseBibleNormalizer.ProjectIntoDraft(draft);

        Assert.Equal(
            ["event.preparation", "event.incident", "event.discovery", "event.opened"],
            bible.TruthTimeline.Select(beat => beat.Id));
        Assert.Equal("Harbor City, New York, United States", draft.Metadata.Location);
        Assert.Equal(bible.Incident.OccurredAt, draft.Metadata.IncidentDate);
        Assert.Equal(bible.Incident.OpenedAt, draft.Metadata.OpenedAt);
        Assert.Equal("America/New_York", draft.Blueprint.Locale.TimeZoneId);
        Assert.Equal("suspect.alex", draft.CulpritId);
        Assert.Equal(
            bible.People.Where(person => person.Roles.Contains(CaseBiblePersonRole.Suspect))
                .Select(person => person.SuspectId)
                .Order(StringComparer.Ordinal),
            draft.SuspectStubs.Select(suspect => suspect.Id));
        Assert.All(draft.Blueprint.ClueLadder, clue => Assert.NotEmpty(clue.ObservationIds));
    }

    [Fact]
    public void Normalizer_CanonicalizesOffsetAndOpeningVisibility()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        bible.World.UtcOffset = "UTC-04:00";
        var openingObservationId = bible.InvestigationConstraints.OpeningObservationIds[0];
        var openingObservation = bible.Observations.Single(value => value.Id == openingObservationId);
        var openingFact = bible.Facts.Single(value => value.Id == openingObservation.FactId);
        openingObservation.Visibility = FactVisibility.Private;
        openingFact.Visibility = FactVisibility.Private;

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal("-04:00", bible.World.UtcOffset);
        Assert.Equal(FactVisibility.Public, openingObservation.Visibility);
        Assert.Equal(FactVisibility.Public, openingFact.Visibility);
    }

    [Fact]
    public void Normalizer_CanonicalizesTypedIdsAndRedirectsReferences()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var fact = bible.Facts[0];
        var observation = bible.Observations.First(value => value.FactId == fact.Id);
        var source = bible.Sources.Single(value => value.Id == observation.SourceId);
        var clue = bible.DifficultyIntent.Clues.First(value =>
            value.ObservationIds.Contains(observation.Id, StringComparer.Ordinal));
        var originalFactId = fact.Id;
        var originalObservationId = observation.Id;
        var originalSourceId = source.Id;
        var originalClueId = clue.Id;

        fact.Id = "fact.access.owner";
        observation.Id = "endpoint_owner";
        observation.FactId = fact.Id;
        source.Id = "source.endpoint.record";
        observation.SourceId = source.Id;
        clue.Id = "clue.access.owner";
        clue.ObservationIds = clue.ObservationIds
            .Select(id => id == originalObservationId ? observation.Id : id)
            .ToList();
        foreach (var beat in bible.TruthTimeline)
            beat.FactIds = beat.FactIds.Select(id => id == originalFactId ? fact.Id : id).ToList();
        foreach (var path in bible.DifficultyIntent.ProofPaths)
        {
            path.ClueIds = path.ClueIds.Select(id => id == originalClueId ? clue.Id : id).ToList();
            if (path.ConclusionFactId == originalFactId)
                path.ConclusionFactId = fact.Id;
        }
        bible.InvestigationConstraints.OpeningObservationIds =
            bible.InvestigationConstraints.OpeningObservationIds
                .Select(id => id == originalObservationId ? observation.Id : id)
                .ToList();
        bible.InvestigationConstraints.MustPreserveFactIds =
            bible.InvestigationConstraints.MustPreserveFactIds
                .Select(id => id == originalFactId ? fact.Id : id)
                .ToList();

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal("fact.access_owner", fact.Id);
        Assert.Equal("observation.endpoint_owner", observation.Id);
        Assert.Equal("source.endpoint_record", source.Id);
        Assert.Equal("clue.access_owner", clue.Id);
        Assert.Equal(fact.Id, observation.FactId);
        Assert.Equal(source.Id, observation.SourceId);
        Assert.Contains(observation.Id, clue.ObservationIds);
        Assert.DoesNotContain(
            CaseBibleValidator.Validate(bible, "Rookie", "en-US", required: true).Issues,
            issue => issue.Code == "typed_id_invalid");
    }

    [Fact]
    public void Normalizer_RewritesTimestampsToCanonicalOffsetPreservingInstant()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        bible.World.UtcOffset = "UTC-04:00";
        bible.Incident.OccurredAt = "2026-07-21T23:30:00Z";
        var truthBeat = bible.TruthTimeline[0];
        truthBeat.Time = "2026-07-21T22:00:00Z";
        var workSchedule = bible.WorkSchedules[0];
        workSchedule.Start = "2026-07-21T20:00:00Z";
        workSchedule.End = "2026-07-22T04:00:00Z";
        var observation = bible.Observations[0];
        observation.ObservedAt = "2026-07-21T23:35:00Z";
        var dateTimeFact = bible.Facts.First(fact => fact.LiteralType == LiteralValueType.DateTime);
        dateTimeFact.LiteralValue = "2026-07-21T23:40:00Z";

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal("-04:00", bible.World.UtcOffset);
        Assert.Equal(TimeSpan.FromHours(-4), DateTimeOffset.Parse(bible.Incident.OccurredAt).Offset);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-21T23:30:00Z"),
            DateTimeOffset.Parse(bible.Incident.OccurredAt));
        Assert.All(
            new[]
            {
                truthBeat.Time,
                workSchedule.Start,
                workSchedule.End,
                observation.ObservedAt!,
                dateTimeFact.LiteralValue!
            },
            value => Assert.Equal(TimeSpan.FromHours(-4), DateTimeOffset.Parse(value).Offset));
    }

    [Fact]
    public void Normalizer_InterpretsOffsetlessTimestampInCanonicalZone()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        bible.World.UtcOffset = "-04:00";
        bible.Incident.OccurredAt = "2026-07-21T23:30:00";

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal(
            DateTimeOffset.Parse("2026-07-21T23:30:00-04:00"),
            DateTimeOffset.Parse(bible.Incident.OccurredAt));
    }

    [Fact]
    public void Normalizer_ReplacesRejectedInvestigatorPlaceholderDomain()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var investigator = bible.People.Single(person =>
            person.Id == bible.World.InvestigatorPersonId);
        investigator.Email = "detective@agency.example";

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal("detective@casezero.local", investigator.Email);
    }

    [Theory]
    [InlineData("\"verified\"", FactTruthStatus.Confirmed)]
    [InlineData("\"contested\"", FactTruthStatus.Disputed)]
    [InlineData("\"disproven\"", FactTruthStatus.False)]
    public void FactTruthStatus_DeserializesSafeAliases(string json, FactTruthStatus expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<FactTruthStatus>(json));
    }

    [Theory]
    [InlineData("\"direct\"", ObservationFidelity.DirectRecord)]
    [InlineData("\"witness_statement\"", ObservationFidelity.Testimony)]
    [InlineData("\"indirect\"", ObservationFidelity.Circumstantial)]
    public void ObservationFidelity_DeserializesSafeAliases(string json, ObservationFidelity expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<ObservationFidelity>(json));
    }

    [Fact]
    public void Normalizer_MakesClueAndDecoyFactsPlayerVisible()
    {
        var bible = CaseBibleTestData.ValidDetectiveBible();
        var observationId = bible.DifficultyIntent.Clues[0].ObservationIds[0];
        var observation = bible.Observations.Single(value => value.Id == observationId);
        var fact = bible.Facts.Single(value => value.Id == observation.FactId);
        observation.Visibility = FactVisibility.Private;
        fact.Visibility = FactVisibility.Private;

        CaseBibleNormalizer.Normalize(bible);

        Assert.Equal(FactVisibility.Public, observation.Visibility);
        Assert.Equal(FactVisibility.Public, fact.Visibility);
    }

    [Fact]
    public void Normalizer_ReplacesIncidentFactSubjectWithCaseContext()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var factId = bible.Facts[0].Id;
        bible.Facts[0].SubjectId = "incident";

        CaseBibleNormalizer.Normalize(bible);
        var report = CaseBibleValidator.Validate(bible, "Rookie", "en-US", required: true);

        Assert.Equal(
            "organization.case_context",
            bible.Facts.Single(value => value.Id == factId).SubjectId);
        Assert.Contains(bible.Institutions, value => value.Id == "organization.case_context");
        Assert.DoesNotContain(report.Issues, issue => issue.Code == "broken_reference");
    }

    [Fact]
    public void Normalizer_RepairsReciprocalDeviceLinksAndFactValues()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var account = bible.Accounts[0];
        var device = bible.Devices.Single(value => value.Id == account.DeviceIds[0]);
        var factId = bible.Facts[0].Id;
        device.AccountIds.Remove(account.Id);
        bible.Facts[0].ObjectId = null;
        bible.Facts[0].LiteralValue = null;
        bible.Facts[0].LiteralType = null;

        CaseBibleNormalizer.Normalize(bible);
        var report = CaseBibleValidator.Validate(bible, "Rookie", "en-US", required: true);

        Assert.Contains(account.Id, device.AccountIds);
        Assert.Equal("true", bible.Facts.Single(value => value.Id == factId).LiteralValue);
        Assert.DoesNotContain(report.Issues, issue =>
            issue.Code is "account_device_link_not_reciprocal" or "fact_value_invalid");
    }

    [Fact]
    public async Task PlotOutlineTask_OverwritesConflictingModelValuesWithBibleProjection()
    {
        var bible = CaseBibleTestData.ValidRookieBible();
        var draft = new CaseDraft
        {
            CaseId = "case_plot_projection",
            Request = new GenerateCaseV2Request
            {
                Difficulty = "Rookie",
                RequiredRank = "Rookie",
                Language = "en-US"
            },
            CaseBible = bible
        };
        var provider = new StaticStructuredProvider(JsonSerializer.Serialize(new
        {
            metadata = new
            {
                title = "Projected title",
                description = "A concise public description.",
                location = "Conflicting City",
                incidentDate = "2000-01-01T00:00:00Z",
                openedAt = "2000-01-01T01:00:00Z",
                difficulty = "Commander",
                requiredRank = "Commander",
                category = "Records",
                briefing = "Review the supplied records.",
                tags = new[] { "records" },
                victim = new { name = "Wrong Victim", age = 20, occupation = "none" }
            },
            clueOrderIds = bible.DifficultyIntent.Clues.Select(clue => clue.Id).Reverse(),
            decoyOrderIds = bible.DifficultyIntent.DecoyArcs.Select(decoy => decoy.Id).Reverse()
        }));

        await new PlotOutlineTask(provider, NullLogger.Instance).RunAsync(draft, CancellationToken.None);

        Assert.Equal(bible.World.LocationDisplayName, draft.Metadata.Location);
        Assert.Equal(bible.Incident.OccurredAt, draft.Metadata.IncidentDate);
        Assert.Equal("Morgan Reed", draft.Metadata.Victim?.Name);
        Assert.Equal("Rookie", draft.Metadata.Difficulty);
        Assert.Equal("suspect.alex", draft.CulpritId);
        Assert.DoesNotContain(draft.SuspectStubs, suspect => suspect.Name == "Wrong Victim");
    }

    [Fact]
    public void GraphProjection_UsesCanonicalFactsAndObservations()
    {
        var draft = CaseBibleTestData.ProjectedRookieDraft();
        var identityClue = draft.Blueprint.ClueLadder.Single(clue => clue.Id == "clue.identity");
        draft.AssetStubs.Add(new AssetStub
        {
            Id = "asset.endpoint_record",
            ArchetypeId = "access_log",
            Type = "digital",
            Title = "Endpoint record",
            EvidenceRole = EvidenceRoles.Primary,
            LayoutHint = "AccessLog",
            SupportsClueIds = { identityClue.Id },
            ContainedObjectIds = { "document.endpoint_record" }
        });

        EvidenceGraphCompiler.Compile(draft);

        var fact = draft.CaseGraph.Facts.Single(value => value.Id == "fact.endpoint_owner");
        var observation = draft.CaseGraph.Observations.Single(value =>
            value.FactId == fact.Id && value.SourceAssetId == "asset.endpoint_record");
        Assert.Equal("device.laptop", fact.SubjectId);
        Assert.Equal("person.culprit", fact.ObjectId);
        Assert.Equal("source.endpoint_record", observation.EvidentiaryOriginId);
        Assert.Equal(ObservationFidelity.DirectRecord, observation.Fidelity);
        Assert.Equal(ObservationReliability.Verified, observation.Reliability);
        Assert.Single(draft.CaseGraph.Facts.Where(value =>
            value.Predicate.Equals("isCulprit", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GraphProjection_PreservesIndependentBibleProofOrigins()
    {
        var draft = new CaseDraft
        {
            CaseId = "case_bible_detective_graph",
            Request = new GenerateCaseV2Request
            {
                Difficulty = "Detective",
                RequiredRank = "Detective",
                Language = "en-US"
            },
            CaseBible = CaseBibleTestData.ValidDetectiveBible(),
            Metadata = new PlotMetadata { Title = "Detective graph" }
        };
        CaseBibleNormalizer.ProjectIntoDraft(draft);
        draft.AssetStubs.AddRange(
        [
            new AssetStub
            {
                Id = "asset.identity",
                ArchetypeId = "endpoint_record",
                Type = "digital",
                Title = "Endpoint record",
                EvidenceRole = EvidenceRoles.Primary,
                LayoutHint = "AccessLog",
                SupportsClueIds = { "clue.identity" },
                ForensicInputClueIds = { "clue.forensic_link" },
                ContainedObjectIds = { "document.endpoint_record" }
            },
            new AssetStub
            {
                Id = "asset.action",
                ArchetypeId = "session_log",
                Type = "digital",
                Title = "Session log",
                EvidenceRole = EvidenceRoles.Primary,
                LayoutHint = "AccessLog",
                SupportsClueIds = { "clue.action" },
                ContainedObjectIds = { "session.manifest_change" }
            }
        ]);
        draft.ForensicStubs.Add(new ForensicOutcomeStub
        {
            InputAssetId = "asset.identity",
            InputObjectId = "document.endpoint_record",
            AnalysisType = "MetadataAnalysis",
            Findings = true,
            MatchedSuspectId = draft.CulpritId,
            SupportsClueIds = { "clue.forensic_link" },
            ProducedProperties = { ForensicObservationProperty.DeviceCorrelation },
            LimitationKeys = { "operator_not_proven", "metadata_can_be_modified" }
        });
        draft.ForensicFull.Add(new ForensicsOutcome
        {
            InputAssetId = "asset.identity",
            AnalysisType = "MetadataAnalysis",
            Findings = true,
            MatchedSuspectId = draft.CulpritId,
            ResultAssetId = "asset.forensic_result"
        });
        draft.ResultAssets.Add(new EvidenceAsset
        {
            Id = "asset.forensic_result",
            Type = "document",
            Title = "Metadata result",
            Visibility = "hidden"
        });

        EvidenceGraphCompiler.Compile(draft);
        var reachability = ReachabilityClosureEngine.Calculate(draft.CaseGraph);
        var independent = IndependentProofPathGate.Validate(
            draft.CaseGraph,
            draft.CulpritId,
            "Detective",
            reachability.FinalObservationIds);

        Assert.True(independent.Paths.Count >= 2, string.Join(Environment.NewLine, independent.Failures.Select(failure => failure.Message)));
        Assert.All(independent.Paths, path =>
            Assert.Contains(path.EvidentiaryOriginIds, origin =>
                origin is "source.endpoint_record" or "source.session_log"));
    }

    [Fact]
    public void RefreshGeneratedGraph_SynchronizesSolutionRequirements()
    {
        var draft = CaseBibleTestData.ProjectedRookieDraft();
        draft.AssetStubs.Add(new AssetStub
        {
            Id = "asset.endpoint_record",
            ArchetypeId = "access_log",
            Type = "digital",
            Title = "Endpoint record",
            EvidenceRole = EvidenceRoles.Primary,
            LayoutHint = "AccessLog",
            SupportsClueIds = { "clue.identity" }
        });
        EvidenceGraphCompiler.Compile(draft);
        draft.CaseGraph.Origin = CaseGraphOrigin.Generated;

        draft.RequiredEvidenceIds = ["asset.endpoint_record"];
        CaseGraphProjection.RefreshGeneratedGraph(draft);

        Assert.Equal(["asset.endpoint_record"], draft.CaseGraph.RequiredSourceIds);
    }

    [Fact]
    public void GraphProjection_UsesMethodNeutralRuleForDeepDecoyResolution()
    {
        var draft = CaseBibleTestData.ProjectedRookieDraft();
        draft.Metadata.Difficulty = "Sergeant";

        EvidenceGraphCompiler.Compile(draft);

        var depthDerivations = draft.CaseGraph.Derivations
            .Where(derivation => derivation.Id.StartsWith("derivation.decoy_resolution_", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(depthDerivations);
        Assert.All(depthDerivations, derivation =>
            Assert.Equal(DerivationRule.CrossSourceCorroboration, derivation.Rule));
    }

    [Fact]
    public void GraphProjection_AssignsUniqueIdsToRepeatedForensicRequests()
    {
        var draft = CaseBibleTestData.ProjectedRookieDraft();
        draft.AssetStubs.Add(new AssetStub
        {
            Id = "asset.initial_report",
            ArchetypeId = "initial_report",
            Type = "document",
            Title = "Initial report",
            LayoutHint = "PoliceReport",
            ContainedObjectIds = { "document.initial_report" }
        });
        draft.ForensicStubs.Add(new ForensicOutcomeStub
        {
            InputAssetId = "asset.initial_report",
            InputObjectId = "document.initial_report",
            AnalysisType = "DocumentExamination",
            Findings = true,
            ProducedProperties = { ForensicObservationProperty.DocumentAuthenticity },
            LimitationKeys = { "authenticity_not_authorship", "copy_limits_examination" }
        });
        foreach (var resultId in new[] { "asset.forensic_result_1", "asset.forensic_result_2" })
        {
            draft.ForensicFull.Add(new ForensicsOutcome
            {
                InputAssetId = "asset.initial_report",
                AnalysisType = "DocumentExamination",
                Findings = true,
                ResultAssetId = resultId
            });
            draft.ResultAssets.Add(new EvidenceAsset
            {
                Id = resultId,
                Type = "document",
                Title = resultId,
                Visibility = "hidden"
            });
        }

        EvidenceGraphCompiler.Compile(draft);

        Assert.Equal(2, draft.CaseGraph.Actions.Count(action =>
            action.AnalysisId == "asset.initial_report:DocumentExamination"));
        Assert.Equal(
            draft.CaseGraph.Actions.Count,
            draft.CaseGraph.Actions.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            draft.CaseGraph.ForensicTransforms.Count,
            draft.CaseGraph.ForensicTransforms.Select(transform => transform.Id).Distinct(StringComparer.Ordinal).Count());
    }

    private sealed class StaticStructuredProvider(string content) : ILLMProvider
    {
        public Task<LLMResponse> GenerateTextAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(
            string systemPrompt,
            string userPrompt,
            string jsonSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LLMResponse { Content = content });

        public Task<byte[]> GenerateImageAsync(
            string prompt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MalformedThenValidProvider : ILLMProvider
    {
        public List<string> UserPrompts { get; } = new();

        public Task<LLMResponse> GenerateTextAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(
            string systemPrompt,
            string userPrompt,
            string jsonSchema,
            CancellationToken cancellationToken = default)
        {
            UserPrompts.Add(userPrompt);
            if (UserPrompts.Count == 1)
                JsonDocument.Parse("{");
            return Task.FromResult(new LLMResponse { Content = """{"version":"1.0"}""" });
        }

        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

public class AgentPromptCatalogTests
{
    private static readonly string[] Agents =
    [
        "CaseBible", "PlotOutline", "SuspectCard", "AssetPlan", "AssetCard",
        "ForensicsPlan", "ForensicOutcome", "BriefingEmail", "InitialEmails", "Rules",
        "SolutionSkeleton", "Question", "Explanation", "RedTeam", "RefineCase", "SpecialistReview",
        "ImageRender", "ImageRenderPortrait", "ImageRenderScene", "ImageRenderSurveillance",
        "ImageRenderObject"
    ];

    [Fact]
    public void DefaultCatalog_DiscoversEveryCaseV2PromptPair()
    {
        var catalog = new AgentPromptCatalog(AgentPromptCatalog.ResolveDefaultRoot());

        foreach (var agent in Agents)
        {
            foreach (var role in Enum.GetValues<AgentPromptRole>())
            {
                var path = catalog.GetTemplatePath(agent, role);
                Assert.True(File.Exists(path), $"{agent}.{role}");
                var template = File.ReadAllText(path);
                var variables = Regex.Matches(template, "\\{\\{(?<name>[A-Za-z][A-Za-z0-9_.-]*)\\}\\}")
                    .Select(match => match.Groups["name"].Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToDictionary(name => name, _ => (object?)"value", StringComparer.Ordinal);
                var rendered = catalog.Render(agent, role, variables);
                Assert.DoesNotContain("{{", rendered, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Renderer_ReplacesExactPlaceholdersWithoutRecursiveCollisions()
    {
        var root = CreateCatalogDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "Demo.system.md"), "{{a}}|{{ab}}");
            File.WriteAllText(Path.Combine(root, "Demo.user.md"), "{{value}}");
            var catalog = new AgentPromptCatalog(root);

            var rendered = catalog.RenderSystem("Demo", new Dictionary<string, object?>
            {
                ["a"] = "{{ab}}",
                ["ab"] = "second"
            });

            Assert.Equal("{{ab}}|second", rendered);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Renderer_FailsLoudlyForMissingTemplateOrVariable()
    {
        var root = CreateCatalogDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "Demo.system.md"), "Hello {{name}}");
            File.WriteAllText(Path.Combine(root, "Demo.user.md"), "Body");
            var catalog = new AgentPromptCatalog(root);

            Assert.Throws<InvalidOperationException>(() =>
                catalog.RenderSystem("Demo", new Dictionary<string, object?>()));
            Assert.Throws<FileNotFoundException>(() =>
                catalog.RenderSystem("Missing", new Dictionary<string, object?>()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CaseV2Sources_DoNotContainInlineAgentPromptDeclarations()
    {
        var sourceRoot = Path.Combine(
            RepositoryRoot(),
            "functions",
            "CaseGen.Functions",
            "Services",
            "CaseV2");
        var inlinePrompt = new Regex(
            "var\\s+(system|user)\\s*=\\s*\\$?@?\\\"(?:You are|CASE |CONTEXT:)|system\\s*\\+=\\s*@?\\\"",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.False(inlinePrompt.IsMatch(source), $"Inline agent prompt found in {file}");
            Assert.DoesNotContain("\"You are", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string CreateCatalogDirectory()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "prompt-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if ((Directory.Exists(Path.Combine(current.FullName, ".git"))
                 || File.Exists(Path.Combine(current.FullName, ".git")))
                && Directory.Exists(Path.Combine(current.FullName, "functions")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}

internal static class CaseBibleTestData
{
    public static CaseBible ValidDetectiveBible()
    {
        var bible = JsonSerializer.Deserialize<CaseBible>(
                        JsonSerializer.Serialize(ValidRookieBible()))
                    ?? throw new InvalidOperationException("Could not clone test Bible.");
        bible.DifficultyIntent.Difficulty = "Detective";
        bible.DifficultyIntent.Clues.Single(clue => clue.Id == "clue.action").Strength = "supporting";
        bible.Sources.Add(new CaseBibleSource
        {
            Id = "source.forensic_result",
            Kind = CaseBibleSourceKind.ForensicResult,
            Name = "Metadata correlation result",
            Description = "Controlled metadata comparison result"
        });
        bible.Facts.Add(new CaseBibleFact
        {
            Id = "fact.forensic_link",
            Predicate = "originatedFrom",
            SubjectId = "account.ops",
            ObjectId = "device.laptop",
            Visibility = FactVisibility.Public
        });
        bible.Observations.Add(new CaseBibleObservation
        {
            Id = "observation.forensic_link",
            FactId = "fact.forensic_link",
            SourceId = "source.forensic_result",
            Statement = "Metadata analysis maps session-B to workstation WS-14.",
            ObservedValue = "session-B->WS-14",
            Fidelity = ObservationFidelity.DirectRecord,
            Reliability = ObservationReliability.Verified,
            Visibility = FactVisibility.Public
        });
        bible.DifficultyIntent.Clues.Add(new CaseBibleClueIntent
        {
            Id = "clue.forensic_link",
            ObservationIds = { "observation.forensic_link" },
            SupportsPersonId = "person.culprit",
            Inference = "The action session originated from Alex's assigned workstation.",
            Strength = "decisive",
            SourceType = "forensic",
            Role = "forensicAttribution"
        });
        bible.DifficultyIntent.ForensicOpportunities.Add(new CaseBibleForensicOpportunity
        {
            Id = "forensic.metadata_link",
            InputEntityId = "device.laptop",
            InputSourceId = "source.endpoint_record",
            ResultObservationId = "observation.forensic_link",
            MethodHint = "MetadataAnalysis",
            Purpose = "Map opaque session-B to endpoint WS-14."
        });
        bible.InvestigationConstraints.AllowedForensicMethodIds.Add("MetadataAnalysis");
        return bible;
    }

    public static CaseDraft ProjectedRookieDraft()
    {
        var draft = new CaseDraft
        {
            CaseId = "case_bible_graph",
            Request = new GenerateCaseV2Request
            {
                Difficulty = "Rookie",
                RequiredRank = "Rookie",
                Language = "en-US"
            },
            CaseBible = ValidRookieBible(),
            Metadata = new PlotMetadata { Title = "Canonical graph test" }
        };
        CaseBibleNormalizer.ProjectIntoDraft(draft);
        return draft;
    }

    public static CaseBible ValidRookieBible() => new()
    {
        World = new CaseBibleWorld
        {
            Language = "en-US",
            LocationDisplayName = "Harbor City, New York, United States",
            City = "Harbor City",
            Region = "New York",
            Country = "United States",
            Jurisdiction = "Harbor County",
            TimeZoneId = "America/New_York",
            UtcOffset = "-04:00",
            PoliceAgencyId = "organization.harbor_police",
            InvestigatorPersonId = "person.investigator"
        },
        Institutions =
        {
            new CaseBibleInstitution
            {
                Id = "organization.harbor_police",
                Name = "Harbor City Police Department",
                Kind = "police",
                LocationId = "location.station",
                EmailDomain = "casezero.local"
            },
            new CaseBibleInstitution
            {
                Id = "organization.harbor_market",
                Name = "Harbor Market Cooperative",
                Kind = "business",
                LocationId = "location.market",
                EmailDomain = "harbormarket.local"
            }
        },
        Locations =
        {
            new CaseBibleLocation
            {
                Id = "location.market",
                Name = "Harbor Market office",
                Kind = "office",
                InstitutionId = "organization.harbor_market",
                Address = new CaseBibleAddress
                {
                    Line1 = "18 Pier Street",
                    City = "Harbor City",
                    Region = "New York",
                    PostalCode = "10018",
                    Country = "United States"
                }
            },
            new CaseBibleLocation
            {
                Id = "location.station",
                Name = "Harbor City Police headquarters",
                Kind = "police station",
                InstitutionId = "organization.harbor_police",
                Address = new CaseBibleAddress
                {
                    Line1 = "400 Civic Plaza",
                    City = "Harbor City",
                    Region = "New York",
                    PostalCode = "10019",
                    Country = "United States"
                }
            }
        },
        TravelRelationships =
        {
            new CaseBibleTravelRelationship
            {
                Id = "travel.station_market",
                FromLocationId = "location.station",
                ToLocationId = "location.market",
                Mode = "drive",
                MinimumMinutes = 8,
                TypicalMinutes = 14
            }
        },
        People =
        {
            new CaseBiblePerson
            {
                Id = "person.culprit",
                SuspectId = "suspect.alex",
                Name = "Alex Mercer",
                Age = 37,
                Roles = { CaseBiblePersonRole.Suspect },
                Occupation = "operations supervisor",
                PublicRole = "Operations supervisor with manifest access",
                RelationshipToVictim = "supervised the victim's inventory work",
                Background = "Alex managed the evening dispatch process.",
                PrivateMotive = "Alex needed to hide an unauthorized transfer before the morning audit.",
                StatedAlibi = "Alex said the shift ended at 7:30 PM and the office was left immediately.",
                AlibiVerified = false,
                Email = "alex.mercer@harbormarket.local"
            },
            new CaseBiblePerson
            {
                Id = "person.decoy_one",
                SuspectId = "suspect.blair",
                Name = "Blair Chen",
                Age = 31,
                Roles = { CaseBiblePersonRole.Suspect },
                Occupation = "vendor liaison",
                PublicRole = "Vendor liaison who disputed a recent invoice",
                RelationshipToVictim = "worked with the victim on supplier disputes",
                Background = "Blair handled vendor escalations.",
                PrivateMotive = "Blair feared being blamed for the disputed invoice.",
                StatedAlibi = "Blair said a client call continued through the incident window.",
                AlibiVerified = true
            },
            new CaseBiblePerson
            {
                Id = "person.decoy_two",
                SuspectId = "suspect.casey",
                Name = "Casey Ortiz",
                Age = 42,
                Roles = { CaseBiblePersonRole.Suspect },
                Occupation = "security contractor",
                PublicRole = "Contractor with after-hours building access",
                RelationshipToVictim = "maintained access controls used by the victim",
                Background = "Casey serviced the market's access-control system.",
                PrivateMotive = "Casey wanted to avoid scrutiny over a missed maintenance visit.",
                StatedAlibi = "Casey said a service van was at another client site.",
                AlibiVerified = true
            },
            new CaseBiblePerson
            {
                Id = "person.investigator",
                Name = "Jordan Hale",
                Age = 40,
                Roles = { CaseBiblePersonRole.Investigator },
                Occupation = "detective",
                PublicRole = "Lead investigator",
                RelationshipToVictim = "none",
                Background = "Jordan Hale leads the assigned investigation.",
                Email = "jordan.hale@casezero.local"
            },
            new CaseBiblePerson
            {
                Id = "person.victim",
                Name = "Morgan Reed",
                Age = 45,
                Roles = { CaseBiblePersonRole.Victim },
                Occupation = "inventory auditor",
                PublicRole = "Inventory auditor",
                RelationshipToVictim = "self",
                Background = "Morgan was preparing the cooperative's morning audit."
            }
        },
        Relationships =
        {
            new CaseBibleRelationship
            {
                Id = "relationship.culprit_victim",
                FromPersonId = "person.culprit",
                ToPersonId = "person.victim",
                Type = "supervisor",
                Description = "Alex supervised Morgan's inventory work."
            },
            new CaseBibleRelationship
            {
                Id = "relationship.blair_victim",
                FromPersonId = "person.decoy_one",
                ToPersonId = "person.victim",
                Type = "coworker",
                Description = "Blair worked with Morgan on supplier disputes."
            },
            new CaseBibleRelationship
            {
                Id = "relationship.casey_victim",
                FromPersonId = "person.decoy_two",
                ToPersonId = "person.victim",
                Type = "contractor",
                Description = "Casey maintained systems used by Morgan."
            }
        },
        Employments =
        {
            new CaseBibleEmployment
            {
                Id = "employment.alex_market",
                PersonId = "person.culprit",
                InstitutionId = "organization.harbor_market",
                LocationId = "location.market",
                JobTitle = "operations supervisor",
                StartDate = "2022-03-01"
            },
            new CaseBibleEmployment
            {
                Id = "employment.morgan_market",
                PersonId = "person.victim",
                InstitutionId = "organization.harbor_market",
                LocationId = "location.market",
                JobTitle = "inventory auditor",
                StartDate = "2021-06-15"
            }
        },
        WorkSchedules =
        {
            new CaseBibleWorkSchedule
            {
                Id = "schedule.alex_shift",
                PersonId = "person.culprit",
                EmploymentId = "employment.alex_market",
                LocationId = "location.market",
                Start = "2026-07-21T11:30:00-04:00",
                End = "2026-07-21T19:30:00-04:00",
                Label = "Evening operations shift"
            },
            new CaseBibleWorkSchedule
            {
                Id = "schedule.morgan_shift",
                PersonId = "person.victim",
                EmploymentId = "employment.morgan_market",
                LocationId = "location.market",
                Start = "2026-07-21T12:00:00-04:00",
                End = "2026-07-21T20:00:00-04:00",
                Label = "Audit preparation shift"
            }
        },
        Devices =
        {
            new CaseBibleDevice
            {
                Id = "device.laptop",
                Type = "workstation",
                Manufacturer = "Northstar",
                Model = "N14",
                Identifier = "WS-14",
                AssignedPersonId = "person.culprit",
                UsualLocationId = "location.market",
                AccountIds = { "account.ops" }
            }
        },
        Accounts =
        {
            new CaseBibleAccount
            {
                Id = "account.ops",
                Provider = "Harbor Market dispatch",
                Handle = "amercer.ops",
                Identifier = "acct-4471",
                OwnerPersonId = "person.culprit",
                DeviceIds = { "device.laptop" }
            }
        },
        Incident = new CaseBibleIncident
        {
            CrimeType = "inventory record tampering",
            CulpritPersonId = "person.culprit",
            VictimPersonId = "person.victim",
            PrimaryLocationId = "location.market",
            OccurredAt = "2026-07-21T19:52:00-04:00",
            DiscoveredAt = "2026-07-21T20:18:00-04:00",
            ReportedAt = "2026-07-21T20:26:00-04:00",
            OpenedAt = "2026-07-21T21:00:00-04:00",
            CrimeMechanism = "A valid operations account altered the outbound manifest after the victim locked the audit worksheet.",
            CulpritObjective = "Hide an unauthorized inventory transfer before the morning audit.",
            Concealment = "The change was placed inside an ordinary end-of-shift correction batch.",
            CaseHook = "A routine endpoint assignment record and a separate session log expose the mismatch.",
            PrivateSummary = "Alex used account acct-4471 from workstation WS-14 to alter the manifest at 7:52 PM."
        },
        TruthTimeline =
        {
            new CaseBibleTruthBeat
            {
                Id = "event.preparation",
                Kind = EventKind.Modified,
                Time = "2026-07-21T19:35:00-04:00",
                LocationId = "location.market",
                ParticipantIds = { "person.victim" },
                FactIds = { "fact.last_seen" },
                Event = "Morgan locked the audit worksheet before final reconciliation.",
                PublicDescription = "The audit worksheet was locked at 7:35 PM.",
                Visibility = FactVisibility.Public,
                Source = "sensor",
                Verified = true,
                Importance = "medium",
                CausalRole = "preparation"
            },
            new CaseBibleTruthBeat
            {
                Id = "event.incident",
                Kind = EventKind.Modified,
                Time = "2026-07-21T19:52:00-04:00",
                LocationId = "location.market",
                ParticipantIds = { "person.culprit" },
                DeviceIds = { "device.laptop" },
                AccountIds = { "account.ops" },
                FactIds = { "fact.action_session" },
                Event = "Alex used session B under account acct-4471 to alter the outbound manifest.",
                Visibility = FactVisibility.Private,
                Source = "investigation",
                Verified = true,
                Importance = "critical",
                CausalRole = "crime"
            },
            new CaseBibleTruthBeat
            {
                Id = "event.discovery",
                Kind = EventKind.Discovered,
                Time = "2026-07-21T20:18:00-04:00",
                LocationId = "location.market",
                ParticipantIds = { "person.victim" },
                Event = "Morgan discovered the manifest discrepancy.",
                PublicDescription = "The manifest discrepancy was discovered at 8:18 PM.",
                Visibility = FactVisibility.Public,
                Source = "witness",
                Verified = true,
                Importance = "high",
                CausalRole = "discovery"
            },
            new CaseBibleTruthBeat
            {
                Id = "event.opened",
                Kind = EventKind.Reported,
                Time = "2026-07-21T21:00:00-04:00",
                LocationId = "location.station",
                ParticipantIds = { "person.investigator" },
                Event = "Jordan Hale formally opened the investigation.",
                PublicDescription = "The investigation was formally opened at 9:00 PM.",
                Visibility = FactVisibility.Public,
                Source = "investigation",
                Verified = true,
                Importance = "medium",
                CausalRole = "case_opening"
            }
        },
        Sources =
        {
            Source("source.endpoint_record", CaseBibleSourceKind.Document, "Endpoint assignment record"),
            Source("source.session_log", CaseBibleSourceKind.DigitalRecord, "Dispatch session log"),
            Source("source.audit_lock", CaseBibleSourceKind.DigitalRecord, "Audit lock record"),
            Source("source.blair_message", CaseBibleSourceKind.Testimony, "Blair invoice message"),
            Source("source.blair_call", CaseBibleSourceKind.InstitutionalRecord, "Client call record"),
            Source("source.casey_badge", CaseBibleSourceKind.DigitalRecord, "Casey badge record"),
            Source("source.casey_gps", CaseBibleSourceKind.DigitalRecord, "Service van GPS record")
        },
        Facts =
        {
            new CaseBibleFact
            {
                Id = "fact.endpoint_owner",
                Predicate = "assignedTo",
                SubjectId = "device.laptop",
                ObjectId = "person.culprit",
                Visibility = FactVisibility.Public
            },
            new CaseBibleFact
            {
                Id = "fact.action_session",
                Predicate = "performedManifestChange",
                SubjectId = "account.ops",
                LiteralValue = "session-B at 2026-07-21T19:52:00-04:00",
                LiteralType = LiteralValueType.String,
                Visibility = FactVisibility.Public
            },
            new CaseBibleFact
            {
                Id = "fact.last_seen",
                Predicate = "lastSeenAt",
                SubjectId = "person.victim",
                LiteralValue = "2026-07-21T19:35:00-04:00",
                LiteralType = LiteralValueType.DateTime,
                Visibility = FactVisibility.Public
            },
            TextFact("fact.blair_suspicion", "invoiceDispute", "person.decoy_one", "Blair disputed invoice HM-883."),
            TextFact("fact.blair_verified", "clientCall", "person.decoy_one", "The client call ran from 7:40 PM to 8:05 PM."),
            TextFact("fact.casey_suspicion", "badgeAccess", "person.decoy_two", "Casey's badge was used at 7:20 PM."),
            TextFact("fact.casey_verified", "vehicleLocation", "person.decoy_two", "The service van was 18 miles away at 7:52 PM."),
            new CaseBibleFact
            {
                Id = "fact.culprit_conclusion",
                Predicate = "isCulprit",
                SubjectId = "person.culprit",
                LiteralValue = "true",
                LiteralType = LiteralValueType.Boolean,
                Visibility = FactVisibility.Private
            }
        },
        Observations =
        {
            Observation("observation.endpoint_owner", "fact.endpoint_owner", "source.endpoint_record", "Endpoint assignment record ER-14 lists workstation WS-14 as assigned to Alex Mercer.", "person.culprit", ObservationFidelity.DirectRecord, ObservationReliability.Verified),
            Observation("observation.action_session", "fact.action_session", "source.session_log", "Dispatch session log SL-52 records session-B changing the manifest at 7:52 PM.", "session-B", ObservationFidelity.DirectRecord, ObservationReliability.Corroborated),
            Observation("observation.last_seen", "fact.last_seen", "source.audit_lock", "Audit lock record AL-9 shows the worksheet was locked at 7:35 PM.", "2026-07-21T19:35:00-04:00", ObservationFidelity.DirectRecord, ObservationReliability.Verified),
            Observation("observation.blair_suspicion", "fact.blair_suspicion", "source.blair_message", "A message from Blair disputes invoice HM-883.", "HM-883", ObservationFidelity.DirectRecord, ObservationReliability.Unverified),
            Observation("observation.blair_verified", "fact.blair_verified", "source.blair_call", "Client call record CR-22 runs from 7:40 PM to 8:05 PM with Blair connected.", "19:40-20:05", ObservationFidelity.DirectRecord, ObservationReliability.Verified),
            Observation("observation.casey_suspicion", "fact.casey_suspicion", "source.casey_badge", "Badge log BL-11 records Casey's credential at the market at 7:20 PM.", "2026-07-21T19:20:00-04:00", ObservationFidelity.DirectRecord, ObservationReliability.Unverified),
            Observation("observation.casey_verified", "fact.casey_verified", "source.casey_gps", "Van GPS record VG-4 places Casey's service van 18 miles away at 7:52 PM.", "18 miles away", ObservationFidelity.DirectRecord, ObservationReliability.Verified)
        },
        DifficultyIntent = new CaseBibleDifficultyIntent
        {
            Difficulty = "Rookie",
            Clues =
            {
                new CaseBibleClueIntent
                {
                    Id = "clue.identity",
                    ObservationIds = { "observation.endpoint_owner" },
                    SupportsPersonId = "person.culprit",
                    Inference = "Alex controlled workstation WS-14.",
                    Strength = "supporting",
                    SourceType = "document",
                    Role = "identity"
                },
                new CaseBibleClueIntent
                {
                    Id = "clue.action",
                    ObservationIds = { "observation.action_session" },
                    SupportsPersonId = "person.culprit",
                    Inference = "The manifest was changed through the operations session during the incident window.",
                    Strength = "decisive",
                    SourceType = "digital",
                    Role = "action"
                },
                new CaseBibleClueIntent
                {
                    Id = "clue.timeline",
                    ObservationIds = { "observation.last_seen" },
                    Inference = "The change occurred after the audit worksheet was locked.",
                    Strength = "context",
                    SourceType = "digital",
                    Role = "context"
                },
                new CaseBibleClueIntent
                {
                    Id = "clue.decoy_context",
                    ObservationIds = { "observation.blair_suspicion" },
                    SupportsPersonId = "person.decoy_one",
                    Inference = "Blair had a recent dispute worth checking.",
                    Strength = "context",
                    SourceType = "document",
                    Role = "context"
                }
            },
            ProofPaths =
            {
                new CaseBibleProofPath
                {
                    Id = "proof.endpoint",
                    SupportsPersonId = "person.culprit",
                    ClueIds = { "clue.identity" },
                    ConclusionFactId = "fact.culprit_conclusion",
                    IndependenceKey = "endpoint-assignment",
                    Description = "The endpoint assignment independently establishes control."
                },
                new CaseBibleProofPath
                {
                    Id = "proof.session",
                    SupportsPersonId = "person.culprit",
                    ClueIds = { "clue.action" },
                    ConclusionFactId = "fact.culprit_conclusion",
                    IndependenceKey = "manifest-session",
                    Description = "The session log independently establishes the action."
                }
            },
            DecoyArcs =
            {
                new CaseBibleDecoyArc
                {
                    Id = "decoy.blair",
                    SuspectPersonId = "person.decoy_one",
                    Suspicion = "Blair's invoice dispute creates a plausible reason to interfere with the audit.",
                    SuspicionObservationIds = { "observation.blair_suspicion" },
                    VerificationObservationIds = { "observation.blair_verified" },
                    Resolution = "The continuous client call covers the critical incident window."
                },
                new CaseBibleDecoyArc
                {
                    Id = "decoy.casey",
                    SuspectPersonId = "person.decoy_two",
                    Suspicion = "Casey's credential appeared in the building log shortly before the incident.",
                    SuspicionObservationIds = { "observation.casey_suspicion" },
                    VerificationObservationIds = { "observation.casey_verified" },
                    Resolution = "The service van location makes a return by 7:52 PM implausible."
                }
            }
        },
        InvestigationConstraints = new CaseBibleInvestigationConstraints
        {
            OpeningObservationIds =
            {
                "observation.endpoint_owner",
                "observation.action_session",
                "observation.last_seen"
            },
            RequiredProcedureNotes = { "Preserve the original dispatch export and endpoint assignment record." },
            ProhibitedAssumptions = { "Do not infer guilt from motive or job access alone." },
            MustPreserveFactIds = { "fact.endpoint_owner", "fact.action_session" }
        }
    };

    private static CaseBibleSource Source(string id, CaseBibleSourceKind kind, string name) => new()
    {
        Id = id,
        Kind = kind,
        Name = name,
        Description = name
    };

    private static CaseBibleFact TextFact(string id, string predicate, string subjectId, string value) => new()
    {
        Id = id,
        Predicate = predicate,
        SubjectId = subjectId,
        LiteralValue = value,
        LiteralType = LiteralValueType.String,
        Visibility = FactVisibility.Public
    };

    private static CaseBibleObservation Observation(
        string id,
        string factId,
        string sourceId,
        string statement,
        string observedValue,
        ObservationFidelity fidelity,
        ObservationReliability reliability) => new()
    {
        Id = id,
        FactId = factId,
        SourceId = sourceId,
        Statement = statement,
        ObservedValue = observedValue,
        Fidelity = fidelity,
        Reliability = reliability,
        Visibility = FactVisibility.Public
    };
}
