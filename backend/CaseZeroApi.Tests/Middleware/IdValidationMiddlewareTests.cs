using CaseZeroApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseZeroApi.Tests.Middleware;

public class IdValidationMiddlewareTests
{
    [Theory]
    [InlineData("forensic-1-no-findings")]
    [InlineData("forensic-42-no-findings")]
    [InlineData("forensic-2147483647-no-findings")]
    [InlineData("email.initial_report")]
    [InlineData("email.Legacy_1-Result")]
    [InlineData("no-findings-fd339bd3-2df3-4504-9971-f2f305535705")]
    [InlineData("no-findings-a")]
    public async Task EmailId_AllowsProducedAndExistingPublicFormats(string emailId)
    {
        var (status, reachedEndpoint) = await InvokeAsync(emailId);

        Assert.True(reachedEndpoint);
        Assert.Equal(StatusCodes.Status204NoContent, status);
    }

    [Theory]
    [InlineData("forensic--no-findings")]
    [InlineData("forensic-0-no-findings")]
    [InlineData("forensic-01-no-findings")]
    [InlineData("forensic--1-no-findings")]
    [InlineData("forensic-+1-no-findings")]
    [InlineData("forensic-2147483648-no-findings")]
    [InlineData("forensic-999999999999999999999-no-findings")]
    [InlineData("forensic-1.0-no-findings")]
    [InlineData("forensic-1e2-no-findings")]
    [InlineData("forensic- 1-no-findings")]
    [InlineData("forensic-١-no-findings")]
    [InlineData("forensic-fd339bd3-2df3-4504-9971-f2f305535705-no-findings")]
    [InlineData("Forensic-1-no-findings")]
    [InlineData("forensic-1-No-Findings")]
    [InlineData("forensic-1-no-finding")]
    [InlineData("prefix-forensic-1-no-findings")]
    [InlineData("forensic-1-no-findings-extra")]
    [InlineData("forensic-1-no-findings\n")]
    [InlineData("forensic-1\n-no-findings")]
    [InlineData("forensic-1-no-findings/asset.extra")]
    [InlineData("forensic-../1-no-findings")]
    [InlineData("forensic-1-no-findings?extra")]
    [InlineData("forensic-1-no-findings#fragment")]
    [InlineData("forensic-1-no-findings\0")]
    public async Task EmailId_RejectsSyntheticLookalikes(string emailId)
    {
        var (status, reachedEndpoint) = await InvokeAsync(emailId);

        Assert.False(reachedEndpoint);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Theory]
    [InlineData("assetId")]
    [InlineData("suspectId")]
    [InlineData("caseId")]
    [InlineData("attachmentId")]
    public async Task SyntheticEmail_DoesNotBypassOtherRouteValidation(string routeParameter)
    {
        var (status, reachedEndpoint) = await InvokeAsync("forensic-1-no-findings", routeParameter, "invalid/value");

        Assert.False(reachedEndpoint);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    private static async Task<(int Status, bool ReachedEndpoint)> InvokeAsync(
        string emailId, string? routeParameter = null, string? routeValue = null)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        context.Request.RouteValues["emailId"] = emailId;
        if (routeParameter is not null) context.Request.RouteValues[routeParameter] = routeValue;
        var reachedEndpoint = false;
        var middleware = new IdValidationMiddleware(_ =>
        {
            reachedEndpoint = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, NullLogger<IdValidationMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, reachedEndpoint);
    }
}
