using System.Text.Json.Serialization;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class TaskRunnerTests
{
    private const string Schema = """
        {"type":"object","additionalProperties":false,"required":["text"],
         "properties":{"text":{"type":"string","minLength":1,"pattern":"\\S"}}}
        """;

    public sealed class Output
    {
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }

    [Theory]
    [InlineData("""{"status":"generated"}""")]
    [InlineData("""{"text":null}""")]
    [InlineData("""{"text":123}""")]
    [InlineData("""{"text":" "}""")]
    [InlineData("""{"text":"first","text":"second"}""")]
    [InlineData("""{"text":"valid","extra":true}""")]
    [InlineData("""{"Text":"valid"}""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("not JSON")]
    [InlineData("```json\n{\"text\":\"valid\"}\n```")]
    [InlineData("{\"text\":\"valid\"} trailing")]
    public async Task InvalidResponses_AreRejectedBeforeDeserialization(string content)
    {
        var llm = new ScriptedLlmProvider((_, _) => Task.FromResult(new LLMResponse { Content = content }));
        var error = await Assert.ThrowsAsync<TaskContractException>(() => Run(llm));
        Assert.StartsWith("ContractTest:", error.Message);
        Assert.Equal(2, llm.Calls.Count);
        Assert.Contains("previous attempt failed validation", llm.Calls[1].User);
    }

    [Fact]
    public async Task RepairRetry_ContainsSpecificErrorAndReturnsOnlyValidatedOutput()
    {
        var llm = new ScriptedLlmProvider((call, _) => Task.FromResult(new LLMResponse
        {
            Content = call.User.Contains("previous attempt") ? """{"text":"corrected"}""" : "{}"
        }));
        var output = await Run(llm);
        Assert.Equal("corrected", output.Text);
        Assert.Equal(2, llm.Calls.Count);
        Assert.Contains("text", llm.Calls[1].User);
        Assert.Contains("Preserve the supplied IDs", llm.Calls[1].User);
    }

    [Fact]
    public async Task SemanticFailure_IsRetriedWithoutMutatingTheOutput()
    {
        var llm = new ScriptedLlmProvider((_, _) => Task.FromResult(new LLMResponse { Content = """{"text":"wrong"}""" }));
        await Assert.ThrowsAsync<TaskContractException>(() => TaskRunner.RunStructuredAsync<Output>(
            llm, NullLogger.Instance, "Semantic", "system", "user", Schema, default,
            validate: value => TaskContractException.Require(value.Text == "expected", "text must echo expected.")));
        Assert.Contains("text must echo expected", llm.Calls[1].User);
    }

    [Theory]
    [InlineData(400, 1)]
    [InlineData(401, 1)]
    [InlineData(403, 1)]
    [InlineData(408, 2)]
    [InlineData(429, 2)]
    [InlineData(503, 2)]
    public async Task OnlyTransientProviderErrors_Retry(int status, int expectedCalls)
    {
        var llm = new ScriptedLlmProvider((_, _) => throw new Azure.RequestFailedException(status, "Synthetic failure"));
        await Assert.ThrowsAsync<Azure.RequestFailedException>(() => Run(llm));
        Assert.Equal(expectedCalls, llm.Calls.Count);
        Assert.All(llm.Calls, call => Assert.Equal("user", call.User));
    }

    [Fact]
    public async Task PreCancelledToken_NeverCallsTheProvider()
    {
        var llm = new ScriptedLlmProvider((_, _) => throw new InvalidOperationException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(llm, new CancellationToken(true)));
        Assert.Empty(llm.Calls);
    }

    [Fact]
    public async Task CancellationDuringResponse_NeverStartsRetry()
    {
        using var source = new CancellationTokenSource();
        var llm = new ScriptedLlmProvider((_, _) =>
        {
            source.Cancel();
            return Task.FromResult(new LLMResponse { Content = "{}" });
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(llm, source.Token));
        Assert.Single(llm.Calls);
    }

    [Fact]
    public async Task ProviderCancellationWithoutCallerCancellation_IsNotRetried()
    {
        var llm = new ScriptedLlmProvider((_, _) => throw new OperationCanceledException("Provider stopped"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(llm));
        Assert.Single(llm.Calls);
    }

    [Fact]
    public async Task UnexpectedFailure_IsNotRetried()
    {
        var llm = new ScriptedLlmProvider((_, _) => throw new NotSupportedException("Provider defect"));
        await Assert.ThrowsAsync<NotSupportedException>(() => Run(llm));
        Assert.Single(llm.Calls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task InvalidRetryBudget_NeverCallsTheProvider(int retries)
    {
        var llm = new ScriptedLlmProvider((_, _) => throw new InvalidOperationException());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => TaskRunner.RunStructuredAsync<Output>(
            llm, NullLogger.Instance, "ContractTest", "system", "user", Schema, default, maxRetries: retries));
        Assert.Empty(llm.Calls);
    }

    [Fact]
    public async Task OversizedResponse_IsNotAccepted()
    {
        var llm = new ScriptedLlmProvider((_, _) => Task.FromResult(new LLMResponse
        {
            Content = "{\"text\":\"" + new string('x', StructuredJson.MaxResponseBytes) + "\"}"
        }));
        var error = await Assert.ThrowsAsync<TaskContractException>(() => Run(llm));
        Assert.Contains("exceeds", error.Message);
        Assert.Equal(2, llm.Calls.Count);
    }

    [Fact]
    public void NestedDuplicateProperties_AreNotHiddenByParsing()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => StructuredJson.ParseObject(
            """{"nested":{"id":"first","id":"second"}}"""));
    }

    [Fact]
    public void SchemaValidation_HasNoHourlyValidationQuota()
    {
        var schema = StructuredJson.ParseSchema(Schema);
        for (var i = 0; i < 1501; i++)
            StructuredJson.Validate("""{"text":"validated"}""", schema);
    }

    [Theory]
    [InlineData("""{"timestamp":"not-a-date"}""")]
    [InlineData("""{"timestamp":"2025-02-30T08:00:00Z"}""")]
    [InlineData("""{"timestamp":123}""")]
    [InlineData("""{"timestamp":"2025-01-01 08:00:00Z"}""")]
    [InlineData("""{"timestamp":"2025-01-01T08:00:00"}""")]
    [InlineData("""{"timestamp":"2025-01-01T08:00:00Z\n"}""")]
    public void SchemaValidation_EnforcesDateTimeFormatAndStringType(string json)
    {
        var schema = StructuredJson.ParseSchema("""
            {"type":"object","required":["timestamp"],"additionalProperties":false,
             "properties":{"timestamp":{"type":"string","format":"date-time"}}}
            """);
        var error = Assert.Throws<TaskContractException>(() => StructuredJson.Validate(json, schema));
        Assert.Contains("timestamp", error.Message);
    }

    [Theory]
    [InlineData("2024-02-29T08:00:00Z")]
    [InlineData("2025-01-01T08:00:00.1234567+01:00")]
    [InlineData("2025-01-01t08:00:00z")]
    public void SchemaValidation_AcceptsSupportedRfc3339DateTimes(string timestamp)
    {
        var schema = StructuredJson.ParseSchema("""
            {"type":"object","required":["timestamp"],"properties":{"timestamp":{"type":"string","format":"date-time"}}}
            """);
        StructuredJson.Validate(System.Text.Json.JsonSerializer.Serialize(new { timestamp }), schema);
    }

    [Fact]
    public void SchemaValidation_PreservesDraft202012ReferenceAndSiblingConstraints()
    {
        var schema = StructuredJson.ParseSchema("""
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","required":["value"],
             "$defs":{"bounded":{"type":"integer","minimum":1}},
             "properties":{"value":{"$ref":"#/$defs/bounded","maximum":3}},"additionalProperties":false}
            """);
        StructuredJson.Validate("""{"value":2}""", schema);
        var error = Assert.Throws<TaskContractException>(() => StructuredJson.Validate("""{"value":4}""", schema));
        Assert.Contains("maximum", error.Message);
    }

    [Fact]
    public async Task SchemaValidation_CanShareAParsedSchemaAcrossConcurrentMicrotasks()
    {
        var schema = StructuredJson.ParseSchema(Schema);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
                StructuredJson.Validate("""{"text":"parallel"}""", schema);
        })));
    }

    private static Task<Output> Run(ILLMProvider llm, CancellationToken ct = default) =>
        TaskRunner.RunStructuredAsync<Output>(llm, NullLogger.Instance, "ContractTest", "system", "user", Schema, ct);
}

internal sealed class ScriptedLlmProvider(Func<TestLlmCall, CancellationToken, Task<LLMResponse>> respond) : ILLMProvider
{
    public List<TestLlmCall> Calls { get; } = new();

    public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        var call = new TestLlmCall(systemPrompt, userPrompt, jsonSchema);
        Calls.Add(call);
        return respond(call, cancellationToken);
    }

    public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
