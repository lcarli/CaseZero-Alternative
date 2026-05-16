using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Default fallback layout (≈ the look the renderer had before this PR).
/// Used when an asset's <c>layout</c> id is unknown or simply
/// <c>GeneralReport</c>. Keeps zero-breakage for cases generated prior to
/// the layout overhaul.
/// </summary>
public sealed class GeneralReportLayout : IDocumentLayout
{
    public string Layout => "GeneralReport";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Sans).FontSize(10.5f).LineHeight(1.35f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(8).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, h) =>
                {
                    c.Item().PaddingBottom(4).Text(h.ToUpperInvariant())
                        .FontSize(11).Bold().FontColor("#1F3864").LetterSpacing(0.04f);
                    c.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                });
                if (doc.Signature is not null)
                    col.Item().PaddingTop(20).Element(c => DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.Signed, doc.Language));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }

    private static void BuildHeader(IContainer container, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            col.Item().Background("#FBE5E5").Padding(4).AlignCenter()
                .Text(DocumentChrome.Get(ChromeKey.Confidential, doc.Language).ToUpperInvariant())
                .FontSize(8).FontColor("#B71C1C").Bold().LetterSpacing(0.05f);

            col.Item().PaddingTop(6).Text(doc.Title).FontSize(15).Bold().FontColor(Colors.Grey.Darken4);
            if (!string.IsNullOrEmpty(doc.Subtitle))
                col.Item().Text(doc.Subtitle).FontSize(10).FontColor(Colors.Grey.Darken1);

            if (doc.Header.Count > 0)
            {
                col.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(kvCol =>
                {
                    foreach (var kv in doc.Header)
                    {
                        kvCol.Item().Row(r =>
                        {
                            r.ConstantItem(120).Text(kv.Label).Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                            r.RelativeItem().Text(kv.Value).FontSize(9);
                        });
                    }
                });
            }
        });
    }
}
