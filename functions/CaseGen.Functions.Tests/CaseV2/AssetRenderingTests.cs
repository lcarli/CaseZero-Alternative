using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using CaseGen.Functions.Services.CaseV2.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class AssetRenderingTests
{
    [Fact]
    public void PortraitPrompt_IsNeutralAndDoesNotUseCrimeScenePrefix()
    {
        var prompt = AssetRenderingService.BuildImagePrompt(new EvidenceAsset
        {
            Id = "asset.portrait_ana",
            Type = "photo",
            Title = "Portrait — Ana",
            Body = "Ana standing at a crime scene",
            ImagePurpose = ImagePurposes.SuspectPortrait,
            SubjectSuspectId = "suspect.ana"
        });

        Assert.Contains("neutral identification portrait", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("crime-scene", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("scene", "crime-scene evidence photograph")]
    [InlineData("object", "evidence-object photograph")]
    [InlineData("surveillance", "surveillance-camera still")]
    public void ImagePrompt_UsesPurposeSpecificPrefix(string purpose, string expected)
    {
        var prompt = AssetRenderingService.BuildImagePrompt(new EvidenceAsset
        {
            Id = $"asset.{purpose}",
            Type = "photo",
            Title = purpose,
            Body = $"A bounded documentary {purpose} view.",
            ImagePurpose = purpose
        });

        Assert.Contains(expected, prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MandatoryVisual_RetriesTwiceAndReportsFailure()
    {
        var llm = new RecordingImageProvider(alwaysFail: true);
        var service = CreateService(llm);
        var root = TestRoot();
        try
        {
            var report = await service.RenderAllAsync("case_retry", root,
            [
                new EvidenceAsset
                {
                    Id = "asset.portrait_ana",
                    Type = "photo",
                    Title = "Ana",
                    Body = "A neutral identification portrait of Ana.",
                    ImagePurpose = ImagePurposes.SuspectPortrait
                }
            ]);

            Assert.Equal(2, llm.Calls);
            Assert.Contains(report.Errors, error =>
                error.Contains("Mandatory visual render failed", StringComparison.Ordinal));
            Assert.True(report.HasMandatoryVisualErrors);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImageRendering_BoundsConcurrency()
    {
        var llm = new RecordingImageProvider(alwaysFail: false);
        var service = CreateService(llm);
        var root = TestRoot();
        try
        {
            var assets = Enumerable.Range(1, 8).Select(index => new EvidenceAsset
            {
                Id = $"asset.scene_photo_{index}",
                Type = "photo",
                Title = $"Scene {index}",
                Body = $"Documentary view of scene location {index}.",
                ImagePurpose = ImagePurposes.Scene
            });

            var report = await service.RenderAllAsync("case_concurrency", root, assets);

            Assert.Equal(8, report.ImagesWritten);
            Assert.InRange(llm.MaxConcurrent, 1, 3);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImageRendering_ReportsActualFileExtension()
    {
        var service = CreateService(new JpegImageProvider());
        var root = TestRoot();
        try
        {
            var report = await service.RenderAllAsync("case_jpeg", root,
            [
                new EvidenceAsset
                {
                    Id = "asset.scene_photo_1",
                    Type = "photo",
                    Title = "Scene",
                    Body = "Documentary view of the incident location.",
                    ImagePurpose = ImagePurposes.Scene
                }
            ]);

            Assert.Equal("scene_photo_1.jpg", report.RenderedFileNames["asset.scene_photo_1"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssetCard_PropagatesClassificationMetadata()
    {
        var fixture = new CanonicalDraftFixture();
        await fixture.InitializeAsync();
        var draft = fixture.Draft.Copy();
        var stub = draft.AssetStubs.First(asset => asset.ImagePurpose == ImagePurposes.SuspectPortrait);
        var asset = await new AssetCardTask(fixture.Provider, NullLogger.Instance).RunAsync(draft, stub, default);
        Assert.Equal(stub.EvidenceRole, asset.EvidenceRole);
        Assert.Equal(stub.SubjectSuspectId, asset.SubjectSuspectId);
        Assert.Equal(stub.ImagePurpose, asset.ImagePurpose);
    }

    [Fact]
    public async Task AssetCard_RejectsPhotoWithoutAuthoredPrompt()
    {
        var fixture = new CanonicalDraftFixture();
        await fixture.InitializeAsync();
        var draft = fixture.Draft.Copy();
        var stub = draft.AssetStubs.First(asset => asset.Type == "photo");
        var provider = new MutatingAssetProvider(fixture.Provider, json => json["body"] = "");
        await Assert.ThrowsAsync<TaskContractException>(() =>
            new AssetCardTask(provider, NullLogger.Instance).RunAsync(draft, stub, default));
    }

    [Fact]
    public async Task AssetCard_RejectsDigitalAssetWithoutAuthoredDocument()
    {
        var fixture = new CanonicalDraftFixture();
        await fixture.InitializeAsync();
        var draft = fixture.Draft.Copy();
        var stub = draft.AssetStubs.First(asset => asset.Type == "digital");
        var provider = new MutatingAssetProvider(fixture.Provider, json => json["bodyDoc"] = null);
        await Assert.ThrowsAsync<TaskContractException>(() =>
            new AssetCardTask(provider, NullLogger.Instance).RunAsync(draft, stub, default));
    }

    [Fact]
    public async Task AssetCard_DoesNotInjectFactsMissingFromAuthoredEvidence()
    {
        var fixture = new CanonicalDraftFixture();
        await fixture.InitializeAsync();
        var draft = fixture.Draft.Copy();
        var stub = draft.AssetStubs.First(asset => asset.Type == "document");
        var provider = new MutatingAssetProvider(fixture.Provider, json =>
        {
            json["body"] = "";
            json["bodyDoc"]!["sections"] = System.Text.Json.Nodes.JsonNode.Parse("""
                [{"kind":"narrative","heading":"Summary","text":"Routine facilities records."}]
                """);
        });
        await Assert.ThrowsAsync<TaskContractException>(() =>
            new AssetCardTask(provider, NullLogger.Instance).RunAsync(draft, stub, default));
    }

    private sealed class MutatingAssetProvider(ILLMProvider inner,
        Action<System.Text.Json.Nodes.JsonObject> mutate) : ILLMProvider
    {
        public async Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt,
            string jsonSchema, CancellationToken cancellationToken = default)
        {
            var response = await inner.GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
            var json = System.Text.Json.Nodes.JsonNode.Parse(response.Content)!.AsObject();
            mutate(json);
            response.Content = json.ToJsonString();
            return response;
        }
        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static AssetRenderingService CreateService(ILLMProvider llm) =>
        new(
            new StubPdfRenderer(),
            llm,
            new StubDocumentRenderer(),
            new StubTemplateRegistry(),
            NullLogger<AssetRenderingService>.Instance);

    private static string TestRoot() =>
        Path.Combine(Directory.GetCurrentDirectory(), ".test-artifacts", Guid.NewGuid().ToString("N"));

    private sealed class RecordingImageProvider(bool alwaysFail) : ILLMProvider
    {
        private int _active;
        private int _maxConcurrent;
        private int _calls;
        public int Calls => _calls;
        public int MaxConcurrent => _maxConcurrent;

        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            try
            {
                await Task.Delay(20, cancellationToken);
                if (alwaysFail) throw new InvalidOperationException("synthetic image failure");
                return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            }

            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            GenerateImageAsync(prompt, cancellationToken);

        private void UpdateMaximum(int active)
        {
            int observed;
            do
            {
                observed = _maxConcurrent;
                if (observed >= active) return;
            } while (Interlocked.CompareExchange(ref _maxConcurrent, active, observed) != observed);
        }
    }

    private sealed class AssetCardProvider : ILLMProvider
    {
        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(
            string systemPrompt,
            string userPrompt,
            string jsonSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LLMResponse
            {
                Content = """
                          {
                            "id": "asset.portrait_ana",
                            "type": "photo",
                            "title": "Portrait — Ana",
                            "description": "Neutral identification portrait.",
                            "visibility": "initial",
                            "category": "Document",
                            "body": "Head-and-shoulders portrait of Ana.",
                            "bodyDoc": null
                          }
                          """
            });

        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class JpegImageProvider : ILLMProvider
    {
        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]>([0xFF, 0xD8, 0xFF, 0xE0]);

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            GenerateImageAsync(prompt, cancellationToken);
    }

    private sealed class EmptyPhotoProvider : ILLMProvider
    {
        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LLMResponse
            {
                Content = """
                          {
                            "id": "asset.scene_photo_1",
                            "type": "photo",
                            "title": "Scene photograph",
                            "description": "Documentary view of the scene.",
                            "visibility": "initial",
                            "category": "Document",
                            "body": "",
                            "bodyDoc": null
                          }
                          """
            });

        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class DigitalAssetCardProvider : ILLMProvider
    {
        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(
            string systemPrompt,
            string userPrompt,
            string jsonSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LLMResponse
            {
                Content = """
                          {
                            "id": "asset.access_log",
                            "type": "digital",
                            "title": "Access log",
                            "description": "Facilities access export.",
                            "visibility": "initial",
                            "category": "Digital",
                            "body": "",
                            "bodyDoc": {
                              "layout": "AccessLog",
                              "title": "Access log",
                              "classification": "Official",
                              "header": [],
                              "sections": [
                                {
                                  "kind": "narrative",
                                  "heading": "Summary",
                                  "text": "Routine facilities records."
                                }
                              ]
                            }
                          }
                          """
            });

        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyDigitalProvider : ILLMProvider
    {
        public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LLMResponse> GenerateStructuredResponseAsync(
            string systemPrompt,
            string userPrompt,
            string jsonSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LLMResponse
            {
                Content = """
                          {
                            "id": "asset.bank_statement",
                            "type": "digital",
                            "title": "Bank statement",
                            "description": "Account activity for the review period.",
                            "visibility": "initial",
                            "category": "Digital",
                            "body": "",
                            "bodyDoc": null
                          }
                          """
            });

        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<byte[]> GenerateImageWithReferenceAsync(
            string prompt,
            byte[] referenceImage,
            byte[]? maskImage = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubPdfRenderer : IPdfRenderingService
    {
        public Task<byte[]> GenerateTestPdfAsync(
            string title,
            string markdownContent,
            string documentType = "general",
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());
    }

    private sealed class StubDocumentRenderer : IEvidenceDocumentRenderer
    {
        public byte[] Render(EvidenceDocument doc) => Array.Empty<byte>();
    }

    private sealed class StubTemplateRegistry : IEvidenceTemplateRegistry
    {
        public EvidenceDocument Apply(EvidenceDocument doc) => doc;
    }
}
