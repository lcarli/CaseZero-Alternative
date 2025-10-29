using CaseGen.Functions.Services.Pdf;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf.Templates;

/// <summary>
/// Template for generating Forensic Analysis Report documents with lab certification
/// </summary>
public class ForensicsReportTemplate
{
    private readonly ILogger _logger;

    public ForensicsReportTemplate(ILogger logger)
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
                        var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                            "assets", "LogoMetroPolice_transparent.png");
                        
                        if (File.Exists(logoPath))
                        {
                            headerCol.Item().AlignCenter().Height(120).Image(logoPath);
                        }
                        
                        headerCol.Item().PaddingTop(20).AlignCenter()
                            .Text("FORENSIC ANALYSIS REPORT").FontSize(24).Bold();
                        headerCol.Item().AlignCenter()
                            .Text("CRIME LABORATORY").FontSize(18).FontColor(Colors.Grey.Darken2);
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
                
                // PAGE 2+: Analysis Content
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
                                c.Item().Text("FORENSIC ANALYSIS REPORT").Bold().FontSize(13);
                                c.Item().Text($"Lab Case: {docId ?? "________"}").FontSize(9.5f)
                                    .FontColor(Colors.Grey.Darken2);
                            });
                        });
                        
                        headerCol.Item().PaddingTop(5).Text(title).FontSize(13).Bold();
                        headerCol.Item().PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                            .PaddingBottom(4).Text(classification).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderForensicsReportContent(col, markdownContent, caseId, docId);
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
            _logger.LogError(ex, "Error generating multi-page forensics report: {Message}", ex.Message);
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
        col.Item().AlignCenter().Width(400).Border(2).BorderColor(Colors.Indigo.Darken2)
            .Background(Colors.Indigo.Lighten5).Padding(20).Column(c =>
        {
            c.Item().AlignCenter().Text("LAB CASE INFORMATION").FontSize(14).Bold()
                .FontColor(Colors.Indigo.Darken3);
            
            c.Item().PaddingTop(15).Column(infoCol =>
            {
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Case Number:").Bold().FontSize(11);
                    r.RelativeItem().Text(caseId ?? "________").FontSize(11).FontColor(Colors.Indigo.Darken3);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Lab Case No.:").Bold().FontSize(11);
                    r.RelativeItem().Text(docId ?? "________").FontSize(11).FontColor(Colors.Indigo.Darken3);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Date Analyzed:").Bold().FontSize(11);
                    r.RelativeItem().Text(DateTimeOffset.Now.ToString("MMMM dd, yyyy")).FontSize(11);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Analyst:").Bold().FontSize(11);
                    r.RelativeItem().Text("[NAME], Forensic Scientist").FontSize(11);
                });
                
                infoCol.Item().Row(r =>
                {
                    r.RelativeItem().Text("Lab Director:").Bold().FontSize(11);
                    r.RelativeItem().Text("[NAME], Ph.D.").FontSize(11);
                });
            });
        });
        
        // Analysis Type Badges
        col.Item().PaddingTop(30).AlignCenter().Row(badgeRow =>
        {
            badgeRow.Spacing(10);
            
            badgeRow.AutoItem().Background(Colors.Blue.Lighten3).PaddingVertical(5).PaddingHorizontal(12)
                .Text("FINGERPRINTS").FontSize(9).Bold().FontColor(Colors.Blue.Darken3);
            
            badgeRow.AutoItem().Background(Colors.Green.Lighten3).PaddingVertical(5).PaddingHorizontal(12)
                .Text("DNA").FontSize(9).Bold().FontColor(Colors.Green.Darken3);
            
            badgeRow.AutoItem().Background(Colors.Orange.Lighten3).PaddingVertical(5).PaddingHorizontal(12)
                .Text("TRACE EVIDENCE").FontSize(9).Bold().FontColor(Colors.Orange.Darken3);
        });
        
        // Classification Banner
        col.Item().PaddingTop(40).AlignCenter().Width(400)
            .Background(Colors.Red.Lighten4).Padding(10)
            .Text("CONFIDENTIAL • LABORATORY USE ONLY")
            .FontSize(10).Bold().FontColor(Colors.Red.Darken3);
        
        // Lab Protocols Notice
        col.Item().PaddingTop(30).PaddingHorizontal(60).Column(instrCol =>
        {
            instrCol.Item().Text("LABORATORY CERTIFICATION").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            
            instrCol.Item().PaddingTop(10).Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(10).LineHeight(1.6f));
                t.Line("• Analysis conducted in accordance with FBI Quality Assurance Standards");
                t.Line("• All testing procedures follow validated protocols");
                t.Line("• Evidence handling complies with ISO/IEC 17025 requirements");
                t.Line("• Results reviewed and approved by laboratory supervisor");
                t.Line("• Chain of custody maintained throughout analysis");
            });
        });
    }

    private void RenderForensicsReportContent(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Evidence item reference box
        col.Item().PaddingBottom(15).Background(Colors.Indigo.Lighten5).Border(1)
            .BorderColor(Colors.Indigo.Lighten3).Padding(10).Column(refCol =>
        {
            refCol.Item().Text("EVIDENCE ITEM REFERENCE").FontSize(11).Bold().FontColor(Colors.Indigo.Darken3);
            
            refCol.Item().PaddingTop(8).Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("Evidence ID: [EV001, EV002, etc.]").FontSize(10);
                    c.Item().Text("Item Description: [From case data]").FontSize(10);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("Collection Date: [Date]").FontSize(10);
                    c.Item().Text("Submitting Officer: [Name] - Badge #[####]").FontSize(10);
                });
            });
        });
        
        // Methodology section header
        col.Item().PaddingTop(10).PaddingBottom(5).BorderBottom(2).BorderColor(Colors.Indigo.Darken2)
            .Text("ANALYSIS METHODOLOGY").FontSize(12).Bold().FontColor(Colors.Indigo.Darken3);
        
        // Render markdown content (analysis details)
        PdfCommonComponents.RenderMarkdownContent(col, md);
        
        // Analyst certification footer
        col.Item().PaddingTop(20).BorderTop(2).BorderColor(Colors.Indigo.Darken2)
            .PaddingTop(15).Column(certCol =>
        {
            certCol.Item().Text("ANALYST CERTIFICATION").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken3);
            
            certCol.Item().PaddingTop(10).Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(9).LineHeight(1.5f));
                t.Span("I certify that the above analysis was performed in accordance with established ");
                t.Span("laboratory protocols and quality assurance standards. The findings and conclusions ");
                t.Span("are based on the evidence submitted and the testing procedures employed.");
            });
            
            certCol.Item().PaddingTop(15).Row(sigRow =>
            {
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Forensic Analyst Signature").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                sigRow.ConstantItem(20);
                
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Credentials / Badge").FontSize(8)
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
