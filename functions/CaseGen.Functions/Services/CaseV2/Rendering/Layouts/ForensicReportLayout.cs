using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Forensic / lab examination report: lab letterhead, EVIDENCE INTEGRITY
/// VERIFIED banner, fixed sectioning (Items Submitted, Methodology,
/// Findings, Conclusions, Limitations) styled in sans-serif body with
/// monospace for hashes/measurements. Used for every "Forensic", "Lab",
/// "CCTV correlation" and "Video enhancement" report variant.
/// </summary>
public sealed class ForensicReportLayout : IDocumentLayout
{
    public string Layout => "ForensicReport";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Sans).FontSize(10).LineHeight(1.4f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(10).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    c.Item().PaddingBottom(4).Background("#0F2C5E").Padding(4)
                        .Text(heading.ToUpperInvariant())
                        .FontSize(10).Bold().FontColor(Colors.White).LetterSpacing(0.1f);
                });
                if (doc.Signature is not null)
                    col.Item().PaddingTop(20).Element(c => DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.Examiner, doc.Language));
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
                row.ConstantItem(40).Height(40).Image(DocumentResources.BrasaoPng).FitArea();
                row.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Item().Text(DocumentChrome.Get(ChromeKey.ForensicLetterhead, doc.Language))
                        .FontSize(10).Bold().FontColor("#1F3864").LetterSpacing(0.08f);
                    c.Item().Text(doc.Title).FontSize(13).Bold().FontColor(Colors.Grey.Darken4);
                    if (!string.IsNullOrEmpty(doc.Subtitle))
                        c.Item().Text(doc.Subtitle).FontSize(9.5f).FontColor(Colors.Grey.Darken2).Italic();
                });
            });

            col.Item().PaddingTop(6).Background("#0B6E2B").Padding(4).AlignCenter()
                .Text(DocumentChrome.Get(ChromeKey.EvidenceIntegrityVerified, doc.Language).ToUpperInvariant())
                .FontSize(8).Bold().FontColor(Colors.White).LetterSpacing(0.1f);

            if (doc.Header.Count > 0)
            {
                col.Item().PaddingTop(6).Border(0.5f).BorderColor("#1F3864").Padding(6).Row(r =>
                {
                    var half = (doc.Header.Count + 1) / 2;
                    r.RelativeItem().Column(c => { foreach (var kv in doc.Header.Take(half)) RenderKv(c, kv, mono: ContainsHashLike(kv.Value)); });
                    r.RelativeItem().PaddingLeft(8).Column(c => { foreach (var kv in doc.Header.Skip(half)) RenderKv(c, kv, mono: ContainsHashLike(kv.Value)); });
                });
            }

            col.Item().PaddingTop(4).Background("#FBE5E5").Padding(2).AlignCenter()
                .Text(DocumentChrome.Get(ChromeKey.Confidential, doc.Language).ToUpperInvariant())
                .FontSize(7).FontColor("#B71C1C").Bold().LetterSpacing(0.05f);
        });
    }

    private static void RenderKv(ColumnDescriptor col, EvidenceKeyValue kv, bool mono)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(110).Text(kv.Label).Bold().FontSize(8.5f).FontColor("#1F3864");
            r.RelativeItem().Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default
                    .FontFamily(mono ? DocumentResources.FontFamilies.Mono : DocumentResources.FontFamilies.Sans)
                    .FontSize(8.5f));
                t.Span(kv.Value);
            });
        });
    }

    private static bool ContainsHashLike(string value)
        => value.Length >= 16 && value.All(c => Uri.IsHexDigit(c) || c is '-' or ':' or '.');
}
