using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class RedTeamTaskTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"verdict\":\"ok\"}")]
    [InlineData("{\"verdict\":\"ok\",\"findings\":null}")]
    [InlineData("{\"verdict\":\"ok\",\"findings\":[{\"severity\":\"medium\",\"area\":42,\"issue\":\"Contradiction\"}]}")]
    [InlineData("{\"verdict\":\"ok\",\"findings\":[{\"severity\":\"medium\",\"area\":\"timeline\",\"issue\":\" \"}]}")]
    [InlineData("{\"verdict\":\"ok\",\"findings\":[{\"severity\":\"critical\",\"area\":\"timeline\",\"issue\":\"Contradiction\"}]}")]
    [InlineData("{\"verdict\":\"ok\",\"findings\":[],\"Succeeded\":true}")]
    [InlineData("{\"verdict\":\"reject\",\"verdict\":\"ok\",\"findings\":[]}")]
    [InlineData("not JSON")]
    public async Task InvalidOrIncompleteReview_IsExplicitlyUnsuccessful(string content)
    {
        var report = await RunAsync(new TestCaseLlmProvider { ReviewContent = content });
        Assert.False(report.Succeeded);
        Assert.Equal("red_team_invalid", report.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(report.ErrorMessage));
    }

    [Fact]
    public async Task ValidReview_IsMarkedSuccessfulByApplication()
    {
        var report = await RunAsync(new TestCaseLlmProvider());
        Assert.True(report.Succeeded);
        Assert.Equal("ok", report.Verdict);
        Assert.Null(report.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderFailureOrTimeout_IsUnavailable(bool timeout)
    {
        var report = await RunAsync(new TestCaseLlmProvider
        {
            ReviewException = timeout ? new OperationCanceledException("Provider timeout") : new InvalidOperationException("Provider offline")
        });
        Assert.False(report.Succeeded);
        Assert.Equal("red_team_unavailable", report.ErrorCode);
        Assert.NotNull(report.ErrorMessage);
    }

    [Fact]
    public async Task RequestedCancellation_Propagates()
    {
        using var source = new CancellationTokenSource();
        var provider = new TestCaseLlmProvider { OnReview = source.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(provider, source.Token));
    }

    [Fact]
    public async Task UnexpectedFailure_PropagatesInsteadOfProducingApproval()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => RunAsync(new TestCaseLlmProvider
        {
            ReviewException = new NotSupportedException("Unexpected provider defect")
        }));
    }

    private static Task<RedTeamTask.Report> RunAsync(TestCaseLlmProvider provider, CancellationToken ct = default)
        => new RedTeamTask(provider, NullLogger.Instance).RunAsync(new CaseDraft(), "{}", ct);
}
