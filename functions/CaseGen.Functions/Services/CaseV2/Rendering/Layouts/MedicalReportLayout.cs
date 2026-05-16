using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Medical/coroner report: ME letterhead, sepia accent palette (NOT the
/// blue used elsewhere — visually different from a forensic lab so the
/// reader does not confuse the two), sections History / External
/// Examination / Findings / Cause / Disposition, MD signature footer.
/// Used for Preliminary ME Reports and Urgent Care Visit Records.
/// </summary>
public sealed class MedicalReportLayout : IDocumentLayout
{
    public string Layout => "MedicalReport";

    private const string AccentDark = "#7C4A1E";
    private const string AccentLight = "#FDF3E5";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.SerifCorporate).FontSize(10.5f).LineHeight(1.4f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(10).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    c.Item().PaddingBottom(4).Text(heading.ToUpperInvariant())
                        .FontFamily(DocumentResources.FontFamilies.Sans)
                        .FontSize(10.5f).Bold().FontColor(AccentDark).LetterSpacing(0.06f);
                    c.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(AccentDark);
                });
                if (doc.Signature is not null)
                    col.Item().PaddingTop(20).Element(c => DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.MedicalExaminer, doc.Language));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }

    private static void BuildHeader(IContainer container, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            col.Item().Background(AccentLight).Padding(8).Row(row =>
            {
                row.ConstantItem(40).Height(40).Image(DocumentResources.BrasaoPng).FitArea();
                row.RelativeItem().PaddingLeft(10).Column(c =>
                {
                    c.Item().Text(DocumentChrome.Get(ChromeKey.MedicalExaminerOffice, doc.Language))
                        .FontFamily(DocumentResources.FontFamilies.Sans)
                        .FontSize(10).Bold().FontColor(AccentDark).LetterSpacing(0.08f);
                    c.Item().Text(doc.Title)
                        .FontFamily(DocumentResources.FontFamilies.SerifCorporate)
                        .FontSize(14).Bold().FontColor(Colors.Grey.Darken4);
                    if (!string.IsNullOrEmpty(doc.Subtitle))
                        c.Item().Text(doc.Subtitle)
                            .FontFamily(DocumentResources.FontFamilies.SerifCorporate)
                            .FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
                });
            });

            if (doc.Header.Count > 0)
            {
                col.Item().PaddingTop(6).Border(0.5f).BorderColor(AccentDark).Padding(6).Column(kvCol =>
                {
                    foreach (var kv in doc.Header)
                        kvCol.Item().Row(r =>
                        {
                            r.ConstantItem(140).Text(kv.Label).FontFamily(DocumentResources.FontFamilies.Sans).Bold().FontSize(8.5f).FontColor(AccentDark);
                            r.RelativeItem().Text(kv.Value).FontFamily(DocumentResources.FontFamilies.SerifCorporate).FontSize(9);
                        });
                });
            }
        });
    }
}
