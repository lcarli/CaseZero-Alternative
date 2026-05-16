using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Police report family: brasão + agency letterhead, Case File No. / Report
/// No. strip, serif body (Lora), numbered §1/§2 sections, officer signature
/// footer. Used for Initial Police Report, Supplemental Police Report, and
/// First Responding Officer narratives.
/// </summary>
public sealed class PoliceReportLayout : IDocumentLayout
{
    public string Layout => "PoliceReport";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Serif).FontSize(10.5f).LineHeight(1.4f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(10).Column(col =>
            {
                int counter = 0;
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    counter++;
                    c.Item().PaddingBottom(4).Text(t =>
                    {
                        t.Span($"{DocumentChrome.Get(ChromeKey.SectionPrefix, doc.Language)}{counter}  ")
                            .FontSize(11).Bold().FontColor("#1F3864");
                        t.Span(heading.ToUpperInvariant())
                            .FontSize(11).Bold().FontColor("#1F3864").LetterSpacing(0.04f);
                    });
                    c.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor("#1F3864");
                });
                if (doc.Signature is not null)
                    col.Item().PaddingTop(24).Element(c => DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.SignatureOfficer, doc.Language));
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
                row.ConstantItem(48).Height(48).Image(DocumentResources.BrasaoPng).FitArea();
                row.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Item().Text(DocumentChrome.Get(ChromeKey.PoliceLetterhead, doc.Language))
                        .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(11).Bold().FontColor("#1F3864").LetterSpacing(0.05f);
                    c.Item().Text(doc.Title).FontFamily(DocumentResources.FontFamilies.Sans).FontSize(13).Bold().FontColor(Colors.Grey.Darken4);
                    if (!string.IsNullOrEmpty(doc.Subtitle))
                        c.Item().Text(doc.Subtitle).FontSize(9.5f).FontColor(Colors.Grey.Darken2).Italic();
                });
            });

            col.Item().PaddingTop(6).Background("#FBE5E5").Padding(3).AlignCenter()
                .Text(DocumentChrome.Get(ChromeKey.Confidential, doc.Language).ToUpperInvariant())
                .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(7.5f).FontColor("#B71C1C").Bold().LetterSpacing(0.05f);

            if (doc.Header.Count > 0)
            {
                col.Item().PaddingTop(6).Border(0.5f).BorderColor("#1F3864").Background("#F4F6FB").Padding(6).Row(r =>
                {
                    var groups = SplitInHalf(doc.Header);
                    r.RelativeItem().Column(left =>
                    {
                        foreach (var kv in groups.left) RenderKv(left, kv);
                    });
                    r.RelativeItem().PaddingLeft(8).Column(right =>
                    {
                        foreach (var kv in groups.right) RenderKv(right, kv);
                    });
                });
            }
        });
    }

    private static void RenderKv(ColumnDescriptor col, EvidenceKeyValue kv)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(110).Text(kv.Label).FontFamily(DocumentResources.FontFamilies.Sans).Bold().FontSize(8.5f).FontColor("#1F3864");
            r.RelativeItem().Text(kv.Value).FontFamily(DocumentResources.FontFamilies.Sans).FontSize(8.5f);
        });
    }

    private static (List<EvidenceKeyValue> left, List<EvidenceKeyValue> right) SplitInHalf(List<EvidenceKeyValue> items)
    {
        int half = (items.Count + 1) / 2;
        return (items.Take(half).ToList(), items.Skip(half).ToList());
    }
}
