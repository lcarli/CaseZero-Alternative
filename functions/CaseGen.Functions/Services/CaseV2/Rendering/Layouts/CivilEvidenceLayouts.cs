using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

public sealed class MagazineArticleLayout : IDocumentLayout
{
    public string Layout => "MagazineArticle";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => DocumentResources.ReadableTypography(style).FontFamily(DocumentResources.FontFamilies.Serif).FontSize(10).LineHeight(1.35f));
            page.Header().Column(column =>
            {
                column.Item().BorderBottom(3).BorderColor("#B34B32").PaddingBottom(8)
                    .Text(doc.Subtitle ?? string.Empty).FontFamily(DocumentResources.FontFamilies.Sans).FontSize(12).LetterSpacing(.1f);
                column.Item().PaddingTop(12).Text(doc.Title).FontSize(29).Bold().FontColor("#333333");
                foreach (var item in doc.Header)
                    column.Item().PaddingTop(3).Text($"{item.Label}: {item.Value}").FontSize(9).Italic();
            });
            page.Content().PaddingTop(14).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.Spacing(20);
                    var midpoint = (doc.Sections.Count + 1) / 2;
                    foreach (var sections in new[] { doc.Sections.Take(midpoint), doc.Sections.Skip(midpoint) })
                        row.RelativeItem().Column(content => DocumentBlocks.RenderSections(content,
                            new EvidenceDocument { Sections = sections.ToList(), Language = doc.Language },
                            (body, heading) => body.Item().PaddingTop(8).Text(heading).FontSize(13).Bold()));
                });
                if (doc.Signature is { } signature)
                    column.Item().PaddingTop(16).Element(content => DocumentBlocks.RenderSignature(content, signature, language: doc.Language));
            });
            page.Footer().Element(footer => DocumentBlocks.StandardFooter(footer, doc));
        });
    }
}

public sealed class HandwrittenNoteLayout : IDocumentLayout
{
    public string Layout => "HandwrittenNote";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A6);
            page.Margin(20);
            page.PageColor("#FFF4B8");
            page.DefaultTextStyle(style => DocumentResources.ReadableTypography(style).FontFamily(DocumentResources.FontFamilies.Serif).FontSize(11).Italic().LineHeight(1.4f));
            page.Header().Column(column =>
            {
                column.Item().Text(doc.Title).FontSize(13);
                if (!string.IsNullOrWhiteSpace(doc.Subtitle))
                    column.Item().PaddingTop(4).Text(doc.Subtitle).FontSize(9);
                foreach (var item in doc.Header)
                    column.Item().PaddingTop(3).Text($"{item.Label}: {item.Value}").FontSize(8);
            });
            page.Content().PaddingTop(12).Column(column =>
            {
                DocumentBlocks.RenderSections(column, doc, (content, heading) => content.Item().Text(heading).Underline());
                if (doc.Signature is { } signature)
                {
                    column.Item().PaddingTop(16).Text(signature.Name);
                    if (signature.Title is not null) column.Item().Text(signature.Title).FontSize(8);
                    if (signature.Badge is not null) column.Item().Text(signature.Badge).FontSize(8);
                    if (signature.SignedAt is not null) column.Item().Text(signature.SignedAt).FontSize(8);
                }
            });
        });
    }
}

public sealed class DiaryEntryLayout : IDocumentLayout
{
    public string Layout => "DiaryEntry";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(30);
            page.PageColor("#FAF6EB");
            page.DefaultTextStyle(style => DocumentResources.ReadableTypography(style).FontFamily(DocumentResources.FontFamilies.Serif).FontSize(11).LineHeight(1.45f));
            page.Header().Column(column =>
            {
                column.Item().Text(doc.Title).FontSize(16).Italic();
                if (!string.IsNullOrWhiteSpace(doc.Subtitle)) column.Item().Text(doc.Subtitle).FontSize(9);
                foreach (var item in doc.Header)
                    column.Item().PaddingTop(4).Text($"{item.Label}: {item.Value}").FontSize(9);
            });
            page.Content().PaddingTop(14).Column(column =>
            {
                DocumentBlocks.RenderSections(column, doc,
                    (content, heading) => content.Item().PaddingTop(8).BorderBottom(.4f).BorderColor("#C7C9B4").Text(heading).Italic());
                if (doc.Signature is { } signature)
                    column.Item().PaddingTop(16).Element(content => DocumentBlocks.RenderSignature(content, signature, language: doc.Language));
            });
            page.Footer().Element(footer => DocumentBlocks.StandardFooter(footer, doc));
        });
    }
}

public sealed class WorkScheduleLayout : IDocumentLayout
{
    public string Layout => "WorkSchedule";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(style => DocumentResources.ReadableTypography(style).FontFamily(DocumentResources.FontFamilies.Sans).FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().BorderBottom(2).BorderColor("#355D4E").PaddingBottom(7).Text(doc.Title).FontSize(19).Bold();
                if (!string.IsNullOrWhiteSpace(doc.Subtitle)) column.Item().PaddingTop(4).Text(doc.Subtitle);
                foreach (var item in doc.Header)
                    column.Item().PaddingTop(3).Text($"{item.Label}: {item.Value}");
            });
            page.Content().PaddingTop(12).Column(column =>
            {
                DocumentBlocks.RenderSections(column, doc,
                    (content, heading) => content.Item().PaddingTop(8).Text(heading).Bold());
                if (doc.Signature is { } signature)
                    column.Item().PaddingTop(16).Element(content => DocumentBlocks.RenderSignature(content, signature, language: doc.Language));
            });
            page.Footer().Element(footer => DocumentBlocks.StandardFooter(footer, doc));
        });
    }
}
