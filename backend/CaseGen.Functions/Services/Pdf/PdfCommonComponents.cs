using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf;

/// <summary>
/// Common components and utilities for PDF generation
/// </summary>
public static class PdfCommonComponents
{
    /// <summary>
    /// Builds a standard letterhead with logo and document information
    /// </summary>
    public static void BuildLetterhead(IContainer c, string docTypeLabel, string title, string? caseId, string? docId)
    {
        c.Column(mainCol =>
        {
            mainCol.Item().PaddingBottom(6).Row(row =>
            {
                // Left side - Police Logo only (replaces department name text)
                row.RelativeItem(1).Row(leftRow =>
                {
                    var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                        "assets", "LogoMetroPolice_transparent.png");
                    
                    if (File.Exists(logoPath))
                    {
                        leftRow.AutoItem().Height(50).Image(logoPath);
                    }
                    else
                    {
                        // Fallback to text if logo not found
                        leftRow.RelativeItem().Text("MUNICIPAL POLICE DEPARTMENT")
                            .Bold().FontSize(11.5f);
                    }
                });

                row.RelativeItem(1).AlignRight().Column(col =>
                {
                    col.Item().Text(docTypeLabel).Bold().FontSize(13);
                    if (!string.IsNullOrWhiteSpace(docId))
                        col.Item().Text($"DocId: {docId}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrWhiteSpace(caseId))
                        col.Item().Text($"CaseId: {caseId}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Issued on: {DateTimeOffset.Now:yyyy-MM-dd HH:mm (zzz)}")
                              .FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                });
            });

            // Title
            mainCol.Item().PaddingTop(2).Text(title).FontSize(14).Bold();
        });
    }

    /// <summary>
    /// Adds a diagonal watermark to the background
    /// </summary>
    public static void AddWatermark(IContainer c, string text)
    {
        c.AlignCenter()
         .AlignMiddle()
         .PaddingHorizontal(50)
         .Rotate(315)
         .Text(text)
            .FontSize(48)
            .Bold()
            .FontColor(Colors.Grey.Lighten3);
    }

    /// <summary>
    /// Builds a classification band at the top or bottom
    /// </summary>
    public static void BuildClassificationBand(IContainer c, string classification)
    {
        c.PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Row(r =>
        {
            r.RelativeItem().Text(classification).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
        });
    }

    /// <summary>
    /// Builds a classification band with custom colors
    /// </summary>
    public static void BuildClassificationBand(IContainer c, string classification, string bandBg, string textColor)
    {
        c.PaddingTop(4).Background(bandBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Row(r =>
        {
            r.RelativeItem().Text(classification).FontSize(9.5f).FontColor(textColor);
        });
    }

    /// <summary>
    /// Gets theme colors for different document types
    /// </summary>
    public static (string BandBg, string BandText) GetThemeColors(string documentType)
    {
        return documentType.ToLower() switch
        {
            "police_report" => (Colors.Blue.Lighten5, Colors.Blue.Darken2),
            "forensics_report" => (Colors.Indigo.Lighten5, Colors.Indigo.Darken2),
            "interview" => (Colors.Amber.Lighten5, Colors.Amber.Darken3),
            "evidence_log" => (Colors.Teal.Lighten5, Colors.Teal.Darken2),
            "memo" or "memo_admin" => (Colors.Grey.Lighten4, Colors.Grey.Darken2),
            "witness_statement" => (Colors.DeepOrange.Lighten5, Colors.DeepOrange.Darken2),
            _ => (Colors.Grey.Lighten4, Colors.Grey.Darken2)
        };
    }

    /// <summary>
    /// Gets display label for document types
    /// </summary>
    public static string GetDocumentTypeLabel(string documentType)
    {
        return documentType.ToLower() switch
        {
            "cover_page" => "CASE FILE COVER",
            "suspect_profile" => "SUSPECT INFORMATION",
            "witness_profile" => "WITNESS INFORMATION",
            "police_report" => "INCIDENT REPORT",
            "forensics_report" => "FORENSIC REPORT",
            "interview" => "INTERVIEW TRANSCRIPT",
            "evidence_log" => "EVIDENCE CATALOG & CHAIN OF CUSTODY",
            "memo" or "memo_admin" => "INVESTIGATIVE MEMORANDUM",
            "witness_statement" => "WITNESS STATEMENT",
            "autopsy_report" => "AUTOPSY/MEDICAL EXAMINER REPORT",
            "search_warrant" => "SEARCH WARRANT",
            "case_summary" => "CASE SUMMARY",
            _ => "INVESTIGATIVE DOCUMENT"
        };
    }

    /// <summary>
    /// Gets header text for document types
    /// </summary>
    public static string GetDocumentHeader(string documentType, string title)
    {
        return documentType.ToLower() switch
        {
            "police_report" => "POLICE INCIDENT REPORT",
            "forensics_report" => "FORENSIC ANALYSIS REPORT",
            "interview" => "INVESTIGATION INTERVIEW TRANSCRIPT",
            "evidence_log" => "EVIDENCE CATALOG & CHAIN OF CUSTODY",
            "memo" => "INVESTIGATIVE MEMORANDUM",
            "witness_statement" => "WITNESS STATEMENT FORM",
            _ => "INVESTIGATIVE DOCUMENT"
        };
    }

    /// <summary>
    /// Basic markdown formatting
    /// </summary>
    public static string FormatMarkdownContent(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "No content available.";

        // Basic markdown to text conversion (for non-table content)
        return markdown
            .Replace("**", "")  // Remove bold markers
            .Replace("*", "")   // Remove italic markers
            .Replace("#", "")   // Remove headers
            .Replace("- ", "• ") // Convert bullets
            .Trim();
    }

    /// <summary>
    /// Renders markdown content with basic formatting
    /// </summary>
    public static void RenderMarkdownContent(ColumnDescriptor column, string markdownContent)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            column.Item().Text("No content available.");
            return;
        }

        var lines = markdownContent.Replace("\r\n", "\n").Split('\n');
        var i = 0;
        bool skippedFirstH1 = false;

        while (i < lines.Length)
        {
            var raw = lines[i];
            var line = raw.Trim();

            if (line.Length == 0)
            {
                column.Item().PaddingVertical(2);
                i++;
                continue;
            }

            // Ignore first H1 (already in letterhead)
            if (line.StartsWith("# ") && !skippedFirstH1)
            {
                skippedFirstH1 = true;
                i++;
                continue;
            }

            // Headers
            if (line.StartsWith("### "))
            {
                column.Item().PaddingTop(8).Text(line.Substring(4).Trim()).FontSize(11).Bold();
                i++;
                continue;
            }
            if (line.StartsWith("## "))
            {
                column.Item().PaddingTop(10).Text(line.Substring(3).Trim()).FontSize(12).Bold();
                i++;
                continue;
            }
            if (line.StartsWith("# "))
            {
                column.Item().PaddingTop(12).Text(line.Substring(2).Trim()).FontSize(14).Bold();
                i++;
                continue;
            }

            // Bullet points
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                column.Item().PaddingLeft(12).Row(r =>
                {
                    r.ConstantItem(15).Text("•").FontSize(10);
                    r.RelativeItem().Text(line.Substring(2).Trim()).FontSize(10);
                });
                i++;
                continue;
            }

            // Tables
            if (IsTableLine(line))
            {
                var tableLines = new List<string>();
                while (i < lines.Length && (IsTableLine(lines[i].Trim()) || IsTableSeparatorLine(lines[i].Trim())))
                {
                    tableLines.Add(lines[i].Trim());
                    i++;
                }
                RenderTable(column, tableLines);
                continue;
            }

            // Bold text
            if (line.Contains("**"))
            {
                column.Item().Text(line.Replace("**", "")).FontSize(10).Bold();
                i++;
                continue;
            }

            // Regular paragraph
            column.Item().PaddingBottom(4).Text(line).FontSize(10);
            i++;
        }
    }

    private static bool IsTableLine(string line)
    {
        return line.Contains("|") && line.Split('|').Length > 2;
    }

    private static bool IsTableSeparatorLine(string line)
    {
        return line.Contains("|") && line.Contains("-");
    }

    private static void RenderTable(ColumnDescriptor column, List<string> tableLines)
    {
        if (tableLines.Count < 3) return;

        // Parse header
        var headerCells = tableLines[0].Split('|', StringSplitOptions.RemoveEmptyEntries);
        // Skip separator line (index 1)
        var dataRows = tableLines.Skip(2).Select(l => l.Split('|', StringSplitOptions.RemoveEmptyEntries)).ToList();

        column.Item().PaddingTop(8).PaddingBottom(8).Table(table =>
        {
            // Define columns
            foreach (var _ in headerCells)
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                });
            }

            // Header row
            table.Header(header =>
            {
                foreach (var cell in headerCells)
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1)
                        .Padding(5).Text(cell.Trim()).FontSize(9).Bold();
                }
            });

            // Data rows
            foreach (var row in dataRows)
            {
                foreach (var cell in row)
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2)
                        .Padding(5).Text(cell.Trim()).FontSize(9);
                }
            }
        });
    }
}
