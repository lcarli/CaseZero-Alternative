using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public interface IAssetRenderingService
{
    void PrepareForReview(IEnumerable<EvidenceAsset> assets);
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
    public List<RenderedAssetFile> Files { get; set; } = new();
}

public record RenderedAssetFile(string AssetId, string FileName, string MimeType);

/// <summary>
/// Renders v2 assets to actual files next to the case JSON.
/// - pdf/document  → QuestPDF via <see cref="IPdfRenderingService.GenerateTestPdfAsync"/>
/// - photo/image   → image generation via <see cref="ILLMProvider.GenerateImageAsync"/>
/// - digital       → structured PDF export
/// - unsupported media fail explicitly; transcripts must be planned as documents
/// </summary>
public class AssetRenderingService : IAssetRenderingService
{
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
                    imageTasks.Add(RenderImageAsync(caseId, assetsDir, a, report, ct));
                    break;
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

    private async Task RenderPdfAsync(string caseId, string dir, EvidenceAsset a, AssetRenderingReport report, CancellationToken ct)
    {
        try
        {
            byte[] bytes;
            if (a.BodyDoc is not null && a.BodyDoc.Sections.Count > 0)
            {
                if (!a.PreparedForReview)
                    throw new InvalidOperationException("Structured document has not been prepared for review.");
                bytes = _docRenderer.Render(a.BodyDoc);
            }
            else
            {
                // Fallback: legacy markdown body via QuestPDF generic template.
                var body = string.IsNullOrWhiteSpace(a.Body) ? (a.Description ?? a.Title) : a.Body!;
                bytes = await _pdf.GenerateTestPdfAsync(a.Title, body, a.Category ?? "general", ct);
            }
            var path = Path.Combine(dir, $"{a.Id.Replace("asset.", "")}.pdf");
            await File.WriteAllBytesAsync(path, bytes, ct);
            lock (report)
            {
                report.PdfsWritten++;
                report.Files.Add(new RenderedAssetFile(a.Id, Path.GetFileName(path), "application/pdf"));
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

    private async Task RenderImageAsync(string caseId, string dir, EvidenceAsset a, AssetRenderingReport report, CancellationToken ct)
    {
        try
        {
            // Compose a prompt. If body is set we treat it as the prompt; otherwise fall back to description.
            var prompt = !string.IsNullOrWhiteSpace(a.Body) ? a.Body! :
                         !string.IsNullOrWhiteSpace(a.Description) ? a.Description! :
                         a.Title;

            // Light safety prefix so we don't get NSFW or graphic content by accident.
            prompt = $"Photorealistic crime-scene evidence photograph for a fictional investigation game. " +
                     $"No graphic gore. Subject: {prompt}. Composition: documentary, neutral lighting, evidence-style.";

            var bytes = await _llm.GenerateImageAsync(prompt, ct);
            // GPT-image returns PNG (the SDK doesn't expose the response content-type easily here).
            // Inspect the magic bytes so we don't mislabel the file with the wrong extension.
            var (ext, mimeType) = CaseFileIntegrity.DetectImage(bytes);
            var path = Path.Combine(dir, $"{a.Id.Replace("asset.", "")}.{ext}");
            await File.WriteAllBytesAsync(path, bytes, ct);
            lock (report)
            {
                report.ImagesWritten++;
                report.Files.Add(new RenderedAssetFile(a.Id, Path.GetFileName(path), mimeType));
            }
            _logger.LogDebug("Image rendered: {Path}", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var msg = $"Image render failed for {a.Id}: {ex.Message}";
            _logger.LogWarning(ex, msg);
            lock (report) report.Errors.Add(msg);
        }
    }

}
