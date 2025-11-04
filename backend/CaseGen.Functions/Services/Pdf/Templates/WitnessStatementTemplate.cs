using CaseGen.Functions.Services.Pdf;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf.Templates;

/// <summary>
/// Template for generating Witness Statement documents with notary certification
/// </summary>
public class WitnessStatementTemplate
{
    private readonly ILogger _logger;

    public WitnessStatementTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public byte[] Generate(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • LAW ENFORCEMENT SENSITIVE";
            
            return Document.Create(container =>
            {
                // PAGE 1: Cover page with witness information
                container.Page(page =>
                {
                    ConfigurePage(page, classification);
                    
                    page.Header().Column(headerCol =>
                    {
                        var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                            "assets", "LogoMetroPolice_transparent.png");
                        
                        if (File.Exists(logoPath))
                        {
                            headerCol.Item().AlignCenter().Height(100).Image(logoPath);
                        }
                        
                        headerCol.Item().PaddingTop(15).AlignCenter()
                            .Text("MUNICIPAL POLICE DEPARTMENT").FontSize(16).Bold();
                        
                        headerCol.Item().PaddingTop(5).AlignCenter()
                            .Text("WITNESS STATEMENT").FontSize(20).Bold().LetterSpacing(1);
                        
                        headerCol.Item().PaddingTop(10).BorderBottom(2).BorderColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(20).Column(col =>
                    {
                        RenderCoverPage(col, caseId, docId);
                    });
                    
                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span(classification).FontSize(8).FontColor(Colors.Grey.Darken2);
                        txt.Span(" • Page 1").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
                
                // PAGE 2+: Statement content
                container.Page(page =>
                {
                    ConfigurePage(page, classification);
                    
                    page.Header().Column(col =>
                    {
                        col.Item().Element(e => PdfCommonComponents.BuildLetterhead(e, "WITNESS STATEMENT", title, caseId, docId));
                        col.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                    });
                    
                    page.Content().PaddingTop(15).Column(col =>
                    {
                        RenderWitnessStatementContent(col, markdownContent, caseId, docId);
                    });
                    
                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span(classification).FontSize(8).FontColor(Colors.Grey.Darken2);
                        txt.Span(" • Page 2").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating multi-page witness statement: {Message}", ex.Message);
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
        // Case information box
        col.Item().Border(2).BorderColor(Colors.Blue.Darken2)
            .Padding(12).Column(c =>
        {
            c.Item().PaddingBottom(10).Row(r =>
            {
                r.RelativeItem().Text($"CASE NUMBER:").FontSize(10).Bold();
                r.RelativeItem().Text(caseId ?? "N/A").FontSize(11).Bold()
                    .FontColor(Colors.Blue.Darken2);
            });
            
            c.Item().PaddingBottom(8).Row(r =>
            {
                r.RelativeItem().Text("DOCUMENT ID:").FontSize(9);
                r.RelativeItem().Text(docId ?? "N/A").FontSize(9)
                    .FontColor(Colors.Grey.Darken2);
            });
            
            c.Item().Row(r =>
            {
                r.RelativeItem().Text("DATE OF STATEMENT:").FontSize(9);
                r.RelativeItem().Text(DateTime.Now.ToString("MMMM dd, yyyy")).FontSize(9);
            });
        });
        
        // Witness information section
        col.Item().PaddingTop(20).Text("WITNESS INFORMATION").FontSize(12).Bold()
            .FontColor(Colors.Grey.Darken4);
        
        col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(12).Column(c =>
        {
            c.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(150).Text("FULL NAME:").FontSize(10).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .AlignMiddle().Text("________________________________________").FontSize(9);
            });
            
            c.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(150).Text("DATE OF BIRTH:").FontSize(10).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .AlignMiddle().Text("________________________________________").FontSize(9);
            });
            
            c.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(150).Text("ADDRESS:").FontSize(10).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .AlignMiddle().Text("________________________________________").FontSize(9);
            });
            
            c.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(150).Text("PHONE NUMBER:").FontSize(10).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .AlignMiddle().Text("________________________________________").FontSize(9);
            });
            
            c.Item().Row(r =>
            {
                r.ConstantItem(150).Text("OCCUPATION:").FontSize(10).Bold();
                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .AlignMiddle().Text("________________________________________").FontSize(9);
            });
        });
        
        // Statement certification box
        col.Item().PaddingTop(20).Background(Colors.Grey.Lighten3)
            .Border(1).BorderColor(Colors.Grey.Darken1)
            .Padding(12).Column(c =>
        {
            c.Item().Text("STATEMENT CERTIFICATION").FontSize(10).Bold()
                .FontColor(Colors.Grey.Darken4);
            
            c.Item().PaddingTop(8).Text("I hereby certify that the following statement is true and accurate to the best of my knowledge. I understand that providing false information to law enforcement is a crime.")
                .FontSize(9).Italic().LineHeight(1.4f);
        });
        
        // Oath acknowledgment
        col.Item().PaddingTop(20).Row(r =>
        {
            r.ConstantItem(25).AlignMiddle().Text("☑").FontSize(16)
                .FontColor(Colors.Blue.Darken2);
            r.RelativeItem().AlignMiddle().Text("Witness has been advised of their rights and obligations")
                .FontSize(9).Bold();
        });
        
        col.Item().PaddingTop(8).Row(r =>
        {
            r.ConstantItem(25).AlignMiddle().Text("☑").FontSize(16)
                .FontColor(Colors.Blue.Darken2);
            r.RelativeItem().AlignMiddle().Text("Statement given voluntarily without coercion")
                .FontSize(9).Bold();
        });
        
        col.Item().PaddingTop(8).Row(r =>
        {
            r.ConstantItem(25).AlignMiddle().Text("☑").FontSize(16)
                .FontColor(Colors.Blue.Darken2);
            r.RelativeItem().AlignMiddle().Text("Witness has read and approved this statement")
                .FontSize(9).Bold();
        });
    }

    private void RenderWitnessStatementContent(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Statement header
        col.Item().PaddingBottom(15).Column(c =>
        {
            c.Item().Text("WITNESS STATEMENT").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken4);
            c.Item().PaddingTop(5).Text($"Case Reference: {caseId ?? "N/A"}").FontSize(9)
                .FontColor(Colors.Grey.Darken2);
            c.Item().Text($"Statement Date: {DateTime.Now.ToString("MMMM dd, yyyy")}").FontSize(9)
                .FontColor(Colors.Grey.Darken2);
        });
        
        // Statement content
        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(12).Column(c =>
        {
            c.Item().PaddingBottom(10).Text("STATEMENT:").FontSize(10).Bold()
                .FontColor(Colors.Grey.Darken3);
            
            var lines = md.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // Skip markdown headers
                if (line.TrimStart().StartsWith("#")) continue;
                
                c.Item().PaddingBottom(8).Text(line.Trim()).FontSize(10)
                    .LineHeight(1.5f);
            }
        });
        
        // Witness signature section
        col.Item().PaddingTop(30).Border(2).BorderColor(Colors.Blue.Darken2)
            .Padding(15).Column(c =>
        {
            c.Item().PaddingBottom(15).Text("WITNESS SIGNATURE").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken4);
            
            // Witness signature
            c.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(sigCol =>
                {
                    sigCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________________________________").FontSize(9);
                    sigCol.Item().PaddingTop(2).Text("Witness Signature").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });
                
                row.ConstantItem(20);
                
                row.RelativeItem().Column(dateCol =>
                {
                    dateCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________").FontSize(9);
                    dateCol.Item().PaddingTop(2).Text("Date").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });
            });
            
            // Print name
            c.Item().PaddingTop(20).Row(row =>
            {
                row.ConstantItem(120).Text("PRINT NAME:").FontSize(9).Bold()
                    .FontColor(Colors.Grey.Darken3);
                row.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                    .AlignMiddle().Text("__________________________________________").FontSize(9);
            });
        });
        
        // Officer witnessing signature section
        col.Item().PaddingTop(20).Border(1).BorderColor(Colors.Grey.Darken2)
            .Padding(12).Column(c =>
        {
            c.Item().PaddingBottom(15).Text("OFFICER VERIFICATION").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken4);
            
            c.Item().PaddingBottom(10).Text("I certify that this statement was given voluntarily and that the witness was advised of their rights and obligations.")
                .FontSize(9).Italic().LineHeight(1.4f);
            
            // Officer signature
            c.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(sigCol =>
                {
                    sigCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________________________________").FontSize(9);
                    sigCol.Item().PaddingTop(2).Text("Officer Signature").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });
                
                row.ConstantItem(20);
                
                row.RelativeItem().Column(badgeCol =>
                {
                    badgeCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________").FontSize(9);
                    badgeCol.Item().PaddingTop(2).Text("Badge Number").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });
                
                row.ConstantItem(20);
                
                row.RelativeItem().Column(dateCol =>
                {
                    dateCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________").FontSize(9);
                    dateCol.Item().PaddingTop(2).Text("Date").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }
}
