using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace CaseGen.Functions.Tests.CaseV2;

public class MicrotaskContractTests(CanonicalDraftFixture baseline, ITestOutputHelper output) : IClassFixture<CanonicalDraftFixture>
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
    [InlineData("wrong_presentation_id", "**outline**")]
    [InlineData("duplicate_presentation_ids", "**outline**")]
    [InlineData("duplicate_forensic_input_owner", "Plan the complete initial evidence dossier")]
    [InlineData("duplicate_forensic_pair", "**forensics layer**")]
    [InlineData("unsupported_analysis", "**forensics layer**")]
    [InlineData("missing_result_email", "**detailed forensic outcome**")]
    [InlineData("missing_result_asset", "**detailed forensic outcome**")]
    [InlineData("unreachable_evidence", "**skeleton**")]
    [InlineData("unknown_analysis", "**skeleton**")]
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
        Assert.DoesNotContain(fixture.Llm.Calls, call => call.System.Contains("specialist for an interactive detective case"));
        var calls = fixture.Llm.Calls.Where(call => call.System.Contains(target)).ToList();
        Assert.Contains(calls.Skip(1), call => call.User != calls[0].User && call.User.Contains("validation", StringComparison.OrdinalIgnoreCase));
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
        Assert.StartsWith(orphan ? "ForensicsPlan:" : "PlotOutline:", error.Message);
        Assert.DoesNotContain(fixture.Llm.Calls, call => call.System.Contains("**detailed forensic outcome**"));
        Assert.Equal(0, fixture.Publisher.Calls);
    }

    [Fact]
    public async Task NoFindingsOutcome_CannotFabricateLabResults()
    {
        var draft = baseline.Draft.Copy();
        var provider = new TestCaseLlmProvider();
        await Assert.ThrowsAsync<TaskContractException>(() => new ForensicOutcomeTask(provider, NullLogger.Instance)
            .RunAsync(draft, new ForensicOutcomeStub
            {
                InputAssetId = "asset.initial_report", AnalysisType = "MetadataAnalysis", Findings = false
            }, default));
        Assert.Equal(2, provider.Calls.Count);
    }

    [Fact]
    public async Task UnscopedRefineOfAnInvalidRootFailsWithoutCallingAModel()
    {
        var provider = new ScriptedLlmProvider((_, _) => Task.FromResult(new Services.LLMResponse { Content = "{}" }));
        var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"));
        var result = await new RefineCaseTask(provider, NullLogger.Instance).RunAsync(
            new CaseDraft(), Array.Empty<string>(), new RedTeamTask.Report(), schema, default);
        Assert.False(result.Succeeded);
        Assert.Empty(provider.Calls);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuestionRepairPreservesPublicChoicesWithoutExposingItsAnswerKey(bool changeOption)
    {
        var draft = baseline.Draft.Copy();
        var previous = draft.Questions[0];
        var provider = new TestCaseLlmProvider
        {
            TransformResponse = (_, _, node) =>
            {
                node["prompt"] = "Which exact statement is present in this recorded source?";
                if (changeOption) node["options"]![1]!["label"] = "A replacement distractor that changes the fixed choices.";
                return node;
            }
        };
        var task = new QuestionTask(provider, NullLogger.Instance);
        if (changeOption)
            await Assert.ThrowsAsync<TaskContractException>(() => task.RunAsync(draft, draft.QuestionTopics[0], default));
        else
        {
            var repaired = await task.RunAsync(draft, draft.QuestionTopics[0], default);
            Assert.NotEqual(previous.Prompt, repaired.Prompt);
            Assert.Equal(previous.CorrectOptionId, repaired.CorrectOptionId);
            Assert.Equal(previous.Options.Select(o => (o.Id, o.Label)), repaired.Options.Select(o => (o.Id, o.Label)));
        }
        foreach (var call in provider.Calls)
        {
            Assert.Contains("\"options\"", call.User);
            Assert.DoesNotContain("\"correctOptionId\"", call.User);
        }
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
            .RunAsync(baseline.Draft, Array.Empty<string>(), RepairFindings(),
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json")), default));
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task RefineCancellation_PropagatesWithoutRetry()
    {
        var provider = new ScriptedLlmProvider((_, _) => throw new OperationCanceledException("Stopped"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RefineCaseTask(provider, NullLogger.Instance)
            .RunAsync(baseline.Draft, Array.Empty<string>(), RepairFindings(),
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json")), default));
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task EveryMicrotask_ReceivesALocallyEnforcedSchema()
    {
        using var fixture = new CaseGenerationTestFixture();
        GenerateCaseV2Response response;
        try
        {
            response = await fixture.GenerateAsync(writeToDisk: false);
        }
        catch
        {
            var calls = fixture.Llm.Calls.ToArray();
            foreach (var group in calls.GroupBy(call => call.System.Contains("BlindPlayerInvestigator", StringComparison.Ordinal)
                ? "BlindPlayerInvestigator" : call.System.Split('\n').First(line => !string.IsNullOrWhiteSpace(line)).Trim()))
                output.WriteLine($"{group.Count()} calls: {group.Key}");
            throw;
        }
        Assert.True(response.Quality.Approved);
        foreach (var call in fixture.Llm.Calls)
        {
            var root = JsonNode.Parse(call.Schema)!.AsObject();
            Assert.NotNull(root["properties"]);
            Assert.False(root["additionalProperties"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task FakeProviderSuppliesExplicitConsistentUsageWithoutChangingMissingUsageCoverage()
    {
        var call = baseline.Provider.Calls.First();
        var provider = new TestCaseLlmProvider();
        var response = await provider.GenerateStructuredResponseAsync(call.System, call.User, call.Schema);
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.PromptTokens > 0);
        Assert.True(response.Usage.CompletionTokens > 0);
        Assert.Equal(response.Usage.PromptTokens + response.Usage.CompletionTokens, response.Usage.TotalTokens);
        provider.ReturnTokenUsage = false;
        Assert.Null((await provider.GenerateStructuredResponseAsync(call.System, call.User, call.Schema)).Usage);
    }

    [Fact]
    public async Task FakeRendererReturnsTheSameExplicitReadableTextAtPreparationAndFinalRendering()
    {
        using var fixture = new CaseGenerationTestFixture();
        var assets = baseline.Draft.AssetFull.Concat(baseline.Draft.ResultAssets).ToList();
        var prepared = await fixture.Renderer.ReadPreparedDocumentsAsync(assets);
        var rendered = await fixture.Renderer.RenderAllAsync(baseline.Draft.CaseId, fixture.Root, assets);
        Assert.NotEmpty(prepared);
        Assert.Equal(prepared.OrderBy(pair => pair.Key), rendered.ReadableTexts.OrderBy(pair => pair.Key));
    }

    private static JsonNode Corrupt(JsonNode node, string defect)
    {
        if (defect is "empty_section" or "table_width" or "missing_document"
            && node["type"]?.GetValue<string>() == "photo") return node;
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
            case "wrong_presentation_id": node["clueOrderIds"]![0] = "clue.unknown"; break;
            case "duplicate_presentation_ids": node["clueOrderIds"]![1] = node["clueOrderIds"]![0]!.GetValue<string>(); break;
            case "duplicate_forensic_input_owner":
                var assets = node["assets"]!.AsArray();
                var owner = assets.Single(a => a!["forensicInputClueIds"]!.AsArray().Count > 0)!;
                var duplicate = assets.First(a => a != owner && a!["evidenceRole"]!.GetValue<string>() != "contextual")!;
                duplicate["forensicInputClueIds"] = owner["forensicInputClueIds"]!.DeepClone();
                break;
            case "duplicate_forensic_pair":
                node["outcomes"]!.AsArray().Add(node["outcomes"]![0]!.DeepClone()); break;
            case "unsupported_analysis": node["outcomes"]![0]!["analysisType"] = "UnknownAnalysis"; break;
            case "missing_result_email": node["resultEmail"] = null; break;
            case "missing_result_asset": node["resultAsset"] = null; break;
            case "unreachable_evidence": node["requiredEvidenceIds"]![0] = "asset.unknown"; break;
            case "unknown_analysis": node["requiredAnalysisIds"]![0] = "asset.initial_report:UnknownAnalysis"; break;
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
                                new() { Type = "asset_viewed", AssetId = defect == "unknown_nested_trigger" ? "asset.unknown" : "asset.initial_report" },
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

    private static RedTeamTask.Finding[] RepairFindings() =>
        [new() { Severity = "medium", OwnerStage = "assetCards", NodeId = "asset.initial_report", Issue = "Clarify source presentation." }];
}
