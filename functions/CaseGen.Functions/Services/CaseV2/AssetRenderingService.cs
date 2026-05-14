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
/// - audio/video   → placeholder .txt sidecar with the body/description
/// - digital       → placeholder .txt sidecar with the body
/// </summary>
public class AssetRenderingService : IAssetRenderingService
{
    private readonly IPdfRenderingService _pdf;
    private readonly ILLMProvider _llm;
    private readonly ILogger<AssetRenderingService> _logger;

    public AssetRenderingService(IPdfRenderingService pdf, ILLMProvider llm, ILogger<AssetRenderingService> logger)
    {
        _pdf = pdf;
        _llm = llm;
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
                    pdfTasks.Add(RenderPdfAsync(caseId, assetsDir, a, report, ct));
                    break;
                case "photo":
                case "image":
                    imageTasks.Add(RenderImageAsync(caseId, assetsDir, a, report, ct));
                    break;
                case "audio":
                case "video":
                case "digital":
                    RenderSidecar(assetsDir, a);
                    report.Skipped++;
                    break;
                default:
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
            var body = string.IsNullOrWhiteSpace(a.Body) ? (a.Description ?? a.Title) : a.Body!;
            var bytes = await _pdf.GenerateTestPdfAsync(a.Title, body, a.Category ?? "general", ct);
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
            var path = Path.Combine(dir, $"{a.Id.Replace("asset.", "")}.jpg");
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
        var ext = a.Type switch { "audio" => "mp3", "video" => "mp4", _ => "bin" };
        var placeholder = Path.Combine(dir, $"{slug}.{ext}.txt");
        var content = $"# Placeholder for {a.Title} ({a.Type})\n\n{a.Description}\n\n{a.Body}";
        File.WriteAllText(placeholder, content);
    }
}
