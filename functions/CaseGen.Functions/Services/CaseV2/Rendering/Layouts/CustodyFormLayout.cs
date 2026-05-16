using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Custody / accountability form: bold FORM stamp on top-right, dotted /
/// underlined fields, multi-signature base. Used for Key Control Sign-Out
/// Sheet, Cash Count Sheet, Drop-Safe Audit, Initial Case Briefing.
/// </summary>
public sealed class CustodyFormLayout : IDocumentLayout
{
    public string Layout => "CustodyForm";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Sans).FontSize(10).LineHeight(1.35f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(8).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    c.Item().PaddingBottom(4).Text(heading.ToUpperInvariant())
                        .FontSize(10).Bold().FontColor("#1F3864").LetterSpacing(0.06f);
                    c.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor("#1F3864");
                });

                col.Item().PaddingTop(20).Text(DocumentChrome.Get(ChromeKey.SignatureBlock, doc.Language))
                    .FontSize(9.5f).Bold().FontColor("#1F3864").LetterSpacing(0.06f);
                col.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Element(c => RenderEmptySig(c, doc));
                    r.ConstantItem(20);
                    r.RelativeItem().Element(c =>
                    {
                        if (doc.Signature is not null)
                            DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.Signed, doc.Language);
                        else
                            RenderEmptySig(c, doc);
                    });
                });
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }

    private static void BuildHeader(IContainer container, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(36).Height(36).Image(DocumentResources.BrasaoPng).FitArea();
                row.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Item().Text(DocumentChrome.Get(ChromeKey.PoliceLetterhead, doc.Language))
                        .FontSize(9.5f).Bold().FontColor("#1F3864").LetterSpacing(0.08f);
                    c.Item().Text(doc.Title).FontSize(14).Bold().FontColor(Colors.Grey.Darken4);
                    if (!string.IsNullOrEmpty(doc.Subtitle))
                        c.Item().Text(doc.Subtitle).FontSize(9.5f).FontColor(Colors.Grey.Darken2).Italic();
                });
                row.ConstantItem(80).AlignTop().AlignRight().Border(1).BorderColor("#1F3864").Padding(4)
                    .Text(DocumentChrome.Get(ChromeKey.Form, doc.Language))
                    .FontSize(11).Bold().FontColor("#1F3864").LetterSpacing(0.15f);
            });

            if (doc.Header.Count > 0)
            {
                col.Item().PaddingTop(6).Border(0.5f).BorderColor("#1F3864").Padding(6).Column(kvCol =>
                {
                    foreach (var kv in doc.Header)
                        kvCol.Item().Row(r =>
                        {
                            r.ConstantItem(140).Text(kv.Label).Bold().FontSize(8.5f).FontColor("#1F3864");
                            r.RelativeItem().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingBottom(1)
                                .Text(kv.Value).FontSize(9);
                        });
                });
            }
        });
    }

    private static void RenderEmptySig(IContainer container, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            col.Item().PaddingTop(20).LineHorizontal(0.5f).LineColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(3).Text(DocumentChrome.Get(ChromeKey.Signed, doc.Language))
                .FontSize(8).FontColor(Colors.Grey.Darken2);
        });
    }
}
