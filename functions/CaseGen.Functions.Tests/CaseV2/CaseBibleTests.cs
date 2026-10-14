using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseBibleTests(CanonicalDraftFixture baseline) : IClassFixture<CanonicalDraftFixture>
{
    [Fact]
    public async Task CanonicalPlanningPrecedesAuthors_AndPrivateProvenanceNeverEntersPublicJson()
    {
        using var fixture = new CaseGenerationTestFixture();
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Published);
        var calls = fixture.Llm.Calls.ToList();
        var planning = calls.FindIndex(c => c.System.Contains("**canonical CaseBible**"));
        Assert.True(planning > 0);
        Assert.All(calls.Where(IsAuthor), call =>
        {
            Assert.True(calls.IndexOf(call) > planning);
            Assert.DoesNotContain("\"culpritId\"", call.User);
            Assert.DoesNotContain("\"isCulprit\"", call.User);
            Assert.DoesNotContain("\"truthStatus\"", call.User);
            Assert.DoesNotContain("fact.crime", call.User);
            Assert.DoesNotContain(CaseBibleTestData.Create().Facts[0].Statement, call.User);
        });
        Assert.DoesNotContain(calls, c => c.System.Contains("**initial evidence assets**") ||
            c.System.Contains("**forensics layer**") || c.System.Contains("**skeleton**") || c.System.Contains("**narrative timeline**"));
        Assert.Contains("caseBible", response.StageLatencyMs.Keys);
        foreach (var field in new[] { "caseBible", "support", "explanationSupport", "knownFactIds", "truthStatus", "crimeFactId", "isPrivate", "decisive" })
            Assert.DoesNotContain($"\"{field}\"", response.CaseJson);
        Assert.DoesNotContain("fact.crime", response.CaseJson);
        Assert.Equal("2.0", JsonNode.Parse(response.CaseJson)!["version"]!.GetValue<string>());
        Assert.Contains(calls, c => c.System.Contains("red-team reviewer") && c.User.Contains("\"caseBible\""));
    }

    [Fact]
    public void SourceContextContainsOnlyAuthorizedFactsAndAttributedClaims()
    {
        var context = JsonNode.Parse(CaseBibleAuthorship.Context(baseline.Draft, "suspect.jordan"))!;
        Assert.Empty(context["facts"]!.AsArray());
        Assert.Single(context["claims"]!.AsArray());
        Assert.Equal("claim.jordan_alibi", context["claims"]![0]!["id"]!.GetValue<string>());
        Assert.Null(context["claims"]![0]!["truthStatus"]);
        Assert.Null(context["sources"]![0]!["discovery"]);
        Assert.Null(context["sources"]![0]!["role"]);
        var receipt = CaseBibleAuthorship.Context(baseline.Draft, "asset.receipt");
        Assert.Contains("fact.riley_exclusion", receipt);
        Assert.DoesNotContain("fact.backup", receipt);
        Assert.DoesNotContain("fact.record\"", receipt);
        Assert.DoesNotContain("fact.crime", receipt);
    }

    [Fact]
    public async Task CollectiveProofIsValidWithoutAForensicSuspectMatch()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (!system.Contains("**canonical CaseBible**")) return node;
            node["outcomes"]![0]!["matchedSuspectId"] = null;
            node["outcomes"]![0]!["matchBasis"] = "none";
            return node;
        };
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Approved);
        Assert.True(response.Quality.Published);
        Assert.Null(JsonNode.Parse(response.CaseJson)!["forensicOutcomes"]![0]!["matchedSuspectId"]);
    }

    [Fact]
    public async Task IndependentlyPlannedMatchesAreNotRejectedByAnExactlyOneHeuristic()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (!system.Contains("**canonical CaseBible**")) return node;
            var bible = node.Deserialize<CaseBible>(CaseDraft.CanonicalJsonOptions)!;
            var outcome = JsonSerializer.Deserialize<CaseForensicPlan>(JsonSerializer.Serialize(bible.Outcomes[0]))!;
            outcome.AnalysisType = "DocumentAuthenticity";
            outcome.ResultAssetId = "asset.report_auth";
            outcome.ResultEmailId = "email.lab_auth";
            bible.Outcomes.Add(outcome);
            foreach (var original in bible.Sources.Where(s => s.Kind is "result_asset" or "result_email").ToList())
            {
                var source = JsonSerializer.Deserialize<CaseSourcePlan>(JsonSerializer.Serialize(original))!;
                source.Id = source.Kind == "result_asset" ? outcome.ResultAssetId : outcome.ResultEmailId;
                source.Discovery!.AnalysisType = outcome.AnalysisType;
                if (source.Kind == "result_email") source.Attachments = [outcome.ResultAssetId];
                bible.Sources.Add(source);
            }
            return JsonSerializer.SerializeToNode(bible)!;
        };
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Published, string.Join("\n", response.Quality.Issues.Select(i => i.Message)));
        Assert.Equal(2, JsonNode.Parse(response.CaseJson)!["forensicOutcomes"]!.AsArray()
            .Count(o => o!["matchedSuspectId"]!.GetValue<string>() == "suspect.jordan"));
    }

    [Fact]
    public async Task PlannedInitialEmailAndNestedNonForensicDiscoveryAreAuthoredWithoutPromotingEvidence()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (!system.Contains("**canonical CaseBible**")) return node;
            var bible = node.Deserialize<CaseBible>(CaseDraft.CanonicalJsonOptions)!;
            bible.Sources.Add(new()
            {
                Id = "email.witness", Kind = "email", Type = "email", Title = "Records follow-up",
                Role = "Initial records notice", AuthorEntityId = "entity.investigator", From = "Records <records@example.invalid>",
                RecordedAt = "2025-01-01T09:00:00Z", FactIds = ["fact.context"], Attachments = ["asset.access_log"]
            });
            var statement = bible.Sources.Single(s => s.Id == "asset.statement");
            statement.Visibility = "hidden";
            statement.Discovery = new()
            {
                Type = "multiple_conditions", Operator = "AND",
                Conditions =
                [
                    new() { Type = "email_opened", EmailId = "email.witness" },
                    new() { Type = "asset_viewed", AssetId = "asset.access_log" }
                ]
            };
            bible.Chronology[1].SourceId = "asset.scene";
            bible.ProofSets[0].FactIds.Add("fact.context");
            bible.ProofSets[0].SourceIds.Add(statement.Id);
            bible.ExplanationFactIds.Add("fact.context");
            return JsonSerializer.SerializeToNode(bible)!;
        };
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Published, string.Join("\n", response.Quality.Issues.Select(i => i.Message)));
        var json = JsonNode.Parse(response.CaseJson)!;
        Assert.Equal("hidden", json["assets"]!.AsArray().Single(a => a!["id"]!.GetValue<string>() == "asset.statement")!["visibility"]!.GetValue<string>());
        var email = json["emails"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "email.witness")!;
        Assert.Contains("An access discrepancy", email["body"]!.GetValue<string>());
        Assert.Equal("asset.access_log", email["attachments"]![0]!.GetValue<string>());
        var rule = json["rules"]!.AsArray().Single(r => r!["ruleId"]!.GetValue<string>() == "rule.discover_asset_statement")!;
        Assert.Equal("AND", rule["trigger"]!["operator"]!.GetValue<string>());
        var author = Assert.Single(fixture.Llm.Calls.Where(c => c.System.Contains("0-3 OPTIONAL")));
        Assert.DoesNotContain("fact.backup", author.User);
        Assert.DoesNotContain("culpritId", author.User);
    }

    [Fact]
    public async Task DocumentaryProofCanBePublishedWithoutRequiringForensicAnalysis()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (!system.Contains("**canonical CaseBible**")) return node;
            var bible = node.Deserialize<CaseBible>(CaseDraft.CanonicalJsonOptions)!;
            MakeDocumentary(bible);
            return JsonSerializer.SerializeToNode(bible)!;
        };
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Published, string.Join("\n", response.Quality.Issues.Select(i => i.Message)));
        var json = JsonNode.Parse(response.CaseJson)!;
        Assert.NotEmpty(json["forensicsDefaults"]!["analysisTypes"]!.AsArray());
        Assert.Empty(json["forensicOutcomes"]!.AsArray());
        Assert.Empty(json["solution"]!["requiredAnalysisIds"]!.AsArray());
        Assert.DoesNotContain(fixture.Llm.Calls, c => c.System.Contains("**detailed forensic outcome**"));
    }

    [Fact]
    public async Task NoFindingsCanExistWithoutBecomingProofOrExoneration()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (!system.Contains("**canonical CaseBible**")) return node;
            var bible = node.Deserialize<CaseBible>(CaseDraft.CanonicalJsonOptions)!;
            MakeDocumentary(bible);
            bible.AnalysisTypes = [new() { Type = "DigitalForensics", DurationMinutes = 15, AvailableFor = ["pdf"] }];
            bible.Facts.Add(new()
            {
                Id = "fact.no_findings", Kind = "no_findings", Statement = "The recovered copy contains no usable deleted entry.",
                EntityIds = ["entity.records"]
            });
            bible.Outcomes =
            [
                new()
                {
                    InputAssetId = "asset.access_log", AnalysisType = "DigitalForensics", FactIds = ["fact.no_findings"],
                    Limitations = "No recovered entry does not establish who used the terminal or prove innocence."
                }
            ];
            return JsonSerializer.SerializeToNode(bible)!;
        };
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Published, string.Join("\n", response.Quality.Issues.Select(i => i.Message)));
        var outcome = JsonNode.Parse(response.CaseJson)!["forensicOutcomes"]![0]!;
        Assert.False(outcome["findings"]!.GetValue<bool>());
        Assert.Null(outcome["resultAssetId"]);
        Assert.Null(outcome["matchedSuspectId"]);
    }

    [Fact]
    public async Task AnAttributedAlibiIsNotAutomaticallyVerified()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (!system.Contains("**canonical CaseBible**")) return node;
            node["alibis"]![1]!["verifiedFactIds"] = new JsonArray();
            node["alibis"]![1]!["sourceIds"] = new JsonArray();
            return node;
        };
        var response = await fixture.GenerateAsync(writeToDisk: false);
        Assert.True(response.Quality.Approved);
        var suspect = JsonNode.Parse(response.CaseJson)!["suspects"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == "suspect.riley")!;
        Assert.False(suspect["alibiVerified"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("pt-BR", "Intervalo estimado")]
    [InlineData("en-US", "Estimated window")]
    [InlineData("es-ES", "Intervalo estimado")]
    [InlineData("fr-FR", "Plage estim\u00e9e")]
    public void ChronologyPreservesEstimatedWindowsAndAttributedDisagreement(string language, string label)
    {
        var draft = baseline.Draft.Copy();
        draft.Request.Language = language;
        var fact = draft.Bible!.Facts.Single(f => f.Id == "fact.context");
        fact.Time = CaseBibleTestData.Window("08:40", "08:50");
        fact.Time.Precision = "estimate";
        foreach (var source in draft.Bible.Sources.Where(s => s.FactIds.Contains(fact.Id) && s.Kind != "briefing"))
            source.RecordedAt = "2025-01-01T08:50:00Z";
        draft.Bible.Chronology.Add(new()
        {
            ReferenceId = "claim.jordan_alibi", SourceId = "suspect.jordan", Source = "witness"
        });
        CaseBibleValidator.Plan(draft, draft.Bible);
        var timeline = CaseBibleAuthorship.Timeline(draft);
        Assert.Contains(label, timeline[1].Event);
        Assert.Contains(fact.Time.Latest, timeline[1].Event);
        Assert.False(timeline[1].Verified);
        Assert.StartsWith("jordan:", timeline[2].Event);
        Assert.False(timeline[2].Verified);
    }

    [Theory]
    [InlineData("private_disclosure")]
    [InlineData("crime_location")]
    [InlineData("victim_identity")]
    [InlineData("author_knowledge")]
    [InlineData("source_predates_fact")]
    [InlineData("future_initial_source")]
    [InlineData("crime_after_opening")]
    [InlineData("invalid_time_precision")]
    [InlineData("accurate_claim_disagrees")]
    [InlineData("unrelated_identification")]
    [InlineData("similarity_match")]
    [InlineData("no_findings_alibi")]
    [InlineData("unrelated_alibi")]
    [InlineData("self_verified_alibi")]
    [InlineData("uncovered_question")]
    [InlineData("photo_question")]
    [InlineData("missing_report_attachment")]
    [InlineData("discovery_cycle")]
    public void CanonicalPlanRejectsBrokenProvenanceBeforeAuthorship(string defect)
    {
        var draft = baseline.Draft.Copy();
        var bible = draft.Bible!;
        var log = bible.Sources.Single(s => s.Id == "asset.access_log");
        var backup = bible.Facts.Single(f => f.Id == "fact.backup");
        switch (defect)
        {
            case "private_disclosure": log.FactIds.Add("fact.crime"); break;
            case "crime_location": bible.Entities.Single(e => e.Id == "entity.location").Name = "A different city"; break;
            case "victim_identity": draft.Metadata.Victim = new() { Name = "Unplanned victim", Age = 30, Occupation = "employee" }; break;
            case "author_knowledge": log.FactIds.Add("fact.backup"); break;
            case "source_predates_fact": log.RecordedAt = "2025-01-01T08:00:00Z"; break;
            case "future_initial_source": log.RecordedAt = "2025-01-01T09:30:00Z"; break;
            case "crime_after_opening": bible.Facts[0].Time = CaseBibleTestData.Exact("10:00"); break;
            case "invalid_time_precision": bible.Facts[0].Time!.Latest = "2025-01-01T08:20:00Z"; break;
            case "accurate_claim_disagrees": bible.Claims[1].Time = CaseBibleTestData.Window("07:00", "07:30"); break;
            case "unrelated_identification": backup.EntityIds = ["entity.records"]; break;
            case "similarity_match": bible.Outcomes[0].MatchBasis = "similarity"; backup.Kind = "similarity"; backup.Decisive = false; break;
            case "no_findings_alibi": bible.Facts.Single(f => f.Id == "fact.riley_exclusion").Kind = "no_findings"; break;
            case "unrelated_alibi": bible.Facts.Single(f => f.Id == "fact.riley_exclusion").EntityIds = ["suspect.sam"]; break;
            case "self_verified_alibi":
                var receipt = bible.Sources.Single(s => s.Id == "asset.receipt");
                receipt.AuthorEntityId = "suspect.riley";
                bible.Entities.Single(e => e.Id == "suspect.riley").KnownFactIds.Add("fact.sam_exclusion");
                break;
            case "uncovered_question": bible.Questions[0].SourceIds = ["asset.statement"]; break;
            case "photo_question":
                var scene = bible.Sources.Single(s => s.Id == "asset.scene");
                scene.Type = "photo"; scene.FactIds.Add("fact.record"); bible.Questions[0].SourceIds = [scene.Id]; break;
            case "missing_report_attachment": bible.Sources.Single(s => s.Id == "email.lab_access").Attachments.Clear(); break;
            case "discovery_cycle":
                log.Visibility = "hidden";
                log.Discovery = new() { Type = "asset_viewed", AssetId = "asset.report_access" };
                bible.Sources.Single(s => s.Id == "email.briefing").Attachments.Clear();
                bible.Chronology[0] = new()
                {
                    ReferenceId = "fact.riley_exclusion", SourceId = "asset.receipt", Source = "investigation"
                };
                break;
        }
        Assert.Throws<TaskContractException>(() => CaseBibleValidator.Plan(draft, bible));
    }

    [Theory]
    [InlineData("**full profile**")]
    [InlineData("**detail card**")]
    [InlineData("**detailed forensic outcome**")]
    [InlineData("ONLY the briefing email")]
    [InlineData("ONE multiple-choice question")]
    [InlineData("post-mortem explanation")]
    public async Task MissingContentSupportFailsAtItsAuthoringTask(string target)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (system.Contains(target)) node["support"] = new JsonArray();
            return node;
        };
        await Assert.ThrowsAsync<TaskContractException>(() => fixture.GenerateAsync());
        Assert.DoesNotContain(fixture.Llm.Calls, c => c.System.Contains("red-team reviewer"));
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
    }

    [Fact]
    public async Task ListingDescriptionCannotStandInForDeliveredEvidence()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (system.Contains("**detail card**")) node["support"]![0]!["excerpt"] = node["description"]!.GetValue<string>();
            return node;
        };
        var error = await Assert.ThrowsAsync<TaskContractException>(() => fixture.GenerateAsync());
        Assert.Contains("not present in the actual content", error.Message);
        Assert.Equal(0, fixture.Publisher.Calls);
    }

    [Fact]
    public async Task AnchoredExcerptStillRequiresSemanticComparisonWithTheCanonicalFact()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (system.Contains("**detail card**") && node["id"]!.GetValue<string>() == "asset.scene")
                node["support"]![0]!["excerpt"] = "scene";
            return node;
        };
        fixture.Llm.ReviewContent = """
            {"verdict":"reject","findings":[{"severity":"high","area":"evidence","issue":"The cited title does not substantiate fact.context","reference":"asset.scene"}]}
            """;
        var response = await fixture.GenerateAsync();
        Assert.False(response.Quality.Approved);
        var review = Assert.Single(fixture.Llm.Calls.Where(c => c.System.Contains("red-team reviewer")));
        Assert.Contains("\"excerpt\":\"scene\"", review.User);
        Assert.Contains("\"statement\":\"An access discrepancy was reported at 08:45.\"", review.User);
        Assert.Contains("Compare EVERY support excerpt", review.System);
        Assert.Equal(0, fixture.Publisher.Calls);
    }

    [Theory]
    [InlineData("discovery")]
    [InlineData("analysis_definition")]
    [InlineData("answer")]
    [InlineData("alibi")]
    [InlineData("requirements")]
    [InlineData("stale_support")]
    public void AuthoredDraftCannotDriftFromTheApprovedPlan(string defect)
    {
        var draft = baseline.Draft.Copy();
        switch (defect)
        {
            case "discovery": draft.Rules[0].Trigger = new() { Type = "time_elapsed", AtMinutes = 0 }; break;
            case "analysis_definition": draft.AnalysisTypes[0].DurationMinutes++; break;
            case "answer": draft.Questions[0].Options.Single(o => o.Id == draft.Questions[0].CorrectOptionId).Label = "An unsupported alternative"; break;
            case "alibi": draft.SuspectFull[0].AlibiVerified = true; break;
            case "requirements": draft.RequiredEvidenceIds.RemoveAt(0); break;
            case "stale_support": draft.AssetFull.Single(a => a.Id == "asset.access_log").BodyDoc!.Sections[0].Text = "A replacement without the planned observation."; break;
        }
        Assert.Throws<TaskContractException>(() => CaseBibleValidator.Authored(draft));
    }

    [Fact]
    public void CanonicalRequirementsAreNotSilentlyDroppedByConsistency()
    {
        var draft = baseline.Draft.Copy();
        draft.RequiredEvidenceIds.Add("asset.unknown");
        draft.RequiredAnalysisIds.Add("asset.access_log:UnknownAnalysis");
        var report = new ConsistencyValidator(NullLogger<ConsistencyValidator>.Instance).Validate(draft);
        Assert.True(report.HasErrors);
        Assert.Contains("asset.unknown", draft.RequiredEvidenceIds);
        Assert.Contains("asset.access_log:UnknownAnalysis", draft.RequiredAnalysisIds);
        Assert.DoesNotContain(report.AutoFixes, f => f.Contains("dropped"));
    }

    [Fact]
    public void RepairSchemaExcludesCaseBible_AndDirectReplacementIsRejectedAtomically()
    {
        var original = baseline.Draft.ToReviewJson();
        var schema = CaseDraftRepair.BuildSchema(baseline.Draft, PublicSchema());
        var targets = JsonNode.Parse(schema)!["properties"]!["changes"]!["items"]!["oneOf"]!.AsArray()
            .Select(n => n!["properties"]!["target"]!["enum"]![0]!.GetValue<string>());
        Assert.DoesNotContain("caseBible", targets);
        var changedBible = baseline.Draft.Copy().Bible!;
        changedBible.Facts[0].Statement = "Replace the original criminal act.";
        var repair = new CaseDraftRepair
        {
            Changes = [new() { Target = "caseBible", Reason = "Rewrite truth.", Value = JsonSerializer.SerializeToElement(changedBible) }]
        };
        Assert.Throws<TaskContractException>(() => repair.Apply(baseline.Draft, PublicSchema()));
        Assert.Equal(original, baseline.Draft.ToReviewJson());
    }

    [Fact]
    public void IndependentCopyDoesNotShareTheProtectedGraph()
    {
        var draft = baseline.Draft.Copy();
        draft.Bible!.Facts[0].Statement = "Changed only in the candidate.";
        Assert.NotEqual(draft.Bible.Facts[0].Statement, baseline.Draft.Bible!.Facts[0].Statement);
        Assert.Throws<TaskContractException>(() => CaseDraftRepair.Validate(draft, baseline.Draft, PublicSchema()));
    }

    private static bool IsAuthor(TestLlmCall call) => new[]
    {
        "**full profile**", "**detail card**", "**detailed forensic outcome**", "ONLY the briefing email",
        "0-3 OPTIONAL", "ONLY the narrative", "ONE multiple-choice question", "post-mortem explanation"
    }.Any(call.System.Contains);

    private static string PublicSchema() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"));

    private static void MakeDocumentary(CaseBible bible)
    {
        bible.Sources.RemoveAll(s => s.Kind is "result_asset" or "result_email");
        bible.Outcomes.Clear();
        var statement = bible.Sources.Single(s => s.Id == "asset.statement");
        statement.FactIds.Add("fact.backup");
        bible.Entities.Single(e => e.Id == statement.AuthorEntityId).KnownFactIds.Add("fact.backup");
        bible.ProofSets[0].SourceIds = ["asset.access_log", statement.Id];
        bible.ProofSets[0].AnalysisIds.Clear();
        bible.Questions[1].SourceIds = [statement.Id];
    }
}
