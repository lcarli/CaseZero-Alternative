using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Evidence log / chain of custody: navy banner header, landscape page so
/// the canonical chain-of-custody table (Item / Description / Collected
/// By / Time / Location / Container / Seal) fits without truncation. The
/// document is expected to ship at least one <c>table</c> section with
/// those columns; non-table sections are still rendered for narrative
/// notes.
/// </summary>
public sealed class EvidenceLogLayout : IDocumentLayout
{
    public string Layout => "EvidenceLog";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Sans).FontSize(9).LineHeight(1.3f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(8).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    c.Item().PaddingBottom(3).Text(heading.ToUpperInvariant())
                        .FontSize(9.5f).Bold().FontColor("#0F2C5E").LetterSpacing(0.06f);
                    c.Item().PaddingBottom(3).LineHorizontal(0.5f).LineColor("#0F2C5E");
                });
                if (doc.Signature is not null)
                    col.Item().PaddingTop(16).Element(c => DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.Transferred, doc.Language));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }

    private static void BuildHeader(IContainer container, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            col.Item().Background("#0F2C5E").Padding(6).Row(r =>
            {
                r.ConstantItem(30).Height(30).Image(DocumentResources.BrasaoPng).FitArea();
                r.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Item().Text(DocumentChrome.Get(ChromeKey.EvidenceLogTitle, doc.Language))
                        .FontSize(10).Bold().FontColor(Colors.White).LetterSpacing(0.1f);
                    c.Item().Text(doc.Title).FontSize(13).Bold().FontColor(Colors.White);
                    if (!string.IsNullOrEmpty(doc.Subtitle))
                        c.Item().Text(doc.Subtitle).FontSize(9).FontColor("#C7D2FE").Italic();
                });
            });

            if (doc.Header.Count > 0)
            {
                col.Item().Background("#F4F6FB").Padding(6).Row(r =>
                {
                    foreach (var kv in doc.Header.Take(4))
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(kv.Label).FontSize(7.5f).Bold().FontColor("#1F3864").LetterSpacing(0.05f);
                            c.Item().Text(kv.Value).FontSize(9);
                        });
                    }
                });
            }
        });
    }
}
