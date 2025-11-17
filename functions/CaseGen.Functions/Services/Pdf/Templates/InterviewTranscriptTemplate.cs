using CaseGen.Functions.Services.Pdf;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf.Templates;

/// <summary>
/// Template for generating Interview Transcript documents with Miranda rights
/// </summary>
public class InterviewTranscriptTemplate
{
    private readonly ILogger _logger;

    public InterviewTranscriptTemplate(ILogger logger)
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
                            headerCol.Item().AlignCenter().Height(100).Image(logoPath);
                        }
                        
                        headerCol.Item().PaddingTop(20).AlignCenter()
                            .Text("INTERVIEW TRANSCRIPT").FontSize(24).Bold();
                        headerCol.Item().AlignCenter()
                            .Text("OFFICIAL STATEMENT RECORD").FontSize(16).FontColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(40).Column(col =>
                    {
                        RenderCoverPage(col, title, caseId, docId);
                    });
                    
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                        t.Span("   •   Page 1");
                    });
                });
                
                // PAGE 2+: Interview Content
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
                                c.Item().Text("INTERVIEW TRANSCRIPT").Bold().FontSize(13);
                                c.Item().Text($"Interview ID: {docId ?? "________"}").FontSize(9.5f)
                                    .FontColor(Colors.Grey.Darken2);
                            });
                        });
                        
                        headerCol.Item().PaddingTop(5).Text(title).FontSize(13).Bold();
                        headerCol.Item().PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                            .PaddingBottom(4).Text(classification).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderInterviewContent(col, markdownContent, caseId, docId);
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
            _logger.LogError(ex, "Error generating multi-page interview: {Message}", ex.Message);
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

    private void RenderCoverPage(ColumnDescriptor col, string title, string? caseId, string? docId)
    {
        // Interview Information Box
        col.Item().AlignCenter().Width(450).Border(2).BorderColor(Colors.Amber.Darken2)
            .Background(Colors.Amber.Lighten5).Padding(20).Column(c =>
        {
            c.Item().AlignCenter().Text("INTERVIEW DETAILS").FontSize(14).Bold()
                .FontColor(Colors.Amber.Darken3);
            
            c.Item().PaddingTop(15).Column(infoCol =>
            {
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Case Number:").Bold().FontSize(11);
                    r.RelativeItem().Text(caseId ?? "________").FontSize(11).FontColor(Colors.Amber.Darken3);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Interview ID:").Bold().FontSize(11);
                    r.RelativeItem().Text(docId ?? "________").FontSize(11).FontColor(Colors.Amber.Darken3);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Date/Time:").Bold().FontSize(11);
                    r.RelativeItem().Text(DateTimeOffset.Now.ToString("MMMM dd, yyyy - HH:mm")).FontSize(11);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Location:").Bold().FontSize(11);
                    r.RelativeItem().Text("Police Station - Interview Room [#]").FontSize(11);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Subject Name:").Bold().FontSize(11);
                    r.RelativeItem().Text("[Name from case data]").FontSize(11);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Subject Type:").Bold().FontSize(11);
                    r.RelativeItem().Text("☐ Witness  ☐ Suspect  ☐ Victim").FontSize(10);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Interviewer(s):").Bold().FontSize(11);
                    r.RelativeItem().Text("Det. [NAME] - Badge #[####]").FontSize(11);
                });
                
                infoCol.Item().Row(r =>
                {
                    r.RelativeItem().Text("Duration:").Bold().FontSize(11);
                    r.RelativeItem().Text("[##] minutes").FontSize(11);
                });
            });
        });
        
        // Miranda Rights / Rights Advisement (if suspect)
        col.Item().PaddingTop(30).AlignCenter().Width(450)
            .Border(1).BorderColor(Colors.Red.Lighten2)
            .Background(Colors.Red.Lighten5).Padding(15).Column(mirandaCol =>
        {
            mirandaCol.Item().Text("RIGHTS ADVISEMENT (If Suspect)").FontSize(11).Bold()
                .FontColor(Colors.Red.Darken3);
            
            mirandaCol.Item().PaddingTop(8).Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(9).LineHeight(1.5f));
                t.Line("☐ Subject advised of Miranda Rights");
                t.Line("☐ Subject understood rights as explained");
                t.Line("☐ Subject willing to speak without attorney");
                t.Line("☐ Rights waiver signed and documented");
            });
        });
        
        // Recording Information
        col.Item().PaddingTop(20).AlignCenter().Width(450).Column(recCol =>
        {
            recCol.Item().Row(r =>
            {
                r.RelativeItem().Background(Colors.Blue.Lighten4).PaddingVertical(5).PaddingHorizontal(10)
                    .Text("☑ Audio Recording").FontSize(10).Bold();
                
                r.ConstantItem(10);
                
                r.RelativeItem().Background(Colors.Green.Lighten4).PaddingVertical(5).PaddingHorizontal(10)
                    .Text("☐ Video Recording").FontSize(10).Bold();
            });
            
            recCol.Item().PaddingTop(8).Text("Recording File: [filename.mp3/.mp4]").FontSize(9)
                .FontColor(Colors.Grey.Darken2);
        });
        
        // Instructions
        col.Item().PaddingTop(30).PaddingHorizontal(60).Column(instrCol =>
        {
            instrCol.Item().Text("TRANSCRIPT NOTES").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            
            instrCol.Item().PaddingTop(10).Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(10).LineHeight(1.6f));
                t.Line("• Transcript produced from audio/video recording");
                t.Line("• Speaker labels: Q: (Interviewer), A: (Subject)");
                t.Line("• [Brackets] indicate non-verbal actions or clarifications");
                t.Line("• Pauses and hesitations noted where significant");
                t.Line("• Subject reviewed and verified transcript accuracy");
            });
        });
    }

    private void RenderInterviewContent(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Interview start notification
        col.Item().PaddingBottom(15).Background(Colors.Amber.Lighten5).Border(1)
            .BorderColor(Colors.Amber.Lighten3).Padding(10).Text(t =>
        {
            t.DefaultTextStyle(TextStyle.Default.FontSize(10).FontColor(Colors.Amber.Darken3));
            t.Span("Interview commenced at ").Bold();
            t.Span(DateTimeOffset.Now.ToString("HH:mm"));
            t.Span(". Subject present and acknowledged understanding of interview purpose.");
        });
        
        // Transcript notation guide
        col.Item().PaddingBottom(10).Text(t =>
        {
            t.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(Colors.Grey.Darken2).Italic());
            t.Span("Notation: ");
            t.Span("Q: ").Bold().FontColor(Colors.Blue.Darken2);
            t.Span("= Interviewer Question  ");
            t.Span("A: ").Bold().FontColor(Colors.Green.Darken2);
            t.Span("= Subject Answer  ");
            t.Span("[Action] ").FontColor(Colors.Grey.Darken1);
            t.Span("= Non-verbal notation");
        });
        
        // Render markdown content (Q&A transcript)
        PdfCommonComponents.RenderMarkdownContent(col, md);
        
        // Interview conclusion
        col.Item().PaddingTop(15).Background(Colors.Amber.Lighten5).Border(1)
            .BorderColor(Colors.Amber.Lighten3).Padding(10).Text(t =>
        {
            t.DefaultTextStyle(TextStyle.Default.FontSize(10).FontColor(Colors.Amber.Darken3));
            t.Span("Interview concluded at ").Bold();
            t.Span(DateTimeOffset.Now.AddMinutes(45).ToString("HH:mm"));
            t.Span(". Subject provided voluntary statement. No coercion or duress observed.");
        });
        
        // Certification section
        col.Item().PaddingTop(20).BorderTop(2).BorderColor(Colors.Amber.Darken2)
            .PaddingTop(15).Column(certCol =>
        {
            certCol.Item().Text("INTERVIEW CERTIFICATION").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken3);
            
            certCol.Item().PaddingTop(10).Text(t =>
            {
                t.DefaultTextStyle(TextStyle.Default.FontSize(9).LineHeight(1.5f));
                t.Span("I certify that this transcript is a true and accurate representation of the ");
                t.Span("interview conducted. The subject was treated fairly and professionally. ");
                t.Span("All statements were voluntary and recorded accurately.");
            });
            
            // Signature rows
            certCol.Item().PaddingTop(15).Row(sigRow =>
            {
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Subject Signature").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                sigRow.ConstantItem(20);
                
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Interviewer Signature / Badge").FontSize(8)
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
