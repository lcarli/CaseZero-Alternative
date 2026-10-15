using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseCanonicalRepairTests(CanonicalDraftFixture baseline) : IClassFixture<CanonicalDraftFixture>
{
    private const string Note = "The source is presented as received; its recorded observations remain unchanged.";
    private static string Schema => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"));
    private const string ReportId = "asset.report_initial_report_metadataanalysis";
    private const string EmailId = "email.lab_initial_report_metadataanalysis";
    private const string Pair = "asset.initial_report:MetadataAnalysis";

    [Fact]
    public async Task SupportedPresentationRepairIsAtomicAndPreservesOneCanonicalTruth()
    {
        var original = baseline.Draft.ToReviewJson();
        var bible = JsonSerializer.Serialize(baseline.Draft.CaseBible, CaseDraft.CanonicalJsonOptions);
        var proposal = RepairPresentation(JsonNode.Parse(original)!.AsObject()).ToJsonString();
        StructuredJson.Validate(proposal, StructuredJson.ParseSchema(CaseDraftRepair.BuildSchema(baseline.Draft, Schema)));
        var candidate = JsonSerializer.Deserialize<CaseDraftRepair>(proposal, CaseDraft.CanonicalJsonOptions)!.Apply(baseline.Draft, Schema);
        Assert.Equal(original, baseline.Draft.ToReviewJson());
        Assert.Equal(1, candidate.Revision);
        Assert.Equal(bible, JsonSerializer.Serialize(candidate.CaseBible, CaseDraft.CanonicalJsonOptions));
        Assert.Equal(baseline.Draft.Questions.Select(q => q.CorrectOptionId), candidate.Questions.Select(q => q.CorrectOptionId));
        CaseDraftRepair.Validate(candidate, baseline.Draft, Schema);

        var renderer = new TestAssetRenderer();
        var readable = await renderer.ReadPreparedDocumentsAsync(candidate.AssetFull.Concat(candidate.ResultAssets));
        Assert.Contains(Note, readable["asset.initial_report"]);
        Assert.Contains(Note, readable[ReportId]);
        Assert.All(candidate.AssetFull.Concat(candidate.ResultAssets).SelectMany(a => a.Support), s =>
            Assert.Contains(candidate.AssetFull.Concat(candidate.ResultAssets), a => CaseBibleAuthorship.Text(a).Contains(s.Excerpt, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PublicOutlineRepairKeepsTheTitleAndFrozenClueAssignments()
    {
        var candidate = baseline.Draft.Copy();
        var provider = new TestCaseLlmProvider
        {
            TransformResponse = (_, _, node) =>
            {
                node["metadata"]!["description"] = Note;
                return node;
            }
        };
        await new PlotOutlineTask(provider, NullLogger.Instance).RunAsync(candidate, default);
        Assert.Equal(Note, candidate.Metadata.Description);
        CaseDraftRepair.Validate(candidate, baseline.Draft, Schema);
        var call = Assert.Single(provider.Calls);
        Assert.Contains("\"preserveOrder\":true", call.User);
        Assert.DoesNotContain("\"culpritId\"", call.User);
    }

    [Theory]
    [InlineData("unknown_culprit")]
    [InlineData("different_culprit")]
    [InlineData("asset_identity")]
    [InlineData("suspect_identity")]
    [InlineData("partial_rename")]
    [InlineData("table_width")]
    [InlineData("missing_document")]
    [InlineData("broken_mechanical_rule")]
    [InlineData("missing_answer")]
    [InlineData("duplicate_options")]
    [InlineData("changed_answer")]
    [InlineData("changed_option_label")]
    [InlineData("new_forensic_match")]
    [InlineData("fabricated_findings")]
    [InlineData("changed_result_reference")]
    [InlineData("changed_source_time")]
    [InlineData("changed_historical_time")]
    [InlineData("changed_alibi")]
    [InlineData("missing_dependency")]
    [InlineData("unknown_target")]
    [InlineData("duplicate_target")]
    [InlineData("wrong_revision")]
    [InlineData("noop")]
    [InlineData("scoring")]
    [InlineData("immutable_root")]
    public void InvalidRepairDoesNotMutateTheOriginal(string defect)
    {
        var original = baseline.Draft.ToReviewJson();
        var proposal = Corrupt(JsonNode.Parse(original)!.AsObject(), defect).ToJsonString();
        Assert.Throws<TaskContractException>(() =>
        {
            StructuredJson.Validate(proposal, StructuredJson.ParseSchema(CaseDraftRepair.BuildSchema(baseline.Draft, Schema)));
            JsonSerializer.Deserialize<CaseDraftRepair>(proposal, CaseDraft.CanonicalJsonOptions)!.Apply(baseline.Draft, Schema);
        });
        Assert.Equal(original, baseline.Draft.ToReviewJson());
    }

    [Fact]
    public async Task LegacyRefinementRoutesOnlyThroughSourceScopedOwnerAuthors()
    {
        var original = baseline.Draft.ToReviewJson();
        var provider = new TestCaseLlmProvider
        {
            TransformResponse = (system, _, node) =>
            {
                if (system.Contains("**detail card**", StringComparison.Ordinal)
                    && node["id"]!.GetValue<string>() == "asset.initial_report")
                    node["bodyDoc"]!["sections"]![0]!["text"] = node["bodyDoc"]!["sections"]![0]!["text"]!.GetValue<string>() + "\n" + Note;
                return node;
            }
        };
        var result = await new RefineCaseTask(provider, NullLogger.Instance).RunAsync(baseline.Draft, [],
            new[] { new RedTeamTask.Finding { Severity = "medium", OwnerStage = "assetCards", NodeId = "asset.initial_report", Issue = "Clarify procedural presentation." } },
            Schema, default);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(original, baseline.Draft.ToReviewJson());
        Assert.Equal(1, result.Candidate!.Revision);
        Assert.DoesNotContain(provider.Calls, call => call.System.Contains("**refine agent**", StringComparison.Ordinal));
        Assert.All(provider.Calls.Where(call => call.System.Contains("**detail card**", StringComparison.Ordinal)), call =>
        {
            Assert.DoesNotContain("\"caseBible\"", call.User);
            Assert.DoesNotContain("\"correctOptionId\"", call.User);
            Assert.DoesNotContain("\"culpritId\"", call.User);
        });
    }

    [Theory]
    [InlineData("bible")]
    [InlineData("source_assignment")]
    [InlineData("forensic_plan")]
    [InlineData("graph_truth")]
    [InlineData("question_sources")]
    public void OwnerRepairsCannotRewriteProtectedPlans(string target)
    {
        var candidate = baseline.Draft.Copy();
        switch (target)
        {
            case "bible": candidate.CaseBible!.Incident.CulpritPersonId = "person.decoy_one"; break;
            case "source_assignment": candidate.AssetStubs[0].SupportsClueIds.Clear(); break;
            case "forensic_plan": candidate.ForensicStubs[0].Findings = false; break;
            case "graph_truth": candidate.CaseGraph.Facts[0].Predicate = "invented"; break;
            case "question_sources": candidate.QuestionTopics[0].SupportingEvidenceIds = ["asset.unknown"]; break;
        }
        Assert.Throws<TaskContractException>(() => CaseDraftRepair.ValidateProtected(baseline.Draft, candidate));
    }

    [Fact]
    public async Task BrokenImmutableRootIsActionableAndNeverRegenerated()
    {
        var draft = baseline.Draft.Copy();
        draft.CaseBible!.Incident.CulpritPersonId = "person.victim";
        var provider = new TestCaseLlmProvider();
        var coordinator = new RepairCoordinator(provider,
            new MechanicalRulesBuilder(NullLogger<MechanicalRulesBuilder>.Instance), NullLogger.Instance);
        var error = await Assert.ThrowsAsync<TaskContractException>(() => coordinator.RepairAsync(
            draft, [], [new RepairIssue("caseBible", "The immutable culprit is invalid.")], false, default));
        Assert.Contains("immutable_root_invalid", error.Message);
        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task ValidImmutableRootCannotBeReplannedInResponseToAReviewFinding()
    {
        var draft = baseline.Draft.Copy();
        var original = JsonSerializer.Serialize(draft.CaseBible);
        var provider = new TestCaseLlmProvider();
        var coordinator = new RepairCoordinator(provider,
            new MechanicalRulesBuilder(NullLogger<MechanicalRulesBuilder>.Instance), NullLogger.Instance);
        var error = await Assert.ThrowsAsync<TaskContractException>(() => coordinator.RepairAsync(
            draft, [], [new RepairIssue("caseBible", "Change the canonical attribution.")], false, default));
        Assert.Contains("immutable_root_review", error.Message);
        Assert.Equal(original, JsonSerializer.Serialize(draft.CaseBible));
        Assert.Empty(provider.Calls);
    }

    [Fact]
    public void StaleReviewCannotApproveAnotherRevision()
    {
        var quality = CaseQualityGate.Evaluate(new GenerateCaseV2Request(), false, [], [],
            new RedTeamTask.Report { Verdict = "ok", Succeeded = true, Revision = 0 }, revision: 1);
        Assert.False(quality.Approved);
        Assert.Contains(quality.Issues, issue => issue.Code == "review_revision_mismatch");
    }

    private static JsonObject RepairPresentation(JsonObject snapshot)
    {
        var components = snapshot["components"]!;
        var input = components["asset.initial_report"]!.DeepClone();
        input["bodyDoc"]!["sections"]![0]!["text"] = input["bodyDoc"]!["sections"]![0]!["text"]!.GetValue<string>() + "\n" + Note;
        var report = components[ReportId]!.DeepClone();
        report["bodyDoc"]!["sections"]![0]!["text"] = report["bodyDoc"]!["sections"]![0]!["text"]!.GetValue<string>() + "\n" + Note;
        var outcome = components[Pair]!.DeepClone();
        outcome["conclusionText"] = outcome["conclusionText"]!.GetValue<string>() + "\n" + Note;
        var email = components[EmailId]!.DeepClone();
        email["body"] = email["body"]!.GetValue<string>() + "\n" + Note;
        var solution = components["solution"]!.DeepClone();
        solution["explanation"] = solution["explanation"]!.GetValue<string>() + "\n" + Note;
        return new JsonObject
        {
            ["baseRevision"] = snapshot["revision"]!.GetValue<int>(),
            ["changes"] = new JsonArray(Change("asset.initial_report", input), Change(ReportId, report), Change(Pair, outcome),
                Change(EmailId, email), Change("solution", solution), Change("briefing", components["briefing"]!.DeepClone()))
        };
    }

    private static JsonObject Change(string target, JsonNode value) => new()
    {
        ["target"] = target, ["reason"] = "Clarify procedural presentation while preserving the recorded source.", ["value"] = value
    };

    private static JsonObject Corrupt(JsonObject snapshot, string defect)
    {
        var repair = RepairPresentation(snapshot);
        var changes = repair["changes"]!.AsArray();
        JsonNode Value(string target) => changes.Single(c => c!["target"]!.GetValue<string>() == target)!["value"]!;
        void Add(string target, Action<JsonNode> mutate)
        {
            var node = snapshot["components"]![target]!.DeepClone();
            mutate(node);
            changes.Add(Change(target, node));
        }
        switch (defect)
        {
            case "unknown_culprit": Value("solution")["culpritId"] = "suspect.unknown"; break;
            case "different_culprit": Value("solution")["culpritId"] = "suspect.riley"; break;
            case "asset_identity": Value(ReportId)["id"] = "asset.new_report"; break;
            case "suspect_identity": Add("suspect.jordan", n => n["id"] = "suspect.unknown"); break;
            case "partial_rename": Add("suspect.jordan", n => n["name"] = "Another identity"); break;
            case "table_width":
                Value("asset.initial_report")["bodyDoc"]!["sections"] = JsonNode.Parse("""
                    [{"kind":"table","heading":null,"table":{"columns":["Time","Event"],"rows":[["19:52"]],"footnote":null}}]
                    """);
                break;
            case "missing_document": Value("asset.initial_report")["bodyDoc"] = null; break;
            case "broken_mechanical_rule": Add("rules", n => n[0]!["trigger"]!["inputAssetId"] = "asset.unknown"); break;
            case "missing_answer": Value("solution")["questions"]![0]!["correctOptionId"] = "opt.unknown"; break;
            case "duplicate_options":
                Value("solution")["questions"]![0]!["options"]![1]!["id"] = Value("solution")["questions"]![0]!["options"]![0]!["id"]!.GetValue<string>();
                break;
            case "changed_answer": Value("solution")["questions"]![0]!["correctOptionId"] = Value("solution")["questions"]![0]!["options"]![1]!["id"]!.GetValue<string>(); break;
            case "changed_option_label": Value("solution")["questions"]![0]!["options"]![1]!["label"] = "A replacement answer."; break;
            case "new_forensic_match": Value(Pair)["matchedSuspectId"] = "suspect.riley"; break;
            case "fabricated_findings": Value(Pair)["findings"] = false; break;
            case "changed_result_reference": Value(Pair)["resultAssetId"] = "asset.initial_report"; break;
            case "changed_source_time": Value(EmailId)["sentAt"] = "2026-07-21T22:30:00-04:00"; break;
            case "changed_historical_time": Add("timeline", n => n[0]!["time"] = "2027-01-01T00:00:00Z"); break;
            case "changed_alibi": Add("suspect.jordan", n => n["alibi"] = "A different account."); break;
            case "missing_dependency": changes.RemoveAt(3); break;
            case "unknown_target": changes[0]!["target"] = "asset.unknown"; break;
            case "duplicate_target": changes.Add(changes[0]!.DeepClone()); break;
            case "wrong_revision": repair["baseRevision"] = 99; break;
            case "noop": repair["changes"] = new JsonArray(Change("metadata", snapshot["components"]!["metadata"]!.DeepClone())); break;
            case "scoring": Value("solution")["minimumScore"] = 0.1; break;
            case "immutable_root": Add("caseBible", n => n["incident"]!["culpritPersonId"] = "person.decoy_one"); break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        return repair;
    }
}

public sealed class CanonicalDraftFixture : IAsyncLifetime
{
    public CaseDraft Draft { get; private set; } = new();
    internal TestCaseLlmProvider Provider { get; } = new();

    public async Task InitializeAsync()
    {
        var draft = new CaseDraft
        {
            CaseId = "case_quality", Request = new GenerateCaseV2Request
            {
                CaseId = "case_quality", Difficulty = "Detective", Language = "en-US", Seed = 17, WriteToDisk = false
            }
        };
        await new CaseBibleTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        var frozen = JsonSerializer.Serialize(draft.CaseBible);
        await new PlotOutlineTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        foreach (var stub in draft.SuspectStubs)
            draft.SuspectFull.Add(await new SuspectCardTask(Provider, NullLogger.Instance).RunAsync(draft, stub, default));
        await new AssetPlanTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        EvidenceGraphCompiler.Compile(draft);
        foreach (var stub in draft.AssetStubs)
            draft.AssetFull.Add(await new AssetCardTask(Provider, NullLogger.Instance).RunAsync(draft, stub, default));
        await new TimelineTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        await new BriefingEmailTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        await new ForensicsPlanTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        foreach (var stub in draft.ForensicStubs)
        {
            var (full, asset, email) = await new ForensicOutcomeTask(Provider, NullLogger.Instance).RunAsync(draft, stub, default);
            draft.ForensicFull.Add(full);
            if (asset is not null) draft.ResultAssets.Add(asset);
            if (email is not null) draft.ResultEmails.Add(email);
        }
        await new InitialEmailsTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        EvidenceGraphCompiler.Compile(draft);
        new MechanicalRulesBuilder(NullLogger<MechanicalRulesBuilder>.Instance).Build(draft);
        await new RulesTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        await new SolutionSkeletonTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        foreach (var topic in draft.QuestionTopics)
            draft.Questions.Add(await new QuestionTask(Provider, NullLogger.Instance).RunAsync(draft, topic, default));
        await new ExplanationTask(Provider, NullLogger.Instance).RunAsync(draft, default);
        CaseGraphProjection.RefreshGeneratedGraph(draft);
        Assert.Equal(frozen, JsonSerializer.Serialize(draft.CaseBible));
        CaseBibleValidator.Authored(draft);
        Draft = draft;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
