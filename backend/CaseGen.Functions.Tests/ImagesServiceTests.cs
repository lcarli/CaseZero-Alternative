using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;

namespace CaseGen.Functions.Tests;

public class ImagesServiceTests
{
    private readonly IImagesService _imagesService;
    private readonly IStorageService _mockStorageService;
    private readonly ILLMService _mockLLMService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImagesService> _logger;

    public ImagesServiceTests()
    {
        // Create simple test configuration
        var configDict = new Dictionary<string, string>
        {
            ["CaseGeneratorStorage:BundlesContainer"] = "test-bundles"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();

        // Create mock logger
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ImagesService>();

        // Create mock storage service
        _mockStorageService = new MockStorageService();

        // Create mock LLM service
        _mockLLMService = new MockLLMProvider();

        // Create the service
        _imagesService = new ImagesService(_mockStorageService, _configuration, _logger, _mockLLMService);
    }

    [Fact]
    public async Task GenerateAsync_WithPhotoType_ShouldCreatePlaceholderImage()
    {
        // Arrange
        var caseId = "TEST-CASE-001";
        var mediaSpec = new MediaSpec
        {
            EvidenceId = "photo_001",
            Kind = MediaTypes.Photo,
            Title = "Test Photo Evidence",
            Prompt = JsonSerializer.Serialize(new { genPrompt = "A forensic photo of evidence" }),
            Constraints = new Dictionary<string, object> { ["lighting"] = "bright", ["angle"] = "top-down" }
        };

        // Act
        var result = await _imagesService.GenerateAsync(caseId, mediaSpec);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("test-bundles", result);
        Assert.Contains("photo_001.placeholder.json", result);
    }

    [Fact]
    public async Task GenerateAsync_WithAudioType_ShouldDefer()
    {
        // Arrange
        var caseId = "TEST-CASE-002";
        var mediaSpec = new MediaSpec
        {
            EvidenceId = "audio_001",
            Kind = MediaTypes.Audio,
            Title = "Test Audio Evidence",
            Prompt = "Audio recording of interview"
        };

        // Act
        var result = await _imagesService.GenerateAsync(caseId, mediaSpec);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("DEFERRED:", result);
        Assert.Contains("Non-image type deferred", result);
    }

    [Fact]
    public async Task GenerateAsync_WithDiagramType_ShouldCreatePlaceholderImage()
    {
        // Arrange
        var caseId = "TEST-CASE-003";
        var mediaSpec = new MediaSpec
        {
            EvidenceId = "diagram_001",
            Kind = MediaTypes.Diagram,
            Title = "Crime Scene Diagram",
            Prompt = JsonSerializer.Serialize(new { genPrompt = "A detailed diagram of the crime scene layout" })
        };

        // Act
        var result = await _imagesService.GenerateAsync(caseId, mediaSpec);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("diagram_001.placeholder.json", result);
    }

    [Fact]
    public async Task GenerateAsync_WithMissingPrompt_ShouldDefer()
    {
        // Arrange
        var caseId = "TEST-CASE-004";
        var mediaSpec = new MediaSpec
        {
            EvidenceId = "photo_002",
            Kind = MediaTypes.Photo,
            Title = "Test Photo Without Prompt",
            Prompt = JsonSerializer.Serialize(new { title = "No genPrompt here" })
        };

        // Act
        var result = await _imagesService.GenerateAsync(caseId, mediaSpec);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("DEFERRED:", result);
        Assert.Contains("No generation prompt available", result);
    }
}

public class MockStorageService : IStorageService
{
    private readonly Dictionary<string, object> _storage = new();

    public Task<string> SaveFileAsync(string containerName, string fileName, string content, CancellationToken cancellationToken = default)
    {
        var key = $"{containerName}/{fileName}";
        _storage[key] = content;
        return Task.FromResult($"https://mock-storage.com/{key}");
    }

    public Task<string> SaveFileAsync(string containerName, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var key = $"{containerName}/{fileName}";
        _storage[key] = content;
        return Task.FromResult($"https://mock-storage.com/{key}");
    }

    public Task<string> GetFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default)
    {
        var key = $"{containerName}/{fileName}";
        if (_storage.TryGetValue(key, out var content) && content is string stringContent)
        {
            return Task.FromResult(stringContent);
        }
        throw new FileNotFoundException($"File not found: {key}");
    }

    public Task<bool> FileExistsAsync(string containerName, string fileName, CancellationToken cancellationToken = default)
    {
        var key = $"{containerName}/{fileName}";
        return Task.FromResult(_storage.ContainsKey(key));
    }

    public Task DeleteFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default)
    {
        var key = $"{containerName}/{fileName}";
        _storage.Remove(key);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix = "", CancellationToken cancellationToken = default)
    {
        var keys = _storage.Keys
            .Where(k => k.StartsWith($"{containerName}/") && k.Substring(containerName.Length + 1).StartsWith(prefix))
            .Select(k => k.Substring(containerName.Length + 1));
        return Task.FromResult(keys);
    }
}