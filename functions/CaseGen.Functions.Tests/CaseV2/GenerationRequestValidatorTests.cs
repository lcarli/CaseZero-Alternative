using System.Text;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class GenerationRequestValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"difficulty":null,"requiredRank":null,"language":null}""")]
    public async Task ValidOptionalRequests_PreserveDocumentedDefaults(string json)
    {
        var request = await Read(json);
        Assert.Null(request.Difficulty);
        Assert.Null(request.RequiredRank);
        Assert.True(request.WriteToDisk);
        Assert.Equal("en-US", request.Language ?? "en-US");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"difficulty":"Expert"}""")]
    [InlineData("""{"requiredRank":"detective"}""")]
    [InlineData("""{"language":"de-DE"}""")]
    [InlineData("""{"caseId":"../case_previous"}""")]
    [InlineData("""{"caseId":"case_"}""")]
    [InlineData("""{"title":" "}""")]
    [InlineData("""{"seed":1.5}""")]
    [InlineData("""{"seed":2147483648}""")]
    [InlineData("""{"writeToDisk":null}""")]
    [InlineData("""{"writeToDisk":"false"}""")]
    [InlineData("""{"language":"en-US","language":"pt-BR"}""")]
    [InlineData("""{"language":"en-US","Language":"pt-BR"}""")]
    [InlineData("""{"unexpected":true}""")]
    [InlineData("{} trailing")]
    public async Task InvalidRequests_NeverBecomeDefaults(string json)
    {
        var error = await Assert.ThrowsAsync<GenerationRequestException>(() => Read(json));
        Assert.NotEmpty(error.Errors);
        Assert.False(error.TooLarge);
    }

    [Theory]
    [InlineData("Rookie")]
    [InlineData("Detective")]
    [InlineData("Detective2")]
    [InlineData("Sergeant")]
    [InlineData("Lieutenant")]
    [InlineData("Captain")]
    [InlineData("Commander")]
    public async Task AllRanks_AreAccepted(string rank)
    {
        var request = await Read($$"""{"difficulty":"{{rank}}","requiredRank":"{{rank}}"}""");
        Assert.Equal(rank, request.Difficulty);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("fr-FR")]
    public async Task AllFourLanguages_AreAccepted(string language) =>
        Assert.Equal(language, (await Read($$"""{"language":"{{language}}"}""")).Language);

    [Theory]
    [InlineData("CASE-Example", "case_example")]
    [InlineData("example", "case_example")]
    [InlineData("case_example", "case_example")]
    public void ExistingCaseIdNormalization_IsPreserved(string input, string expected)
    {
        GenerationRequestValidator.Validate(new GenerateCaseV2Request { CaseId = input });
        Assert.Equal(expected, GenerationRequestValidator.NormalizeCaseId(input));
    }

    [Fact]
    public async Task CaseInsensitiveValidPropertyNames_RemainSupported()
    {
        var request = await Read("""{"Difficulty":"Detective","Language":"fr-FR","Seed":-2147483648}""");
        Assert.Equal("Detective", request.Difficulty);
        Assert.Equal("fr-FR", request.Language);
        Assert.Equal(int.MinValue, request.Seed);
    }

    [Theory]
    [InlineData("title", 200)]
    [InlineData("theme", 2000)]
    [InlineData("location", 300)]
    [InlineData("caseId", 128)]
    public async Task TextLimits_AreEnforcedAtTheExactBoundary(string property, int maximum)
    {
        await Read($"{{\"{property}\":\"{new string('x', maximum)}\"}}");
        await Assert.ThrowsAsync<GenerationRequestException>(() => Read($"{{\"{property}\":\"{new string('x', maximum + 1)}\"}}"));
    }

    [Fact]
    public async Task OversizedNonSeekableBody_IsRejectedByByteCount()
    {
        using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes(new string('\u00e9', GenerationRequestValidator.MaxBodyBytes)));
        var error = await Assert.ThrowsAsync<GenerationRequestException>(() => GenerationRequestValidator.ReadAsync(stream, default));
        Assert.True(error.TooLarge);
        Assert.True(stream.BytesRead <= GenerationRequestValidator.MaxBodyBytes + 4096);
    }

    [Fact]
    public async Task BodyByteLimit_AcceptsTheExactBoundaryAndRejectsOneMoreByte()
    {
        var body = "{}" + new string(' ', GenerationRequestValidator.MaxBodyBytes - 2);
        await Read(body);
        var error = await Assert.ThrowsAsync<GenerationRequestException>(() => Read(body + " "));
        Assert.True(error.TooLarge);
    }

    [Fact]
    public async Task InvalidUtf8_IsNotSilentlyReplaced()
    {
        using var stream = new MemoryStream(new byte[] { 0x7B, 0x22, 0x74, 0x69, 0x74, 0x6C, 0x65, 0x22, 0x3A, 0x22, 0xFF, 0x22, 0x7D });
        await Assert.ThrowsAsync<GenerationRequestException>(() => GenerationRequestValidator.ReadAsync(stream, default));
    }

    [Fact]
    public async Task Cancellation_IsNotInterpretedAsAnEmptyRequest()
    {
        using var stream = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GenerationRequestValidator.ReadAsync(stream, new CancellationToken(true)));
    }

    private static async Task<GenerateCaseV2Request> Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await GenerationRequestValidator.ReadAsync(stream, default);
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int BytesRead { get; private set; }
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = base.ReadAsync(buffer, cancellationToken);
            BytesRead += read.Result;
            return read;
        }
    }
}
