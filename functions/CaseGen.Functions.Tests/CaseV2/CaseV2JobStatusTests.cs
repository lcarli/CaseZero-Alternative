using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Functions.CaseV2;
using CaseGen.Functions.Models.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseV2JobStatusTests
{
    [Fact]
    public void CompletedWithLogicalFailure_IsFailedAndRetainsFullDiagnostics()
    {
        var response = new GenerateCaseV2Response { CaseId = "case_quality" };
        response.Quality.Block("asset_render_failed", "rendering", "PDF generation failed", "asset.scene");
        var result = CaseV2JobResult.FromResponse("job", response);
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(JsonSerializer.Serialize(result));
        Assert.True(result.HasErrors);
        Assert.Equal("failed", completed.Status);
        Assert.Contains("asset_render_failed", completed.Error);
        Assert.Equal("case_quality", completed.Result!.Value.GetProperty("CaseId").GetString());
        Assert.Equal("asset.scene", completed.Result.Value.GetProperty("Quality").GetProperty("Issues")[0].GetProperty("Reference").GetString());
    }

    [Fact]
    public void BlockingQuality_WinsEvenIfLegacyHasErrorsFlagIsFalse()
    {
        var result = JsonNode.Parse(JsonSerializer.Serialize(SuccessfulResult()))!;
        result["Quality"]!["Issues"] = new JsonArray(JsonSerializer.SerializeToNode(
            new CaseQualityIssue("red_team_finding", "medium", "timeline", null, "Conflicting dates", true)));
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(result.ToJsonString());
        Assert.Equal("failed", completed.Status);
        Assert.NotNull(completed.Result);
        Assert.False(string.IsNullOrWhiteSpace(completed.Error));
    }

    [Fact]
    public void ApprovedPreview_IsDoneWithoutClaimingPersistenceOrPublication()
    {
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(JsonSerializer.Serialize(SuccessfulResult()));
        Assert.Equal("done", completed.Status);
        Assert.Null(completed.Error);
        Assert.False(completed.Result!.Value.GetProperty("Quality").GetProperty("Published").GetBoolean());
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void InvalidOrWrongRevisionBundle_IsFailedEvenWithLegacySuccessFlags(bool verified, int revision)
    {
        var result = SuccessfulResult() with { Bundle = new CaseBundleReport { BundleId = "bundle", Verified = verified, Revision = revision } };
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(JsonSerializer.Serialize(result));
        Assert.Equal("failed", completed.Status);
        Assert.NotNull(completed.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoricalResultWithoutQuality_RetainsLegacyOutcome(bool errors)
    {
        var historical = SuccessfulResult() with { Quality = null, HasErrors = errors, ErrorMessage = errors ? "Original failure" : null };
        var json = JsonSerializer.SerializeToNode(historical)!.AsObject();
        json.Remove("Quality");
        json.Remove("AssetsSkipped");
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(json.ToJsonString());
        Assert.Equal(errors ? "failed" : "done", completed.Status);
        Assert.Equal(historical.ErrorMessage, completed.Error);
        Assert.NotNull(completed.Result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not JSON")]
    [InlineData("{}")]
    [InlineData("null")]
    public void MissingOrInvalidCompletedResult_IsNeverSuccess(string output)
    {
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(output);
        Assert.Equal("failed", completed.Status);
        Assert.False(string.IsNullOrWhiteSpace(completed.Error));
    }

    [Fact]
    public void InvalidQualityReport_IsAnExplicitFailure()
    {
        var result = JsonNode.Parse(JsonSerializer.Serialize(SuccessfulResult()))!;
        result["Quality"]!["Issues"] = null;
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(result.ToJsonString());
        Assert.Equal("failed", completed.Status);
        Assert.NotNull(completed.Result);
        Assert.Contains("invalid quality report", completed.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FailureWithoutMessage_StillHasUsefulError(string? message)
    {
        var result = SuccessfulResult() with { HasErrors = true, ErrorMessage = message };
        var completed = GetCaseV2JobStatusFunction.ResolveCompletedResult(JsonSerializer.Serialize(result));
        Assert.Equal("failed", completed.Status);
        Assert.False(string.IsNullOrWhiteSpace(completed.Error));
    }

    [Fact]
    public void ResponseMapping_PreservesLegacyErrorsWithoutDuplicatingQualityMessages()
    {
        var response = new GenerateCaseV2Response { CaseId = "case_quality" };
        response.ValidationErrors.AddRange(new[] { "Known schema error", "Additional validation error" });
        response.Quality.Block("schema_invalid", "schema", "Known schema error");
        var result = CaseV2JobResult.FromResponse("job", response);
        Assert.Contains("Additional validation error", result.ErrorMessage);
        Assert.Equal(1, result.ErrorMessage!.Split("Known schema error").Length - 1);
    }

    private static CaseV2JobResult SuccessfulResult() => CaseV2JobResult.FromResponse("job", new GenerateCaseV2Response
    {
        CaseId = "case_quality",
        Quality = new CaseQualityReport { Approved = true, Preview = true }
    });
}
