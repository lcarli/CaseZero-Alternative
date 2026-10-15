using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public interface IAssetRenderingService
{
    void PrepareForReview(IEnumerable<EvidenceAsset> assets);
    Task<IReadOnlyDictionary<string, string>> ReadPreparedDocumentsAsync(
        IEnumerable<EvidenceAsset> assets, CancellationToken ct = default);
    /// <summary>
    /// Materialises every asset that has renderable content (pdf/document/photo/image)
    /// to <c>cases/&lt;caseId&gt;/assets/</c>. Returns the number of files written + any errors.
    /// </summary>
    Task<AssetRenderingReport> RenderAllAsync(string caseId, string casesBasePath, IEnumerable<EvidenceAsset> assets, CancellationToken ct = default);
}

public class AssetRenderingReport
{
    public int PdfsWritten { get; set; }
    public int ImagesWritten { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, string> RenderedFileNames { get; set; } = new(StringComparer.Ordinal);
    public List<RenderedAssetFile> Files { get; set; } = new();
    public Dictionary<string, string> ReadableTexts { get; set; } = new(StringComparer.Ordinal);

    public bool HasMandatoryVisualErrors =>
        Errors.Any(error => error.StartsWith("Mandatory visual render failed", StringComparison.Ordinal));
}

public record RenderedAssetFile(string AssetId, string FileName, string MimeType);

/// <summary>
/// Renders v2 assets to actual files next to the case JSON.
/// - pdf/document  → QuestPDF via <see cref="IPdfRenderingService.GenerateTestPdfAsync"/>
/// - photo/image   → image generation via <see cref="ILLMProvider.GenerateImageAsync"/>
/// - digital       → PDF (digital-forensics export rendered via the doc renderer)
/// Audio is intentionally NOT supported anymore — transcribed audio is
/// produced as a `document` with layout = AudioTranscript.
/// </summary>
public class AssetRenderingService : IAssetRenderingService
{
    private const int MaxConcurrentImages = 3;
    private const int MaxImageAttempts = 2;
    private readonly IPdfRenderingService _pdf;
    private readonly ILLMProvider _llm;
    private readonly IEvidenceDocumentRenderer _docRenderer;
    private readonly Templates.IEvidenceTemplateRegistry _templates;
    private readonly ILogger<AssetRenderingService> _logger;

    public AssetRenderingService(
        IPdfRenderingService pdf,
        ILLMProvider llm,
        IEvidenceDocumentRenderer docRenderer,
        Templates.IEvidenceTemplateRegistry templates,
        ILogger<AssetRenderingService> logger)
    {
        _pdf = pdf;
        _llm = llm;
        _docRenderer = docRenderer;
        _templates = templates;
        _logger = logger;
    }

    public void PrepareForReview(IEnumerable<EvidenceAsset> assets)
    {
        foreach (var asset in assets)
        {
            if (asset.PreparedForReview || asset.BodyDoc is null) continue;
            if (string.IsNullOrEmpty(asset.BodyDoc.Title)) asset.BodyDoc.Title = asset.Title;
            if (asset.BodyDoc.Layout is "NewspaperClipping" or "MagazineArticle" or "HandwrittenNote"
                or "PersonalLetter" or "Receipt" or "DiaryEntry" or "WorkSchedule" or "Calendar")
            {
                if (asset.BodyDoc.Classification == "CONFIDENTIAL · INTERNAL USE ONLY")
                    asset.BodyDoc.Classification = string.Empty;
            }
            if (!asset.CanonicalContent) asset.BodyDoc = _templates.Apply(asset.BodyDoc);
            asset.PreparedForReview = true;
        }
    }

    public async Task<AssetRenderingReport> RenderAllAsync(
        string caseId,
        string casesBasePath,
        IEnumerable<EvidenceAsset> assets,
        CancellationToken ct = default)
    {
        var assetsDir = Path.Combine(casesBasePath, caseId, "assets");
        Directory.CreateDirectory(assetsDir);

        var report = new AssetRenderingReport();
        var pdfTasks = new List<Task>();
        var imageTasks = new List<Task>();
        using var imageGate = new SemaphoreSlim(MaxConcurrentImages, MaxConcurrentImages);

        foreach (var a in assets)
        {
            ct.ThrowIfCancellationRequested();
            // Anything with body content goes to the right renderer.
            switch (a.Type)
            {
                case "pdf":
                case "document":
                case "digital":
                    // Digital evidence (POS exports, phone dumps, sensor logs) renders as a PDF
                    // report so the detective gets something readable in the FileViewer.
                    pdfTasks.Add(RenderPdfAsync(caseId, assetsDir, a, report, ct));
                    break;
                case "photo":
                case "image":
                    imageTasks.Add(RenderImageBoundedAsync(caseId, assetsDir, a, report, imageGate, ct));
                    break;
                case "audio":
                default:
                    var error = $"Unsupported evidence type {a.Type} on {a.Id}; transcripts must be documents.";
                    _logger.LogWarning("{Error}", error);
                    lock (report)
                    {
                        report.Errors.Add(error);
                        report.Skipped++;
                    }
                    break;
            }
        }

        // Run PDF renders in parallel (QuestPDF is sync inside, but the LLM
        // model isn't involved). Images go in parallel too — separate from PDFs
        // to avoid stepping on each other when the LLM is the bottleneck.
        await Task.WhenAll(pdfTasks);
        await Task.WhenAll(imageTasks);

        _logger.LogInformation("Rendered {Pdfs} PDFs and {Imgs} images for case {CaseId} (skipped {Skipped}, errors {Errors})",
            report.PdfsWritten, report.ImagesWritten, caseId, report.Skipped, report.Errors.Count);

        return report;
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadPreparedDocumentsAsync(
        IEnumerable<EvidenceAsset> assets, CancellationToken ct = default)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in assets.Where(asset => asset.Type is "pdf" or "document" or "digital"))
        {
            ct.ThrowIfCancellationRequested();
            var bytes = await RenderReadableAsync(asset, ct);
            texts.Add(asset.Id, RenderedEvidenceText.ExtractAndValidate(asset, bytes));
        }
        return texts;
    }

    private Task<byte[]> RenderReadableAsync(EvidenceAsset asset, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (asset.BodyDoc is { Sections.Count: > 0 })
        {
            if (!asset.PreparedForReview)
                throw new TaskContractException($"Document {asset.Id} has not been prepared for review.");
            return Task.FromResult(_docRenderer.Render(asset.BodyDoc));
        }
        if (string.IsNullOrWhiteSpace(asset.Body))
            throw new TaskContractException($"Evidence {asset.Id} has no authored readable content.");
        return _pdf.GenerateTestPdfAsync(asset.Title, asset.Body, asset.Category ?? "general", ct);
    }

    private async Task RenderPdfAsync(string caseId, string dir, EvidenceAsset a, AssetRenderingReport report, CancellationToken ct)
    {
        try
        {
            var bytes = await RenderReadableAsync(a, ct);
            var text = RenderedEvidenceText.ExtractAndValidate(a, bytes);
            var path = Path.Combine(dir, $"{a.Id.Replace("asset.", "")}.pdf");
            await File.WriteAllBytesAsync(path, bytes, ct);
            lock (report)
            {
                report.PdfsWritten++;
                report.ReadableTexts[a.Id] = text;
                report.RenderedFileNames[a.Id] = Path.GetFileName(path);
                report.Files.Add(new(a.Id, Path.GetFileName(path), "application/pdf"));
            }
            _logger.LogDebug("PDF rendered: {Path}", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var msg = $"PDF render failed for {a.Id}: {ex.Message}";
            _logger.LogWarning(ex, msg);
            lock (report) report.Errors.Add(msg);
        }
    }

    private async Task RenderImageBoundedAsync(
        string caseId,
        string dir,
        EvidenceAsset asset,
        AssetRenderingReport report,
        SemaphoreSlim gate,
        CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            await RenderImageAsync(caseId, dir, asset, report, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RenderImageAsync(string caseId, string dir, EvidenceAsset asset, AssetRenderingReport report, CancellationToken ct)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= MaxImageAttempts; attempt++)
        {
            try
            {
                var prompt = BuildImagePrompt(asset);
                var bytes = await CaseGenerationRuntime.ImageAsync(_llm, prompt, ct);
                var ext = SniffImageExtension(bytes);
                var path = Path.Combine(dir, $"{asset.Id.Replace("asset.", "")}.{ext}");
                await File.WriteAllBytesAsync(path, bytes, ct);
                lock (report)
                {
                    report.ImagesWritten++;
                    report.RenderedFileNames[asset.Id] = Path.GetFileName(path);
                    report.Files.Add(new(asset.Id, Path.GetFileName(path), ext switch
                    {
                        "jpg" => "image/jpeg", "gif" => "image/gif", "webp" => "image/webp", _ => "image/png"
                    }));
                }
                _logger.LogDebug("Image rendered: {Path}", path);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Image render attempt {Attempt}/{MaxAttempts} failed for {Id}",
                    attempt, MaxImageAttempts, asset.Id);
            }
        }

        var mandatory = asset.ImagePurpose is ImagePurposes.Scene or ImagePurposes.SuspectPortrait;
        var prefix = mandatory ? "Mandatory visual render failed" : "Image render failed";
        var msg = $"{prefix} for {asset.Id} after {MaxImageAttempts} attempts: {lastError?.Message}";
        lock (report) report.Errors.Add(msg);
    }

    public static string BuildImagePrompt(EvidenceAsset asset)
    {
        var subject = !string.IsNullOrWhiteSpace(asset.Body) ? asset.Body
            : throw new InvalidOperationException($"Image {asset.Id} has no authored image specification.");
        if (asset.ImagePurpose == ImagePurposes.SuspectPortrait)
        {
            subject = subject
                .Replace("crime-scene", "case dossier", StringComparison.OrdinalIgnoreCase)
                .Replace("crime scene", "case dossier", StringComparison.OrdinalIgnoreCase);
        }
        var prompts = AgentPromptCatalog.Default;
        var agentName = asset.ImagePurpose switch
        {
            ImagePurposes.SuspectPortrait => "ImageRenderPortrait",
            ImagePurposes.Scene => "ImageRenderScene",
            ImagePurposes.Surveillance => "ImageRenderSurveillance",
            ImagePurposes.Object => "ImageRenderObject",
            _ => "ImageRender"
        };
        var variables = new Dictionary<string, object?>
        {
            ["subject"] = subject
        };
        var system = prompts.RenderSystem(agentName, new Dictionary<string, object?>());
        var user = prompts.RenderUser(agentName, variables);
        return system + Environment.NewLine + Environment.NewLine + user;
    }

    private static string SniffImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "png";
        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "jpg";
        if (bytes.Length >= 4 &&
            bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return "gif";
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "webp";
        throw new InvalidDataException("The image provider did not return a supported image format.");
    }
}
