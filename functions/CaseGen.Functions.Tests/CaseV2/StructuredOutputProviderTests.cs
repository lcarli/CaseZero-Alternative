using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class StructuredOutputProviderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredMode_UsesTheActualSdkResponseFormat(bool native)
    {
        using var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, native);
        var response = await provider.GenerateStructuredResponseAsync("system", "user", MicrotaskSchemas.Question);
        Assert.Equal("{}", response.Content);
        var request = JsonNode.Parse(Assert.Single(handler.Requests))!;
        if (native)
        {
            Assert.Equal("json_schema", request["response_format"]!["type"]!.GetValue<string>());
            Assert.True(request["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());
            var schema = request["response_format"]!["json_schema"]!["schema"]!;
            Assert.Null(schema["properties"]!["id"]!["pattern"]);
            Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        }
        else
            Assert.Null(request["response_format"]);
    }

    [Fact]
    public async Task OpenCanonicalSchema_UsesAnExplicitPromptPathWithoutExtraCalls()
    {
        using var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"));
        await CreateProvider(client, true).GenerateStructuredResponseAsync("system", "user", schema);
        Assert.Null(JsonNode.Parse(Assert.Single(handler.Requests))!["response_format"]);
    }

    [Fact]
    public async Task HarnessCeilingReachesTheActualSdkThroughTheProductionUsageWrapper()
    {
        using var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var inner = CreateProvider(client, true);
        var provider = new UsageTrackingLLMProvider(inner);
        Assert.Equal(inner.GenerationIdentity, provider.GenerationIdentity);
        using var runtime = CaseGenerationRuntime.Begin(new() { Difficulty = "Rookie" }, provider, default);
        runtime.Report.OutputTokens = runtime.Report.Budget.MaximumOutputTokens - 17;
        await CaseGenerationRuntime.StructuredAsync(provider, NullLogger.Instance, "Question", "system", "user", MicrotaskSchemas.Question, 1, default);
        var request = JsonNode.Parse(Assert.Single(handler.Requests))!;
        var limit = request["max_completion_tokens"] ?? request["max_tokens"];
        Assert.NotNull(limit);
        Assert.Equal(17, limit!.GetValue<int>());
        Assert.Equal(10, Assert.Single(runtime.Report.Tasks).OutputTokens);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("```json\n{}\n```")]
    [InlineData("""{"text":"first","text":"second"}""")]
    [InlineData("""{"$schema":"unexpected","text":"value"}""")]
    public async Task Provider_DoesNotExtractRepairOrReplaceInvalidJson(string content)
    {
        using var handler = new CapturingHandler { Content = content };
        using var client = new HttpClient(handler);
        var response = await CreateProvider(client, false).GenerateStructuredResponseAsync("system", "user", MicrotaskSchemas.Question);
        Assert.Equal(content, response.Content);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("", "stop", null)]
    [InlineData("{}", "length", null)]
    [InlineData("{}", "content_filter", null)]
    [InlineData("", "stop", "Synthetic refusal")]
    public async Task EmptyTruncatedOrRefusedCompletion_IsAnExplicitFailure(string content, string reason, string? refusal)
    {
        using var handler = new CapturingHandler { Content = content, FinishReason = reason, Refusal = refusal };
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProvider(client, true)
            .GenerateStructuredResponseAsync("system", "user", MicrotaskSchemas.Question));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SdkRetryPolicy_DoesNotMultiplyTheRunnerBudget()
    {
        using var handler = new CapturingHandler { Status = HttpStatusCode.ServiceUnavailable };
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, true);
        await Assert.ThrowsAsync<ClientResultException>(() => TaskRunner.RunStructuredAsync<JsonElement>(
            provider, NullLogger.Instance, "SdkBudget", "system", "user", MicrotaskSchemas.Question, default));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void MicrotasksUseCompatibleNativeSchemasOrAnExplicitCanonicalBibleFallback()
    {
        foreach (var property in typeof(MicrotaskSchemas).GetProperties())
        {
            var original = (string)property.GetValue(null)!;
            if (!StructuredOutputSchema.TryCreate(original, out var native, out var reason))
            {
                Assert.Equal(nameof(MicrotaskSchemas.CaseBible), property.Name);
                Assert.Contains("100-property/five-object-level", reason);
                continue;
            }
            StructuredJson.ParseSchema(native);
            Assert.DoesNotContain("\"pattern\"", native);
            Assert.DoesNotContain("\"minItems\"", native);
            Assert.DoesNotContain("\"oneOf\"", native);
        }
    }

    [Fact]
    public void CanonicalSchema_IsLinkedIntoBuildOutputWithoutChangingThePublicContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "schemas", "case.schema.json")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        Assert.Equal(File.ReadAllText(Path.Combine(directory!.FullName, "schemas", "case.schema.json")),
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json")));
    }

    private static AzureFoundryLLMProvider CreateProvider(HttpClient client, bool native)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureFoundry:Endpoint"] = "https://casegen.invalid/v1",
            ["AzureFoundry:ModelName"] = "test-text",
            ["AzureFoundry:ImageDeploymentName"] = "test-image",
            ["AzureFoundry:ApiKey"] = "synthetic-test-key",
            ["AzureFoundry:UseStructuredOutputs"] = native.ToString()
        }).Build();
        return new AzureFoundryLLMProvider(configuration, NullLogger<AzureFoundryLLMProvider>.Instance,
            new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(client) });
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public string Content { get; set; } = "{}";
        public string FinishReason { get; set; } = "stop";
        public string? Refusal { get; set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var json = Status == HttpStatusCode.OK
                ? JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-test", @object = "chat.completion", created = 1, model = "test-text",
                    choices = new[]
                    {
                        new { index = 0, finish_reason = FinishReason, message = new { role = "assistant", content = Content, refusal = Refusal } }
                    },
                    usage = new { prompt_tokens = 10, completion_tokens = 10, total_tokens = 20 }
                })
                : """{"error":{"message":"Synthetic unavailable provider","type":"test_error","code":"Unavailable"}}""";
            return new HttpResponseMessage(Status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
