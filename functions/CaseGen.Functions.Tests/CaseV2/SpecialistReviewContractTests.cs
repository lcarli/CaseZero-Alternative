using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class SpecialistReviewContractTests(CanonicalDraftFixture baseline) : IClassFixture<CanonicalDraftFixture>
{
    [Theory]
    [InlineData("reject", false)]
    [InlineData("reject", true)]
    [InlineData("needs_review", false)]
    [InlineData("needs_review", true)]
    public async Task ExplicitReviewerVetoSurvivesLowOrEmptyFindings(string verdict, bool includeLowFinding)
    {
        var provider = new TestCaseLlmProvider
        {
            ReviewContent = JsonSerializer.Serialize(new
            {
                verdict,
                findings = includeLowFinding
                    ? new[] { new { severity = "low", area = "document", issue = "A minor presentation concern.", reference = "asset.initial_report" } }
                    : []
            })
        };
        var draft = baseline.Draft.Copy();
        var review = await new RedTeamTask(provider, NullLogger.Instance).RunAsync(draft, "{}", default);

        Assert.True(review.Succeeded, review.ErrorMessage);
        Assert.Equal(verdict, review.Verdict);
        Assert.Equal(includeLowFinding ? 1 : 0, review.Findings.Count);
        Assert.All(review.Findings, finding =>
        {
            Assert.Equal("low", finding.Severity);
            Assert.False(finding.Blocking);
        });
        var quality = CaseQualityGate.Evaluate(draft.Request, false, [], [], review, draft.Revision);
        Assert.False(quality.Approved);
        Assert.Contains(quality.Issues, issue =>
            issue.Code == (verdict == "reject" ? "red_team_rejected" : "red_team_needs_review"));
    }

    [Fact]
    public async Task ExplicitApprovalOfAValidDraftStillPasses()
    {
        var draft = baseline.Draft.Copy();
        var review = await new RedTeamTask(new TestCaseLlmProvider(), NullLogger.Instance)
            .RunAsync(draft, "{}", default);

        Assert.True(review.Succeeded, review.ErrorMessage);
        Assert.Equal("ok", review.Verdict);
        Assert.Empty(review.Findings);
        Assert.True(CaseQualityGate.Evaluate(draft.Request, false, [], [], review, draft.Revision).Approved);
    }

    [Theory]
    [InlineData("reject")]
    [InlineData("needs_review")]
    public async Task OneSpecialistVetoCannotBeOutvotedByOtherApprovals(string verdict)
    {
        var provider = new TestCaseLlmProvider
        {
            TransformResponse = (system, _, node) =>
            {
                if (system.Contains("`PlayerExperience`", StringComparison.Ordinal))
                {
                    node["verdict"] = verdict;
                    node["findings"] = new JsonArray(new JsonObject
                    {
                        ["severity"] = "low", ["constraintId"] = "presentation",
                        ["owningNodeId"] = "asset.initial_report",
                        ["message"] = "A minor presentation concern.", ["suggestedRepair"] = null
                    });
                }
                return node;
            }
        };
        var draft = baseline.Draft.Copy();
        var review = await new RedTeamTask(provider, NullLogger.Instance).RunAsync(draft, "{}", default);

        Assert.True(review.Succeeded, review.ErrorMessage);
        Assert.Equal(verdict, review.Verdict);
        Assert.Equal("low", Assert.Single(review.Findings).Severity);
        Assert.False(CaseQualityGate.Evaluate(draft.Request, false, [], [], review, draft.Revision).Approved);
    }

    [Theory]
    [InlineData("missing_verdict")]
    [InlineData("missing_findings")]
    [InlineData("unknown_verdict")]
    [InlineData("null_verdict")]
    [InlineData("wrong_case_verdict")]
    [InlineData("extra_field")]
    public async Task InvalidSpecialistOutputFailsTheRealClosedContract(string defect)
    {
        var provider = new TestCaseLlmProvider
        {
            TransformResponse = (system, _, node) =>
            {
                if (!system.Contains("specialist for an interactive detective case", StringComparison.Ordinal))
                    return node;
                switch (defect)
                {
                    case "missing_verdict": node.AsObject().Remove("verdict"); break;
                    case "missing_findings": node.AsObject().Remove("findings"); break;
                    case "unknown_verdict": node["verdict"] = "approve"; break;
                    case "null_verdict": node["verdict"] = null; break;
                    case "wrong_case_verdict": node["verdict"] = "OK"; break;
                    case "extra_field": node["approved"] = true; break;
                }
                return node;
            }
        };
        var draft = baseline.Draft.Copy();
        var review = await new RedTeamTask(provider, NullLogger.Instance).RunAsync(draft, "{}", default);

        Assert.False(review.Succeeded);
        Assert.Equal("red_team_invalid", review.ErrorCode);
        Assert.NotEqual("ok", review.Verdict);
        Assert.Contains(review.Findings, finding => finding.ConstraintId!.EndsWith(".reviewer_invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(review.Findings, finding => finding.Severity == "high");
        Assert.Equal(2 * Enum.GetValues<SpecialistReviewKind>().Length, provider.Calls.Count);
        var quality = CaseQualityGate.Evaluate(draft.Request, false, [], [], review, draft.Revision);
        Assert.False(quality.Approved);
        Assert.Contains(quality.Issues, issue => issue.Code == "red_team_invalid");
    }

    [Theory]
    [InlineData("""{"verdict":"ok"}""")]
    [InlineData("""{"findings":[]}""")]
    public async Task FakeAdapterDoesNotFillMissingApprovalOrFindings(string content)
    {
        var draft = baseline.Draft.Copy();
        var review = await new RedTeamTask(new TestCaseLlmProvider { ReviewContent = content }, NullLogger.Instance)
            .RunAsync(draft, "{}", default);

        Assert.False(review.Succeeded);
        Assert.Equal("red_team_invalid", review.ErrorCode);
        Assert.NotEqual("ok", review.Verdict);
    }

    [Theory]
    [InlineData("reject", ReviewSeverity.Low, "reject")]
    [InlineData("needs_review", ReviewSeverity.Low, "needs_review")]
    [InlineData("ok", ReviewSeverity.Low, "ok")]
    [InlineData("ok", ReviewSeverity.Medium, "needs_review")]
    [InlineData("ok", ReviewSeverity.High, "reject")]
    [InlineData(null, ReviewSeverity.Low, "needs_review")]
    public void FindingsMayEscalateButNeverDowngradeTheExplicitVerdict(
        string? verdict, ReviewSeverity severity, string expected)
    {
        var report = new SpecialistReport { Succeeded = true, ReviewerVerdict = verdict };
        report.Findings.Add(new SpecialistFinding(
            SpecialistReviewKind.DocumentFidelity, "presentation", "asset.initial_report",
            severity, "A finding at its actual severity.", null, false));

        Assert.Equal(expected, report.Verdict);
        Assert.Equal(severity, Assert.Single(report.Findings).Severity);
    }
}
