using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class RuntimePlayerSimulatorTests
{
    [Fact]
    public void CompoundRulesUseCumulativeHistoryInsteadOfOnlyTheLatestEvent()
    {
        var draft = Draft();
        draft.AssetFull.Add(new() { Id = "asset.second", Type = "document", Title = "Second", Visibility = "initial" });
        draft.Rules.Add(new()
        {
            RuleId = "rule.cumulative", Trigger = new()
            {
                Type = "multiple_conditions", Operator = "AND", Conditions =
                [
                    new() { Type = "asset_viewed", AssetId = "asset.record" },
                    new() { Type = "asset_viewed", AssetId = "asset.second" }
                ]
            },
            Actions = [new() { Type = "reveal_asset", AssetId = "asset.hidden" }]
        });
        var simulator = Simulator(draft);
        simulator.Apply("inspect:asset.record");
        Assert.DoesNotContain(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden");
        simulator.Apply("inspect:asset.second");
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden");
    }

    [Fact]
    public void AnalysisCompletesOnlyAfterItsPublicDurationAndThenRevealsResults()
    {
        var draft = Draft();
        AddAnalysis(draft, findings: true);
        var simulator = Simulator(draft);
        simulator.Apply("analyze:asset.record:MetadataAnalysis");
        Assert.Empty(simulator.CompletedAnalyses);
        Assert.DoesNotContain(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden");
        Assert.Contains("completionAtMinutes", JsonSerializer.Serialize(simulator.Projection()));
        simulator.Apply("wait:12");
        Assert.Equal(12, simulator.GameMinutes);
        Assert.Contains("asset.record:MetadataAnalysis", simulator.CompletedAnalyses);
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden");
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "email:email.result");
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("fr-FR")]
    public void NoFindingsUsesTheSamePublicTemplateAndDoesNotRevealDeclaredPositiveResults(string language)
    {
        var draft = Draft();
        draft.Request.Language = language;
        AddAnalysis(draft, findings: false);
        var simulator = Simulator(draft);
        simulator.Apply("analyze:asset.record:MetadataAnalysis");
        simulator.Apply("wait:12");
        Assert.Contains("asset.record:MetadataAnalysis", simulator.CompletedAnalyses);
        Assert.DoesNotContain(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden" || action.Id == "email:email.result");
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "email:forensic-1-no-findings");
        simulator.Apply("email:forensic-1-no-findings");
        var expected = CaseEmailProjection.NoFindings(draft, 1, "MetadataAnalysis", "Record", 12);
        Assert.Equal(expected.Body, simulator.ReadableSource(expected.Id));
        Assert.Equal("2026-05-16T09:12:00.0000000+00:00", expected.SentAt);
        Assert.Empty(expected.Attachments);
    }

    [Fact]
    public void OpeningAnEmailDoesNotDownloadItsHiddenAttachment()
    {
        var draft = Draft();
        draft.FollowUpEmails.Add(new()
        {
            Id = "email.request", From = "Archive", Subject = "Record", Body = "Enclosed record.", Visibility = "initial",
            Attachments = ["asset.hidden"]
        });
        draft.Rules.Add(new()
        {
            RuleId = "rule.download", Trigger = new() { Type = "attachment_download", EmailId = "email.request", AssetId = "asset.hidden" },
            Actions = [new() { Type = "reveal_email", EmailId = "email.result" }]
        });
        var simulator = Simulator(draft);
        simulator.Apply("email:email.request");
        Assert.DoesNotContain(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden" || action.Id == "email:email.result");
        simulator.Apply("download:email.request:asset.hidden");
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden");
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "email:email.result");
    }

    [Fact]
    public void BriefingUsesTheSameFirstTwoInitialAttachmentsAsPublicAssembly()
    {
        var draft = Draft();
        draft.AssetFull.AddRange(
        [
            new() { Id = "asset.second", Type = "document", Title = "Second", Visibility = "initial" },
            new() { Id = "asset.third", Type = "document", Title = "Third", Visibility = "initial" }
        ]);
        var simulator = Simulator(draft);
        simulator.Apply("email:email.briefing");
        Assert.Equal(CaseEmailProjection.BriefingAttachments(draft),
            simulator.AvailableActions().Where(action => action.Kind == PlayerActionKind.DownloadAttachment)
                .Select(action => Assert.IsType<string>(action.TargetId)).ToList());
    }

    [Fact]
    public void DueTemporalEventsRevealBothPayloadEntitiesAndNotifyOnlyOnce()
    {
        var draft = Draft();
        draft.TemporalEvents.Add(new()
        {
            Id = "event.archive", TriggerAtMinutes = 8, Type = "memo",
            Payload = new() { AssetId = "asset.hidden", EmailId = "email.result", Message = "Archive arrived." }
        });
        var simulator = Simulator(draft);
        simulator.Apply("wait:8");
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "inspect:asset.hidden");
        Assert.Contains(simulator.AvailableActions(), action => action.Id == "email:email.result");
        simulator.Apply("inspect:asset.hidden");
        using var projection = JsonDocument.Parse(JsonSerializer.Serialize(simulator.Projection()));
        Assert.Single(projection.RootElement.GetProperty("notifications").EnumerateArray());
    }

    [Theory]
    [InlineData("Rookie", "Detective", true)]
    [InlineData("Detective", "Rookie", false)]
    public void AllInitialVisibilityFollowsTheLiveRequiredRankPolicy(string requiredRank, string difficulty, bool visible)
    {
        var draft = Draft();
        draft.Metadata.RequiredRank = requiredRank;
        draft.Metadata.Difficulty = difficulty;
        var simulator = Simulator(draft);
        Assert.Equal(visible, simulator.AvailableActions().Any(action => action.Id == "inspect:asset.hidden"));
        Assert.Equal(visible, simulator.AvailableActions().Any(action => action.Id == "email:email.result"));
    }

    [Fact]
    public void PresentationActionsAreAppliedAndUnknownActionsAreRejected()
    {
        var draft = Draft();
        draft.SuspectFull.Add(new() { Id = "suspect.clerk", Name = "Clerk" });
        draft.Rules.Add(new()
        {
            RuleId = "rule.presentation", Trigger = new() { Type = "asset_viewed", AssetId = "asset.record" },
            Actions =
            [
                new() { Type = "mark_alibi_verified", SuspectId = "suspect.clerk", Verified = true },
                new() { Type = "update_suspect_status", SuspectId = "suspect.clerk", Status = "cleared" },
                new() { Type = "send_notification", Message = "Record verified.", Level = "info" }
            ]
        });
        var simulator = Simulator(draft);
        simulator.Apply("inspect:asset.record");
        simulator.Apply("suspect:suspect.clerk");
        var projection = JsonSerializer.Serialize(simulator.Projection());
        Assert.Contains("\"AlibiVerified\":true", projection);
        Assert.Contains("\"status\":\"cleared\"", projection);
        Assert.Contains("Record verified.", projection);
        draft.Rules[0].Actions.Add(new() { Type = "unsupported" });
        var invalid = Simulator(draft);
        Assert.Throws<TaskContractException>(() => invalid.Apply("inspect:asset.record"));
    }

    private static CaseDraft Draft()
    {
        var draft = new CaseDraft
        {
            CaseId = "case_simulator",
            Request = new() { Difficulty = "Detective", RequiredRank = "Detective", Language = "en-US" },
            Metadata = new() { Difficulty = "Detective", RequiredRank = "Detective", OpenedAt = "2026-05-16T09:00:00Z" },
            AssetFull =
            [
                new() { Id = "asset.record", Type = "document", Title = "Record", Visibility = "initial" },
                new() { Id = "asset.hidden", Type = "document", Title = "Hidden", Visibility = "hidden" }
            ],
            ResultEmails = [new() { Id = "email.result", From = "Archive", Subject = "Result", Body = "Report ready.", Visibility = "hidden" }]
        };
        draft.Blueprint.Locale.PoliceAgency = "Records Lab";
        draft.Blueprint.Locale.InvestigatorName = "Investigator";
        return draft;
    }

    private static RuntimePlayerSimulator Simulator(CaseDraft draft) => new(draft,
        draft.AssetFull.Concat(draft.ResultAssets).ToDictionary(asset => asset.Id, asset => $"{asset.Title} readable text."));

    private static void AddAnalysis(CaseDraft draft, bool findings)
    {
        draft.AnalysisTypes.Add(new() { Type = "MetadataAnalysis", DurationMinutes = 12, AvailableFor = ["document"] });
        draft.ForensicFull.Add(new()
        {
            InputAssetId = "asset.record", AnalysisType = "MetadataAnalysis", Findings = findings,
            ResultAssetId = "asset.hidden", ResultEmailId = "email.result"
        });
    }
}
