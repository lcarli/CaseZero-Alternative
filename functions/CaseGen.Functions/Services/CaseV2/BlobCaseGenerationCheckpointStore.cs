using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class BlobCaseGenerationCheckpointStore : ICaseGenerationCheckpointStore
{
    public const int MaximumArtifactBytes = 20 * 1024 * 1024;
    private readonly BlobContainerClient _container;
    private bool _privateContainerVerified;
    public BlobCaseGenerationCheckpointStore(IConfiguration configuration) =>
        _container = BlobServiceClientFactory.Create(configuration).GetBlobContainerClient("jobs");
    public BlobCaseGenerationCheckpointStore(BlobContainerClient container) => _container = container;

    public async Task<CaseCheckpointReference?> FindAsync(string jobId, CaseGenerationStage stage, CancellationToken ct)
    {
        var path = PathFor(jobId, stage);
        await EnsurePrivateAsync(ct);
        try
        {
            var properties = (await _container.GetBlobClient(path).GetPropertiesAsync(cancellationToken: ct)).Value;
            if (!properties.Metadata.TryGetValue("sha256", out var hash)
                || !properties.Metadata.TryGetValue("definitionhash", out var definition)
                || !properties.Metadata.TryGetValue("requesthash", out var requestHash))
                throw new TaskContractException("Private checkpoint is missing integrity or version metadata.");
            return new(jobId, stage, path, hash, definition, requestHash);
        }
        catch (RequestFailedException exception) when (exception.Status == 404) { return null; }
    }

    public async Task<CaseGenerationCheckpoint> ReadAsync(CaseCheckpointReference reference, CancellationToken ct, bool restoreAssets = true)
    {
        if (reference.Path != PathFor(reference.JobId, reference.Stage))
            throw new TaskContractException("Private checkpoint path does not match its job and stage.");
        await EnsurePrivateAsync(ct);
        var bytes = await ReadVerifiedAsync(reference.Path, reference.Sha256, ct);
        var checkpoint = JsonSerializer.Deserialize<CaseGenerationCheckpoint>(bytes, CaseDraft.CanonicalJsonOptions)
            ?? throw new TaskContractException("Private checkpoint has no generation state.");
        if (checkpoint.CompletedStage != reference.Stage || checkpoint.Version != CaseGenerationRuntime.Version
            || checkpoint.Harness?.DefinitionHash != reference.DefinitionHash || checkpoint.JobId != reference.JobId
            || checkpoint.RequestHash != reference.RequestHash || checkpoint.RequestHash != CaseGenerationCheckpoint.Fingerprint(checkpoint.Draft.Request))
            throw new TaskContractException("Private checkpoint stage or harness version does not match its reference.");
        checkpoint.RestorePreparation();
        if (restoreAssets && checkpoint.StagedBundle is { } staged)
        {
            var root = Path.Combine(Path.GetTempPath(), "casegen-checkpoint", Guid.NewGuid().ToString("N"));
            var caseDirectory = Path.Combine(root, checkpoint.Draft.CaseId);
            var bundleDirectory = Path.Combine(caseDirectory, "assets", staged.Manifest.BundleId);
            checkpoint.StagedCaseDirectory = caseDirectory;
            var restored = false;
            try
            {
                Directory.CreateDirectory(bundleDirectory);
                foreach (var file in staged.Manifest.Files)
                {
                    var data = await ReadVerifiedAsync(AssetPath(reference.JobId, file.RelativePath), file.Sha256, ct);
                    await File.WriteAllBytesAsync(Path.Combine(caseDirectory, SafeRelative(file.RelativePath)), data, ct);
                }
                var manifest = await ReadVerifiedAsync(AssetPath(reference.JobId, $"assets/{staged.Manifest.BundleId}/manifest.json"),
                    staged.ManifestSha256, ct);
                await File.WriteAllBytesAsync(Path.Combine(bundleDirectory, "manifest.json"), manifest, ct);
                var response = checkpoint.Response ?? throw new TaskContractException("Staged checkpoint has no evaluated response.");
                await File.WriteAllTextAsync(Path.Combine(caseDirectory, "case.json"), response.CaseJson, ct);
                await CaseBundle.RestoreAsync(staged.Manifest, caseDirectory, response.CaseJson, staged.ManifestSha256, ct);
                restored = true;
            }
            finally
            {
                if (!restored) checkpoint.CleanupStaging();
            }
        }
        return checkpoint;
    }

    public async Task<CaseCheckpointReference> SaveAsync(string jobId, CaseGenerationCheckpoint checkpoint, CancellationToken ct)
    {
        var stage = checkpoint.CompletedStage ?? throw new TaskContractException("Cannot save an unconfirmed generation stage.");
        var definition = checkpoint.Harness?.DefinitionHash ?? throw new TaskContractException("Cannot save an unversioned generation stage.");
        if (checkpoint.JobId != jobId || checkpoint.RequestHash != CaseGenerationCheckpoint.Fingerprint(checkpoint.Draft.Request))
            throw new TaskContractException("Cannot save a checkpoint for another job or request.");
        checkpoint.CapturePreparation();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint, CaseDraft.CanonicalJsonOptions);
        if (bytes.Length > MaximumArtifactBytes)
            throw new TaskContractException("Private checkpoint exceeds the 20 MiB limit.");
        await EnsurePrivateAsync(ct);
        if (checkpoint.StagedBundle is { } staged && stage == CaseGenerationStage.Rendering)
        {
            var directory = checkpoint.StagedCaseDirectory ?? throw new TaskContractException("Rendering checkpoint assets are missing.");
            var bundle = await CaseBundle.RestoreAsync(staged.Manifest, directory, checkpoint.Response!.CaseJson, staged.ManifestSha256, ct);
            foreach (var file in staged.Manifest.Files)
                await UploadFileAsync(AssetPath(jobId, file.RelativePath), Path.Combine(directory, SafeRelative(file.RelativePath)), file.Sha256, ct);
            await UploadFileAsync(AssetPath(jobId, bundle.ManifestRelativePath), bundle.ManifestPath, staged.ManifestSha256, ct);
        }
        var hash = Hash(bytes);
        var path = PathFor(jobId, stage);
        var blob = _container.GetBlobClient(path);
        try
        {
            await blob.UploadAsync(BinaryData.FromBytes(bytes), new BlobUploadOptions
            {
                Conditions = new() { IfNoneMatch = ETag.All },
                HttpHeaders = new() { ContentType = "application/json" },
                Metadata = new Dictionary<string, string>
                {
                    ["sha256"] = hash, ["definitionhash"] = definition, ["requesthash"] = checkpoint.RequestHash
                }
            }, ct);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            await ReadVerifiedAsync(path, hash, ct);
        }
        return new(jobId, stage, path, hash, definition, checkpoint.RequestHash);
    }

    public async Task<bool> TryBeginStageAsync(CaseStageInput input, CaseGenerationCheckpoint checkpoint, CancellationToken ct)
    {
        checkpoint.Bind(input);
        var budget = GenerationCallBudget.For(DifficultyProfileCatalog.Get(input.Request.Difficulty));
        // A lost execution outcome is never replayed. These are ceilings, not a claim of actual billing.
        var reservation = new
        {
            version = CaseGenerationRuntime.Version, input.JobId, input.Stage, checkpoint.RequestHash,
            predecessor = input.Previous?.Sha256, definition = checkpoint.Harness?.DefinitionHash,
            startedAt = DateTimeOffset.UtcNow,
            maximumUnconfirmedCalls = budget.MaximumCalls - (checkpoint.Harness?.Calls ?? 0),
            maximumUnconfirmedInputTokens = budget.MaximumInputTokens - (checkpoint.Harness?.InputTokens ?? 0),
            maximumUnconfirmedOutputTokens = budget.MaximumOutputTokens - (checkpoint.Harness?.OutputTokens ?? 0),
            imageBilling = "unknown; image units are bounded by provider calls"
        };
        await EnsurePrivateAsync(ct);
        try
        {
            await _container.GetBlobClient($"{input.JobId}/attempts/{(int)input.Stage}-{input.Stage}.json")
                .UploadAsync(BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(reservation)), new BlobUploadOptions
                {
                    Conditions = new() { IfNoneMatch = ETag.All },
                    HttpHeaders = new() { ContentType = "application/json" }
                }, ct);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412) { return false; }
    }

    public async Task RecordFailureAsync(CaseStageInput input, CaseGenerationCheckpoint checkpoint, string failure, CancellationToken ct)
    {
        await EnsurePrivateAsync(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = CaseGenerationRuntime.Version, input.JobId, input.Stage, checkpoint.RequestHash,
            failure, checkpoint.Harness, usageMayBeEstimated = true, finishedAt = DateTimeOffset.UtcNow
        }, CaseDraft.CanonicalJsonOptions);
        if (bytes.Length > MaximumArtifactBytes)
            throw new TaskContractException("Private failure report exceeds the 20 MiB limit.");
        await _container.GetBlobClient($"{input.JobId}/attempts/{(int)input.Stage}-{input.Stage}-failure.json")
            .UploadAsync(BinaryData.FromBytes(bytes), new BlobUploadOptions
            {
                Conditions = new() { IfNoneMatch = ETag.All },
                HttpHeaders = new() { ContentType = "application/json" }
            }, ct);
    }

    private async Task EnsurePrivateAsync(CancellationToken ct)
    {
        if (_privateContainerVerified) return;
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        var access = (await _container.GetAccessPolicyAsync(cancellationToken: ct)).Value;
        if (access.BlobPublicAccess != PublicAccessType.None)
            throw new TaskContractException("Private generation checkpoints require a non-public jobs container.");
        _privateContainerVerified = true;
    }

    private async Task<byte[]> ReadVerifiedAsync(string path, string hash, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(path);
        var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
        if (properties.ContentLength > MaximumArtifactBytes)
            throw new TaskContractException("Private checkpoint artifact exceeds the 20 MiB limit.");
        var bytes = (await blob.DownloadContentAsync(ct)).Value.Content.ToArray();
        if (Hash(bytes) != hash)
            throw new TaskContractException("Private checkpoint artifact checksum does not match.");
        return bytes;
    }

    private async Task UploadFileAsync(string path, string localPath, string hash, CancellationToken ct)
    {
        await using var stream = File.OpenRead(localPath);
        if (stream.Length > MaximumArtifactBytes)
            throw new TaskContractException("Private bundle artifact exceeds the 20 MiB limit.");
        try
        {
            await _container.GetBlobClient(path).UploadAsync(stream, new BlobUploadOptions
            {
                Conditions = new() { IfNoneMatch = ETag.All },
                HttpHeaders = new() { ContentType = "application/octet-stream" },
                Metadata = new Dictionary<string, string> { ["sha256"] = hash }
            }, ct);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            await ReadVerifiedAsync(path, hash, ct);
        }
    }

    private static string PathFor(string jobId, CaseGenerationStage stage)
    {
        CaseGenerationCheckpoint.ValidateJobId(jobId);
        if (!Enum.IsDefined(stage))
            throw new TaskContractException("Invalid private checkpoint job or stage.");
        return $"{jobId}/checkpoints/{(int)stage}-{stage}.json";
    }

    private static string AssetPath(string jobId, string relativePath)
    {
        SafeRelative(relativePath);
        return $"{PathFor(jobId, CaseGenerationStage.Rendering).Split("/checkpoints/")[0]}/artifacts/{relativePath}";
    }

    private static string SafeRelative(string path)
    {
        if (!Regex.IsMatch(path, @"\Aassets/[a-f0-9]{32}/[a-z0-9_.-]+\z"))
            throw new TaskContractException("Unsafe private bundle artifact path.");
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
