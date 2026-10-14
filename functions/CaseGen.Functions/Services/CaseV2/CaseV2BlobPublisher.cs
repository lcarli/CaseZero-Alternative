using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public interface ICaseV2BlobPublisher
{
    /// <summary>Whether the publisher has a connection string and a container configured.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Publishes a verified immutable bundle, then conditionally commits case.json.
    /// Returns the number of objects in the published bundle, including verified objects reused on retry.
    /// </summary>
    Task<int> PublishAsync(CaseBundle bundle, CancellationToken ct = default);
}

/// <summary>
/// Mirrors the freshly-generated case (case.json + assets/*) to Azure Blob Storage so the
/// site's CaseV2StorageService (running on a different host in production) can pick it up
/// from the shared `bundles` container. In local dev it can be disabled by leaving the
/// connection string empty. Configured storage failures propagate to the quality gate.
/// </summary>
public class CaseV2BlobPublisher : ICaseV2BlobPublisher
{
    private readonly BlobServiceClient? _client;
    private readonly string _container;
    private readonly ILogger<CaseV2BlobPublisher> _logger;
    public bool IsConfigured { get; }

    public CaseV2BlobPublisher(IConfiguration configuration, ILogger<CaseV2BlobPublisher> logger, BlobServiceClient? client = null)
    {
        _logger = logger;
        _container = configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        try
        {
            _client = client ?? BlobServiceClientFactory.Create(configuration);
            IsConfigured = true;
        }
        catch (InvalidOperationException ex)
        {
            IsConfigured = false;
            _logger.LogInformation(ex, "CaseV2BlobPublisher disabled — no storage configuration (MI or connection string) found");
        }
    }

    public async Task<int> PublishAsync(CaseBundle bundle, CancellationToken ct = default)
    {
        if (!IsConfigured || _client is null) return 0;

        await bundle.VerifyAsync(ct);
        var container = _client.GetBlobContainerClient(_container);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var caseId = bundle.Manifest.CaseId;
        var caseBlob = container.GetBlobClient($"{caseId}/case.json");
        BlobProperties? previous = null;
        try { previous = (await caseBlob.GetPropertiesAsync(cancellationToken: ct)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { }
        var snapshot = bundle.RememberPublicationSnapshot(previous?.ETag.ToString());
        var committed = false;
        try
        {
            foreach (var file in bundle.Manifest.Files)
            {
                var blob = container.GetBlobClient($"{caseId}/{file.RelativePath}");
                var local = Path.Combine(bundle.CaseDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                await UploadImmutableAsync(blob, local, file.MimeType, file.SizeBytes, file.Sha256, file.AssetId, bundle, ct);
            }
            var manifestBlob = container.GetBlobClient($"{caseId}/{bundle.ManifestRelativePath}");
            await UploadImmutableAsync(manifestBlob, bundle.ManifestPath, "application/json", new FileInfo(bundle.ManifestPath).Length,
                bundle.ManifestSha256, "manifest", bundle, ct);
            await bundle.VerifyAsync(ct);

            if (previous?.Metadata.TryGetValue("bundleid", out var previousBundle) == true && previousBundle == bundle.Manifest.BundleId)
            {
                await VerifyRemoteAsync(caseBlob, "application/json", Encoding.UTF8.GetByteCount(bundle.CaseJson), bundle.CaseJsonSha256, "case.json", bundle, ct);
            }
            else
            {
                var conditions = snapshot.ETag is null
                    ? new BlobRequestConditions { IfNoneMatch = ETag.All }
                    : new BlobRequestConditions { IfMatch = new ETag(snapshot.ETag) };
                try
                {
                    await using var stream = File.OpenRead(bundle.CaseJsonPath);
                    var checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
                    if (checksum != bundle.CaseJsonSha256)
                        throw new CaseBundleValidationException("bundle_invalid", "case.json changed immediately before its conditional commit.");
                    stream.Position = 0;
                    await caseBlob.UploadAsync(stream, UploadOptions("application/json", bundle.CaseJsonSha256, bundle, conditions), ct);
                }
                catch (RequestFailedException ex) when (ex.Status is 409 or 412)
                {
                    BlobProperties? current = null;
                    try { current = (await caseBlob.GetPropertiesAsync(cancellationToken: ct)).Value; }
                    catch (RequestFailedException missing) when (missing.Status == 404) { }
                    if (current is null || !current.Metadata.TryGetValue("bundleid", out var currentBundle) || currentBundle != bundle.Manifest.BundleId)
                        throw new CasePublicationConflictException("Blob case.json changed during publication; the previous committed bundle was not overwritten.", ex);
                    // A successful conditional commit may lose its HTTP response and be retried by the SDK.
                    await VerifyRemoteAsync(caseBlob, "application/json", Encoding.UTF8.GetByteCount(bundle.CaseJson), bundle.CaseJsonSha256, "case.json", bundle, ct);
                }
            }
            committed = true;
            var published = bundle.Manifest.Files.Count + 2;
            _logger.LogInformation("Published verified bundle {BundleId} revision {Revision} for case {CaseId} ({N} blobs)",
                bundle.Manifest.BundleId, bundle.Manifest.Revision, caseId, published);
            return published;
        }
        finally
        {
            if (!committed)
                _logger.LogWarning("Uncommitted or uncertain bundle {BundleId} retained under {CaseId}/{Path}; no previous assets were overwritten",
                    bundle.Manifest.BundleId, caseId, bundle.BundleRelativePath);
        }
    }

    private static async Task UploadImmutableAsync(BlobClient blob, string localFile, string contentType, long size, string checksum,
        string reference, CaseBundle bundle, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(localFile);
            await blob.UploadAsync(stream, UploadOptions(contentType, checksum, bundle, new BlobRequestConditions { IfNoneMatch = ETag.All }), ct);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            // Reuse requires byte-level verification, not just trusting an existing name or metadata.
        }
        await VerifyRemoteAsync(blob, contentType, size, checksum, reference, bundle, ct);
    }

    private static BlobUploadOptions UploadOptions(string contentType, string checksum, CaseBundle bundle, BlobRequestConditions conditions) => new()
    {
        HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        Conditions = conditions,
        Metadata = new Dictionary<string, string>
        {
            ["bundleid"] = bundle.Manifest.BundleId, ["sha256"] = checksum,
            ["revision"] = bundle.Manifest.Revision.ToString(CultureInfo.InvariantCulture)
        },
        TransferValidation = new Azure.Storage.UploadTransferValidationOptions { ChecksumAlgorithm = Azure.Storage.StorageChecksumAlgorithm.Auto }
    };

    private static async Task VerifyRemoteAsync(BlobClient blob, string contentType, long size, string checksum,
        string reference, CaseBundle bundle, CancellationToken ct)
    {
        var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
        if (properties.ContentLength != size || properties.ContentType != contentType ||
            !properties.Metadata.TryGetValue("bundleid", out var bundleId) || bundleId != bundle.Manifest.BundleId ||
            !properties.Metadata.TryGetValue("revision", out var revision) || revision != bundle.Manifest.Revision.ToString(CultureInfo.InvariantCulture) ||
            !properties.Metadata.TryGetValue("sha256", out var declared) || declared != checksum)
            throw new CaseBundleValidationException("asset_integrity_failed", $"Remote artifact metadata differs from the sealed bundle: {reference}", reference);
        var download = await blob.DownloadStreamingAsync(new BlobDownloadOptions { Conditions = new BlobRequestConditions { IfMatch = properties.ETag } }, ct);
        await using var stream = download.Value.Content;
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        if (actual != checksum)
            throw new CaseBundleValidationException("asset_integrity_failed", $"Remote artifact checksum differs from the sealed bundle: {reference}", reference);
    }
}
