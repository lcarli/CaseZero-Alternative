using System.Text;
using System.Text.Json;
using Azure.Core;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class CaseV2StorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "case-v2-storage-tests", Guid.NewGuid().ToString("N"));
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly CaseV2StorageService _storage;

    public CaseV2StorageServiceTests()
    {
        Directory.CreateDirectory(_root);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CaseGenV2:LocalCasesPath"] = _root,
            ["CaseGenV2:UseBlobStorage"] = "false",
            ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true"
        }).Build();
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_root);
        _storage = new CaseV2StorageService(configuration, Mock.Of<ICaseV2SanitizerService>(),
            NullLogger<CaseV2StorageService>.Instance, _cache, env.Object, Mock.Of<TokenCredential>());
    }

    [Theory]
    [InlineData("assets/record.pdf", "application/pdf")]
    [InlineData("assets/record.jpg", "image/jpeg")]
    [InlineData("assets/0123456789abcdef0123456789abcdef/record.pdf", "application/pdf")]
    [InlineData("assets/0123456789abcdef0123456789abcdef/record.jpg", "image/jpeg")]
    [InlineData("assets/0123456789abcdef0123456789abcdef/record.png", "image/png")]
    [InlineData("assets/0123456789abcdef0123456789abcdef/record.gif", "image/gif")]
    [InlineData("assets/0123456789abcdef0123456789abcdef/record.webp", "image/webp")]
    public async Task LegacyAndImmutableUris_ReadActualFileWithCorrectMime(string relative, string contentType)
    {
        var raw = Definition(relative);
        var caseDirectory = Path.Combine(_root, raw.CaseId);
        var file = Path.Combine(caseDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "fixture bytes");
        await File.WriteAllTextAsync(Path.Combine(caseDirectory, "case.json"), JsonSerializer.Serialize(raw));
        using var stream = await _storage.GetAssetStreamAsync(raw.CaseId, "asset.record");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        Assert.Equal("fixture bytes", await reader.ReadToEndAsync());
        Assert.Equal(contentType, await _storage.GetAssetContentTypeAsync(raw.CaseId, "asset.record"));
    }

    [Fact]
    public async Task Dashboard_DoesNotDiscoverStagingOrUncommittedBundleDirectories()
    {
        var committed = Definition("assets/record.pdf");
        var caseDirectory = Path.Combine(_root, committed.CaseId);
        Directory.CreateDirectory(caseDirectory);
        await File.WriteAllTextAsync(Path.Combine(caseDirectory, "case.json"), JsonSerializer.Serialize(committed));
        var staging = Path.Combine(_root, ".casegen-staging", "job", "case_uncommitted");
        Directory.CreateDirectory(staging);
        await File.WriteAllTextAsync(Path.Combine(staging, "case.json"), JsonSerializer.Serialize(Definition("assets/record.pdf")));
        Directory.CreateDirectory(Path.Combine(_root, "case_uncommitted", "assets", "bundle"));
        Directory.CreateDirectory(Path.Combine(_root, ".casegen-locks"));
        var listed = await _storage.ListCasesAsync();
        Assert.Equal(committed.CaseId, Assert.Single(listed).CaseId);
    }

    private static CaseV2 Definition(string relative) => new()
    {
        Version = "2.0", CaseId = "case_storage",
        Metadata = new CaseV2Metadata { Title = "Storage fixture", Description = "Synthetic case", Location = "Test city" },
        Assets = [new CaseV2Asset { Id = "asset.record", Type = "pdf", Title = "Record", Visibility = "initial", Uri = $"case://case_storage/{relative}" }]
    };

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, recursive: true);
    }
}
