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
/// Template for generating Forensic Analysis Report documents with lab certification
/// </summary>
public class ForensicsReportTemplate
{
    private readonly ILogger _logger;

    public ForensicsReportTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public byte[] Generate(string title, string markdownContent, string documentType, string? caseId, string? docId, ForensicsReportData? structuredData)
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
                        RenderCoverPage(col, caseId, docId, structuredData);
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
                        if (structuredData is not null)
                        {
                            RenderStructuredForensicsContent(col, structuredData);
                        }
                        else
                        {
                            RenderForensicsReportContent(col, markdownContent, caseId, docId);
                        }
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

    private void RenderCoverPage(ColumnDescriptor col, string? caseId, string? docId, ForensicsReportData? data)
    {
        var labName = string.IsNullOrWhiteSpace(data?.LabName) ? "Metro Crime Laboratory" : data!.LabName!;
        var examDate = data?.ExaminationDate?.ToString("MMMM dd, yyyy") ?? DateTimeOffset.Now.ToString("MMMM dd, yyyy");
        var examTime = string.IsNullOrWhiteSpace(data?.ExaminationTime) ? "--:--" : data!.ExaminationTime!;
        var examiner = string.IsNullOrWhiteSpace(data?.ExaminerName) ? "[NAME], Forensic Scientist" : data!.ExaminerName!;

        col.Item().AlignCenter().Width(420).Border(2).BorderColor(Colors.Indigo.Darken2)
            .Background(Colors.Indigo.Lighten5).Padding(20).Column(c =>
        {
            c.Item().AlignCenter().Text("LAB CASE INFORMATION").FontSize(14).Bold()
                .FontColor(Colors.Indigo.Darken3);
            
            c.Item().PaddingTop(15).Column(infoCol =>
            {
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Case Number:").Bold().FontSize(11);
                    r.RelativeItem().Text(data?.CaseId ?? caseId ?? "________").FontSize(11)
                        .FontColor(Colors.Indigo.Darken3);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Lab Case No.:").Bold().FontSize(11);
                    r.RelativeItem().Text(data?.DocumentId ?? docId ?? "________").FontSize(11)
                        .FontColor(Colors.Indigo.Darken3);
                });

                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Laboratory:").Bold().FontSize(11);
                    r.RelativeItem().Text(labName).FontSize(11);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Date Analyzed:").Bold().FontSize(11);
                    r.RelativeItem().Text(examDate).FontSize(11);
                });
                
                infoCol.Item().PaddingBottom(8).Row(r =>
                {
                    r.RelativeItem().Text("Time Logged:").Bold().FontSize(11);
                    r.RelativeItem().Text(examTime).FontSize(11);
                });
                
                infoCol.Item().Row(r =>
                {
                    r.RelativeItem().Text("Analyst:").Bold().FontSize(11);
                    r.RelativeItem().Text(examiner).FontSize(11);
                });
            });
        });
        
        col.Item().PaddingTop(30).AlignCenter().Row(badgeRow =>
        {
            badgeRow.Spacing(10);
            badgeRow.AutoItem().Background(Colors.Indigo.Lighten4).PaddingVertical(5).PaddingHorizontal(12)
                .Text("LOCK & KEYPAD").FontSize(9).Bold().FontColor(Colors.Indigo.Darken3);
            badgeRow.AutoItem().Background(Colors.Blue.Lighten4).PaddingVertical(5).PaddingHorizontal(12)
                .Text("TRACE RESIDUE").FontSize(9).Bold().FontColor(Colors.Blue.Darken3);
            if (!string.IsNullOrWhiteSpace(data?.PhotoReferenceId))
            {
                badgeRow.AutoItem().Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(12)
                    .Text(data.PhotoReferenceId!).FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
            }
        });
        
        col.Item().PaddingTop(40).AlignCenter().Width(400)
            .Background(Colors.Red.Lighten4).Padding(10)
            .Text("CONFIDENTIAL • LABORATORY USE ONLY")
            .FontSize(10).Bold().FontColor(Colors.Red.Darken3);
        
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

            if (!string.IsNullOrWhiteSpace(data?.ObjectiveSummary))
            {
                instrCol.Item().PaddingTop(18).Border(1).BorderColor(Colors.Indigo.Lighten3)
                    .Background(Colors.White).Padding(12).Column(summaryCol =>
                {
                    summaryCol.Item().Text("OBJECTIVE SUMMARY").FontSize(11).Bold()
                        .FontColor(Colors.Indigo.Darken2);
                    summaryCol.Item().PaddingTop(4).Text(data.ObjectiveSummary!)
                        .FontSize(10).LineHeight(1.45f);
                });
            }
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
        
        RenderCertification(col);
    }

    private void RenderStructuredForensicsContent(ColumnDescriptor col, ForensicsReportData data)
    {
        RenderObjectiveBlock(col, data);
        RenderMethodologyCards(col, data);
        RenderNarrativePanel(col, "OBSERVATIONS", data.ObservationsSummary, Colors.Indigo.Darken3);
        RenderPhotographicReference(col, data);
        RenderChainOfCustodyTable(col, data.ChainOfCustody ?? Array.Empty<ForensicsChainEntry>());
        RenderNarrativePanel(col, "FINDINGS", data.Findings, Colors.Green.Darken2);
        RenderNarrativePanel(col, "LIMITATIONS", data.Limitations, Colors.Orange.Darken2);
        RenderCertification(col);
    }

    private void RenderObjectiveBlock(ColumnDescriptor col, ForensicsReportData data)
    {
        col.Item().Border(1).BorderColor(Colors.Indigo.Lighten3)
            .Background(Colors.Indigo.Lighten5).Padding(12).Column(block =>
        {
            block.Item().Text("OBJECTIVE & CONTEXT").FontSize(11).Bold()
                .FontColor(Colors.Indigo.Darken3);

            block.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Laboratory").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(data.LabName ?? "Metro Crime Laboratory").FontSize(10.5f).Bold();
                });

                row.ConstantItem(10);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Examiner").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(data.ExaminerName ?? "Assigned Examiner").FontSize(10.5f).Bold();
                });

                row.ConstantItem(10);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Date / Time").FontSize(9).FontColor(Colors.Grey.Darken2);
                    var dateText = data.ExaminationDate?.ToString("MMM dd, yyyy") ?? "--";
                    var timeText = string.IsNullOrWhiteSpace(data.ExaminationTime) ? "--" : data.ExaminationTime;
                    c.Item().Text($"{dateText} • {timeText}").FontSize(10.5f).Bold();
                });
            });

            if (!string.IsNullOrWhiteSpace(data.ObjectiveSummary))
            {
                block.Item().PaddingTop(10).Text(data.ObjectiveSummary)
                    .FontSize(10).LineHeight(1.45f);
            }
        });
    }

    private void RenderMethodologyCards(ColumnDescriptor col, ForensicsReportData data)
    {
        var cards = new List<(string Title, string? Content, string Accent)>
        {
            ("PROCEDURES", data.Procedures, Colors.Blue.Darken2),
            ("RESULTS", data.Results, Colors.Teal.Darken2),
            ("INTERPRETATION", data.Interpretation, Colors.Orange.Darken2)
        };

        var anyContent = cards.Any(c => !string.IsNullOrWhiteSpace(c.Content));
        if (!anyContent)
        {
            return;
        }

        col.Item().PaddingTop(12).Row(row =>
        {
            row.Spacing(10);
            foreach (var card in cards)
            {
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2)
                    .Background(Colors.White).Padding(10).Column(cardCol =>
                {
                    cardCol.Item().Text(card.Title).FontSize(10.5f).Bold()
                        .FontColor(card.Accent);
                    if (string.IsNullOrWhiteSpace(card.Content))
                    {
                        cardCol.Item().PaddingTop(6).Text("No data logged").FontSize(9.5f)
                            .FontColor(Colors.Grey.Lighten1);
                    }
                    else
                    {
                        cardCol.Item().PaddingTop(6).Text(card.Content!).FontSize(9.5f)
                            .LineHeight(1.35f);
                    }
                });
            }
        });
    }

    private void RenderNarrativePanel(ColumnDescriptor col, string heading, string? content, string accentColor)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        col.Item().PaddingTop(12).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(12).Column(c =>
        {
            c.Item().Text(heading).FontSize(10.5f).Bold().FontColor(accentColor);
            c.Item().PaddingTop(6).Text(content).FontSize(9.5f).LineHeight(1.4f);
        });
    }

    private void RenderPhotographicReference(ColumnDescriptor col, ForensicsReportData data)
    {
        if (string.IsNullOrWhiteSpace(data.PhotoReferenceId) && string.IsNullOrWhiteSpace(data.PhotoDescription))
        {
            return;
        }

        col.Item().PaddingTop(12).Background(Colors.Grey.Lighten4)
            .Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
        {
            row.ConstantItem(110).Column(chipCol =>
            {
                chipCol.Item().Background(Colors.Indigo.Darken1).Padding(6)
                    .Text("PHOTO REF").FontSize(9).Bold().FontColor(Colors.White);
                chipCol.Item().PaddingTop(4).Text(data.PhotoReferenceId ?? "Not Provided")
                    .FontSize(9.5f).Bold();
            });

            row.RelativeItem().Column(textCol =>
            {
                textCol.Item().Text("Photographic Evidence Context").FontSize(10).Bold();
                if (!string.IsNullOrWhiteSpace(data.PhotoDescription))
                {
                    textCol.Item().PaddingTop(4).Text(data.PhotoDescription)
                        .FontSize(9.5f).LineHeight(1.35f);
                }
            });
        });
    }

    private void RenderChainOfCustodyTable(ColumnDescriptor col, IReadOnlyList<ForensicsChainEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        col.Item().PaddingTop(14).Text("CHAIN OF CUSTODY").FontSize(11).Bold();
        col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(3);
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Grey.Lighten4).Padding(6)
                    .BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .Text("Timestamp").FontSize(9.5f).Bold();
                header.Cell().Background(Colors.Grey.Lighten4).Padding(6)
                    .BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .Text("Event Details").FontSize(9.5f).Bold();
            });

            foreach (var entry in entries)
            {
                var timestampText = entry.Timestamp?.ToString("MMM dd, yyyy HH:mm") ?? "—";
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                    .Padding(6).Text(timestampText).FontSize(9.5f);
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                    .Padding(6).Text(entry.Description).FontSize(9.5f).LineHeight(1.35f);
            }
        });
    }

    private void RenderCertification(ColumnDescriptor col)
    {
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
