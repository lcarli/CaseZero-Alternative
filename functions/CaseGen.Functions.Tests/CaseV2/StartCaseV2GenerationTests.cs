using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using CaseGen.Functions.Functions.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class StartCaseV2GenerationTests
{
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("""{"difficulty":"InvalidRank"}""")]
    [InlineData("""{"language":"de-DE"}""")]
    [InlineData("""{"caseId":"../previous"}""")]
    public async Task InvalidRequest_Returns400WithoutTouchingDurable(string body)
    {
        using var context = new TestContext(default);
        var request = new TestRequest(context, Encoding.UTF8.GetBytes(body));
        // Invalid input must return before accessing the Durable dependency.
        var response = await new StartCaseV2GenerationFunction(NullLogger<StartCaseV2GenerationFunction>.Instance, new ConfigurationBuilder().Build())
            .Run(request, null!, context);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(response.Body);
        Assert.Equal("invalid_request", json.RootElement.GetProperty("error").GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task OversizedRequest_Returns413WithoutTouchingDurable()
    {
        using var context = new TestContext(default);
        var request = new TestRequest(context, Encoding.UTF8.GetBytes(new string('x', GenerationRequestValidator.MaxBodyBytes + 1)));
        var response = await new StartCaseV2GenerationFunction(NullLogger<StartCaseV2GenerationFunction>.Instance, new ConfigurationBuilder().Build())
            .Run(request, null!, context);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(response.Body);
        Assert.Equal("request_too_large", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CancelledStarter_DoesNotScheduleAJob()
    {
        using var context = new TestContext(new CancellationToken(true));
        var request = new TestRequest(context, Encoding.UTF8.GetBytes("{}"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new StartCaseV2GenerationFunction(NullLogger<StartCaseV2GenerationFunction>.Instance, new ConfigurationBuilder().Build()).Run(request, null!, context));
    }

    internal sealed class TestContext : FunctionContext, IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly CancellationToken _token;

        public TestContext(CancellationToken token)
        {
            _token = token;
            _services = new ServiceCollection().Configure<WorkerOptions>(options =>
                options.Serializer = new JsonObjectSerializer()).BuildServiceProvider();
            InstanceServices = _services;
        }

        public override string InvocationId => "test-invocation";
        public override string FunctionId => "test-function";
        public override TraceContext TraceContext => throw new NotSupportedException();
        public override BindingContext BindingContext => throw new NotSupportedException();
        public override RetryContext RetryContext => throw new NotSupportedException();
        public override IServiceProvider InstanceServices { get; set; }
        public override FunctionDefinition FunctionDefinition => throw new NotSupportedException();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IInvocationFeatures Features => throw new NotSupportedException();
        public override CancellationToken CancellationToken => _token;
        public void Dispose() => _services.Dispose();
    }

    private sealed class TestRequest(FunctionContext context, byte[] bytes) : HttpRequestData(context)
    {
        public override Stream Body { get; } = new MemoryStream(bytes);
        public override HttpHeadersCollection Headers { get; } = new();
        public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();
        public override Uri Url => new("https://casegen.invalid/api/cases/v2/generate");
        public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();
        public override string Method => "POST";
        public override HttpResponseData CreateResponse() => new TestResponse(FunctionContext);
    }

    private sealed class TestResponse(FunctionContext context) : HttpResponseData(context)
    {
        public override HttpStatusCode StatusCode { get; set; }
        public override HttpHeadersCollection Headers { get; set; } = new();
        public override Stream Body { get; set; } = new MemoryStream();
        public override HttpCookies Cookies => throw new NotSupportedException();
    }
}
