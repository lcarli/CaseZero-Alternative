using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class MicrotaskContractTests
{
    [Theory]
    [InlineData("empty_suspect", "**full profile**")]
    [InlineData("suspect_identity", "**full profile**")]
    [InlineData("asset_identity", "**detail card**")]
    [InlineData("empty_section", "**detail card**")]
    [InlineData("table_width", "**detail card**")]
    [InlineData("missing_document", "**detail card**")]
    [InlineData("bad_answer", "ONE multiple-choice question")]
    [InlineData("duplicate_options", "ONE multiple-choice question")]
    [InlineData("wrong_weight", "ONE multiple-choice question")]
    [InlineData("missing_culprit", "**outline**")]
    [InlineData("multiple_culprits", "**outline**")]
    [InlineData("duplicate_forensic_pair", "**canonical CaseBible**")]
    [InlineData("unsupported_analysis", "**canonical CaseBible**")]
    [InlineData("missing_result_email", "**detailed forensic outcome**")]
    [InlineData("missing_result_asset", "**detailed forensic outcome**")]
    [InlineData("unreachable_evidence", "**canonical CaseBible**")]
    [InlineData("unknown_analysis", "**canonical CaseBible**")]
    [InlineData("unknown_nested_trigger", "ONLY the narrative")]
    [InlineData("incomplete_action", "ONLY the narrative")]
    public async Task InvalidMicrotask_DoesNotReachReviewRenderingOrPublication(string defect, string target)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) => system.Contains(target) ? Corrupt(node, defect) : node;
        await Assert.ThrowsAsync<TaskContractException>(() => fixture.GenerateAsync());
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
        Assert.DoesNotContain(fixture.Llm.Calls, call => call.System.Contains("red-team reviewer"));
        var calls = fixture.Llm.Calls.Where(call => call.System.Contains(target)).ToList();
        Assert.Contains(calls, call => call.User.Contains("previous attempt failed validation"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidRankOrOrphanInput_FailsAtTheOrigin(bool orphan)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.InvalidDifficulty = !orphan;
        fixture.Llm.OrphanForensicInput = orphan;
        var error = await Assert.ThrowsAsync<TaskContractException>(() => fixture.GenerateAsync());
        Assert.StartsWith(orphan ? "CaseBible:" : "PlotOutline:", error.Message);
        Assert.DoesNotContain(fixture.Llm.Calls, call => call.System.Contains("**detailed forensic outcome**"));
        Assert.Equal(0, fixture.Publisher.Calls);
    }

    [Fact]
    public async Task NoFindingsOutcome_CannotFabricateLabResults()
    {
        var draft = new CaseDraft();
        var provider = new TestCaseLlmProvider();
        await Assert.ThrowsAsync<TaskContractException>(() => new ForensicOutcomeTask(provider, NullLogger.Instance)
            .RunAsync(draft, new ForensicOutcomeStub
            {
                InputAssetId = "asset.access_log", AnalysisType = "DigitalForensics", Findings = false
            }, default));
        Assert.Equal(2, provider.Calls.Count);
    }

    [Fact]
    public async Task InvalidRefine_IsUnsuccessfulAndDoesNotRetry()
    {
        var provider = new ScriptedLlmProvider((_, _) => Task.FromResult(new Services.LLMResponse { Content = "{}" }));
        var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"));
        var result = await new RefineCaseTask(provider, NullLogger.Instance).RunAsync(
            new CaseDraft(), Array.Empty<string>(), new RedTeamTask.Report(), schema, default);
        Assert.False(result.Succeeded);
        Assert.Single(provider.Calls);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task InvalidDirectRequest_DoesNotCallAnyModel()
    {
        using var fixture = new CaseGenerationTestFixture();
        await Assert.ThrowsAsync<GenerationRequestException>(() => fixture.GenerateAsync(
            request: new GenerateCaseV2Request { Difficulty = "InvalidRank" }));
        Assert.Empty(fixture.Llm.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
    }

    [Fact]
    public async Task CancelledGenerator_DoesNotCallAnyModel()
    {
        using var fixture = new CaseGenerationTestFixture();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.GenerateAsync(ct: new CancellationToken(true)));
        Assert.Empty(fixture.Llm.Calls);
    }

    [Fact]
    public async Task RefineUnexpectedFailure_PropagatesInsteadOfBecomingASuccessfulRepair()
    {
        var provider = new ScriptedLlmProvider((_, _) => throw new NotSupportedException("Provider defect"));
        await Assert.ThrowsAsync<NotSupportedException>(() => new RefineCaseTask(provider, NullLogger.Instance)
            .RunAsync(new CaseDraft(), Array.Empty<string>(), new RedTeamTask.Report(),
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json")), default));
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task RefineCancellation_PropagatesWithoutRetry()
    {
        var provider = new ScriptedLlmProvider((_, _) => throw new OperationCanceledException("Stopped"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RefineCaseTask(provider, NullLogger.Instance)
            .RunAsync(new CaseDraft(), Array.Empty<string>(), new RedTeamTask.Report(),
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json")), default));
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task EveryMicrotask_ReceivesALocallyEnforcedSchema()
    {
        using var fixture = new CaseGenerationTestFixture();
        var response = await fixture.GenerateAsync(writeToDisk: false);
        Assert.True(response.Quality.Approved);
        foreach (var call in fixture.Llm.Calls)
        {
            var root = JsonNode.Parse(call.Schema)!.AsObject();
            Assert.NotNull(root["properties"]);
            Assert.False(root["additionalProperties"]!.GetValue<bool>());
        }
    }

    private static JsonNode Corrupt(JsonNode node, string defect)
    {
        switch (defect)
        {
            case "empty_suspect": return new JsonObject { ["status"] = "generated" };
            case "suspect_identity": node["id"] = "suspect.foreign"; break;
            case "asset_identity": node["id"] = "asset.foreign"; break;
            case "empty_section":
                node["bodyDoc"]!["sections"] = JsonNode.Parse("""[{"kind":"narrative","heading":null}]"""); break;
            case "table_width":
                node["bodyDoc"]!["sections"] = JsonNode.Parse("""
                    [{"kind":"table","heading":null,"table":{"columns":["Time","Event"],"rows":[["08:15"]],"footnote":null}}]
                    """); break;
            case "missing_document": node["bodyDoc"] = null; node["body"] = "Markdown cannot replace the requested document."; break;
            case "bad_answer": node["correctOptionId"] = "opt.unknown"; break;
            case "duplicate_options": node["options"]![1]!["id"] = node["options"]![0]!["id"]!.GetValue<string>(); break;
            case "wrong_weight": node["weight"] = 0.5; break;
            case "missing_culprit": node["culpritId"] = "suspect.unknown"; break;
            case "multiple_culprits": node["suspects"]![1]!["isCulprit"] = true; break;
            case "duplicate_forensic_pair":
                node["outcomes"]!.AsArray().Add(node["outcomes"]![0]!.DeepClone()); break;
            case "unsupported_analysis": node["outcomes"]![0]!["analysisType"] = "UnknownAnalysis"; break;
            case "missing_result_email": node["resultEmail"] = null; break;
            case "missing_result_asset": node["resultAsset"] = null; break;
            case "unreachable_evidence": node["proofSets"]![0]!["sourceIds"]![0] = "asset.unknown"; break;
            case "unknown_analysis": node["proofSets"]![0]!["analysisIds"]![0] = "asset.access_log:UnknownAnalysis"; break;
            case "unknown_nested_trigger":
            case "incomplete_action":
                node["rules"] = JsonSerializer.SerializeToNode(new[]
                {
                    new RulesRule
                    {
                        RuleId = "rule.contract_test",
                        Trigger = new RulesTrigger
                        {
                            Type = "multiple_conditions", Operator = "AND",
                            Conditions = new()
                            {
                                new() { Type = "asset_viewed", AssetId = defect == "unknown_nested_trigger" ? "asset.unknown" : "asset.scene" },
                                new() { Type = "email_opened", EmailId = "email.briefing" }
                            }
                        },
                        Actions = new()
                        {
                            new() { Type = "send_notification", Level = "info", Message = defect == "incomplete_action" ? null : "Review the record." }
                        }
                    }
                }); break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        return node;
    }
}
