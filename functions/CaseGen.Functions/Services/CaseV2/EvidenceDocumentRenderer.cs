using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2;

public interface IEvidenceDocumentRenderer
{
    byte[] Render(EvidenceDocument doc);
}

/// <summary>
/// Generic structured-document renderer used for every v2 PDF asset
/// (police report, call log, POS export, phone dump, sensor log, transcript, etc.).
/// One renderer, many layouts — the LLM picks the right <c>section.kind</c> values.
/// </summary>
public class EvidenceDocumentRenderer : IEvidenceDocumentRenderer
{
    private readonly ILogger<EvidenceDocumentRenderer> _logger;

    public EvidenceDocumentRenderer(ILogger<EvidenceDocumentRenderer> logger)
    {
        _logger = logger;
    }

    public byte[] Render(EvidenceDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));

                page.Header().Element(h => BuildHeader(h, doc));
                page.Content().PaddingTop(8).Column(col =>
                {
                    foreach (var s in doc.Sections)
                    {
                        col.Item().PaddingTop(10).Element(c => BuildSection(c, s));
                    }
                    if (doc.Signature is not null)
                        col.Item().PaddingTop(20).Element(c => BuildSignature(c, doc.Signature));
                });
                page.Footer().Element(f => BuildFooter(f, doc));
            });
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------
    // Header / footer
    // ------------------------------------------------------------------

    private void BuildHeader(IContainer container, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            // Classification band
            col.Item().Background(Colors.Red.Lighten4).Padding(4).AlignCenter()
                .Text(doc.Classification.ToUpperInvariant())
                .FontSize(8).FontColor(Colors.Red.Darken3).Bold().LetterSpacing(0.05f);

            // Title block
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(doc.Title).FontSize(15).Bold().FontColor(Colors.Grey.Darken4);
                    if (!string.IsNullOrEmpty(doc.Subtitle))
                        c.Item().Text(doc.Subtitle).FontSize(10).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(2).Text($"Layout: {doc.Layout}").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });

            // Header key/value table
            if (doc.Header.Count > 0)
            {
                col.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6)
                    .Column(kvCol =>
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

    private void BuildFooter(IContainer container, EvidenceDocument doc)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(doc.Classification.ToUpperInvariant())
                .FontSize(7).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.Span("Page ").FontSize(7).FontColor(Colors.Grey.Medium);
                t.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                t.Span(" / ").FontSize(7).FontColor(Colors.Grey.Medium);
                t.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
            });
        });
    }

    // ------------------------------------------------------------------
    // Sections
    // ------------------------------------------------------------------

    private void BuildSection(IContainer container, EvidenceSection s)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrEmpty(s.Heading))
            {
                col.Item().PaddingBottom(4).Text(s.Heading.ToUpperInvariant())
                    .FontSize(11).Bold().FontColor(Colors.Blue.Darken3).LetterSpacing(0.04f);
                col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            }

            switch ((s.Kind ?? "narrative").ToLowerInvariant())
            {
                case "narrative":
                    RenderNarrative(col, s.Text ?? string.Empty);
                    break;
                case "table":
                    RenderTable(col, s.Table);
                    break;
                case "keyvalue":
                    RenderKeyValue(col, s.Items);
                    break;
                case "transcript":
                    RenderTranscript(col, s.Transcript);
                    break;
                case "code":
                    RenderCode(col, s.Code);
                    break;
                case "callout":
                    RenderCallout(col, s.Text ?? string.Empty);
                    break;
                default:
                    RenderNarrative(col, s.Text ?? $"(unsupported section kind: {s.Kind})");
                    break;
            }
        });
    }

    private static void RenderNarrative(ColumnDescriptor col, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        // Render markdown-ish paragraphs: split on blank lines, keep bullets ('- ') as bullet points.
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paragraphs)
        {
            var trimmed = p.TrimEnd();
            var bulletLines = trimmed.Split('\n')
                .Where(l => l.TrimStart().StartsWith("- "))
                .ToList();
            if (bulletLines.Count > 0 && bulletLines.Count == trimmed.Split('\n').Length)
            {
                foreach (var line in bulletLines)
                    col.Item().PaddingLeft(8).Text($"•  {line.TrimStart().Substring(2).Trim()}").FontSize(10);
            }
            else
            {
                col.Item().PaddingBottom(3).Text(trimmed).FontSize(10).FontColor(Colors.Grey.Darken4);
            }
        }
    }

    private static void RenderTable(ColumnDescriptor col, EvidenceTable? table)
    {
        if (table is null || table.Columns.Count == 0) return;
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                foreach (var _ in table.Columns) c.RelativeColumn();
            });
            t.Header(h =>
            {
                foreach (var colName in table.Columns)
                {
                    h.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                        .Text(colName).Bold().FontSize(9).FontColor(Colors.Blue.Darken3);
                }
            });
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                var stripe = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                foreach (var cell in row)
                {
                    t.Cell().Background(stripe).BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2)
                        .Padding(4).Text(cell ?? string.Empty).FontSize(9);
                }
                // Pad short rows so the grid stays aligned
                for (int extra = row.Count; extra < table.Columns.Count; extra++)
                {
                    t.Cell().Background(stripe).BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2)
                        .Padding(4).Text(string.Empty);
                }
            }
        });
        if (!string.IsNullOrEmpty(table.Footnote))
            col.Item().PaddingTop(4).Text(table.Footnote).FontSize(8).FontColor(Colors.Grey.Darken1).Italic();
    }

    private static void RenderKeyValue(ColumnDescriptor col, List<EvidenceKeyValue>? items)
    {
        if (items is null || items.Count == 0) return;
        col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(kvCol =>
        {
            foreach (var kv in items)
            {
                kvCol.Item().Row(r =>
                {
                    r.ConstantItem(140).Text(kv.Label).Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    r.RelativeItem().Text(kv.Value).FontSize(9);
                });
            }
        });
    }

    private static void RenderTranscript(ColumnDescriptor col, List<EvidenceTranscriptEntry>? entries)
    {
        if (entries is null || entries.Count == 0) return;
        col.Item().Column(tc =>
        {
            foreach (var e in entries)
            {
                tc.Item().PaddingBottom(4).Row(r =>
                {
                    r.ConstantItem(80).Column(meta =>
                    {
                        if (!string.IsNullOrEmpty(e.Timestamp))
                            meta.Item().Text(e.Timestamp).FontSize(8).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(e.Speaker))
                            meta.Item().Text(e.Speaker).FontSize(9).Bold().FontColor(Colors.Blue.Darken2);
                    });
                    r.RelativeItem().Text(e.Text).FontSize(10).FontColor(Colors.Grey.Darken4);
                });
            }
        });
    }

    private static void RenderCode(ColumnDescriptor col, EvidenceCode? code)
    {
        if (code is null || string.IsNullOrEmpty(code.Content)) return;
        col.Item().Background(Colors.Grey.Lighten5).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(6).Text(code.Content)
            .FontFamily(Fonts.Courier).FontSize(8.5f).FontColor(Colors.Grey.Darken4);
    }

    private static void RenderCallout(ColumnDescriptor col, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        col.Item().Background(Colors.Yellow.Lighten4).Border(0.5f).BorderColor(Colors.Yellow.Darken1)
            .Padding(6).Text(text).FontSize(10).FontColor(Colors.Grey.Darken4).Italic();
    }

    private static void BuildSignature(IContainer container, EvidenceSignature sig)
    {
        container.AlignRight().Width(220).Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(3).Text(sig.Name).FontSize(10).Bold();
            if (!string.IsNullOrEmpty(sig.Title))
                col.Item().Text(sig.Title).FontSize(9).FontColor(Colors.Grey.Darken2);
            if (!string.IsNullOrEmpty(sig.Badge))
                col.Item().Text($"Badge #{sig.Badge}").FontSize(9).FontColor(Colors.Grey.Darken2);
            if (!string.IsNullOrEmpty(sig.SignedAt))
                col.Item().Text(sig.SignedAt).FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }
}
