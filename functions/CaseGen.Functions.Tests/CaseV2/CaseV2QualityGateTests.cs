using CaseGen.Functions.Functions.CaseV2;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Templates;
using CaseGen.Functions.Services.CaseV2.Rendering;
using CaseGen.Functions.Services.CaseV2.Rendering.Layouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseV2QualityGateTests
{
    [Theory]
    [InlineData("reject", "high")]
    [InlineData("needs_review", "medium")]
    [InlineData("ok", "high")]
    [InlineData("ok", "medium")]
    [InlineData("reject", "low")]
    [InlineData("needs_review", "low")]
    public async Task BlockingReview_NeverRendersPersistsOrPublishes(string verdict, string severity)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.ReviewContent = $$"""
            {"verdict":"{{verdict}}","findings":[{"severity":"{{severity}}","area":"timeline","issue":"Contradictory times","reference":"asset.scene"}]}
            """;
        var response = await fixture.GenerateAsync();
        Assert.DoesNotContain(response.ValidationErrors, error => error.StartsWith("$/", StringComparison.Ordinal));
        Assert.False(response.Quality.Approved);
        Assert.True(response.Quality.HasBlockingIssues);
        Assert.Contains(response.Quality.Issues, issue => issue.Reference == "asset.scene");
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
        Assert.True(CaseV2JobResult.FromResponse("job", response).HasErrors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrUnavailableReview_BlocksPublication(bool unavailable)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.ReviewContent = "{\"verdict\":\"ok\"}";
        if (unavailable) fixture.Llm.ReviewException = new HttpRequestException("Offline");
        var response = await fixture.GenerateAsync();
        Assert.False(response.Quality.Approved);
        Assert.Contains(response.Quality.Issues, issue => issue.Code == (unavailable ? "red_team_unavailable" : "red_team_invalid"));
        Assert.False(response.Repair.Attempted);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
    }

    [Fact]
    public async Task LowFinding_IsDiagnosticAndDoesNotBlockAnOkReview()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.ReviewContent = """{"verdict":"ok","findings":[{"severity":"low","area":"tone","issue":"Minor wording issue"}]}""";
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Approved);
        Assert.False(Assert.Single(response.Quality.Issues).Blocking);
        Assert.True(response.Quality.Published);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderingFailure_IsIsolatedAndDoesNotChangeExistingCase(bool existing)
    {
        using var fixture = new CaseGenerationTestFixture();
        if (existing) fixture.SeedExistingCase();
        fixture.Renderer.Fail = true;
        fixture.Renderer.OnRender = (caseId, basePath) =>
        {
            Assert.NotEqual(fixture.Root, basePath);
            Assert.StartsWith(Path.Combine(fixture.Root, ".casegen-staging"), basePath);
            Assert.True(File.Exists(Path.Combine(basePath, caseId, "case.json")));
            Assert.Equal(existing, File.Exists(fixture.CasePath));
        };
        var response = await fixture.GenerateAsync();
        Assert.Empty(response.ValidationErrors);
        Assert.False(response.Quality.Approved);
        Assert.False(response.Quality.Persisted);
        Assert.False(response.Quality.Published);
        Assert.Contains(response.Quality.Issues, issue => issue.Code == "asset_render_failed");
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(response.OutputPath);
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Root, ".casegen-staging")));
        if (existing)
        {
            Assert.Equal("original case", File.ReadAllText(fixture.CasePath));
            Assert.Equal("original asset", File.ReadAllText(Path.Combine(fixture.Root, "case_quality", "assets", "scene.pdf")));
        }
        else
            Assert.Empty(fixture.DiscoverableCases());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrEmptyFiles_BlockEvenWhenRendererReportsSuccess(bool empty)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Renderer.MissingFiles = !empty;
        fixture.Renderer.EmptyFiles = empty;
        var response = await fixture.GenerateAsync();
        Assert.Empty(response.AssetRenderingErrors);
        Assert.Equal(PublicAssets(response).Count(asset => asset!["type"]!.GetValue<string>() is "pdf" or "document" or "digital"),
            response.AssetsRenderedPdfs);
        Assert.Contains(response.Quality.Issues, issue => issue.Code == "asset_file_missing" && issue.Reference == "asset.initial_report");
        Assert.False(response.Quality.Approved);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
    }

    [Fact]
    public async Task SkippedAssets_BlockPublication()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Renderer.Skip = true;
        var response = await fixture.GenerateAsync();
        Assert.Contains(response.Quality.Issues, issue => issue.Code == "assets_skipped");
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
    }

    [Fact]
    public async Task ActualJpegFilename_DeterminesThePublishedUri()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Llm.PhotoAsset = true;
        fixture.Renderer.JpegPhotos = true;
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Approved);
        Assert.True(response.Bundle!.Verified);
        var images = PublicAssets(response).Where(asset => asset!["type"]!.GetValue<string>() is "photo" or "image").ToList();
        Assert.NotEmpty(images);
        Assert.Equal(images.Count, response.AssetsRenderedImages);
        foreach (var asset in images)
        {
            var uri = asset!["uri"]!.GetValue<string>();
            Assert.StartsWith($"case://case_quality/assets/{response.Bundle.BundleId}/", uri);
            Assert.EndsWith(".jpg", uri);
            Assert.Equal("image/jpeg", asset["metadata"]!["mimeType"]!.GetValue<string>());
            Assert.True(File.Exists(Path.Combine(fixture.Root, "case_quality", uri["case://case_quality/".Length..])));
        }
        Assert.Equal(1, fixture.Publisher.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredPublisherFailure_IsNotReportedAsSuccess(bool returnsZero)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Publisher.ReturnZero = returnsZero;
        if (!returnsZero) fixture.Publisher.Exception = new Azure.RequestFailedException(503, "Storage offline");
        var response = await fixture.GenerateAsync();
        Assert.False(response.Quality.Approved);
        Assert.False(response.Quality.Persisted);
        Assert.False(response.Quality.Published);
        Assert.Contains(response.Quality.Issues, issue => issue.Code == "publication_failed");
        Assert.Equal(1, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
        Assert.True(CaseV2JobResult.FromResponse("job", response).HasErrors);
        Assert.Contains("publishToBlob", response.StageLatencyMs.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovedBundle_ReportsActualLocalAndPublicationState(bool publishing)
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Publisher.IsConfigured = publishing;
        fixture.Publisher.OnPublish = (jsonPath, assetsPath) =>
        {
            Assert.False(File.Exists(fixture.CasePath));
            Assert.True(File.Exists(jsonPath));
            var assetCount = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(jsonPath))!["assets"]!.AsArray().Count;
            Assert.Equal(assetCount + 1, Directory.EnumerateFiles(assetsPath).Count());
            Assert.True(File.Exists(Path.Combine(assetsPath, "manifest.json")));
        };
        var response = await fixture.GenerateAsync();
        Assert.Empty(response.ValidationErrors);
        Assert.True(response.Quality.Approved);
        Assert.True(response.Quality.Persisted);
        Assert.Equal(publishing, response.Quality.PublicationRequested);
        Assert.Equal(publishing, response.Quality.Published);
        Assert.False(response.Quality.Preview);
        Assert.Equal(publishing ? 1 : 0, fixture.Publisher.Calls);
        Assert.Equal(fixture.CasePath, response.OutputPath);
        Assert.Equal(response.CaseJson, File.ReadAllText(fixture.CasePath));
        Assert.Single(fixture.DiscoverableCases());
        Assert.True(response.Solver!.Correct);
        Assert.InRange(response.Solver.Score, .9, 1);
        Assert.Equal("player-visible", response.Quality.SolverMode);
        Assert.False(CaseV2JobResult.FromResponse("job", response).HasErrors);
    }

    [Fact]
    public async Task SuccessfulReplacement_PreservesUnrelatedFiles()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        fixture.Publisher.OnPublish = (_, _) => Assert.Equal("original case", File.ReadAllText(fixture.CasePath));
        var response = await fixture.GenerateAsync();
        Assert.True(response.Quality.Persisted);
        Assert.Equal(response.CaseJson, File.ReadAllText(fixture.CasePath));
        Assert.Equal("preserved extra", File.ReadAllText(Path.Combine(fixture.Root, "case_quality", "assets", "extra.txt")));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Root, ".casegen-staging")));
    }

    [Fact]
    public async Task UnmanifestedRenderedDirectory_PreservesOriginalCaseAndDoesNotPublish()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        fixture.Renderer.OnRender = (id, basePath) =>
            Directory.CreateDirectory(Path.Combine(basePath, id, "assets", "extra.txt"));
        var response = await fixture.GenerateAsync();
        Assert.Contains(response.Quality.Issues, issue => issue.Code == "bundle_invalid");
        Assert.False(response.Quality.Persisted);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Equal("original case", File.ReadAllText(fixture.CasePath));
        Assert.Equal("preserved extra", File.ReadAllText(Path.Combine(fixture.Root, "case_quality", "assets", "extra.txt")));
    }

    [Fact]
    public async Task LocalPromotionFailure_AfterPublicationRetainsActualPublicationState()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.Publisher.OnPublish = (_, _) =>
            Directory.CreateDirectory(fixture.CasePath);
        var response = await fixture.GenerateAsync();
        Assert.Contains(response.Quality.Issues, issue => issue.Code == "persistence_failed");
        Assert.False(response.Quality.Approved);
        Assert.False(response.Quality.Persisted);
        Assert.True(response.Quality.Published);
        Assert.Equal(PublicAssets(response).Count + 2, response.BlobsPublished);
        Assert.Empty(response.OutputPath);
        Assert.Empty(fixture.DiscoverableCases());
        Assert.True(CaseV2JobResult.FromResponse("job", response).HasErrors);
    }

    [Fact]
    public async Task Preview_ApprovesOnlyDraftWithoutRenderingSavingOrPublishing()
    {
        using var fixture = new CaseGenerationTestFixture { RepairMaxIterations = 0 };
        var response = await fixture.GenerateAsync(writeToDisk: false);
        Assert.True(response.Quality.Approved, string.Join("\n", response.ValidationErrors.Concat(
            response.Quality.Issues.Select(issue => $"{issue.Code}: {issue.Message}"))
            .Append($"Solver: {response.Solver?.ErrorCode} {response.Solver?.Notes}")));
        Assert.True(response.Quality.Preview);
        Assert.False(response.Quality.PersistenceRequested);
        Assert.False(response.Quality.PublicationRequested);
        Assert.False(response.Quality.Persisted);
        Assert.False(response.Quality.Published);
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
        Assert.False(CaseV2JobResult.FromResponse("job", response).HasErrors);
    }

    [Fact]
    public async Task AcceptedRefine_IsCompiledReviewedAndRenderedFromCanonicalRevision()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.BeforeConsistency = draft => draft.AssetFull[0].BodyDoc!.Sections[0].Text = "Unreadable transcription.";
        var response = await fixture.GenerateAsync();
        Assert.True(response.RefineAttempted);
        Assert.True(response.Repair.Accepted, response.Repair.ErrorMessage);
        Assert.True(response.RefineErrorsBefore > response.RefineErrorsAfter);
        Assert.Empty(response.ValidationErrors);
        Assert.True(response.Quality.Approved);
        Assert.Equal(1, response.Quality.Revision);
        Assert.Equal(1, response.Quality.ReviewedRevision);
        Assert.Contains("targetedRepair", response.StageLatencyMs.Keys);
        Assert.True(response.RedTeamRerun);
        Assert.True(response.Solver!.Correct);
        Assert.Equal(1, fixture.Renderer.Calls);
        Assert.Equal(1, fixture.Publisher.Calls);
        Assert.Single(fixture.DiscoverableCases());
    }

    [Fact]
    public async Task SchemaError_RemainsBlockingAfterUnsuccessfulRefine()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.BeforeConsistency = draft => draft.Metadata.Difficulty = "InvalidRank";
        fixture.Llm.InvalidRefine = true;
        var response = await fixture.GenerateAsync();
        Assert.NotEmpty(response.ValidationErrors);
        Assert.Contains(response.Quality.Issues, issue => issue.Code == "schema_invalid");
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
    }

    [Fact]
    public async Task OriginalConsistencyError_CannotBeClearedBySchemaOnlyRefine()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.BeforeConsistency = draft => draft.ForensicFull[0].InputAssetId = "asset.unknown";
        var response = await fixture.GenerateAsync();
        Assert.False(response.Quality.Approved);
        Assert.Contains(response.Quality.Issues, issue => issue.Blocking && issue.Message.Contains("asset.unknown", StringComparison.Ordinal));
        Assert.Contains(response.ValidationErrors, error => error.Contains("asset.unknown", StringComparison.Ordinal));
        Assert.Equal(0, fixture.Renderer.Calls);
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Empty(fixture.DiscoverableCases());
    }

    [Fact]
    public async Task RenderCancellation_CleansWorkspaceAndPreservesExistingCase()
    {
        using var fixture = new CaseGenerationTestFixture();
        fixture.SeedExistingCase();
        using var source = new CancellationTokenSource();
        fixture.Renderer.OnRender = (_, _) => source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.GenerateAsync(ct: source.Token));
        Assert.Equal(0, fixture.Publisher.Calls);
        Assert.Equal("original case", File.ReadAllText(fixture.CasePath));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Root, ".casegen-staging")));
    }

    [Fact]
    public async Task ActualImageRenderer_DoesNotSwallowRequestedCancellation()
    {
        using var fixture = new CaseGenerationTestFixture();
        using var source = new CancellationTokenSource();
        fixture.Llm.OnImage = source.Cancel;
        var renderer = new AssetRenderingService(
            new PdfRenderingService(new ConfigurationBuilder().Build(), NullLogger<PdfRenderingService>.Instance),
            fixture.Llm,
            new EvidenceDocumentRenderer(
                new DocumentLayoutRegistry([new GeneralReportLayout()], NullLogger<DocumentLayoutRegistry>.Instance),
                NullLogger<EvidenceDocumentRenderer>.Instance),
            new EvidenceTemplateRegistry(Array.Empty<IEvidenceTemplate>()),
            NullLogger<AssetRenderingService>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            renderer.RenderAllAsync("case_quality", fixture.Root,
                new[] { new EvidenceAsset { Id = "asset.scene", Type = "photo", Title = "Scene", Body = "Documentary scene view." } }, source.Token));
    }

    private static System.Text.Json.Nodes.JsonArray PublicAssets(GenerateCaseV2Response response) =>
        System.Text.Json.Nodes.JsonNode.Parse(response.CaseJson)!["assets"]!.AsArray();
}
