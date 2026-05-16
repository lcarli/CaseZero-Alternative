using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Witness Statement: "STATEMENT OF" headline, oath block, numbered prose
/// (juramentado convention), double signature footer (witness + officer).
/// </summary>
public sealed class WitnessStatementLayout : IDocumentLayout
{
    public string Layout => "WitnessStatement";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Serif).FontSize(11).LineHeight(1.5f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(10).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    c.Item().PaddingBottom(3).Text(heading.ToUpperInvariant())
                        .FontFamily(DocumentResources.FontFamilies.Sans)
                        .FontSize(10).Bold().FontColor("#1F3864").LetterSpacing(0.05f);
                });

                col.Item().PaddingTop(20).PaddingBottom(8)
                    .Text(BuildOathText(doc))
                    .FontFamily(DocumentResources.FontFamilies.Serif).FontSize(10).Italic().FontColor(Colors.Grey.Darken3);

                col.Item().PaddingTop(20).Row(r =>
                {
                    r.RelativeItem().Element(c => RenderSigBlock(c, ChromeKey.SignatureWitness, doc));
                    r.ConstantItem(20);
                    r.RelativeItem().Element(c =>
                    {
                        if (doc.Signature is not null)
                            DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.SignatureOfficer, doc.Language);
                        else
                            RenderSigBlock(c, ChromeKey.SignatureOfficer, doc);
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
            col.Item().Background("#FBE5E5").Padding(3).AlignCenter()
                .Text(DocumentChrome.Get(ChromeKey.Confidential, doc.Language).ToUpperInvariant())
                .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(7.5f).FontColor("#B71C1C").Bold().LetterSpacing(0.05f);

            col.Item().PaddingTop(10).AlignCenter()
                .Text(DocumentChrome.Get(ChromeKey.StatementOf, doc.Language))
                .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(13).Bold().LetterSpacing(0.15f).FontColor("#1F3864");

            col.Item().AlignCenter().Text(doc.Title)
                .FontFamily(DocumentResources.FontFamilies.Serif).FontSize(15).Bold().FontColor(Colors.Grey.Darken4);

            if (!string.IsNullOrEmpty(doc.Subtitle))
                col.Item().AlignCenter().Text(doc.Subtitle)
                    .FontFamily(DocumentResources.FontFamilies.Serif).FontSize(10).Italic().FontColor(Colors.Grey.Darken2);

            if (doc.Header.Count > 0)
            {
                col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor("#1F3864");
                col.Item().PaddingTop(6).Row(r =>
                {
                    foreach (var kv in doc.Header.Take(3))
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(kv.Label).FontFamily(DocumentResources.FontFamilies.Sans).FontSize(7.5f).FontColor("#1F3864").LetterSpacing(0.05f);
                            c.Item().Text(kv.Value).FontFamily(DocumentResources.FontFamilies.Serif).FontSize(10);
                        });
                    }
                });
            }
        });
    }

    private static void RenderSigBlock(IContainer container, ChromeKey caption, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            col.Item().PaddingTop(15).LineHorizontal(0.5f).LineColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(3).Text(DocumentChrome.Get(caption, doc.Language))
                .FontFamily(DocumentResources.FontFamilies.Sans).FontSize(8).FontColor(Colors.Grey.Darken2);
        });
    }

    private static string BuildOathText(EvidenceDocument doc)
    {
        var name = doc.Title?.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "_____________________";
        return DocumentChrome.Format(ChromeKey.OathBlock, doc.Language, name);
    }
}
