using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseBibleTests(CanonicalDraftFixture baseline, ITestOutputHelper output) : IClassFixture<CanonicalDraftFixture>
{
    [Fact]
    public void ProductionHasExactlyOneRichCanonicalBible()
    {
        var canonical = Assert.Single(typeof(CaseBible).Assembly.GetTypes().Where(t => t.Name == "CaseBible"));
        Assert.Equal(typeof(CaseBible), canonical);
        Assert.Null(typeof(CaseDraft).GetProperty("Bible"));
    }

    [Fact]
    public void CanonicalPlanningPrecedesAllSourceScopedAuthors()
    {
        var calls = baseline.Provider.Calls.ToList();
        var planning = calls.FindIndex(c => c.System.Contains("**canonical CaseBible**", StringComparison.Ordinal));
        Assert.Equal(0, planning);
        foreach (var call in calls.Where(IsAuthor))
        {
            Assert.True(calls.IndexOf(call) > planning);
            foreach (var privateField in new[] { "culpritId", "isCulprit", "truthStatus", "privateSummary", "backgroundPrivate", "conclusionFactId", "correctOptionId" })
                Assert.DoesNotContain($"\"{privateField}\"", call.User);
            Assert.DoesNotContain(baseline.Draft.CaseBible!.Incident.PrivateSummary, call.User);
        }
        Assert.Contains(calls, c => c.System.Contains("**forensics layer**", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.System.Contains("**skeleton**", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceContextContainsOnlyAssignedRawObservationsAndPublicIdentities()
    {
        var context = JsonNode.Parse(CaseBibleAuthorship.Context(baseline.Draft, "suspect.jordan"))!;
        Assert.Null(context["facts"]);
        Assert.Null(context["solution"]);
        Assert.Null(context["metadata"]!["incidentDate"]);
        Assert.DoesNotContain("person.culprit", context.ToJsonString());
        var observation = Assert.Single(context["observations"]!.AsArray());
        Assert.Equal("observation.stated_alibi_jordan", observation!["id"]!.GetValue<string>());
        Assert.StartsWith("jordan:", observation["statement"]!.GetValue<string>());
        Assert.Null(observation["reliability"]);
        Assert.Null(observation["factId"]);
        var record = CaseBibleAuthorship.Context(baseline.Draft, "asset.initial_report");
        Assert.Contains("observation.endpoint_owner", record);
        Assert.DoesNotContain("observation.forensic_link", record);
        Assert.DoesNotContain("observation.action_session", record);
        Assert.DoesNotContain("source.forensic_result", record);
    }

    [Fact]
    public void DisputedObservationIsNotReplacedWithItsCanonicalValue()
    {
        var draft = baseline.Draft.Copy();
        var observation = draft.CaseBible!.Observations.Single(o => o.Id == "observation.endpoint_owner");
        observation.ObservedValue = "an uncertain workstation";
        observation.Statement = "The custodian tentatively recalls a different workstation; the recollection is disputed.";
        observation.Reliability = ObservationReliability.Disputed;
        var context = CaseBibleAuthorship.Context(draft, "asset.initial_report");
        Assert.Contains(observation.Statement, context);
        Assert.Contains(observation.ObservedValue, context);
        Assert.DoesNotContain("\"canonicalValue\"", context);
        Assert.DoesNotContain("\"truthStatus\"", context);
    }

    [Fact]
    public async Task PlotAuthorDoesNotReceiveThePrivatePlanningTheme()
    {
        var draft = baseline.Draft.Copy();
        draft.Request.Theme = "PRIVATE_ROOT_PLANNING_HINT_NOT_A_SOURCE_OBSERVATION";
        var provider = new TestCaseLlmProvider();
        await new PlotOutlineTask(provider, NullLogger.Instance).RunAsync(draft, default);
        Assert.DoesNotContain(draft.Request.Theme, Assert.Single(provider.Calls).User);
    }

    [Fact]
    public void SourceCannotKnowAnObservationRecordedAfterItsIssueTime()
    {
        var draft = baseline.Draft.Copy();
        var observation = draft.CaseBible!.Observations.Single(o => o.Id == "observation.endpoint_owner");
        var source = draft.CaseBible.Sources.Single(s => s.Id == observation.SourceId);
        observation.ObservedAt = DateTimeOffset.Parse(source.RecordedAt!).AddMinutes(1).ToString("o");
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.Context(draft, "asset.initial_report"));
        Assert.Contains(CaseBibleValidator.Validate(draft.CaseBible, "Detective", "en-US", true).Issues,
            issue => issue.Code == "observation_after_source");
    }

    [Fact]
    public void BriefingCannotReceiveAFutureLabFindingEvenIfItIsPublic()
    {
        var draft = baseline.Draft.Copy();
        draft.CaseBible!.InvestigationConstraints.OpeningObservationIds.Add("observation.forensic_link");
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.Context(draft, "email.briefing"));
        Assert.Contains(CaseBibleValidator.Validate(draft.CaseBible, "Detective", "en-US", true).Issues,
            issue => issue.Code == "opening_observation_unavailable");
    }

    [Fact]
    public void InitialDocumentCannotQuoteARecordCreatedAfterCaseOpening()
    {
        var draft = baseline.Draft.Copy();
        draft.CaseBible!.Sources.Single(s => s.Id == "source.endpoint_record").RecordedAt =
            DateTimeOffset.Parse(draft.Metadata.OpenedAt).AddHours(1).ToString("o");
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.Context(draft, "asset.initial_report"));
        Assert.Contains(CaseBibleValidator.Validate(draft.CaseBible, "Detective", "en-US", true).Issues,
            issue => issue.Code == "clue_source_unavailable_at_opening");
    }

    [Fact]
    public void DescriptionAndMarkdownCannotStandInForReadableDocumentSupport()
    {
        var draft = baseline.Draft.Copy();
        var asset = draft.AssetFull.Single(a => a.Id == "asset.initial_report");
        asset.Description = CaseBibleAuthorship.Text(asset);
        asset.Body = asset.Description;
        asset.BodyDoc!.Sections.Clear();
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.Asset(draft, asset));
    }

    [Fact]
    public void ExactSupportMustAppearInTheBodyAndRetainTheFullAttributedObservation()
    {
        var draft = baseline.Draft.Copy();
        var asset = draft.AssetFull.Single(a => a.Id == "asset.initial_report");
        asset.Support[0].Excerpt = "An unsupported invented identity assertion.";
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.Asset(draft, asset));

        var suspect = draft.SuspectFull.Single(s => s.Id == "suspect.jordan");
        suspect.Support[0].Excerpt = suspect.Alibi!;
        suspect.Background = suspect.Alibi;
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.Suspect(draft, suspect));
    }

    [Fact]
    public void PhotoPromptsCannotBeRequiredReadableProof()
    {
        var draft = baseline.Draft.Copy();
        var asset = draft.AssetFull.Single(a => a.Id == "asset.initial_report");
        var support = asset.Support;
        asset.Type = "photo";
        asset.Body = CaseBibleAuthorship.Text(asset);
        asset.BodyDoc = null;
        Assert.DoesNotContain(CaseBibleAuthorship.ReadableMaterials(draft), m => m.Id == asset.Id);
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.SupportFromMaterials(draft, support, [asset.Id], "question"));
    }

    [Fact]
    public void QuestionAnswerAndExplanationMustUseActualReachableReadableExcerpts()
    {
        var draft = baseline.Draft.Copy();
        CaseBibleValidator.Authored(draft);
        var question = draft.Questions[0];
        question.Options.Single(o => o.Id == question.CorrectOptionId).Label = "An answer absent from every source.";
        Assert.Throws<TaskContractException>(() => CaseBibleValidator.Authored(draft));
        draft = baseline.Draft.Copy();
        draft.ExplanationSupport[0].Excerpt = "An invented source quotation.";
        Assert.Throws<TaskContractException>(() => CaseBibleValidator.Authored(draft));
    }

    [Fact]
    public void AnObservationIdCannotLaunderUnassignedProseIntoAnswerSupport()
    {
        var draft = baseline.Draft.Copy();
        var asset = draft.AssetFull.Single(a => a.Id == "asset.initial_report");
        const string inventedClaim = "An invented claim not established by any assigned source observation.";
        asset.BodyDoc!.Sections.Add(new EvidenceSection { Kind = "narrative", Text = inventedClaim });
        var support = new CaseContentSupport { ReferenceId = asset.Support[0].ReferenceId, Excerpt = inventedClaim };
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.SupportFromMaterials(draft, [support], [asset.Id], "question"));
    }

    [Fact]
    public void DependentCitationsCannotStripAttributionFromAnAlibiClaim()
    {
        var draft = baseline.Draft.Copy();
        var alibi = draft.SuspectFull.Single(s => s.Id == "suspect.jordan").Alibi!;
        var support = new CaseContentSupport { ReferenceId = "observation.stated_alibi_jordan", Excerpt = alibi };
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.SupportFromMaterials(
            draft, [support], ["asset.interview_jordan"], "explanation"));
    }

    [Fact]
    public void CollectiveForensicProofDoesNotNeedAnIndividualSuspectMatch()
    {
        var draft = baseline.Draft.Copy();
        Assert.NotEmpty(draft.ForensicStubs);
        Assert.All(draft.ForensicStubs, outcome => Assert.Null(outcome.MatchedSuspectId));
        Assert.All(draft.ForensicFull, outcome => Assert.Null(outcome.MatchedSuspectId));
        ForensicsPlanTask.ValidatePlan(draft, draft.AnalysisTypes, draft.ForensicStubs);
        CaseBibleValidator.Authored(draft);
    }

    [Theory]
    [InlineData("unknown_input", "Forensic transform 'asset.unknown:")]
    [InlineData("unknown_method", ":UnknownMethod' must match exactly one frozen forensic plan")]
    [InlineData("missing_plan", "must match exactly one frozen forensic plan")]
    [InlineData("duplicate_plan", "forensic plans contains duplicate ID")]
    [InlineData("duplicate_outcome", "authored forensic transforms contains duplicate ID")]
    [InlineData("missing_input", "Forensic input material '")]
    [InlineData("missing_report", "Forensic report '")]
    [InlineData("duplicate_report", "Forensic report '")]
    [InlineData("missing_email", "Forensic delivery email '")]
    [InlineData("duplicate_email", "Forensic delivery email '")]
    [InlineData("changed_findings", "cannot change planned findings or attribution")]
    [InlineData("changed_attribution", "cannot change planned findings or attribution")]
    public void InvalidAuthoredForensicReferencesProduceActionableContractErrors(string defect, string expected)
    {
        var draft = baseline.Draft.Copy();
        var outcome = draft.ForensicFull[0];
        switch (defect)
        {
            case "unknown_input": outcome.InputAssetId = "asset.unknown"; break;
            case "unknown_method": outcome.AnalysisType = "UnknownMethod"; break;
            case "missing_plan": draft.ForensicStubs.Clear(); break;
            case "duplicate_plan": draft.ForensicStubs.Add(draft.ForensicStubs[0]); break;
            case "duplicate_outcome": draft.ForensicFull.Add(outcome); break;
            case "missing_input": draft.AssetFull.RemoveAll(a => a.Id == outcome.InputAssetId); break;
            case "missing_report": draft.ResultAssets.RemoveAll(a => a.Id == outcome.ResultAssetId); break;
            case "duplicate_report": draft.ResultAssets.Add(draft.ResultAssets.Single(a => a.Id == outcome.ResultAssetId)); break;
            case "missing_email": draft.ResultEmails.RemoveAll(e => e.Id == outcome.ResultEmailId); break;
            case "duplicate_email": draft.ResultEmails.Add(draft.ResultEmails.Single(e => e.Id == outcome.ResultEmailId)); break;
            case "changed_findings": outcome.Findings = !outcome.Findings; break;
            case "changed_attribution": outcome.MatchedSuspectId = "suspect.riley"; break;
        }
        var before = JsonSerializer.Serialize(draft, CaseDraft.CanonicalJsonOptions);

        var error = Assert.Throws<TaskContractException>(() => CaseBibleValidator.Authored(draft));

        Assert.Contains(expected, error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(draft, CaseDraft.CanonicalJsonOptions));
    }

    [Theory]
    [InlineData("missing_clue", "Proof clue 'clue.unknown'")]
    [InlineData("duplicate_clue", "must identify exactly one canonical clue")]
    [InlineData("missing_topic", "must identify exactly one assigned topic")]
    [InlineData("duplicate_topic", "must identify exactly one assigned topic")]
    public void InvalidAuthoredProofReferencesProduceActionableContractErrors(string defect, string expected)
    {
        var draft = baseline.Draft.Copy();
        switch (defect)
        {
            case "missing_clue": draft.CaseBible!.DifficultyIntent.ProofPaths[0].ClueIds.Add("clue.unknown"); break;
            case "duplicate_clue": draft.CaseBible!.DifficultyIntent.Clues.Add(draft.CaseBible.DifficultyIntent.Clues[0]); break;
            case "missing_topic": draft.QuestionTopics.Clear(); break;
            case "duplicate_topic": draft.QuestionTopics.Add(draft.QuestionTopics[0]); break;
        }

        var error = Assert.Throws<TaskContractException>(() => CaseBibleValidator.Authored(draft));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void ForensicPlanCannotMoveAResultToAnotherCompatibleButUnassignedInputOwner()
    {
        var draft = baseline.Draft.Copy();
        var outcome = draft.ForensicStubs[0];
        var original = draft.CaseGraph.AssetSpecs.Single(s => s.Id == outcome.InputAssetId);
        var unrelated = draft.CaseGraph.AssetSpecs.First(s => s.Id != outcome.InputAssetId && s.AssetType == original.AssetType);
        unrelated.ContainedObjectIds.Add(outcome.InputObjectId);
        unrelated.ContainedObjectTypes[outcome.InputObjectId] = original.ContainedObjectTypes[outcome.InputObjectId];
        outcome.InputAssetId = unrelated.Id;
        Assert.Throws<TaskContractException>(() => ForensicsPlanTask.ValidatePlan(draft, draft.AnalysisTypes, draft.ForensicStubs));
    }

    [Fact]
    public void PrivateCanonicalIdentityDoesNotTurnAnUncertainRawFindingIntoAMatch()
    {
        var draft = baseline.Draft.Copy();
        var outcome = draft.ForensicStubs[0];
        var observation = CaseBibleAuthorship.OutcomeObservations(draft, outcome).First();
        var fact = draft.CaseBible!.Facts.Single(f => f.Id == observation.FactId);
        fact.ObjectId = draft.CaseBible.Incident.CulpritPersonId;
        observation.ObservedValue = draft.CaseBible.People.Single(p => p.SuspectId == "suspect.riley").Id;
        observation.Reliability = ObservationReliability.Disputed;
        outcome.MatchedSuspectId = draft.CulpritId;
        Assert.Throws<TaskContractException>(() => ForensicsPlanTask.ValidatePlan(draft, draft.AnalysisTypes, draft.ForensicStubs));
    }

    [Fact]
    public void NoFindingsCannotClaimProducedObservations()
    {
        var draft = baseline.Draft.Copy();
        draft.ForensicStubs[0].Findings = false;
        draft.ForensicStubs[0].SupportsClueIds.Clear();
        var error = Assert.Throws<TaskContractException>(() => ForensicsPlanTask.ValidatePlan(draft, draft.AnalysisTypes, draft.ForensicStubs));
        Assert.Contains("cannot claim produced observations", error.Message);
    }

    [Fact]
    public void SupportedCatalogMethodStillRequiresCanonicalCasePermission()
    {
        var draft = baseline.Draft.Copy();
        var method = ForensicMethodCatalog.Get("DigitalForensics");
        draft.AnalysisTypes.Add(new ForensicsAnalysisType
        {
            Type = method.Id, DurationMinutes = 5, AvailableFor = method.AcceptedAssetTypes.ToList()
        });
        Assert.Throws<TaskContractException>(() => ForensicsPlanTask.ValidatePlan(draft, draft.AnalysisTypes, draft.ForensicStubs));
        draft.CaseBible!.DifficultyIntent.ForensicOpportunities[0].MethodHint = method.Id;
        Assert.Contains(CaseBibleValidator.Validate(draft.CaseBible, "Detective", "en-US", true).Issues,
            issue => issue.Code == "forensic_method_not_allowed");
    }

    [Theory]
    [InlineData("visualSimilarity")]
    [InlineData("noFindings")]
    public void NoFindingsOrSimilarityCannotBecomeDecisiveProofOrExoneration(string predicate)
    {
        var bible = CaseBibleTestData.ValidDetectiveBible();
        bible.Facts.Single(f => f.Id == "fact.forensic_link").Predicate = predicate;
        Assert.Contains(CaseBibleValidator.Validate(bible, "Detective", "en-US", true).Issues,
            issue => issue.Code == "unsupported_decisive_proof");
        var verification = bible.DifficultyIntent.DecoyArcs[0].VerificationObservationIds[0];
        var factId = bible.Observations.Single(o => o.Id == verification).FactId;
        bible.Facts.Single(f => f.Id == factId).Predicate = predicate;
        Assert.Contains(CaseBibleValidator.Validate(bible, "Detective", "en-US", true).Issues,
            issue => issue.Code == "unsupported_exoneration");
    }

    [Fact]
    public void AttributedAlibiIsNotSelfVerified()
    {
        var draft = baseline.Draft.Copy();
        var suspect = draft.SuspectFull.Single(s => s.Id == "suspect.jordan");
        Assert.False(suspect.AlibiVerified);
        suspect.AlibiVerified = true;
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.Suspect(draft, suspect));
    }

    [Fact]
    public void EveryProfileAlibiHasARealReachableRecordedInterview()
    {
        var draft = baseline.Draft.Copy();
        foreach (var suspect in draft.SuspectFull)
        {
            var interview = draft.AssetFull.Single(a => a.Id == $"asset.interview_{suspect.Id["suspect.".Length..]}");
            Assert.Contains(interview.Support, s => s.ReferenceId == $"observation.stated_alibi_{suspect.Id["suspect.".Length..]}");
            Assert.Contains($"{suspect.Name}: {suspect.Alibi}", CaseBibleAuthorship.Text(interview));
        }
        draft.AssetFull.Single(a => a.Id == "asset.interview_sam").Visibility = "hidden";
        foreach (var email in draft.FollowUpEmails.Where(e => e.Support.Any(s => s.ReferenceId == "observation.stated_alibi_sam")))
            email.Visibility = "hidden";
        var error = Assert.Throws<TaskContractException>(() => CaseBibleValidator.Authored(draft));
        Assert.Contains("Alibi claim 'suspect.sam'", error.Message);
    }

    [Fact]
    public void FutureTestimonyCannotLeakAnAlibiIntoAnInitialProfile()
    {
        var draft = baseline.Draft.Copy();
        var source = draft.CaseBible!.Sources.Single(s => s.Id == "source.statement_jordan");
        source.RecordedAt = DateTimeOffset.Parse(draft.Metadata.OpenedAt).AddHours(1).ToString("o");
        Assert.Throws<TaskContractException>(() => CaseBibleAuthorship.ProfileContext(draft, "suspect.jordan"));
        Assert.Contains(CaseBibleValidator.Validate(draft.CaseBible, "Detective", "en-US", true).Issues,
            issue => issue.Code == "alibi_source_missing");
    }

    [Theory]
    [InlineData("en-US", "neither identity nor innocence")]
    [InlineData("pt-BR", "não estabelece identidade nem inocência")]
    [InlineData("es-ES", "no establece identidad ni inocencia")]
    [InlineData("fr-FR", "ni l’identité ni l’innocence")]
    public void NoFindingsLimitationsAreExplicitInAllFourLocales(string language, string expected)
    {
        Assert.Contains(expected, ForensicOutcomeTask.NoFindingsText(language), StringComparison.Ordinal);
    }

    [Fact]
    public void RookieDifficultyAndRequiredRankAreIndependent()
    {
        var draft = CaseBibleTestData.ProjectedRookieDraft();
        draft.Request.RequiredRank = "commander";
        CaseBibleNormalizer.ProjectIntoDraft(draft);
        Assert.Equal("Rookie", draft.Metadata.Difficulty);
        Assert.Equal("Commander", draft.Metadata.RequiredRank);
        Assert.Equal("commander", draft.Request.RequiredRank);
        Assert.True(DifficultyProfileCatalog.Get(draft).AllEvidenceInitial);
        Assert.Empty(draft.CaseBible!.DifficultyIntent.ForensicOpportunities);
        Assert.True(CaseBibleValidator.Validate(draft, true).IsValid);
    }

    [Fact]
    public async Task ExistingBibleCannotBeRegeneratedByARepeatPlanningCall()
    {
        var draft = baseline.Draft.Copy();
        var original = JsonSerializer.Serialize(draft.CaseBible, CaseDraft.CanonicalJsonOptions);
        var provider = new TestCaseLlmProvider();
        await new CaseBibleTask(provider, NullLogger.Instance).RunAsync(draft, default);
        CaseBibleNormalizer.ProjectIntoDraft(draft);
        Assert.Empty(provider.Calls);
        Assert.Equal(original, JsonSerializer.Serialize(draft.CaseBible, CaseDraft.CanonicalJsonOptions));
    }

    [Fact]
    public async Task CanonicalFixtureSupportsABlindInvestigationOfRealReadableMaterials()
    {
        var draft = baseline.Draft.Copy();
        var texts = await new TestAssetRenderer().ReadPreparedDocumentsAsync(draft.AssetFull.Concat(draft.ResultAssets));
        var result = await new SolverTask(baseline.Provider, NullLogger.Instance).RunAsync(draft, default, texts);
        Assert.True(result.Correct, JsonSerializer.Serialize(result, CaseDraft.CanonicalJsonOptions));
    }

    [Theory]
    [InlineData("observation.endpoint_owner")]
    [InlineData("observation.action_session")]
    [InlineData("observation.forensic_link")]
    public void CanonicalSupportedExcerptsResolveToTheirGraphObservationAliases(string referenceId)
    {
        var draft = baseline.Draft;
        var asset = draft.AssetFull.Concat(draft.ResultAssets).Single(a => a.Support.Any(s => s.ReferenceId == referenceId));
        var support = asset.Support.Single(s => s.ReferenceId == referenceId);
        var canonical = draft.CaseBible!.Observations.Single(o => o.Id == referenceId);
        var graph = Assert.Single(draft.CaseGraph.Observations.Where(o => o.SourceAssetId == asset.Id && o.FactId == canonical.FactId));
        Assert.Equal(referenceId, CaseBibleAuthorship.ResolveObservation(draft, graph).Id);
        Assert.Same(asset.Support, ((CaseAuthoredContent)asset).Support);
        Assert.Equal(typeof(CaseAuthoredContent), typeof(EvidenceAsset).GetProperty(nameof(EvidenceAsset.Support))!.DeclaringType);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            citation = new { sourceId = asset.Id, quote = support.Excerpt },
            support = new { support.ReferenceId },
            graph = new { graph.Id, graph.FactId, graph.SourceAssetId }
        }, CaseDraft.CanonicalJsonOptions));
    }

    [Fact]
    public async Task PublicJsonV2OmitsTheBibleAndAllPrivateSupportProvenance()
    {
        using var fixture = new CaseGenerationTestFixture();
        var response = await fixture.GenerateAsync(writeToDisk: false);
        Assert.True(response.Quality.Approved, string.Join("\n", response.Quality.Issues.Select(i => i.Message)));
        Assert.Equal("2.0", JsonNode.Parse(response.CaseJson)!["version"]!.GetValue<string>());
        foreach (var field in new[] { "caseBible", "support", "explanationSupport", "truthStatus", "privateSummary", "authorPersonId", "canonicalValue" })
            Assert.DoesNotContain($"\"{field}\"", response.CaseJson);
    }

    private static bool IsAuthor(TestLlmCall call) =>
        new[] { "**outline**", "**full profile**", "**detail card**", "**detailed forensic outcome**", "ONLY the briefing email", "0-3 OPTIONAL", "ONE multiple-choice question", "post-mortem explanation" }
            .Any(marker => call.System.Contains(marker, StringComparison.Ordinal));
}
