using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Internal memorandum: corporate serif body, no classification band on
/// top (memos circulate internally so the red band is visual noise),
/// TO/FROM/DATE/RE block, optional action callout at the bottom. Used for
/// Internal Memo, Records Preservation Hold, Records Request Checklist,
/// Tasking Memo, Admin Summary, Compliance Brief, Access Custody Request,
/// Preservation Directive.
/// </summary>
public sealed class MemoLayout : IDocumentLayout
{
    public string Layout => "Memo";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(48);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.SerifCorporate).FontSize(11).LineHeight(1.45f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(12).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    c.Item().PaddingBottom(2).Text(heading)
                        .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(10.5f).Bold().FontColor("#1F3864");
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
            col.Item().Row(row =>
            {
                row.ConstantItem(34).Height(34).Image(DocumentResources.BrasaoPng).FitArea();
                row.RelativeItem().PaddingLeft(8).AlignMiddle().Text(t =>
                {
                    t.Span(DocumentChrome.Get(ChromeKey.PoliceLetterhead, doc.Language))
                        .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(9.5f).Bold().FontColor("#1F3864").LetterSpacing(0.08f);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#1F3864");
            col.Item().PaddingTop(6).AlignCenter().Text(DocumentChrome.Get(ChromeKey.MemoLabel, doc.Language))
                .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(16).Bold().LetterSpacing(0.25f).FontColor("#1F3864");

            var to = FindHeader(doc, ChromeKey.To, "TO", "PARA", "À");
            var from = FindHeader(doc, ChromeKey.From, "FROM", "DE");
            var date = FindHeader(doc, ChromeKey.Date, "DATE", "DATA", "FECHA");
            var re = FindHeader(doc, ChromeKey.Re, "RE", "REF", "ASUNTO", "OBJET");

            col.Item().PaddingTop(10).Column(block =>
            {
                RenderRow(block, ChromeKey.To, to ?? string.Empty, doc);
                RenderRow(block, ChromeKey.From, from ?? string.Empty, doc);
                RenderRow(block, ChromeKey.Date, date ?? string.Empty, doc);
                RenderRow(block, ChromeKey.Re, re ?? doc.Title, doc);
            });
        });
    }

    private static void RenderRow(ColumnDescriptor col, ChromeKey key, string value, EvidenceDocument doc)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().PaddingBottom(2).Row(r =>
        {
            r.ConstantItem(60).Text(DocumentChrome.Get(key, doc.Language) + ":")
                .FontFamily(DocumentResources.FontFamilies.Sans).Bold().FontSize(10).FontColor("#1F3864");
            r.RelativeItem().Text(value).FontFamily(DocumentResources.FontFamilies.SerifCorporate).FontSize(10.5f);
        });
    }

    private static string? FindHeader(EvidenceDocument doc, ChromeKey key, params string[] synonyms)
    {
        var localized = DocumentChrome.Get(key, doc.Language);
        var hit = doc.Header.FirstOrDefault(h =>
            string.Equals(h.Label?.TrimEnd(':'), localized.TrimEnd(':'), StringComparison.OrdinalIgnoreCase));
        if (hit is not null) return hit.Value;
        foreach (var s in synonyms)
        {
            hit = doc.Header.FirstOrDefault(h =>
                string.Equals(h.Label?.TrimEnd(':'), s, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit.Value;
        }
        return null;
    }
}
