using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering;

/// <summary>
/// Section-payload helpers reused by every layout. A layout decides the
/// chrome (header, footer, section heading style) and then delegates the
/// section bodies (narrative paragraphs, tables, transcripts, …) to the
/// helpers here. Keeping the rendering of payloads central means a new
/// section <c>kind</c> needs to be wired in exactly one place.
/// </summary>
public static class DocumentBlocks
{
    public static void RenderSections(
        ColumnDescriptor col,
        EvidenceDocument doc,
        Action<ColumnDescriptor, string> renderHeading,
        TextStyle? bodyStyle = null)
    {
        foreach (var s in doc.Sections)
        {
            col.Item().PaddingTop(10).Column(sectionCol =>
            {
                if (!string.IsNullOrEmpty(s.Heading))
                {
                    renderHeading(sectionCol, s.Heading!);
                }

                switch ((s.Kind ?? "narrative").ToLowerInvariant())
                {
                    case "narrative":
                        RenderNarrative(sectionCol, s.Text ?? string.Empty, bodyStyle);
                        break;
                    case "table":
                        RenderTable(sectionCol, s.Table);
                        break;
                    case "keyvalue":
                        RenderKeyValue(sectionCol, s.Items);
                        break;
                    case "transcript":
                        RenderTranscript(sectionCol, s.Transcript);
                        break;
                    case "code":
                        RenderCode(sectionCol, s.Code);
                        break;
                    case "callout":
                        RenderCallout(sectionCol, s.Text ?? string.Empty);
                        break;
                    default:
                        RenderNarrative(sectionCol, s.Text ?? $"(unsupported section kind: {s.Kind})", bodyStyle);
                        break;
                }
            });
        }
    }

    public static void RenderNarrative(ColumnDescriptor col, string text, TextStyle? bodyStyle = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paragraphs)
        {
            var trimmed = p.TrimEnd();
            var lines = trimmed.Split('\n');
            var bulletLines = lines.Where(l => l.TrimStart().StartsWith("- ")).ToList();
            if (bulletLines.Count > 0 && bulletLines.Count == lines.Length)
            {
                foreach (var line in bulletLines)
                    col.Item().PaddingLeft(8).Text(t =>
                    {
                        t.DefaultTextStyle(bodyStyle ?? TextStyle.Default.FontSize(10));
                        t.Span($"•  {line.TrimStart().Substring(2).Trim()}");
                    });
            }
            else
            {
                col.Item().PaddingBottom(3).Text(t =>
                {
                    t.DefaultTextStyle((bodyStyle ?? TextStyle.Default.FontSize(10)).FontColor(Colors.Grey.Darken4));
                    t.Span(trimmed);
                });
            }
        }
    }

    public static void RenderTable(ColumnDescriptor col, EvidenceTable? table, string headerBg = "#1F3864", string headerFg = "#FFFFFF")
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
                    h.Cell().Background(headerBg).Padding(4)
                        .Text(colName).Bold().FontSize(9).FontColor(headerFg);
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

    public static void RenderKeyValue(ColumnDescriptor col, List<EvidenceKeyValue>? items, float labelWidth = 140f)
    {
        if (items is null || items.Count == 0) return;
        col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(kvCol =>
        {
            foreach (var kv in items)
            {
                kvCol.Item().Row(r =>
                {
                    r.ConstantItem(labelWidth).Text(kv.Label).Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    r.RelativeItem().Text(kv.Value).FontSize(9);
                });
            }
        });
    }

    public static void RenderTranscript(ColumnDescriptor col, List<EvidenceTranscriptEntry>? entries)
    {
        if (entries is null || entries.Count == 0) return;
        col.Item().Column(tc =>
        {
            foreach (var e in entries)
            {
                tc.Item().PaddingBottom(4).Row(r =>
                {
                    r.ConstantItem(90).Column(meta =>
                    {
                        if (!string.IsNullOrEmpty(e.Speaker))
                            meta.Item().Text(e.Speaker).FontSize(9).Bold().FontColor(Colors.Blue.Darken2);
                        if (!string.IsNullOrEmpty(e.Timestamp))
                            meta.Item().Text(e.Timestamp).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    r.RelativeItem().Text(e.Text).FontSize(10).FontColor(Colors.Grey.Darken4);
                });
            }
        });
    }

    public static void RenderCode(ColumnDescriptor col, EvidenceCode? code)
    {
        if (code is null || string.IsNullOrEmpty(code.Content)) return;
        col.Item().Background(Colors.Grey.Lighten5).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(6).Text(code.Content)
            .FontFamily(DocumentResources.FontFamilies.Mono)
            .FontSize(8.5f).FontColor(Colors.Grey.Darken4);
    }

    public static void RenderCallout(ColumnDescriptor col, string text, string bg = "#FFF7D6", string border = "#E0B400")
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        col.Item().Background(bg).Border(0.5f).BorderColor(border)
            .Padding(6).Text(text).FontSize(10).FontColor(Colors.Grey.Darken4).Italic();
    }

    public static void RenderSignature(IContainer container, EvidenceSignature sig, ChromeKey caption = ChromeKey.Signed, string? language = null)
    {
        container.AlignRight().Width(240).Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(3).Text(sig.Name).FontSize(10).Bold();
            if (!string.IsNullOrEmpty(sig.Title))
                col.Item().Text(sig.Title).FontSize(9).FontColor(Colors.Grey.Darken2);
            if (!string.IsNullOrEmpty(sig.Badge))
                col.Item().Text($"#{sig.Badge}").FontSize(9).FontColor(Colors.Grey.Darken2);
            if (!string.IsNullOrEmpty(sig.SignedAt))
                col.Item().Text($"{DocumentChrome.Get(caption, language)} · {sig.SignedAt}")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }

    /// <summary>Footer with classification + page X of Y, localized.</summary>
    public static void StandardFooter(IContainer container, EvidenceDocument doc)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(doc.Classification.ToUpperInvariant())
                .FontSize(7).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(7).FontColor(Colors.Grey.Medium));
                t.Span($"{DocumentChrome.Get(ChromeKey.Page, doc.Language)} ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }
}
