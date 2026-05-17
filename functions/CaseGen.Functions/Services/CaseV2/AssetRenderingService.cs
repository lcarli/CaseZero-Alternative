using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public interface IAssetRenderingService
{
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
}

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
                case "audio":
                    // Audio is no longer planned; if the LLM ignores the prompt
                    // and emits one anyway, fall back to a sidecar so the run
                    // doesn't fail — but flag it.
                    _logger.LogWarning("Legacy audio asset {Id} fell through — emitting sidecar; expected AudioTranscript document instead", a.Id);
                    RenderSidecar(assetsDir, a);
                    report.Skipped++;
                    break;
                default:
                    _logger.LogWarning("Unknown asset type {Type} on {Id} — emitting sidecar", a.Type, a.Id);
                    RenderSidecar(assetsDir, a);
                    report.Skipped++;
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
                // Preferred path: structured evidence document → run through the
                // template registry (enriches per layout) → real tables / transcripts / etc.
                if (string.IsNullOrEmpty(a.BodyDoc.Title)) a.BodyDoc.Title = a.Title;
                var enriched = _templates.Apply(a.BodyDoc);
                bytes = _docRenderer.Render(enriched);
            }
            else
            {
                // Fallback: legacy markdown body via QuestPDF generic template.
                var body = string.IsNullOrWhiteSpace(a.Body) ? (a.Description ?? a.Title) : a.Body!;
                bytes = await _pdf.GenerateTestPdfAsync(a.Title, body, a.Category ?? "general", ct);
            }
            var path = Path.Combine(dir, $"{a.Id.Replace("asset.", "")}.pdf");
            await File.WriteAllBytesAsync(path, bytes, ct);
            lock (report) report.PdfsWritten++;
            _logger.LogDebug("PDF rendered: {Path}", path);
        }
        catch (Exception ex)
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
            var ext = SniffImageExtension(bytes);
            var path = Path.Combine(dir, $"{a.Id.Replace("asset.", "")}.{ext}");
            await File.WriteAllBytesAsync(path, bytes, ct);
            lock (report) report.ImagesWritten++;
            _logger.LogDebug("Image rendered: {Path}", path);
        }
        catch (Exception ex)
        {
            var msg = $"Image render failed for {a.Id}: {ex.Message}";
            _logger.LogWarning(ex, msg);
            lock (report) report.Errors.Add(msg);
        }
    }

    private static void RenderSidecar(string dir, EvidenceAsset a)
    {
        var slug = a.Id.Replace("asset.", "");
        var ext = a.Type switch
        {
            "audio" => "mp3",
            _ => "txt"
        };
        var placeholder = Path.Combine(dir, $"{slug}.{ext}.txt");
        var content = $"# Placeholder for {a.Title} ({a.Type})\n\n{a.Description}\n\n{a.Body}";
        File.WriteAllText(placeholder, content);
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
        return "png"; // safe default for gpt-image-*
    }
}
