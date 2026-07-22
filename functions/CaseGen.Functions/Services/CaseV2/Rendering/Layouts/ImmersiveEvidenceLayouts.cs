using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

public sealed class NewspaperClippingLayout : IDocumentLayout
{
    public string Layout => "NewspaperClipping";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.PageColor("#F5F0E4");
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Serif).FontSize(10).LineHeight(1.25f));
            page.Header().Column(col =>
            {
                col.Item().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5)
                    .AlignCenter().Text(doc.Subtitle ?? doc.Title).FontSize(22).Bold();
                col.Item().PaddingTop(8).Text(doc.Title).FontSize(18).Bold();
                RenderHeaderLine(col, doc);
            });
            page.Content().PaddingTop(10).Column(col =>
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                    c.Item().PaddingTop(6).Text(heading).FontSize(13).Bold()));
            page.Footer().AlignCenter().Text(doc.Classification).FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void RenderHeaderLine(ColumnDescriptor col, EvidenceDocument doc)
    {
        if (doc.Header.Count == 0) return;
        col.Item().PaddingTop(4).Row(row =>
        {
            foreach (var item in doc.Header.Take(3))
                row.RelativeItem().Text($"{item.Label}: {item.Value}").FontSize(8).Italic();
        });
    }
}

public sealed class PersonalLetterLayout : IDocumentLayout
{
    public string Layout => "PersonalLetter";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(54);
            page.MarginHorizontal(64);
            page.PageColor("#FFFDF7");
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Serif).FontSize(11).LineHeight(1.5f));
            page.Header().Column(col =>
            {
                col.Item().AlignRight().Text(doc.Subtitle ?? string.Empty).FontSize(9).Italic();
                if (doc.Header.Count > 0)
                    col.Item().PaddingTop(8).AlignRight()
                        .Text(string.Join(" · ", doc.Header.Select(h => h.Value))).FontSize(9);
            });
            page.Content().PaddingTop(20).Column(col =>
            {
                col.Item().Text(doc.Title).FontSize(15).Italic();
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                    c.Item().PaddingTop(12).Text(heading).FontSize(10).Italic());
                if (doc.Signature is not null)
                    col.Item().PaddingTop(28).AlignRight().Text(doc.Signature.Name).FontSize(13).Italic();
            });
            page.Footer().AlignCenter().Text(doc.Classification).FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }
}

public sealed class ReceiptLayout : IDocumentLayout
{
    public string Layout => "Receipt";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(24);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Mono).FontSize(8.5f).LineHeight(1.2f));
            page.Header().Column(col =>
            {
                col.Item().AlignCenter().Text(doc.Title).FontSize(13).Bold();
                if (!string.IsNullOrWhiteSpace(doc.Subtitle))
                    col.Item().AlignCenter().Text(doc.Subtitle).FontSize(8);
                col.Item().PaddingVertical(6).LineHorizontal(0.8f);
                foreach (var item in doc.Header)
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(item.Label);
                        row.RelativeItem().AlignRight().Text(item.Value);
                    });
            });
            page.Content().PaddingTop(8).Column(col =>
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                    c.Item().PaddingVertical(5).BorderBottom(0.5f).Text(heading).Bold()));
            page.Footer().Column(col =>
            {
                col.Item().PaddingTop(8).LineHorizontal(0.8f);
                col.Item().PaddingTop(4).AlignCenter().Text(doc.Classification).FontSize(6.5f);
            });
        });
    }
}

public sealed class SearchWarrantLayout : IDocumentLayout
{
    public string Layout => "SearchWarrant";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(34);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Serif).FontSize(10).LineHeight(1.35f));
            page.Header().Border(1.2f).BorderColor("#1F3864").Padding(10).Column(col =>
            {
                col.Item().AlignCenter().Text(doc.Title.ToUpperInvariant()).FontSize(16).Bold();
                if (!string.IsNullOrWhiteSpace(doc.Subtitle))
                    col.Item().AlignCenter().Text(doc.Subtitle).FontSize(9);
                foreach (var item in doc.Header)
                    col.Item().PaddingTop(3).Row(row =>
                    {
                        row.ConstantItem(125).Text(item.Label.ToUpperInvariant()).FontSize(8).Bold();
                        row.RelativeItem().Text(item.Value).FontSize(9);
                    });
            });
            page.Content().PaddingTop(12).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                    c.Item().PaddingTop(8).Text(heading.ToUpperInvariant()).FontSize(9).Bold().FontColor("#1F3864"));
                if (doc.Signature is not null)
                    col.Item().PaddingTop(20).Element(c =>
                        DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.Signed, doc.Language));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }
}

public sealed class DispatchLogLayout : IDocumentLayout
{
    public string Layout => "DispatchLog";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.PageColor("#F7F8FA");
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Mono).FontSize(8.5f).LineHeight(1.2f));
            page.Header().Background("#202A35").Padding(10).Column(col =>
            {
                col.Item().Text(doc.Title).FontSize(14).Bold().FontColor(Colors.White);
                if (!string.IsNullOrWhiteSpace(doc.Subtitle))
                    col.Item().Text(doc.Subtitle).FontSize(8).FontColor("#C9D4E2");
            });
            page.Content().PaddingTop(10).Column(col =>
            {
                if (doc.Header.Count > 0)
                {
                    col.Item().Background("#E7ECF2").Padding(6)
                        .Text(string.Join(" | ", doc.Header.Select(h => $"{h.Label}: {h.Value}"))).FontSize(8);
                }
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                    c.Item().PaddingTop(8).Text($"> {heading.ToUpperInvariant()}").Bold().FontColor("#203A5F"));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }
}

public sealed class CaseMapLayout : IDocumentLayout
{
    public string Layout => "CaseMap";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(30);
            page.PageColor("#EEF3E8");
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Sans).FontSize(9).LineHeight(1.25f));
            page.Header().Row(row =>
            {
                row.RelativeItem().Text(doc.Title).FontSize(16).Bold().FontColor("#244A32");
                row.RelativeItem().AlignRight().Text(doc.Subtitle ?? doc.Classification).FontSize(8);
            });
            page.Content().PaddingTop(10).Border(1).BorderColor("#6F856F").Padding(12).Column(col =>
            {
                if (doc.Header.Count > 0)
                {
                    col.Item().Background("#DCE8D5").Padding(6)
                        .Text(string.Join("  •  ", doc.Header.Select(h => $"{h.Label}: {h.Value}"))).FontSize(8);
                }
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                    c.Item().PaddingTop(8).Background("#E6EEE1").Padding(4)
                        .Text(heading.ToUpperInvariant()).Bold().FontColor("#244A32"));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }
}

public sealed class CalendarLayout : IDocumentLayout
{
    public string Layout => "Calendar";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Sans).FontSize(9.5f));
            page.Header().Background("#8B3A3A").Padding(10).Row(row =>
            {
                row.RelativeItem().Text(doc.Title).FontSize(16).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignRight().Text(doc.Subtitle ?? string.Empty).FontSize(9).FontColor(Colors.White);
            });
            page.Content().PaddingTop(12).Column(col =>
            {
                if (doc.Header.Count > 0)
                    col.Item().Border(0.7f).BorderColor("#C9A6A6").Padding(6)
                        .Text(string.Join(" | ", doc.Header.Select(h => $"{h.Label}: {h.Value}")));
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                    c.Item().PaddingTop(8).BorderBottom(0.8f).BorderColor("#8B3A3A")
                        .PaddingBottom(3).Text(heading).Bold().FontColor("#8B3A3A"));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }
}
