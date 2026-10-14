using System.Globalization;
using System.Net;
using System.Text;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseBundlePublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publication_VerifiesImmutableFilesAndConditionallyCommitsJsonLast(bool existing)
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        string? previousEtag = null;
        if (existing)
        {
            previousEtag = storage.Seed("case_bundle/case.json", "previous case"u8.ToArray(), "application/json").Etag;
            storage.Seed("case_bundle/assets/record.pdf", "previous evidence"u8.ToArray(), "application/pdf");
        }
        var published = await Publisher(storage).PublishAsync(bundle);
        Assert.Equal(3, published);
        var writes = storage.Operations.Where(o => o.Method == "PUT" && !o.Container).ToList();
        Assert.Equal("case_bundle/case.json", writes[^1].Path);
        Assert.All(writes.Take(writes.Count - 1), o =>
        {
            Assert.StartsWith($"case_bundle/assets/{bundle.Manifest.BundleId}/", o.Path);
            Assert.Equal("*", o.IfNoneMatch);
        });
        Assert.Equal(existing ? previousEtag : null, writes[^1].IfMatch);
        Assert.Equal(existing ? null : "*", writes[^1].IfNoneMatch);
        Assert.Equal(bundle.CaseJson, Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
        Assert.Contains(storage.Operations, o => o.Method == "GET" && o.Path.EndsWith("/record.pdf", StringComparison.Ordinal));
        Assert.Contains(storage.Operations, o => o.Method == "GET" && o.Path.EndsWith("/manifest.json", StringComparison.Ordinal));
        if (existing)
            Assert.Equal("previous evidence", Encoding.UTF8.GetString(storage.Blobs["case_bundle/assets/record.pdf"].Bytes));
    }

    [Fact]
    public async Task FailedAssetUpload_PreservesPreviousJsonAndEvidence()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        storage.Seed("case_bundle/case.json", "previous case"u8.ToArray(), "application/json");
        storage.Seed("case_bundle/assets/record.pdf", "previous evidence"u8.ToArray(), "application/pdf");
        storage.FailBefore = operation => operation.Method == "PUT" && operation.Path.EndsWith("/manifest.json", StringComparison.Ordinal)
            ? MemoryBlobStorage.Error(HttpStatusCode.ServiceUnavailable, "ServerBusy") : null;
        await Assert.ThrowsAsync<RequestFailedException>(() => Publisher(storage).PublishAsync(bundle));
        Assert.Equal("previous case", Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
        Assert.Equal("previous evidence", Encoding.UTF8.GetString(storage.Blobs["case_bundle/assets/record.pdf"].Bytes));
        Assert.DoesNotContain(storage.Operations, o => o.Method == "PUT" && o.Path == "case_bundle/case.json");
    }

    [Fact]
    public async Task ConcurrentCaseCommit_IsNotOverwrittenByAStaleEtag()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        storage.Seed("case_bundle/case.json", "previous case"u8.ToArray(), "application/json");
        storage.OnRequest = operation =>
        {
            if (operation.Method == "PUT" && operation.Path.EndsWith("/record.pdf", StringComparison.Ordinal))
                storage.Seed("case_bundle/case.json", "concurrent winner"u8.ToArray(), "application/json",
                    new Dictionary<string, string> { ["bundleid"] = "another_bundle" });
        };
        await Assert.ThrowsAsync<CasePublicationConflictException>(() => Publisher(storage).PublishAsync(bundle));
        Assert.Equal("concurrent winner", Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
    }

    [Fact]
    public async Task RemoteByteCorruption_IsDetectedEvenWithCorrectMetadataAndLength()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        storage.Seed("case_bundle/case.json", "previous case"u8.ToArray(), "application/json");
        storage.AfterPut = (operation, blob) =>
        {
            if (operation.Path.EndsWith("/record.pdf", StringComparison.Ordinal)) blob.Bytes[12] ^= 1;
            return null;
        };
        var error = await Assert.ThrowsAsync<CaseBundleValidationException>(() => Publisher(storage).PublishAsync(bundle));
        Assert.Equal("asset_integrity_failed", error.Code);
        Assert.Equal("asset.record", error.Reference);
        Assert.Equal("previous case", Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
        Assert.DoesNotContain(storage.Operations, o => o.Method == "PUT" && o.Path == "case_bundle/case.json");
    }

    [Fact]
    public async Task RetryAfterPartialUpload_ReusesOnlyVerifiedIdenticalFiles()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        storage.FailBefore = operation => operation.Method == "PUT" && operation.Path.EndsWith("/manifest.json", StringComparison.Ordinal)
            ? MemoryBlobStorage.Error(HttpStatusCode.ServiceUnavailable, "ServerBusy") : null;
        var publisher = Publisher(storage);
        await Assert.ThrowsAsync<RequestFailedException>(() => publisher.PublishAsync(bundle));
        var originalAssetEtag = storage.Blobs[$"case_bundle/{bundle.Manifest.Files[0].RelativePath}"].Etag;
        storage.FailBefore = null;
        Assert.Equal(3, await publisher.PublishAsync(bundle));
        Assert.Equal(originalAssetEtag, storage.Blobs[$"case_bundle/{bundle.Manifest.Files[0].RelativePath}"].Etag);
        Assert.Equal(bundle.CaseJson, Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
    }

    [Fact]
    public async Task RetryAfterCommittedBundle_DoesNotRewriteAnyPublishedObject()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        var publisher = Publisher(storage);
        await publisher.PublishAsync(bundle);
        var etags = storage.Blobs.ToDictionary(b => b.Key, b => b.Value.Etag);
        var previousCaseWrites = storage.Operations.Count(o => o.Method == "PUT" && o.Path == "case_bundle/case.json");
        Assert.Equal(3, await publisher.PublishAsync(bundle));
        Assert.All(etags, b => Assert.Equal(b.Value, storage.Blobs[b.Key].Etag));
        Assert.Equal(previousCaseWrites, storage.Operations.Count(o => o.Method == "PUT" && o.Path == "case_bundle/case.json"));
    }

    [Fact]
    public async Task RetryOfOldBundle_AfterNewerCommitCannotRollBackTheCase()
    {
        using var workspace = new BundleTestWorkspace();
        var older = await workspace.CreateAsync();
        var newer = await workspace.CreateAsync(revision: 1);
        using var storage = new MemoryBlobStorage();
        var publisher = Publisher(storage);
        await publisher.PublishAsync(older);
        await publisher.PublishAsync(newer);
        await Assert.ThrowsAsync<CasePublicationConflictException>(() => Publisher(storage).PublishAsync(older));
        Assert.Equal(newer.CaseJson, Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
    }

    [Fact]
    public async Task RetryOfPartialOldBundle_CannotReplaceAConcurrentWinner()
    {
        using var workspace = new BundleTestWorkspace();
        var older = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        storage.Seed("case_bundle/case.json", "original"u8.ToArray(), "application/json");
        storage.FailBefore = operation => operation.Method == "PUT" && operation.Path.EndsWith("/manifest.json", StringComparison.Ordinal)
            ? MemoryBlobStorage.Error(HttpStatusCode.ServiceUnavailable, "ServerBusy") : null;
        var publisher = Publisher(storage);
        await Assert.ThrowsAsync<RequestFailedException>(() => publisher.PublishAsync(older));
        storage.FailBefore = null;
        storage.Seed("case_bundle/case.json", "concurrent winner"u8.ToArray(), "application/json");
        await Assert.ThrowsAsync<CasePublicationConflictException>(() => publisher.PublishAsync(older));
        Assert.Equal("concurrent winner", Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
    }

    [Fact]
    public async Task ExistingImmutableNameWithDifferentBytes_IsNeverOverwritten()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        var path = $"case_bundle/{bundle.Manifest.Files[0].RelativePath}";
        var previous = storage.Seed(path, "foreign evidence"u8.ToArray(), "application/pdf");
        await Assert.ThrowsAsync<CaseBundleValidationException>(() => Publisher(storage).PublishAsync(bundle));
        Assert.Same(previous, storage.Blobs[path]);
        Assert.False(storage.Blobs.ContainsKey("case_bundle/case.json"));
    }

    [Fact]
    public async Task LocalTamperingBeforePublication_MakesNoStorageCalls()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        await File.AppendAllTextAsync(Path.Combine(bundle.CaseDirectory, bundle.Manifest.Files[0].RelativePath), "changed");
        using var storage = new MemoryBlobStorage();
        await Assert.ThrowsAsync<CaseBundleValidationException>(() => Publisher(storage).PublishAsync(bundle));
        Assert.Empty(storage.Operations);
    }

    [Fact]
    public async Task StagingTamperingDuringUpload_BlocksFinalJsonCommit()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        storage.AfterPut = (operation, _) =>
        {
            if (operation.Path.EndsWith("/manifest.json", StringComparison.Ordinal))
                File.AppendAllText(bundle.CaseJsonPath, "changed after upload");
            return null;
        };
        await Assert.ThrowsAsync<CaseBundleValidationException>(() => Publisher(storage).PublishAsync(bundle));
        Assert.False(storage.Blobs.ContainsKey("case_bundle/case.json"));
    }

    [Fact]
    public async Task LostSuccessfulCommitResponse_IsVerifiedAfterSdkConditionalRetry()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        var lost = false;
        storage.AfterPut = (operation, _) =>
        {
            if (operation.Path == "case_bundle/case.json" && !lost)
            {
                lost = true;
                return MemoryBlobStorage.Error(HttpStatusCode.ServiceUnavailable, "ServerBusy");
            }
            return null;
        };
        Assert.Equal(3, await Publisher(storage, retries: 1).PublishAsync(bundle));
        Assert.True(lost);
        Assert.Equal(bundle.CaseJson, Encoding.UTF8.GetString(storage.Blobs["case_bundle/case.json"].Bytes));
        Assert.Equal(2, storage.Operations.Count(o => o.Method == "PUT" && o.Path == "case_bundle/case.json"));
    }

    [Fact]
    public async Task RequestedCancellation_NeverCommitsJson()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var storage = new MemoryBlobStorage();
        using var source = new CancellationTokenSource();
        storage.OnRequest = operation =>
        {
            if (operation.Method == "PUT" && !operation.Container) source.Cancel();
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Publisher(storage).PublishAsync(bundle, source.Token));
        Assert.False(storage.Blobs.ContainsKey("case_bundle/case.json"));
    }

    private static CaseV2BlobPublisher Publisher(MemoryBlobStorage storage, int retries = 0)
    {
        var options = new BlobClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(storage, disposeHandler: false)),
            Retry = { MaxRetries = retries, Delay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero }
        };
        return new CaseV2BlobPublisher(new ConfigurationBuilder().Build(), NullLogger<CaseV2BlobPublisher>.Instance,
            new BlobServiceClient(new Uri("https://casegen.invalid"), options));
    }
}

internal record MemoryBlobOperation(string Method, string Path, bool Container, string? IfMatch, string? IfNoneMatch);
internal record MemoryBlob(byte[] Bytes, string ContentType, Dictionary<string, string> Metadata, string Etag);

internal sealed class MemoryBlobStorage : HttpMessageHandler
{
    private int _etag;
    public Dictionary<string, MemoryBlob> Blobs { get; } = new(StringComparer.Ordinal);
    public List<MemoryBlobOperation> Operations { get; } = [];
    public Action<MemoryBlobOperation>? OnRequest { get; set; }
    public Func<MemoryBlobOperation, HttpResponseMessage?>? FailBefore { get; set; }
    public Func<MemoryBlobOperation, MemoryBlob, HttpResponseMessage?>? AfterPut { get; set; }

    public MemoryBlob Seed(string path, byte[] bytes, string contentType, Dictionary<string, string>? metadata = null)
    {
        var blob = new MemoryBlob(bytes, contentType, metadata ?? new(StringComparer.OrdinalIgnoreCase), $"\"{++_etag}\"");
        Blobs[path] = blob;
        return blob;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var container = request.RequestUri!.Query.Contains("restype=container", StringComparison.Ordinal);
        var path = container ? "" : Uri.UnescapeDataString(request.RequestUri.AbsolutePath)["/bundles/".Length..];
        var operation = new MemoryBlobOperation(request.Method.Method, path, container, Header("If-Match"), Header("If-None-Match"));
        Operations.Add(operation);
        OnRequest?.Invoke(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (FailBefore?.Invoke(operation) is { } injected) return injected;
        if (container) return new HttpResponseMessage(HttpStatusCode.Created);
        Blobs.TryGetValue(path, out var existing);
        if ((operation.IfMatch is not null && existing?.Etag != operation.IfMatch) ||
            (operation.IfNoneMatch == "*" && existing is not null))
            return Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet");
        if (request.Method == HttpMethod.Put)
        {
            var bytes = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var metadata = request.Headers.Where(h => h.Key.StartsWith("x-ms-meta-", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key["x-ms-meta-".Length..], h => h.Value.Single(), StringComparer.OrdinalIgnoreCase);
            var blob = Seed(path, bytes, Header("x-ms-blob-content-type") ?? "application/octet-stream", metadata);
            if (AfterPut?.Invoke(operation, blob) is { } after) return after;
            var response = Response(HttpStatusCode.Created, blob, body: false);
            response.Headers.TryAddWithoutValidation("x-ms-request-server-encrypted", "true");
            return response;
        }
        if (existing is null) return Error(HttpStatusCode.NotFound, "BlobNotFound");
        if (request.Method == HttpMethod.Head) return Response(HttpStatusCode.OK, existing, body: false);
        if (request.Method == HttpMethod.Get) return Response(HttpStatusCode.OK, existing, body: true);
        throw new NotSupportedException($"Unexpected offline blob request: {request.Method} {request.RequestUri}");

        string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
    }

    private static HttpResponseMessage Response(HttpStatusCode status, MemoryBlob blob, bool body)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body ? blob.Bytes : []) };
        response.Content.Headers.ContentLength = blob.Bytes.Length;
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(blob.ContentType);
        response.Content.Headers.LastModified = DateTimeOffset.UtcNow;
        response.Headers.TryAddWithoutValidation("ETag", blob.Etag);
        response.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
        response.Headers.TryAddWithoutValidation("x-ms-creation-time", DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));
        foreach (var pair in blob.Metadata) response.Headers.TryAddWithoutValidation($"x-ms-meta-{pair.Key}", pair.Value);
        return response;
    }

    public static HttpResponseMessage Error(HttpStatusCode status, string code)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent($"<Error><Code>{code}</Code><Message>Injected offline storage error</Message></Error>") };
        response.Headers.TryAddWithoutValidation("x-ms-error-code", code);
        return response;
    }
}
