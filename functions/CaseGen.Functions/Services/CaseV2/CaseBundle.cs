using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public record CaseBundleFile(string AssetId, string RelativePath, string MimeType, long SizeBytes, string Sha256);
public record CaseBundleManifest(string Version, string CaseId, string BundleId, int Revision, IReadOnlyList<CaseBundleFile> Files);
internal record CasePublicationSnapshot(string? ETag);

public sealed class CaseBundle
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private CasePublicationSnapshot? _publicationSnapshot;
    public CaseBundleManifest Manifest { get; }
    public string CaseDirectory { get; }
    public string CaseJson { get; }
    public string CaseJsonSha256 { get; }
    public string ManifestSha256 { get; }
    public string CaseJsonPath => Path.Combine(CaseDirectory, "case.json");
    public string BundleRelativePath => $"assets/{Manifest.BundleId}";
    public string BundleDirectory => Path.Combine(CaseDirectory, "assets", Manifest.BundleId);
    public string ManifestRelativePath => $"{BundleRelativePath}/manifest.json";
    public string ManifestPath => Path.Combine(BundleDirectory, "manifest.json");
    public CaseBundleReport Report { get; }

    internal CasePublicationSnapshot RememberPublicationSnapshot(string? etag)
    {
        var snapshot = new CasePublicationSnapshot(etag);
        return Interlocked.CompareExchange(ref _publicationSnapshot, snapshot, null) ?? snapshot;
    }

    private CaseBundle(CaseBundleManifest manifest, string caseDirectory, string caseJson, string manifestSha256, long manifestBytes)
    {
        Manifest = manifest;
        CaseDirectory = caseDirectory;
        CaseJson = caseJson;
        CaseJsonSha256 = Hash(Encoding.UTF8.GetBytes(caseJson));
        ManifestSha256 = manifestSha256;
        Report = new CaseBundleReport
        {
            BundleId = manifest.BundleId, Revision = manifest.Revision, AssetsVerified = manifest.Files.Count,
            TotalBytes = manifest.Files.Sum(f => f.SizeBytes) + manifestBytes + Encoding.UTF8.GetByteCount(caseJson),
            ManifestChecksum = $"sha256:{manifestSha256}"
        };
    }

    public static async Task<CaseBundle> CreateAsync(string caseId, int revision, string caseDirectory,
        string caseJson, IEnumerable<EvidenceAsset> assets, AssetRenderingReport rendering, CancellationToken ct = default)
    {
        Require(Regex.IsMatch(caseId, @"\Acase_[a-z0-9_-]+\z"), "Invalid bundle case ID.");
        var declared = assets.ToList();
        var assetIds = declared.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        Require(assetIds.Count == declared.Count && declared.Count > 0, "Bundle asset IDs must be unique and nonempty.");
        Require(rendering.Files.Count == declared.Count && rendering.Files.Select(f => f.AssetId).ToHashSet(StringComparer.Ordinal).SetEquals(assetIds),
            "Renderer must declare exactly one file for each canonical asset.");
        Require(rendering.Files.Select(f => f.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == declared.Count,
            "Rendered evidence filenames must be unique.");
        var assetsDirectory = Path.Combine(caseDirectory, "assets");
        Require(Directory.Exists(assetsDirectory) && (File.GetAttributes(assetsDirectory) & FileAttributes.ReparsePoint) == 0,
            "The staged assets directory must be a regular directory.");
        Require(!Directory.EnumerateDirectories(assetsDirectory).Any(), "Staging contains an unmanifested asset directory.");
        var actualNames = Directory.EnumerateFiles(assetsDirectory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);

        var bundleId = Guid.NewGuid().ToString("N");
        var files = new List<CaseBundleFile>();
        foreach (var asset in declared)
        {
            ct.ThrowIfCancellationRequested();
            var rendered = rendering.Files.Single(f => f.AssetId == asset.Id);
            Require(Regex.IsMatch(asset.Id, @"\Aasset\.[a-z0-9_]+\z") && rendered.FileName == Path.GetFileName(rendered.FileName) &&
                !rendered.FileName.Contains('\\') && Path.GetFileNameWithoutExtension(rendered.FileName) == asset.Id["asset.".Length..],
                $"Unsafe or mismatched rendered filename for {asset.Id}.");
            var inspection = await CaseFileIntegrity.InspectAsync(Path.Combine(assetsDirectory, rendered.FileName), asset.Id, ct);
            Require(inspection.MimeType == rendered.MimeType, $"Renderer MIME does not match evidence bytes: {asset.Id}");
            var supported = asset.Type switch
            {
                "pdf" or "document" or "digital" => inspection.MimeType == "application/pdf",
                "photo" or "image" => inspection.MimeType.StartsWith("image/", StringComparison.Ordinal),
                _ => false
            };
            Require(supported, $"Asset type has no supported evidence renderer: {asset.Type}");
            files.Add(new CaseBundleFile(asset.Id, $"assets/{bundleId}/{rendered.FileName}", inspection.MimeType, inspection.SizeBytes, inspection.Sha256));
        }
        Require(actualNames.SetEquals(rendering.Files.Select(f => f.FileName)), "Rendered file coverage does not match the staged directory.");

        var manifest = new CaseBundleManifest("1.0", caseId, bundleId, revision, files.AsReadOnly());
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        var manifestSha256 = Hash(manifestBytes);
        var compiled = JsonNode.Parse(caseJson)!.AsObject();
        Require(compiled["caseId"]!.GetValue<string>() == caseId, "Compiled JSON references a different case.");
        var publicAssets = compiled["assets"]!.AsArray();
        Require(publicAssets.Count == files.Count && publicAssets.Select(a => a!["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal).SetEquals(assetIds),
            "Compiled JSON and manifest asset coverage differ.");
        foreach (var node in publicAssets)
        {
            var asset = node!.AsObject();
            var file = files.Single(f => f.AssetId == asset["id"]!.GetValue<string>());
            asset["uri"] = $"case://{caseId}/{file.RelativePath}";
            asset["checksum"] = $"sha256:{file.Sha256}";
            asset["metadata"] = new JsonObject { ["mimeType"] = file.MimeType, ["sizeBytes"] = file.SizeBytes };
        }
        compiled["gameMetadata"]!["generation"]!["bundleChecksum"] = $"sha256:{manifestSha256}";
        var finalJson = compiled.ToJsonString(JsonOptions);
        var bundleDirectory = Path.Combine(assetsDirectory, bundleId);
        Directory.CreateDirectory(bundleDirectory);
        foreach (var rendered in rendering.Files)
            File.Move(Path.Combine(assetsDirectory, rendered.FileName), Path.Combine(bundleDirectory, rendered.FileName));
        await File.WriteAllBytesAsync(Path.Combine(bundleDirectory, "manifest.json"), manifestBytes, ct);
        await File.WriteAllTextAsync(Path.Combine(caseDirectory, "case.json"), finalJson, ct);
        var bundle = new CaseBundle(manifest, Path.GetFullPath(caseDirectory), finalJson, manifestSha256, manifestBytes.Length);
        await bundle.VerifyAsync(ct);
        bundle.Report.Verified = true;
        return bundle;
    }

    public async Task VerifyAsync(CancellationToken ct = default)
    {
        await VerifyAssetsAsync(CaseDirectory, ct);
        await VerifyJsonAsync(ct);
    }

    public async Task VerifyJsonAsync(CancellationToken ct = default)
    {
        Require(await HashFileAsync(CaseJsonPath, ct) == CaseJsonSha256, "Compiled case.json changed after bundle verification.");
    }

    public async Task VerifyAssetsAsync(string caseDirectory, CancellationToken ct = default)
    {
        var directory = Path.Combine(caseDirectory, "assets", Manifest.BundleId);
        Require(Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0,
            "Immutable bundle directory is missing or is a symbolic link.");
        Require(!Directory.EnumerateDirectories(directory).Any(), "Immutable bundle contains an unmanifested directory.");
        var expected = Manifest.Files.Select(f => Path.GetFileName(f.RelativePath)).Append("manifest.json").ToHashSet(StringComparer.Ordinal);
        Require(Directory.EnumerateFiles(directory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal).SetEquals(expected),
            "Immutable bundle file coverage changed.");
        var manifestPath = Path.Combine(directory, "manifest.json");
        Require(await HashFileAsync(manifestPath, ct) == ManifestSha256, "Manifest changed after bundle verification.");
        foreach (var file in Manifest.Files)
        {
            var path = Path.Combine(caseDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var actual = await CaseFileIntegrity.InspectAsync(path, file.AssetId, ct);
            if (actual.SizeBytes != file.SizeBytes || actual.MimeType != file.MimeType || actual.Sha256 != file.Sha256)
                throw new CaseBundleValidationException("asset_integrity_failed", $"Evidence bytes changed after manifest creation: {file.AssetId}", file.AssetId);
        }
    }

    public static async Task<string> HashFileAsync(string path, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new CaseBundleValidationException("bundle_invalid", "Bundle metadata cannot be a symbolic link.");
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new CaseBundleValidationException("bundle_invalid", message);
    }
}
