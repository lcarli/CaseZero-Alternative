using CaseGen.Functions.Services.Pdf;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf.Templates;

/// <summary>
/// Template for generating Evidence Log documents with cover page and chain of custody
/// </summary>
public class EvidenceLogTemplate
{
    private readonly ILogger _logger;

    public EvidenceLogTemplate(ILogger logger)
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
                // PAGE 1: Cover Page
                container.Page(page =>
                {
                    ConfigurePage(page, classification);
                    
                    page.Header().Column(headerCol =>
                    {
                        // Logo at top
                        var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                            "assets", "LogoMetroPolice_transparent.png");
                        
                        if (File.Exists(logoPath))
                        {
                            headerCol.Item().AlignCenter().Height(120).Image(logoPath);
                        }
                        
                        headerCol.Item().PaddingTop(20).AlignCenter()
                            .Text("EVIDENCE CATALOG").FontSize(24).Bold();
                        headerCol.Item().AlignCenter()
                            .Text("CHAIN OF CUSTODY LOG").FontSize(18).FontColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(40).Column(col =>
                    {
                        RenderCoverPage(col, caseId, docId);
                    });
                    
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                        t.Span("   •   Page 1");
                    });
                });
                
                // PAGE 2+: Evidence Items Content
                container.Page(page =>
                {
                    ConfigurePage(page, classification);
                    
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(r =>
                        {
                            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                                "assets", "LogoMetroPolice_transparent.png");
                            
                            if (File.Exists(logoPath))
                            {
                                r.AutoItem().Height(40).Image(logoPath);
                            }
                            
                            r.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("EVIDENCE CATALOG").Bold().FontSize(13);
                                c.Item().Text($"Case: {caseId ?? "________"}").FontSize(9.5f)
                                    .FontColor(Colors.Grey.Darken2);
                            });
                        });
                        
                        headerCol.Item().PaddingTop(5).Text(title).FontSize(13).Bold();
                        headerCol.Item().PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                            .PaddingBottom(4).Text(classification).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderEvidenceLogContent(col, markdownContent, caseId, docId);
                    });
                    
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                        t.Span("   •   Page ");
                        t.CurrentPageNumber().FontSize(9);
                    });
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating multi-page evidence log: {Message}", ex.Message);
            throw;
        }
    }

    private void ConfigurePage(PageDescriptor page, string classification)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
        page.Background().Element(e => PdfCommonComponents.AddWatermark(e, classification));
    }

    private void RenderCoverPage(ColumnDescriptor col, string? caseId, string? docId)
    {
        // Case Information Box
        col.Item().AlignCenter().Width(400).Border(2).BorderColor(Colors.Teal.Darken2)
            .Background(Colors.Teal.Lighten5).Padding(20).Column(c =>
        {
            c.Item().AlignCenter().Text("CASE INFORMATION").FontSize(14).Bold()
                .FontColor(Colors.Teal.Darken3);
            
            c.Item().PaddingTop(15).Column(infoCol =>
            {
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Case Number:").Bold().FontSize(11);
                    r.RelativeItem().Text(caseId ?? "________").FontSize(11).FontColor(Colors.Teal.Darken3);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Document ID:").Bold().FontSize(11);
                    r.RelativeItem().Text(docId ?? "________").FontSize(11).FontColor(Colors.Teal.Darken3);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Date Issued:").Bold().FontSize(11);
                    r.RelativeItem().Text(DateTimeOffset.Now.ToString("MMMM dd, yyyy")).FontSize(11);
                });
                
                infoCol.Item().Row(r =>
                {
                    r.RelativeItem().Text("Lead Investigator:").Bold().FontSize(11);
                    r.RelativeItem().Text("[NAME] - Badge #[####]").FontSize(11);
                });
            });
        });
        
        // Classification Banner
        col.Item().PaddingTop(40).AlignCenter().Width(400)
            .Background(Colors.Red.Lighten4).Padding(10)
            .Text("CONFIDENTIAL • CHAIN OF CUSTODY REQUIRED")
            .FontSize(10).Bold().FontColor(Colors.Red.Darken3);
        
        // Instructions
        col.Item().PaddingTop(30).PaddingHorizontal(60).Column(instrCol =>
        {
            instrCol.Item().Text("EVIDENCE HANDLING PROTOCOL").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            
            instrCol.Item().PaddingTop(10).Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(10).LineHeight(1.6f));
                t.Line("• All evidence items must be logged and tagged immediately upon collection");
                t.Line("• Chain of custody must be maintained at all times");
                t.Line("• Transfers must be documented with date, time, and signatures");
                t.Line("• Storage conditions must meet department standards");
                t.Line("• Unauthorized access is strictly prohibited");
            });
        });
    }

    private void RenderEvidenceLogContent(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Evidence summary box
        col.Item().PaddingBottom(15).Background(Colors.Teal.Lighten5).Border(1)
            .BorderColor(Colors.Teal.Lighten3).Padding(10).Row(r =>
        {
            r.RelativeItem().Column(c =>
            {
                c.Item().Text("EVIDENCE SUMMARY").FontSize(11).Bold().FontColor(Colors.Teal.Darken3);
                c.Item().PaddingTop(5).Text("Total Items: [Count from content]").FontSize(10);
                c.Item().Text("Status: Active Collection").FontSize(10);
            });
            r.RelativeItem().AlignRight().Column(c =>
            {
                c.Item().Text("CHAIN OF CUSTODY").FontSize(11).Bold().FontColor(Colors.Teal.Darken3);
                c.Item().PaddingTop(5).Text("All transfers documented").FontSize(10);
                c.Item().Text("Secure storage verified").FontSize(10);
            });
        });
        
        // Render markdown content (evidence items)
        PdfCommonComponents.RenderMarkdownContent(col, md);
        
        // Chain of custody certification footer
        col.Item().PaddingTop(20).BorderTop(2).BorderColor(Colors.Teal.Darken2)
            .PaddingTop(15).Column(certCol =>
        {
            certCol.Item().Text("CHAIN OF CUSTODY CERTIFICATION").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken3);
            
            certCol.Item().PaddingTop(10).Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(9).LineHeight(1.5f));
                t.Span("I certify that the above evidence items have been collected, documented, ");
                t.Span("and stored in accordance with departmental procedures. Chain of custody ");
                t.Span("has been maintained at all times.");
            });
            
            certCol.Item().PaddingTop(15).Row(sigRow =>
            {
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Evidence Custodian Signature").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                sigRow.ConstantItem(20);
                
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                sigRow.ConstantItem(20);
                
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text($"{DateTimeOffset.Now:MM/dd/yyyy}").FontSize(9);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }
}
