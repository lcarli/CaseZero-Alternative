using CaseGen.Functions.Services.Pdf;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf.Templates;

/// <summary>
/// Template for generating Police Report documents (single-page format)
/// </summary>
public class PoliceReportTemplate
{
    private readonly ILogger _logger;

    public PoliceReportTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public byte[] Generate(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • LAW ENFORCEMENT SENSITIVE";
            var docTypeLabel = PdfCommonComponents.GetDocumentTypeLabel(documentType);
            
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
                        headerCol.Item().Element(h => PdfCommonComponents.BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => PdfCommonComponents.BuildClassificationBand(h, classification));
                    });
                    
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderPoliceReport(col, markdownContent, caseId, docId);
                    });
                    
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                        t.Span("   •   Page ");
                        t.CurrentPageNumber().FontSize(9);
                        t.Span(" of ");
                        t.TotalPages().FontSize(9);
                    });
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating police report: {Message}", ex.Message);
            throw;
        }
    }

    private void RenderPoliceReport(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Header box with unit/agent/report info
        col.Item().PaddingBottom(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(r =>
        {
            r.RelativeItem().Column(c =>
            {
                c.Item().Text("RESPONDING UNIT").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                c.Item().Text("Unit: __________________").FontSize(10).FontColor(Colors.Grey.Darken2);
                c.Item().Text("Badge: _________________").FontSize(10).FontColor(Colors.Grey.Darken2);
            });
            r.RelativeItem().Column(c =>
            {
                c.Item().Text("REPORT DETAILS").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                c.Item().Text($"Report No.: {(docId ?? "________")}").FontSize(10).FontColor(Colors.Grey.Darken2);
                c.Item().Text($"Date: {DateTimeOffset.Now:MM/dd/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Darken2);
            });
            r.RelativeItem().AlignRight().Column(c =>
            {
                c.Item().Text("CASE REFERENCE").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                c.Item().Text($"Case: {(caseId ?? "________")}").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                c.Item().Text("Classification: CONFIDENTIAL").FontSize(9).FontColor(Colors.Red.Darken2);
            });
        });
        
        // Synopsis box (highlighted)
        col.Item().PaddingTop(10).PaddingBottom(10).Border(2).BorderColor(Colors.Blue.Lighten3)
            .Background(Colors.Blue.Lighten5).Padding(10).Column(c =>
        {
            c.Item().Text("CASE SYNOPSIS").FontSize(11).Bold();
            c.Item().PaddingTop(5).Text(ExtractSynopsis(md)).FontSize(10).LineHeight(1.4f);
        });
        
        // Classification details table with checkboxes
        col.Item().PaddingTop(10).Column(c =>
        {
            c.Item().Text("CLASSIFICATION DETAILS").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(5).Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                });
                
                // Case Type
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Case Type").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Cold Case Investigation").FontSize(10);
                
                // Priority Level with checkboxes
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Priority Level").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text(t =>
                    {
                        t.Span("☐ Low   ☐ Medium   ☑ High   ☐ Critical").FontSize(10);
                    });
                
                // Status with badge and checkboxes
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Status").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Row(statusRow =>
                    {
                        // Status badge
                        statusRow.AutoItem().Background(Colors.Red.Lighten3).PaddingVertical(2).PaddingHorizontal(6)
                            .Text("ACTIVE INVESTIGATION").FontSize(8).Bold().FontColor(Colors.Red.Darken3);
                        
                        // Checkboxes
                        statusRow.RelativeItem().PaddingLeft(10).Text(t =>
                        {
                            t.Span("   ☑ Active   ☐ Under Review   ☐ Closed").FontSize(9);
                        });
                    });
                
                // Lead Unit
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Lead Unit").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Major Crimes Division").FontSize(10);
            });
        });
        
        // Incident details table
        RenderFormTable(col, "INCIDENT DETAILS", new Dictionary<string, string>
        {
            { "Incident Date", DateTimeOffset.Now.ToString("MMMM dd, yyyy") },
            { "Incident Time", DateTimeOffset.Now.ToString("hh:mm tt zzz") },
            { "Location", "[Location from case data]" },
            { "Reporting Party", "[Officer Name] - Badge #[####]" }
        });
        
        // Victim information (if applicable)
        RenderFormTable(col, "SUBJECT INFORMATION", new Dictionary<string, string>
        {
            { "Name", "[REDACTED]" },
            { "Age", "[##]" },
            { "Sex/Race", "[M/F] / [Race]" },
            { "Last Known Address", "[REDACTED]" }
        });
        
        // Initial response section
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text("INITIAL RESPONSE").FontSize(11).Bold();
            c.Item().PaddingTop(5).Column(responseCol =>
            {
                responseCol.Item().Text($"First Responder: Officer [NAME], Badge #[####]").FontSize(10);
                responseCol.Item().Text($"Arrived: {DateTimeOffset.Now:hh:mm tt zzz}").FontSize(10);
                responseCol.Item().Text($"Scene Secured: {DateTimeOffset.Now.AddMinutes(15):hh:mm tt zzz}").FontSize(10);
            });
        });
        
        // Render remaining markdown content
        col.Item().PaddingTop(15).Column(contentCol =>
        {
            PdfCommonComponents.RenderMarkdownContent(contentCol, md);
        });
        
        // Signature section at the end
        col.Item().PaddingTop(20).Column(sigCol =>
        {
            sigCol.Item().PaddingBottom(5).Text("REPORTING OFFICER CERTIFICATION")
                .FontSize(10).Bold().FontColor(Colors.Grey.Darken3);
            
            sigCol.Item().PaddingTop(10).Row(sigRow =>
            {
                // Officer signature
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .Height(30); // Space for signature
                    c.Item().PaddingTop(2).Text("Officer Signature").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                
                sigRow.ConstantItem(20);
                
                // Badge number
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .AlignMiddle().Text("Badge #: __________").FontSize(9);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                
                sigRow.ConstantItem(20);
                
                // Date
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .AlignMiddle().Text($"{DateTimeOffset.Now:MM/dd/yyyy}").FontSize(9);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
            
            // Supervisor signature
            sigCol.Item().PaddingTop(15).Row(supRow =>
            {
                supRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .Height(30);
                    c.Item().PaddingTop(2).Text("Supervisor Signature (if required)").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                
                supRow.ConstantItem(20);
                
                supRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .AlignMiddle().Text("Badge #: __________").FontSize(9);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                
                supRow.ConstantItem(20);
                
                supRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .AlignMiddle().Text("__________").FontSize(9);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }
    
    private void RenderFormTable(ColumnDescriptor col, string heading, Dictionary<string, string> fields)
    {
        col.Item().PaddingTop(10).Column(c =>
        {
            c.Item().Text(heading).FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(5).Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                });
                
                foreach (var field in fields)
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                        .Padding(5).Text(field.Key).FontSize(9.5f).Bold();
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                        .Padding(5).Text(field.Value).FontSize(10);
                }
            });
        });
    }
    
    private string ExtractSynopsis(string markdown)
    {
        // Extract first substantial paragraph as synopsis
        if (string.IsNullOrWhiteSpace(markdown))
            return "Synopsis not available.";
        
        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Find first paragraph that's not a header and has meaningful content
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("#") && 
                !trimmed.StartsWith("|") && 
                !trimmed.StartsWith("-") && 
                trimmed.Length > 50)
            {
                // Limit to reasonable synopsis length
                return trimmed.Length > 300 
                    ? trimmed.Substring(0, 297) + "..." 
                    : trimmed;
            }
        }
        
        return "Case synopsis will be provided upon further investigation.";
    }
}
