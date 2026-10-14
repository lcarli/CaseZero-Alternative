using System.Text.Json.Nodes;
using CaseGen.Functions.Functions.CaseV2;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using CaseGen.Functions.Services.CaseV2.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseCanonicalRepairTests(CanonicalDraftFixture baseline) : IClassFixture<CanonicalDraftFixture>
{
    private const string RepairedFact = "The independently verified signed backup attributes the altered entry to an individual signing key assigned to jordan.";
    private const string RepairedRecord = "At 08:15 the access log records an authenticated terminal session assigned to jordan saving the altered entry.";
    private const string RepairedExplanation = RepairedRecord + " " + RepairedFact;
    private const string BlockedReview = """
        {"verdict":"needs_review","findings":[{"severity":"medium","area":"evidence","issue":"Report and email contradict the signed backup","reference":"asset.report_access"}]}
        """;
    private const string OkReview = """{"verdict":"ok","findings":[]}""";

    [Theory]
    [InlineData("needs_review", "medium")]
    [InlineData("reject", "high")]
    [InlineData("ok", "medium")]
    [InlineData("ok", "high")]
    public async Task SemanticRepair_ZeroToZeroRevalidatesAndPublishesOneCanonicalRevision(string verdict, string severity)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.RepairResponse = RepairEvidence;
        fixture.Llm.ReviewResponse = user => IsCandidate(user) ? OkReview : $$"""
            {"verdict":"{{verdict}}","findings":[{"severity":"{{severity}}","area":"evidence","issue":"Report and email contradict the signed backup","reference":"asset.report_access"}]}
            """;
        var response = await fixture.GenerateAsync();

        Assert.True(response.Repair.Attempted);
        Assert.True(response.Repair.Accepted, response.Repair.ErrorMessage);
        Assert.Equal(0, response.RefineErrorsBefore);
        Assert.Equal(0, response.RefineErrorsAfter);
        Assert.Equal(0, response.Repair.BaseRevision);
        Assert.Equal(1, response.Repair.CandidateRevision);
        Assert.Equal(1, response.Quality.Revision);
        Assert.Equal(1, response.Quality.ReviewedRevision);
        Assert.True(response.Quality.Published);
        Assert.Contains(response.Repair.OriginalBlockers, message => message.Contains("red_team_finding"));
        Assert.Equal(new[]
        {
            "solution", "asset.access_log", "asset.report_access", "email.lab_access", "asset.access_log:DigitalForensics"
        }.Order(), response.Repair.ChangedComponents.Order());
        Assert.Equal(1, fixture.Llm.Calls.Count(c => c.System.Contains("**refine agent**")));
        Assert.Equal(2, fixture.Llm.Calls.Count(c => c.System.Contains("red-team reviewer")));
        Assert.Equal(2, fixture.Llm.Calls.Count(c => c.System.Contains("AS THE PLAYER")));

        var reviewed = fixture.Llm.Calls.Single(c => c.System.Contains("red-team reviewer") && IsCandidate(c.User));
        Assert.Contains(RepairedFact, reviewed.User);
        Assert.Contains("\"bodyDoc\"", reviewed.User);
        Assert.Contains("\"body\"", reviewed.User);
        Assert.Contains("\"forensics_complete\"", reviewed.User);
        Assert.Contains("\"reveal_email\"", reviewed.User);
        Assert.Contains("\"email.lab_access\"", reviewed.User);
        Assert.Contains("COMPILED case.json:", reviewed.User);
        var solver = fixture.Llm.Calls.Single(c => c.System.Contains("AS THE PLAYER") && IsCandidate(c.User));
        Assert.Contains(RepairedRecord, solver.User);
        Assert.DoesNotContain("\"correctOptionId\"", solver.User);

        var compiled = JsonNode.Parse(response.CaseJson)!;
        Assert.Equal("2.0", compiled["version"]!.GetValue<string>());
        Assert.Equal(RepairedFact, compiled["forensicOutcomes"]![0]!["conclusionText"]!.GetValue<string>());
        Assert.Equal(RepairedFact, compiled["emails"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "email.lab_access")!["body"]!.GetValue<string>());
        Assert.Equal(RepairedExplanation, compiled["solution"]!["explanation"]!.GetValue<string>());
        Assert.Equal(response.CaseJson, File.ReadAllText(fixture.CasePath));
        Assert.Equal(RepairedRecord, fixture.Renderer.RenderedAssets.Single(a => a.Id == "asset.access_log").BodyDoc!.Sections[0].Text);
        Assert.Equal(RepairedFact, fixture.Renderer.RenderedAssets.Single(a => a.Id == "asset.report_access").Body);
        Assert.Contains(RepairedRecord, File.ReadAllText(Path.Combine(fixture.Root, "case_quality", "assets", response.Bundle!.BundleId, "access_log.pdf")));
        Assert.Contains(RepairedFact, File.ReadAllText(Path.Combine(fixture.Root, "case_quality", "assets", response.Bundle.BundleId, "report_access.pdf")));
        Assert.Equal(response.Quality.ReviewedRevision, response.Bundle.Revision);
        var job = CaseV2JobResult.FromResponse("repair-job", response);
        Assert.False(job.HasErrors);
        Assert.Same(response.Repair, job.Repair);
        Assert.Contains("revalidateRepair", job.StageLatencyMs.Keys);
    }

    [Theory]
    [InlineData("unknown_culprit")]
    [InlineData("different_culprit")]
    [InlineData("asset_identity")]
    [InlineData("suspect_identity")]
    [InlineData("partial_rename")]
    [InlineData("table_width")]
    [InlineData("missing_document")]
    [InlineData("broken_nested_rule")]
    [InlineData("missing_answer")]
    [InlineData("duplicate_options")]
    [InlineData("new_forensic_match")]
    [InlineData("fabricated_findings")]
    [InlineData("missing_dependency")]
    [InlineData("unknown_target")]
    [InlineData("duplicate_target")]
    [InlineData("wrong_revision")]
    [InlineData("noop")]
    [InlineData("scoring")]
    [InlineData("missing_email_dependency")]
    public async Task InvalidRepairContract_DoesNotMutateCanonicalDraftOrRetry(string defect)
    {
        var original = baseline.Draft.ToReviewJson();
        var provider = new ScriptedLlmProvider((_, _) => Task.FromResult(new LLMResponse
        {
            Content = CorruptRepair(JsonNode.Parse(original)!.AsObject(), defect).ToJsonString()
        }));
        var result = await new RefineCaseTask(provider, NullLogger.Instance).RunAsync(baseline.Draft, [],
            new RedTeamTask.Report { Succeeded = true, Verdict = "needs_review", Revision = 0 },
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json")), default);
        Assert.False(result.Succeeded);
        Assert.Equal("repair_invalid", result.ErrorCode);
        Assert.NotEmpty(result.Error!);
        Assert.Equal(original, baseline.Draft.ToReviewJson());
        Assert.Single(provider.Calls);
    }

    [Theory]
    [InlineData("unknown_culprit")]
    [InlineData("missing_dependency")]
    [InlineData("noop")]
    public async Task InvalidOrIncompleteRepair_IsAtomicAndNeverUsesThePreviousReview(string defect)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        fixture.Llm.ReviewContent = BlockedReview;
        fixture.Llm.RepairResponse = snapshot => CorruptRepair(snapshot, defect);
        var response = await fixture.GenerateAsync();

        Assert.True(response.Repair.Attempted);
        Assert.False(response.Repair.Accepted);
        Assert.Equal("repair_invalid", response.Repair.ErrorCode);
        Assert.NotEmpty(response.Repair.ErrorMessage!);
        Assert.False(response.Quality.Approved);
        Assert.Equal(0, response.Quality.Revision);
        Assert.Contains(response.Quality.Issues, i => i.Code == "repair_invalid" && i.Blocking);
        Assert.DoesNotContain(RepairedFact, response.CaseJson);
        Assert.Equal(1, fixture.Llm.Calls.Count(c => c.System.Contains("**refine agent**")));
        Assert.Equal(1, fixture.Llm.Calls.Count(c => c.System.Contains("red-team reviewer")));
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Equal("original case", File.ReadAllText(fixture.CasePath));
        Assert.Equal("original asset", File.ReadAllText(Path.Combine(fixture.Root, "case_quality", "assets", "scene.pdf")));
        Assert.True(CaseV2JobResult.FromResponse("job", response).HasErrors);
    }

    [Theory]
    [InlineData("new_blocker")]
    [InlineData("invalid_review")]
    [InlineData("unavailable_review")]
    public async Task CandidateWithFailedFreshReview_ExhaustsSinglePassWithoutChangingOriginalFiles(string defect)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        fixture.Llm.RepairResponse = RepairEvidence;
        fixture.Llm.ReviewResponse = user =>
        {
            if (!IsCandidate(user)) return BlockedReview;
            if (defect == "unavailable_review") throw new InvalidOperationException("Candidate reviewer offline");
            return defect == "invalid_review" ? "{}" : """
                {"verdict":"reject","findings":[{"severity":"high","area":"solution","issue":"New unsupported answer","reference":"q.method"}]}
                """;
        };
        var response = await fixture.GenerateAsync();

        Assert.False(response.Quality.Approved);
        Assert.False(response.Repair.Accepted);
        Assert.Equal("repair_rejected", response.Repair.ErrorCode);
        Assert.Equal(1, response.Repair.CandidateRevision);
        Assert.Equal(0, response.Quality.Revision);
        Assert.Equal(0, response.Quality.ReviewedRevision);
        Assert.Contains(defect == "new_blocker" ? "red_team_rejected" :
            defect == "invalid_review" ? "red_team_invalid" : "red_team_unavailable", response.Repair.ErrorMessage);
        Assert.Equal(1, fixture.Llm.Calls.Count(c => c.System.Contains("**refine agent**")));
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.DoesNotContain(RepairedFact, response.CaseJson);
        Assert.Equal("original case", File.ReadAllText(fixture.CasePath));
    }

    [Fact]
    public void StaleReview_CannotApproveAnotherCanonicalRevision()
    {
        var quality = CaseQualityGate.Evaluate(new GenerateCaseV2Request(), false, [], [],
            new RedTeamTask.Report { Verdict = "ok", Succeeded = true, Revision = 0 }, revision: 1);
        Assert.False(quality.Approved);
        Assert.Contains(quality.Issues, i => i.Code == "review_revision_mismatch");
    }

    [Fact]
    public async Task StructurallyValidCandidateWithImpossibleTimeline_FailsConsistencyBeforeFreshReview()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.ReviewContent = BlockedReview;
        fixture.Llm.RepairResponse = snapshot =>
        {
            var repair = RepairEvidence(snapshot);
            var covered = repair["changes"]!.AsArray().Select(c => c!["target"]!.GetValue<string>()).ToHashSet();
            foreach (var component in snapshot["components"]!.AsObject())
                if (component.Key != "caseBible" && !covered.Contains(component.Key))
                    repair["changes"]!.AsArray().Add(Change(component.Key, component.Value!.DeepClone()));
            var timeline = repair["changes"]!.AsArray().Single(c => c!["target"]!.GetValue<string>() == "timeline")!["value"]!;
            timeline[0]!["time"] = "2026-01-01T08:15:00Z";
            return repair;
        };
        var response = await fixture.GenerateAsync();
        Assert.False(response.Quality.Approved);
        Assert.Equal("repair_rejected", response.Repair.ErrorCode);
        Assert.Equal(0, response.Quality.Revision);
        Assert.Equal(1, fixture.Llm.Calls.Count(c => c.System.Contains("red-team reviewer")));
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Contains("revalidateRepair", response.StageLatencyMs.Keys);
    }

    [Theory]
    [InlineData("2025-01-01T09:00:00Z", false)]
    [InlineData("2025-01-01T09:01:00Z", false)]
    [InlineData("2025-01-01T09:01:01Z", true)]
    [InlineData("2026-01-01T08:15:00Z", true)]
    public void HistoricalTimeline_AfterCaseOpeningIsAHardError(string time, bool expectedError)
    {
        var draft = baseline.Draft.Copy();
        draft.Metadata.OpenedAt = "2025-01-01T09:00:00Z";
        draft.Timeline[0].Time = time;
        var report = new ConsistencyValidator(NullLogger<ConsistencyValidator>.Instance).Validate(draft);
        Assert.Equal(expectedError, report.Findings.Any(f => f.Area == "timeline" && f.Severity == "error" && f.Message.Contains("after openedAt")));
    }

    [Fact]
    public async Task CandidateInvalidatedByDocumentPreparation_FailsRevalidationBeforeFreshReview()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.ReviewContent = BlockedReview;
        fixture.Llm.RepairResponse = RepairEvidence;
        fixture.Renderer.OnPrepare = asset =>
        {
            if (asset.BodyDoc?.Sections[0].Text != RepairedRecord) return;
            asset.BodyDoc.Sections.Add(new EvidenceSection
            {
                Kind = "table", Table = new EvidenceTable { Columns = ["Time", "Event"], Rows = [["08:15"]] }
            });
        };
        var response = await fixture.GenerateAsync();
        Assert.False(response.Quality.Approved);
        Assert.Equal("repair_rejected", response.Repair.ErrorCode);
        Assert.Equal(0, response.Quality.Revision);
        Assert.Equal(1, fixture.Llm.Calls.Count(c => c.System.Contains("red-team reviewer")));
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Contains("revalidateRepair", response.StageLatencyMs.Keys);
    }

    [Fact]
    public async Task RepairCannotChangeRequestedTitle()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.ReviewContent = BlockedReview;
        fixture.Llm.RepairResponse = snapshot =>
        {
            var repair = RepairEvidence(snapshot);
            var metadata = snapshot["components"]!["metadata"]!.DeepClone();
            metadata["title"] = "Replacement identity";
            repair["changes"]!.AsArray().Add(Change("metadata", metadata));
            return repair;
        };
        var response = await fixture.GenerateAsync(request: new GenerateCaseV2Request
        {
            CaseId = "case_quality", Title = "Quality fixture"
        });
        Assert.Equal("repair_invalid", response.Repair.ErrorCode);
        Assert.Contains("metadata.title", response.Repair.ErrorMessage);
        Assert.Equal(0, fixture.Publisher.Calls);
    }

    [Fact]
    public async Task RevalidationCancellation_PropagatesAndNeverPublishes()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        using var source = new CancellationTokenSource();
        fixture.Llm.RepairResponse = RepairEvidence;
        fixture.Llm.ReviewResponse = user =>
        {
            if (IsCandidate(user)) source.Cancel();
            return IsCandidate(user) ? OkReview : BlockedReview;
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.GenerateAsync(ct: source.Token));
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Equal("original case", File.ReadAllText(fixture.CasePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemplatesAreAppliedOnlyToUnboundDocumentsBeforeReview_AndNeverAgainWhileRendering(bool canonical)
    {
        using var fixture = new CaseGenerationTestFixture();
        var template = new CountingTemplate();
        var documents = new RecordingDocumentRenderer();
        var renderer = new AssetRenderingService(
            new PdfRenderingService(new ConfigurationBuilder().Build(), NullLogger<PdfRenderingService>.Instance),
            fixture.Llm, documents, new EvidenceTemplateRegistry([template]), NullLogger<AssetRenderingService>.Instance);
        var asset = new EvidenceAsset
        {
            Id = "asset.prepared", Title = "Prepared document", CanonicalContent = canonical, BodyDoc = new EvidenceDocument
            {
                Layout = template.Layout, Title = "Prepared document",
                Sections = [new EvidenceSection { Kind = "narrative", Text = "Original context." }]
            }
        };
        renderer.PrepareForReview([asset]);
        var draft = new CaseDraft { AssetFull = [asset] };
        var reviewed = draft.ToReviewJson();
        var copy = draft.Copy();
        renderer.PrepareForReview(copy.AssetFull);
        var result = await renderer.RenderAllAsync("case_quality", fixture.Root, copy.AssetFull);
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.PdfsWritten);
        Assert.Equal(canonical ? 0 : 1, template.Calls);
        Assert.Equal(canonical, copy.AssetFull[0].CanonicalContent);
        Assert.Equal(canonical ? "Original context." : RepairedFact, documents.Rendered!.Sections.Last().Text);
        if (canonical) Assert.DoesNotContain(RepairedFact, reviewed);
        else Assert.Contains(RepairedFact, reviewed);
        Assert.Equal(reviewed, draft.ToReviewJson());
    }

    private static bool IsCandidate(string user) => user.Contains("\"revision\":1", StringComparison.Ordinal);

    private static JsonObject RepairEvidence(JsonObject snapshot)
    {
        var components = snapshot["components"]!;
        var input = components["asset.access_log"]!.DeepClone();
        input["bodyDoc"]!["sections"]![0]!["text"] = RepairedRecord;
        Reanchor(input["support"]!, RepairedRecord);
        var report = components["asset.report_access"]!.DeepClone();
        report["body"] = RepairedFact;
        report["description"] = RepairedFact;
        Reanchor(report["support"]!, RepairedFact);
        var outcome = components["asset.access_log:DigitalForensics"]!.DeepClone();
        outcome["conclusionText"] = RepairedFact;
        Reanchor(outcome["support"]!, RepairedFact);
        var email = components["email.lab_access"]!.DeepClone();
        email["body"] = RepairedFact;
        Reanchor(email["support"]!, RepairedFact);
        var solution = components["solution"]!.DeepClone();
        solution["explanation"] = RepairedExplanation;
        foreach (var support in solution["explanationSupport"]!.AsArray())
            support!["excerpt"] = support["referenceId"]!.GetValue<string>() == "fact.record" ? RepairedRecord : RepairedFact;
        return new JsonObject
        {
            ["baseRevision"] = snapshot["revision"]!.GetValue<int>(),
            ["changes"] = new JsonArray(Change("asset.access_log", input), Change("asset.report_access", report),
                Change("asset.access_log:DigitalForensics", outcome), Change("email.lab_access", email), Change("solution", solution),
                Change("briefing", components["briefing"]!.DeepClone()))
        };
    }

    private static void Reanchor(JsonNode support, string excerpt)
    {
        foreach (var entry in support.AsArray()) entry!["excerpt"] = excerpt;
    }

    private static JsonObject Change(string target, JsonNode value) => new()
    {
        ["target"] = target, ["reason"] = "Reconcile the independently recorded fact.", ["value"] = value
    };

    private static JsonObject CorruptRepair(JsonObject snapshot, string defect)
    {
        var repair = RepairEvidence(snapshot);
        var changes = repair["changes"]!.AsArray();
        JsonNode Value(string target) => changes.Single(c => c!["target"]!.GetValue<string>() == target)!["value"]!;
        switch (defect)
        {
            case "unknown_culprit": Value("solution")["culpritId"] = "suspect.unknown"; break;
            case "different_culprit": Value("solution")["culpritId"] = "suspect.riley"; break;
            case "asset_identity": Value("asset.report_access")["id"] = "asset.new_report"; break;
            case "suspect_identity":
            case "partial_rename":
                var suspect = snapshot["components"]!["suspect.jordan"]!.DeepClone();
                suspect[defect == "suspect_identity" ? "id" : "name"] = defect == "suspect_identity" ? "suspect.unknown" : "Another name";
                changes.Add(Change("suspect.jordan", suspect));
                break;
            case "table_width":
                Value("asset.access_log")["bodyDoc"]!["sections"] = JsonNode.Parse("""
                    [{"kind":"table","heading":null,"table":{"columns":["Time","Event"],"rows":[["08:15"]],"footnote":null}}]
                    """);
                break;
            case "missing_document": Value("asset.access_log")["bodyDoc"] = null; break;
            case "broken_nested_rule":
                var rules = snapshot["components"]!["rules"]!.DeepClone();
                var child = rules[0]!["trigger"]!.DeepClone();
                child["inputAssetId"] = "asset.unknown";
                var parent = rules[0]!["trigger"]!.DeepClone();
                parent["type"] = "multiple_conditions";
                parent["inputAssetId"] = null;
                parent["analysisType"] = null;
                parent["operator"] = "AND";
                parent["conditions"] = new JsonArray(child, child.DeepClone());
                rules[0]!["trigger"] = parent;
                changes.Add(Change("rules", rules));
                break;
            case "missing_answer": Value("solution")["questions"]![0]!["correctOptionId"] = "opt.unknown"; break;
            case "duplicate_options":
                Value("solution")["questions"]![0]!["options"]![1]!["id"] =
                    Value("solution")["questions"]![0]!["options"]![0]!["id"]!.GetValue<string>();
                break;
            case "new_forensic_match": Value("asset.access_log:DigitalForensics")["matchedSuspectId"] = "suspect.riley"; break;
            case "fabricated_findings": Value("asset.access_log:DigitalForensics")["findings"] = false; break;
            case "missing_dependency": changes.RemoveAt(3); break;
            case "unknown_target": changes[0]!["target"] = "asset.unknown"; break;
            case "duplicate_target": changes.Add(changes[0]!.DeepClone()); break;
            case "wrong_revision": repair["baseRevision"] = 99; break;
            case "scoring": Value("solution")["minimumScore"] = 0.1; break;
            case "missing_email_dependency":
                repair["changes"] = new JsonArray(changes[3]!.DeepClone());
                break;
            case "noop":
                repair["changes"] = new JsonArray(Change("metadata", snapshot["components"]!["metadata"]!.DeepClone()));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        return repair;
    }

    private sealed class CountingTemplate : IEvidenceTemplate
    {
        public string Layout => "GeneralReport";
        public int Calls { get; private set; }
        public EvidenceDocument Enrich(EvidenceDocument doc)
        {
            Calls++;
            doc.Sections.Add(new EvidenceSection { Kind = "narrative", Text = RepairedFact });
            return doc;
        }
    }

    private sealed class RecordingDocumentRenderer : IEvidenceDocumentRenderer
    {
        public EvidenceDocument? Rendered { get; private set; }
        public byte[] Render(EvidenceDocument doc)
        {
            Rendered = doc;
            return System.Text.Encoding.ASCII.GetBytes("Prepared PDF test artifact.");
        }
    }
}

public sealed class CanonicalDraftFixture : IAsyncLifetime
{
    public CaseDraft Draft { get; private set; } = new();

    public async Task InitializeAsync()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.BeforeConsistency = draft => Draft = draft.Copy();
        var response = await fixture.GenerateAsync(writeToDisk: false);
        Assert.True(response.Quality.Approved);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
