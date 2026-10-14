using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseProgressionTests
{
    [Fact]
    public void HiddenRequiredEvidenceWithoutAPath_IsRejectedNotPromoted()
    {
        var draft = Draft();
        var report = Validate(draft);
        Assert.Contains(report.Errors, e => e.Contains("unreachable"));
        Assert.Equal("hidden", draft.AssetFull.Single(a => a.Id == "asset.target").Visibility);
    }

    [Fact]
    public void CircularReveals_DoNotMakeEvidenceReachable()
    {
        var draft = Draft();
        draft.Rules.Add(Rule("r.cycle", new() { Type = "asset_viewed", AssetId = "asset.target" }, "asset.target"));
        Assert.Contains(Validate(draft).Errors, e => e.Contains("unreachable"));
    }

    [Fact]
    public void AndAcrossDifferentVisibleEvents_IsReachableLikeTheRuntime()
    {
        var draft = Draft();
        draft.Rules.Add(Rule("r.and", new()
        {
            Type = "multiple_conditions", Operator = "AND", Conditions = new()
            {
                new() { Type = "asset_viewed", AssetId = "asset.input" },
                new() { Type = "email_opened", EmailId = "email.briefing" }
            }
        }, "asset.target"));
        Assert.Empty(Validate(draft).Errors);
    }

    [Fact]
    public void NestedOrUnderAnd_OnlyNeedsOneReachableOrBranch()
    {
        var draft = Draft();
        draft.Rules.Add(Rule("r.nested", new()
        {
            Type = "multiple_conditions", Operator = "AND", Conditions = new()
            {
                new() { Type = "asset_viewed", AssetId = "asset.input" },
                new()
                {
                    Type = "multiple_conditions", Operator = "OR", Conditions = new()
                    {
                        new() { Type = "email_opened", EmailId = "email.briefing" },
                        new() { Type = "asset_viewed", AssetId = "asset.target" }
                    }
                }
            }
        }, "asset.target"));
        Assert.Empty(Validate(draft).Errors);
    }

    [Fact]
    public void ForensicResultRequiresReachableCompatibleInput()
    {
        var draft = Draft();
        draft.AnalysisTypes.Add(new() { Type = "Digital", DurationMinutes = 15, AvailableFor = new() { "digital" } });
        draft.ForensicFull.Add(new()
        {
            InputAssetId = "asset.input", AnalysisType = "Digital", Findings = true, ResultAssetId = "asset.target"
        });
        draft.RequiredAnalysisIds.Add("asset.input:Digital");
        Assert.Empty(Validate(draft).Errors);
        draft.AssetFull[0].Visibility = "hidden";
        var blocked = Validate(draft);
        Assert.Contains(blocked.Errors, e => e.Contains("required analysis"));
        Assert.Equal("hidden", draft.AssetFull[0].Visibility);
    }

    [Fact]
    public void TemporalPayloadCanRevealRequiredEvidence()
    {
        var draft = Draft();
        draft.TemporalEvents.Add(new()
        {
            Id = "event.report", Type = "reveal", TriggerAtMinutes = 10, Payload = new() { AssetId = "asset.target" }
        });
        Assert.Empty(Validate(draft).Errors);
    }

    [Fact]
    public void NestedOrphanReferences_AreRejected()
    {
        var draft = Draft();
        draft.Rules.Add(Rule("r.orphan", new()
        {
            Type = "multiple_conditions", Operator = "OR", Conditions = new()
            {
                new() { Type = "asset_viewed", AssetId = "asset.input" },
                new() { Type = "email_opened", EmailId = "email.missing" }
            }
        }, "asset.target"));
        Assert.Contains(Validate(draft).Errors, e => e.Contains("email.missing"));
    }

    [Fact]
    public void TemporalOrphanPayload_AreRejected()
    {
        var draft = Draft();
        draft.TemporalEvents.Add(new()
        {
            Id = "event.orphan", Type = "reveal", TriggerAtMinutes = 5, Payload = new() { AssetId = "asset.missing" }
        });
        Assert.Contains(Validate(draft).Errors, e => e.Contains("payload.assetId"));
    }

    [Fact]
    public void VisibleEmailAttachmentsCanProvideTheForensicInput()
    {
        var draft = Draft();
        draft.AssetFull[0].Visibility = "hidden";
        draft.FollowUpEmails.Add(new()
        {
            Id = "email.input", Visibility = "initial", Attachments = new() { "asset.input" }
        });
        draft.Rules.Add(Rule("r.attachment", new()
        {
            Type = "attachment_download", EmailId = "email.input", AssetId = "asset.input"
        }, "asset.target"));
        Assert.Empty(Validate(draft).Errors);
    }

    private static RulesRule Rule(string id, RulesTrigger condition, string asset) => new()
    {
        RuleId = id, Trigger = condition, Actions = new() { new() { Type = "reveal_asset", AssetId = asset } }
    };
    private static ConsistencyReport Validate(CaseDraft draft) =>
        new ConsistencyValidator(NullLogger<ConsistencyValidator>.Instance).Validate(draft);
    private static CaseDraft Draft() => new()
    {
        CaseId = "case_progression", CulpritId = "suspect.one",
        Metadata = new()
        {
            RequiredRank = "Detective", IncidentDate = "2024-01-01T07:00:00Z", OpenedAt = "2024-01-01T08:00:00Z"
        },
        SuspectFull = new() { new() { Id = "suspect.one" } },
        AssetFull = new()
        {
            new() { Id = "asset.input", Type = "digital", Visibility = "initial" },
            new() { Id = "asset.target", Type = "pdf", Visibility = "hidden" }
        },
        RequiredEvidenceIds = new() { "asset.target" }
    };
}
