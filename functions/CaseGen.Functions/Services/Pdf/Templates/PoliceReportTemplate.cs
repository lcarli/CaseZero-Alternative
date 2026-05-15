using System;
using System.Collections.Generic;
using System.Linq;
using CaseGen.Functions.Services.Pdf;
using CaseGen.Functions.Services.Pdf.Models;
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

    public byte[] Generate(string title, string markdownContent, string documentType, string? caseId, string? docId, PoliceReportData? structuredData)
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
                        RenderPoliceReport(col, markdownContent, caseId, docId, structuredData, classification);
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

    private void RenderPoliceReport(ColumnDescriptor col, string md, string? caseId, string? docId, PoliceReportData? data, string classification)
    {
        if (data == null)
        {
            RenderLegacyLayout(col, md, caseId, docId);
            return;
        }

        RenderInfoBand(col, data, caseId, docId, classification);
        RenderSynopsis(col, data, md);
        RenderTimeline(col, data);
        RenderPeopleAndEvidence(col, data);
        RenderNarratives(col, data);
        RenderNextSteps(col, data);
        RenderSignatureBlock(col, data);
    }

    private void RenderInfoBand(ColumnDescriptor col, PoliceReportData data, string? caseId, string? docId, string classification)
    {
        col.Item().PaddingBottom(12).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Row(row =>
        {
            row.RelativeItem().Column(info =>
            {
                info.Item().Text("RESPONDING UNIT").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                info.Item().PaddingTop(2).Text(data.Unit ?? "Not Provided").FontSize(11).Bold();
                info.Item().PaddingTop(2).Text(data.OfficerName ?? "Officer TBD").FontSize(10);
                if (!string.IsNullOrEmpty(data.OfficerBadge))
                {
                    info.Item().Text(data.OfficerBadge).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                }
            });

            row.RelativeItem().Column(details =>
            {
                details.Item().Text("REPORT DETAILS").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                details.Item().PaddingTop(2).Text($"Report #: {data.ReportNumber ?? docId ?? "N/A"}").FontSize(10);
                var timestamp = data.ReportDateTime ?? DateTimeOffset.Now;
                details.Item().Text($"Filed: {timestamp:MMM dd, yyyy • HH:mm zzz}").FontSize(10);
                details.Item().Text($"Case ID: {caseId ?? data.CaseId ?? "N/A"}").FontSize(10);
            });

            row.RelativeItem().AlignRight().Column(right =>
            {
                right.Item().AlignRight().Text("STATUS").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                right.Item().AlignRight().Border(1).BorderColor(Colors.Red.Darken2)
                    .Background(Colors.Red.Lighten4).PaddingHorizontal(10).PaddingVertical(4)
                    .Text("ACTIVE INVESTIGATION").Bold().FontSize(9).FontColor(Colors.Red.Darken3);
                right.Item().PaddingTop(6).AlignRight().Text(classification)
                    .FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private void RenderSynopsis(ColumnDescriptor col, PoliceReportData data, string markdownContent)
    {
        var synopsis = data.Summary ?? ExtractSynopsis(markdownContent);
        col.Item().Padding(12).Background(Colors.Blue.Lighten5).Border(0.5f)
            .BorderColor(Colors.Blue.Lighten3).Column(block =>
        {
            block.Item().Text("CASE SYNOPSIS").FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
            block.Item().PaddingTop(5).Text(synopsis).FontSize(10.5f).LineHeight(1.4f);
        });
    }

    private void RenderTimeline(ColumnDescriptor col, PoliceReportData data)
    {
        if (data.Timeline.Count == 0)
        {
            return;
        }

        col.Item().PaddingTop(12).Column(timelineCol =>
        {
            timelineCol.Item().Text("INCIDENT TIMELINE").FontSize(11).Bold();
            timelineCol.Item().PaddingTop(5).Border(1).BorderColor(Colors.Grey.Lighten2)
                .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(3);
                });

                foreach (var entry in data.Timeline.OrderBy(e => e.Timestamp ?? DateTimeOffset.MinValue))
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .Background(Colors.Grey.Lighten5).Padding(6)
                        .Text(entry.Timestamp?.ToString("HH:mm zzz") ?? "--:--").FontSize(9).SemiBold();

                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .Padding(6).Text(entry.Summary).FontSize(10).LineHeight(1.35f);
                }
            });
        });
    }

    private void RenderPeopleAndEvidence(ColumnDescriptor col, PoliceReportData data)
    {
        col.Item().PaddingTop(12).Row(row =>
        {
            row.RelativeItem().PaddingRight(6).Element(e =>
            {
                e.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5)
                    .Padding(10).Column(c =>
                {
                    c.Item().Text("PERSONS INVOLVED").FontSize(10.5f).Bold();
                    if (data.Persons.Count == 0)
                    {
                        c.Item().PaddingTop(4).Text("No individuals recorded in this report.")
                            .FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    }
                    else
                    {
                        foreach (var person in data.Persons)
                        {
                            c.Item().PaddingTop(4).Row(r =>
                            {
                                r.AutoItem().Text("• ").FontSize(12);
                                r.RelativeItem().Text(person.Description).FontSize(9.5f);
                            });
                        }
                    }
                });
            });

            row.RelativeItem().PaddingLeft(6).Element(e =>
            {
                e.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.White)
                    .Padding(10).Column(c =>
                {
                    c.Item().Text("EVIDENCE REFERENCE").FontSize(10.5f).Bold();
                    if (data.EvidenceIds.Count == 0)
                    {
                        c.Item().PaddingTop(4).Text("Evidence IDs pending upload.")
                            .FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    }
                    else
                    {
                        foreach (var evidence in data.EvidenceIds)
                        {
                            c.Item().PaddingTop(4).Row(r =>
                            {
                                r.AutoItem().Border(1).BorderColor(Colors.Blue.Darken2)
                                    .PaddingHorizontal(6).PaddingVertical(2)
                                    .Text(evidence).FontSize(9).FontColor(Colors.Blue.Darken2);
                            });
                        }
                    }
                });
            });
        });
    }

    private void RenderNarratives(ColumnDescriptor col, PoliceReportData data)
    {
        col.Item().PaddingTop(12).Row(row =>
        {
            row.RelativeItem().PaddingRight(6).Element(e =>
            {
                e.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text("SCENE DESCRIPTION").FontSize(10.5f).Bold();
                    c.Item().PaddingTop(4).Text(data.SceneDescription ?? "Scene narrative pending.")
                        .FontSize(9.5f).LineHeight(1.4f);
                });
            });

            row.RelativeItem().PaddingLeft(6).Element(e =>
            {
                e.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text("PRELIMINARY ASSESSMENT").FontSize(10.5f).Bold();
                    c.Item().PaddingTop(4).Text(data.Assessment ?? "Assessment pending review.")
                        .FontSize(9.5f).LineHeight(1.4f);
                });
            });
        });
    }

    private void RenderNextSteps(ColumnDescriptor col, PoliceReportData data)
    {
        if (data.NextSteps.Count == 0)
        {
            return;
        }

        col.Item().PaddingTop(12).Element(e =>
        {
            e.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text("NEXT ACTIONS").FontSize(10.5f).Bold();
                foreach (var step in data.NextSteps)
                {
                    c.Item().PaddingTop(4).Row(r =>
                    {
                        r.AutoItem().Text("☐").FontSize(12).FontColor(Colors.Blue.Darken2);
                        r.RelativeItem().PaddingLeft(6).Text(step).FontSize(9.5f);
                    });
                }
            });
        });
    }

    private void RenderSignatureBlock(ColumnDescriptor col, PoliceReportData data)
    {
        col.Item().PaddingTop(18).Column(sigCol =>
        {
            sigCol.Item().PaddingBottom(5).Text("REPORTING OFFICER CERTIFICATION")
                .FontSize(10).Bold().FontColor(Colors.Grey.Darken3);

            sigCol.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(28);
                    c.Item().PaddingTop(2).Text(data.OfficerName ?? "Officer Signature").FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(15);

                row.RelativeItem().Column(c =>
                {
                    var badgeText = string.IsNullOrWhiteSpace(data.OfficerBadge) ? "Badge #: __________" : data.OfficerBadge;
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(28)
                        .AlignMiddle().Text(badgeText).FontSize(9);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(15);

                row.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(28)
                        .AlignMiddle().Text(DateTimeOffset.Now.ToString("MM/dd/yyyy")).FontSize(9);
                    c.Item().PaddingTop(2).Text("Date Filed").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }

    private void RenderLegacyLayout(ColumnDescriptor col, string md, string? caseId, string? docId)
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

                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Case Type").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Cold Case Investigation").FontSize(10);

                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Priority Level").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text(t =>
                    {
                        t.Span("☐ Low   ☐ Medium   ☑ High   ☐ Critical").FontSize(10);
                    });

                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Status").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Row(statusRow =>
                    {
                        statusRow.AutoItem().Background(Colors.Red.Lighten3).PaddingVertical(2).PaddingHorizontal(6)
                            .Text("ACTIVE INVESTIGATION").FontSize(8).Bold().FontColor(Colors.Red.Darken3);

                        statusRow.RelativeItem().PaddingLeft(10).Text(t =>
                        {
                            t.Span("   ☑ Active   ☐ Under Review   ☐ Closed").FontSize(9);
                        });
                    });

                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Lead Unit").FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text("Major Crimes Division").FontSize(10);
            });
        });

        RenderFormTable(col, "INCIDENT DETAILS", new Dictionary<string, string>
        {
            { "Incident Date", DateTimeOffset.Now.ToString("MMMM dd, yyyy") },
            { "Incident Time", DateTimeOffset.Now.ToString("hh:mm tt zzz") },
            { "Location", "[Location from case data]" },
            { "Reporting Party", "[Officer Name] - Badge #[####]" }
        });

        RenderFormTable(col, "SUBJECT INFORMATION", new Dictionary<string, string>
        {
            { "Name", "[REDACTED]" },
            { "Age", "[##]" },
            { "Sex/Race", "[M/F] / [Race]" },
            { "Last Known Address", "[REDACTED]" }
        });

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

        col.Item().PaddingTop(15).Column(contentCol =>
        {
            PdfCommonComponents.RenderMarkdownContent(contentCol, md);
        });

        RenderSignatureBlock(col, new PoliceReportData { OfficerName = "Officer Signature" });
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
