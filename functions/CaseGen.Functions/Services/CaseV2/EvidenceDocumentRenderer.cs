using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2.Rendering;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;

namespace CaseGen.Functions.Services.CaseV2;

public interface IEvidenceDocumentRenderer
{
    byte[] Render(EvidenceDocument doc);
}

/// <summary>
/// Thin dispatcher: resolves the right <see cref="IDocumentLayout"/> for
/// <see cref="EvidenceDocument.Layout"/> and lets it compose the QuestPDF
/// document. All chrome lives inside the layout; shared section payload
/// rendering lives in <see cref="DocumentBlocks"/>. Adding a new visual
/// family means adding one <c>IDocumentLayout</c> implementation and
/// registering it with DI — no edits required here.
/// </summary>
public class EvidenceDocumentRenderer : IEvidenceDocumentRenderer
{
    private readonly IDocumentLayoutRegistry _layouts;
    private readonly ILogger<EvidenceDocumentRenderer> _logger;

    public EvidenceDocumentRenderer(IDocumentLayoutRegistry layouts, ILogger<EvidenceDocumentRenderer> logger)
    {
        _layouts = layouts;
        _logger = logger;
    }

    public byte[] Render(EvidenceDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        // Delay native QuestPDF initialization until an asset is actually rendered.
        // AssetRenderingService isolates render failures per document, so unsupported
        // local architectures can still generate and persist a valid case.json.
        DocumentResources.EnsureInitialized();
        var layout = _layouts.Resolve(doc.Layout);
        return Document.Create(c => layout.Compose(c, doc)).GeneratePdf();
    }
}
