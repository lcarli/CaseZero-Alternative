using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Functions.CaseV2;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseBundleTests
{
    [Fact]
    public async Task Manifest_DescribesActualBytesAndDrivesPublicReferences()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync(revision: 3);
        var file = Assert.Single(bundle.Manifest.Files);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(bundle.CaseDirectory, file.RelativePath));
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(bytes.Length, file.SizeBytes);
        Assert.Equal(checksum, file.Sha256);
        Assert.Equal("application/pdf", file.MimeType);
        Assert.Matches("^[a-f0-9]{32}$", bundle.Manifest.BundleId);
        Assert.Equal(3, bundle.Manifest.Revision);
        Assert.True(bundle.Report.Verified);
        Assert.Equal(1, bundle.Report.AssetsVerified);
        Assert.Equal(bytes.Length + new FileInfo(bundle.ManifestPath).Length + new FileInfo(bundle.CaseJsonPath).Length, bundle.Report.TotalBytes);
        var json = JsonNode.Parse(bundle.CaseJson)!;
        Assert.Equal($"case://case_bundle/{file.RelativePath}", json["assets"]![0]!["uri"]!.GetValue<string>());
        Assert.Equal($"sha256:{checksum}", json["assets"]![0]!["checksum"]!.GetValue<string>());
        Assert.Equal(file.SizeBytes, json["assets"]![0]!["metadata"]!["sizeBytes"]!.GetValue<long>());
        var manifestBytes = await File.ReadAllBytesAsync(bundle.ManifestPath);
        Assert.Equal($"sha256:{Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant()}", json["gameMetadata"]!["generation"]!["bundleChecksum"]!.GetValue<string>());
        var manifest = JsonNode.Parse(manifestBytes)!;
        Assert.Equal(3, manifest["revision"]!.GetValue<int>());
        Assert.Equal(file.AssetId, manifest["files"]![0]!["assetId"]!.GetValue<string>());
        Assert.Equal(file.RelativePath, manifest["files"]![0]!["relativePath"]!.GetValue<string>());
        Assert.DoesNotContain("solution", manifest.ToJsonString());
    }

    [Fact]
    public async Task SameLengthByteChange_IsDetectedDespiteUnchangedMimeAndSize()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        var path = Path.Combine(bundle.CaseDirectory, bundle.Manifest.Files[0].RelativePath);
        var bytes = await File.ReadAllBytesAsync(path);
        bytes[12] ^= 1;
        await File.WriteAllBytesAsync(path, bytes);
        var error = await Assert.ThrowsAsync<CaseBundleValidationException>(() => bundle.VerifyAsync());
        Assert.Equal("asset_integrity_failed", error.Code);
        Assert.Equal("asset.record", error.Reference);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("json")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("directory")]
    public async Task SealedBundle_RejectsChangedMetadataOrCoverage(string mutation)
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        switch (mutation)
        {
            case "manifest": await File.AppendAllTextAsync(bundle.ManifestPath, " "); break;
            case "json": await File.AppendAllTextAsync(bundle.CaseJsonPath, " "); break;
            case "missing": File.Delete(Path.Combine(bundle.CaseDirectory, bundle.Manifest.Files[0].RelativePath)); break;
            case "extra": await File.WriteAllTextAsync(Path.Combine(bundle.BundleDirectory, "unlisted.txt"), "extra"); break;
            case "directory": Directory.CreateDirectory(Path.Combine(bundle.BundleDirectory, "unlisted")); break;
        }
        await Assert.ThrowsAsync<CaseBundleValidationException>(() => bundle.VerifyAsync());
    }

    [Theory]
    [InlineData("extension")]
    [InlineData("mime")]
    [InlineData("truncated")]
    [InlineData("placeholder")]
    [InlineData("traversal")]
    [InlineData("duplicate")]
    [InlineData("extra")]
    [InlineData("audio")]
    public async Task InvalidRenderedArtifact_CannotBecomeASealedBundle(string defect)
    {
        using var workspace = new BundleTestWorkspace();
        var directory = workspace.NewCaseDirectory();
        var assetsDirectory = Path.Combine(directory, "assets");
        Directory.CreateDirectory(assetsDirectory);
        var name = defect == "extension" ? "record.png" : "record.pdf";
        var bytes = defect switch
        {
            "truncated" => "%PDF-1.7\nno trailer"u8.ToArray(),
            "placeholder" => "# Placeholder evidence"u8.ToArray(),
            _ => BundleTestWorkspace.PdfBytes
        };
        await File.WriteAllBytesAsync(Path.Combine(assetsDirectory, name), bytes);
        var report = new AssetRenderingReport
        {
            Files = [new RenderedAssetFile("asset.record", defect == "traversal" ? "../record.pdf" : name,
                defect == "mime" ? "image/png" : "application/pdf")]
        };
        if (defect == "duplicate") report.Files.Add(report.Files[0]);
        if (defect == "extra") await File.WriteAllTextAsync(Path.Combine(assetsDirectory, "sidecar.mp3.txt"), "not audio");
        var asset = new EvidenceAsset { Id = "asset.record", Type = defect == "audio" ? "audio" : "pdf", Title = "Record" };
        await Assert.ThrowsAsync<CaseBundleValidationException>(() =>
            CaseBundle.CreateAsync("case_bundle", 0, directory, BundleTestWorkspace.Json("case_bundle"), [asset], report));
    }

    [Fact]
    public async Task LocalCommit_ChangesOnlyJsonAndPreservesLegacyAndPreviousBundleFiles()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        var root = workspace.LocalRoot;
        var caseDirectory = Path.Combine(root, "case_bundle");
        Directory.CreateDirectory(Path.Combine(caseDirectory, "assets", "previous_bundle"));
        var originalJson = Path.Combine(caseDirectory, "case.json");
        var legacyAsset = Path.Combine(caseDirectory, "assets", "record.pdf");
        var previousAsset = Path.Combine(caseDirectory, "assets", "previous_bundle", "record.pdf");
        await File.WriteAllTextAsync(originalJson, "original case");
        await File.WriteAllTextAsync(legacyAsset, "legacy bytes");
        await File.WriteAllTextAsync(previousAsset, "previous immutable bytes");
        using (var publication = await LocalCasePublication.PrepareAsync(bundle, root, NullLogger.Instance))
        {
            Assert.Equal("original case", await File.ReadAllTextAsync(originalJson));
            Assert.True(File.Exists(Path.Combine(caseDirectory, "assets", bundle.Manifest.BundleId, "manifest.json")));
            Assert.Equal(originalJson, await publication.CommitAsync());
        }
        Assert.Equal(bundle.CaseJson, await File.ReadAllTextAsync(originalJson));
        Assert.Equal("legacy bytes", await File.ReadAllTextAsync(legacyAsset));
        Assert.Equal("previous immutable bytes", await File.ReadAllTextAsync(previousAsset));
    }

    [Fact]
    public async Task UncommittedLocalBundle_CleansOnlyItsOwnFiles()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        var caseDirectory = Path.Combine(workspace.LocalRoot, "case_bundle");
        Directory.CreateDirectory(Path.Combine(caseDirectory, "assets"));
        await File.WriteAllTextAsync(Path.Combine(caseDirectory, "case.json"), "original case");
        await File.WriteAllTextAsync(Path.Combine(caseDirectory, "assets", "extra.txt"), "keep");
        using (await LocalCasePublication.PrepareAsync(bundle, workspace.LocalRoot, NullLogger.Instance)) { }
        Assert.False(Directory.Exists(Path.Combine(caseDirectory, "assets", bundle.Manifest.BundleId)));
        Assert.Equal("original case", await File.ReadAllTextAsync(Path.Combine(caseDirectory, "case.json")));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(caseDirectory, "assets", "extra.txt")));
    }

    [Fact]
    public async Task ConcurrentLocalPublication_SameCaseIsRejectedUntilLockReleased()
    {
        using var workspace = new BundleTestWorkspace();
        var firstBundle = await workspace.CreateAsync();
        var secondBundle = await workspace.CreateAsync();
        using (var first = await LocalCasePublication.PrepareAsync(firstBundle, workspace.LocalRoot, NullLogger.Instance))
        {
            await Assert.ThrowsAsync<CasePublicationConflictException>(() =>
                LocalCasePublication.PrepareAsync(secondBundle, workspace.LocalRoot, NullLogger.Instance));
            await first.CommitAsync();
        }
        using var second = await LocalCasePublication.PrepareAsync(secondBundle, workspace.LocalRoot, NullLogger.Instance);
        await second.CommitAsync();
        Assert.Equal(secondBundle.CaseJson, await File.ReadAllTextAsync(Path.Combine(workspace.LocalRoot, "case_bundle", "case.json")));
        Assert.True(Directory.Exists(Path.Combine(workspace.LocalRoot, "case_bundle", "assets", firstBundle.Manifest.BundleId)));
    }

    [Fact]
    public async Task ExternalLocalJsonChange_IsNotOverwrittenAtCommit()
    {
        using var workspace = new BundleTestWorkspace();
        var bundle = await workspace.CreateAsync();
        using var publication = await LocalCasePublication.PrepareAsync(bundle, workspace.LocalRoot, NullLogger.Instance);
        var path = Path.Combine(workspace.LocalRoot, "case_bundle", "case.json");
        await File.WriteAllTextAsync(path, "concurrently committed case");
        await Assert.ThrowsAsync<CasePublicationConflictException>(() => publication.CommitAsync());
        Assert.Equal("concurrently committed case", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task RepeatedGeneration_UsesDifferentImmutablePathsAndLeavesOldBytesReadable()
    {
        using var fixture = new CaseGenerationTestFixture();
        var first = await fixture.GenerateAsync();
        var firstBytes = JsonNode.Parse(first.CaseJson)!["assets"]!.AsArray().Select(a =>
        {
            var relative = a!["uri"]!.GetValue<string>()["case://case_quality/".Length..];
            var path = Path.Combine(fixture.Root, "case_quality", relative);
            return (path, bytes: File.ReadAllBytes(path));
        }).ToList();
        var second = await fixture.GenerateAsync();
        Assert.True(second.Quality.Approved);
        Assert.NotEqual(first.Bundle!.BundleId, second.Bundle!.BundleId);
        foreach (var (path, bytes) in firstBytes) Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Equal(second.CaseJson, await File.ReadAllTextAsync(fixture.CasePath));
        Assert.Equal(second.Bundle, CaseV2JobResult.FromResponse("bundle-job", second).Bundle);
    }

    [Fact]
    public async Task InvalidBytes_BlockEvenWithCompleteRenderingCounts()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        fixture.Renderer.InvalidBytes = true;
        var response = await fixture.GenerateAsync();
        Assert.Contains(response.Quality.Issues, i => i.Code == "asset_integrity_failed" && i.Reference == "asset.scene");
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Equal("original case", await File.ReadAllTextAsync(fixture.CasePath));
    }

    [Fact]
    public async Task CancellationDuringPublication_RemovesOnlyNewLocalBundle()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        using var source = new CancellationTokenSource();
        fixture.Publisher.OnPublish = (_, _) => source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.GenerateAsync(ct: source.Token));
        Assert.Equal("original case", await File.ReadAllTextAsync(fixture.CasePath));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Root, "case_quality", "assets")));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Root, ".casegen-staging")));
    }

    [Theory]
    [InlineData("audio")]
    [InlineData("video")]
    [InlineData("physical")]
    [InlineData("unsupported")]
    public async Task UnsupportedTypes_NeverWritePlaceholders(string type)
    {
        using var fixture = new CaseGenerationTestFixture();
        var renderer = Renderer(fixture.Llm);
        var result = await renderer.RenderAllAsync("case_quality", fixture.Root, [new EvidenceAsset { Id = "asset.record", Type = type, Title = "Record" }]);
        Assert.Single(result.Errors);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(result.Files);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(fixture.Root, "case_quality", "assets")));
    }

    [Fact]
    public async Task UnknownImageBytes_AreNotSilentlyWrittenAsPng()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.ImageBytes = "not an image"u8.ToArray();
        var result = await Renderer(fixture.Llm).RenderAllAsync("case_quality", fixture.Root,
            [new EvidenceAsset { Id = "asset.record", Type = "photo", Title = "Record" }]);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.ImagesWritten);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task AudioPlan_IsRejectedInsteadOfPublishingAMissingRecording()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.TransformResponse = (system, _, node) =>
        {
            if (system.Contains("**canonical CaseBible**")) node["sources"]![0]!["type"] = "audio";
            return node;
        };
        await Assert.ThrowsAsync<TaskContractException>(() => fixture.GenerateAsync());
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Contains(fixture.Llm.Calls, c => c.System.Contains("document transcript"));
    }

    private static AssetRenderingService Renderer(ILLMProvider llm) => new(
        new PdfRenderingService(new ConfigurationBuilder().Build(), NullLogger<PdfRenderingService>.Instance), llm,
        new EvidenceDocumentRenderer(NullLogger<EvidenceDocumentRenderer>.Instance), new EvidenceTemplateRegistry([]),
        NullLogger<AssetRenderingService>.Instance);
}

internal sealed class BundleTestWorkspace : IDisposable
{
    public static byte[] PdfBytes => "%PDF-1.7\nSynthetic evidence fixture\n%%EOF\n"u8.ToArray();
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "casegen-bundle-tests", Guid.NewGuid().ToString("N"));
    public string LocalRoot => Path.Combine(Root, "local");
    public BundleTestWorkspace() => Directory.CreateDirectory(Root);
    public string NewCaseDirectory(string caseId = "case_bundle") => Path.Combine(Root, Guid.NewGuid().ToString("N"), caseId);

    public async Task<CaseBundle> CreateAsync(string caseId = "case_bundle", int revision = 0)
    {
        var directory = NewCaseDirectory(caseId);
        Directory.CreateDirectory(Path.Combine(directory, "assets"));
        await File.WriteAllBytesAsync(Path.Combine(directory, "assets", "record.pdf"), PdfBytes);
        return await CaseBundle.CreateAsync(caseId, revision, directory, Json(caseId),
            [new EvidenceAsset { Id = "asset.record", Type = "pdf", Title = "Record" }],
            new AssetRenderingReport { PdfsWritten = 1, Files = [new RenderedAssetFile("asset.record", "record.pdf", "application/pdf")] });
    }

    public static string Json(string caseId) => JsonSerializer.Serialize(new
    {
        version = "2.0", caseId, assets = new[] { new { id = "asset.record", type = "pdf", title = "Record", uri = "provisional", visibility = "initial" } },
        gameMetadata = new { generation = new { bundleChecksum = "" } }
    });
    public void Dispose() => Directory.Delete(Root, recursive: true);
}
