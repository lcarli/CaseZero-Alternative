using CaseGen.Functions.Services.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class GenerationProgressTests
{
    [Theory]
    [InlineData("caseBible", "caseDesign")]
    [InlineData("plotOutline", "caseDesign")]
    [InlineData("assetsAndTimelineAndBriefing", "evidenceProduction")]
    [InlineData("redTeamAndSolver", "solutionWitness")]
    [InlineData("targetedRepair", "targetedRepair")]
    [InlineData("publishToBlob", "finalization")]
    public void InternalPhases_MapToStablePublicStages(string internalPhase, string expectedStage)
    {
        Assert.Equal(expectedStage, GenerationProgressCatalog.MapInternalPhase(internalPhase));
    }

    [Fact]
    public void AdvancingStages_IsMonotonicAndMarksBypassedStagesSkipped()
    {
        var now = DateTimeOffset.Parse("2026-07-22T12:00:00Z");
        var status = JobPhaseStatus.Started("job-1", now);

        status.StartStage("assetsAndTimelineAndBriefing", now.AddSeconds(1), retry: false);
        var evidenceProgress = status.ProgressPercent;
        status.StartStage("mechanicalRules", now.AddSeconds(2), retry: false);

        Assert.True(status.ProgressPercent >= evidenceProgress);
        Assert.Equal("skipped", status.Stages.Single(stage => stage.Id == "forensicWorkflow").Status);
        Assert.Equal("running", status.Stages.Single(stage => stage.Id == "solutionDesign").Status);
    }

    [Fact]
    public void TargetedRepair_IncrementsAttemptsWithoutResettingProgress()
    {
        var now = DateTimeOffset.Parse("2026-07-22T12:00:00Z");
        var status = JobPhaseStatus.Started("job-2", now);

        status.StartStage("targetedRepair", now.AddSeconds(1), retry: true);
        var firstProgress = status.ProgressPercent;
        status.StartStage("targetedRepair", now.AddSeconds(2), retry: true);

        var repair = status.Stages.Single(stage => stage.Id == "targetedRepair");
        Assert.Equal(2, repair.Attempt);
        Assert.Equal("running", repair.Status);
        Assert.Equal(firstProgress, status.ProgressPercent);
    }

    [Fact]
    public void TerminalStates_PreserveHistoryAndSetExpectedProgress()
    {
        var now = DateTimeOffset.Parse("2026-07-22T12:00:00Z");
        var completed = JobPhaseStatus.Started("job-3", now);
        completed.StartStage("finalValidation", now.AddSeconds(1), retry: false);
        completed.Complete(now.AddSeconds(2));

        Assert.Equal("done", completed.Status);
        Assert.Equal(100, completed.ProgressPercent);
        Assert.All(completed.Stages, stage => Assert.Contains(stage.Status, new[] { "completed", "skipped" }));

        var failed = JobPhaseStatus.Started("job-4", now);
        failed.StartStage("solutionWitness", now.AddSeconds(1), retry: false);
        var progressBeforeFailure = failed.ProgressPercent;
        failed.Fail("witness failed", now.AddSeconds(2));

        Assert.Equal("failed", failed.Status);
        Assert.Equal("failed", failed.Stages.Single(stage => stage.Id == "solutionWitness").Status);
        Assert.Equal(progressBeforeFailure, failed.ProgressPercent);
        Assert.Equal("witness failed", failed.Error);
    }
}
