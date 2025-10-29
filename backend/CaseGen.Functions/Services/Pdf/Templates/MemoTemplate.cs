using CaseGen.Functions.Services.Pdf;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf.Templates;

/// <summary>
/// Template for generating Memo documents with routing and acknowledgments
/// </summary>
public class MemoTemplate
{
    private readonly ILogger _logger;

    public MemoTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public byte[] Generate(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => PdfCommonComponents.AddWatermark(e, classification));
                    
                    page.Header().Column(headerCol =>
                    {
                        // Logo and department header
                        headerCol.Item().Row(r =>
                        {
                            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                                "assets", "LogoMetroPolice_transparent.png");
                            
                            if (File.Exists(logoPath))
                            {
                                r.AutoItem().Height(60).Image(logoPath);
                            }
                            
                            r.RelativeItem().PaddingLeft(15).AlignMiddle().Column(c =>
                            {
                                c.Item().Text("MUNICIPAL POLICE DEPARTMENT").FontSize(14).Bold();
                                c.Item().Text("Interdepartmental Communication").FontSize(11)
                                    .FontColor(Colors.Grey.Darken2);
                            });
                        });
                        
                        headerCol.Item().PaddingTop(15).AlignCenter()
                            .Text("M E M O R A N D U M").FontSize(18).Bold();
                        
                        headerCol.Item().PaddingTop(10).BorderBottom(2).BorderColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(20).Column(col =>
                    {
                        RenderMemoHeader(col, title, caseId, docId);
                        RenderMemoContent(col, markdownContent, caseId, docId);
                    });
                    
                    page.Footer().Column(footerCol =>
                    {
                        footerCol.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(8);
                        footerCol.Item().AlignCenter().Text(t =>
                        {
                            t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                            t.Span("   •   Page ");
                            t.CurrentPageNumber().FontSize(9);
                        });
                    });
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating multi-page memo: {Message}", ex.Message);
            throw;
        }
    }

    private void RenderMemoHeader(ColumnDescriptor col, string title, string? caseId, string? docId)
    {
        // Memo header block
        col.Item().Column(memoHeader =>
        {
            // TO field
            memoHeader.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(120).Text("TO:").FontSize(11).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .PaddingBottom(3).Text("[Recipient Name / Unit]").FontSize(11);
            });
            
            // FROM field
            memoHeader.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(120).Text("FROM:").FontSize(11).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .PaddingBottom(3).Text("[Sender Name / Unit] - Badge #[####]").FontSize(11);
            });
            
            // DATE field
            memoHeader.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(120).Text("DATE:").FontSize(11).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .PaddingBottom(3).Text(DateTimeOffset.Now.ToString("MMMM dd, yyyy")).FontSize(11);
            });
            
            // CASE REF field
            memoHeader.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(120).Text("CASE REF:").FontSize(11).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .PaddingBottom(3).Text(caseId ?? "N/A").FontSize(11).FontColor(Colors.Blue.Darken2);
            });
            
            // SUBJECT field (RE:)
            memoHeader.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(120).Text("RE:").FontSize(11).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .PaddingBottom(3).Text(title).FontSize(11).Bold();
            });
        });
        
        // Priority indicator (if applicable)
        col.Item().PaddingTop(15).PaddingBottom(15).Row(priorityRow =>
        {
            priorityRow.AutoItem().Text("PRIORITY: ").FontSize(10).Bold();
            priorityRow.AutoItem().Background(Colors.Orange.Lighten3).PaddingVertical(3).PaddingHorizontal(8)
                .Text("☐ ROUTINE  ☐ URGENT  ☑ TIME SENSITIVE").FontSize(9).Bold()
                .FontColor(Colors.Orange.Darken3);
        });
        
        // Horizontal line separator
        col.Item().PaddingBottom(15).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
    }

    private void RenderMemoContent(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Parse markdown sections
        var lines = md.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sections = new Dictionary<string, List<string>>
        {
            ["BACKGROUND"] = new List<string>(),
            ["DISCUSSION"] = new List<string>(),
            ["RECOMMENDATIONS"] = new List<string>(),
            ["ACTION ITEMS"] = new List<string>()
        };
        
        string currentSection = "";
        foreach (var line in lines)
        {
            var upper = line.Trim().ToUpper();
            if (upper.Contains("BACKGROUND"))
                currentSection = "BACKGROUND";
            else if (upper.Contains("DISCUSSION") || upper.Contains("FINDINGS"))
                currentSection = "DISCUSSION";
            else if (upper.Contains("RECOMMENDATION"))
                currentSection = "RECOMMENDATIONS";
            else if (upper.Contains("ACTION ITEM"))
                currentSection = "ACTION ITEMS";
            else if (!string.IsNullOrWhiteSpace(currentSection) && !string.IsNullOrWhiteSpace(line))
                sections[currentSection].Add(line);
        }

        // Render sections
        foreach (var section in sections)
        {
            if (section.Value.Count == 0) continue;

            col.Item().PaddingTop(15).PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Darken2)
                    .Padding(6).AlignMiddle()
                    .Text(section.Key).FontSize(11).Bold()
                    .FontColor(Colors.Grey.Darken4);
            });

            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(10).Column(c =>
            {
                foreach (var line in section.Value)
                {
                    c.Item().PaddingBottom(4).Text(line.Trim()).FontSize(10)
                        .LineHeight(1.4f);
                }
            });
        }

        // If no sections found, render all content
        if (sections.All(s => s.Value.Count == 0))
        {
            col.Item().PaddingTop(15).Border(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(12).Column(c =>
            {
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    c.Item().PaddingBottom(4).Text(line.Trim()).FontSize(10)
                        .LineHeight(1.4f);
                }
            });
        }

        // Acknowledgment section
        col.Item().PaddingTop(30).Border(1).BorderColor(Colors.Grey.Darken2)
            .Padding(15).Column(ackCol =>
        {
            ackCol.Item().PaddingBottom(10).Text("ACKNOWLEDGMENT & ROUTING")
                .FontSize(11).Bold().FontColor(Colors.Grey.Darken4);
            
            ackCol.Item().PaddingTop(10).Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Signature").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                r.ConstantItem(15);
                
                r.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Name / Badge").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                r.ConstantItem(15);
                
                r.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }
}
