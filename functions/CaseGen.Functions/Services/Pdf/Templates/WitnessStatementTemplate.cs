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
/// Template for generating Witness Statement documents with notary certification
/// </summary>
public class WitnessStatementTemplate
{
    private readonly ILogger _logger;

    public WitnessStatementTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public byte[] Generate(string title, string markdownContent, string documentType, string? caseId, string? docId, WitnessStatementData? structuredData)
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
                        RenderCoverPage(col, caseId, docId, structuredData);
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
                        if (structuredData is not null)
                        {
                            RenderStructuredWitnessStatementContent(col, structuredData, caseId, docId);
                        }
                        else
                        {
                            RenderWitnessStatementContent(col, markdownContent, caseId, docId);
                        }
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

    private void RenderCoverPage(ColumnDescriptor col, string? caseId, string? docId, WitnessStatementData? data)
    {
        if (data is null)
        {
            RenderLegacyCoverPage(col, caseId, docId);
            return;
        }

        var recordedAt = data.StatementRecordedAt?.ToString("MMM dd, yyyy HH:mm") ?? "Not Logged";
        var timeline = data.Timeline ?? Array.Empty<WitnessTimelineEntry>();
        var orderedTimestamps = timeline.Where(t => t.Timestamp.HasValue)
            .OrderBy(t => t.Timestamp!.Value).ToList();
        var firstEvent = orderedTimestamps.FirstOrDefault()?.Timestamp?.ToString("HH:mm") ?? "--";
        var lastEvent = orderedTimestamps.LastOrDefault()?.Timestamp?.ToString("HH:mm") ?? "--";

        col.Item().Border(2).BorderColor(Colors.Blue.Darken2)
            .Padding(14).Column(c =>
        {
            c.Item().Text("CASE INFORMATION").FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
            c.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(info =>
                {
                    info.Item().Text("Case Number").FontSize(9).FontColor(Colors.Grey.Darken2);
                    info.Item().Text(data.CaseId ?? caseId ?? "N/A").FontSize(11).Bold();
                });

                row.ConstantItem(12);

                row.RelativeItem().Column(info =>
                {
                    info.Item().Text("Document ID").FontSize(9).FontColor(Colors.Grey.Darken2);
                    info.Item().Text(data.DocumentId ?? docId ?? "N/A").FontSize(11).Bold();
                });

                row.ConstantItem(12);

                row.RelativeItem().Column(info =>
                {
                    info.Item().Text("Recorded At").FontSize(9).FontColor(Colors.Grey.Darken2);
                    info.Item().Text(recordedAt).FontSize(11).Bold();
                });
            });
        });

        col.Item().PaddingTop(18).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(12).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text("Witness").FontSize(10).Bold().FontColor(Colors.Grey.Darken3);
                left.Item().PaddingTop(4).Text(data.WitnessName ?? "Name not provided")
                    .FontSize(12).Bold();
                if (!string.IsNullOrWhiteSpace(data.WitnessRole))
                {
                    left.Item().Text(data.WitnessRole).FontSize(10).FontColor(Colors.Grey.Darken2);
                }
                if (!string.IsNullOrWhiteSpace(data.Identification))
                {
                    left.Item().PaddingTop(6).Text(data.Identification)
                        .FontSize(9.5f).LineHeight(1.4f);
                }
            });

            row.ConstantItem(14);

            row.RelativeItem().Column(right =>
            {
                right.Item().Text("Statement Snapshot").FontSize(10).Bold()
                    .FontColor(Colors.Grey.Darken3);
                right.Item().PaddingTop(6).Border(1).BorderColor(Colors.Blue.Lighten2)
                    .Padding(8).Column(stats =>
                {
                    stats.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Timeline Entries").FontSize(9).FontColor(Colors.Grey.Darken2);
                        r.ConstantItem(50).AlignRight().Text(timeline.Count.ToString()).FontSize(12).Bold();
                    });
                    stats.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Text("Span").FontSize(9).FontColor(Colors.Grey.Darken2);
                        r.ConstantItem(80).AlignRight().Text($"{firstEvent} – {lastEvent}").FontSize(10).Bold();
                    });
                    stats.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Text("Reference Count").FontSize(9).FontColor(Colors.Grey.Darken2);
                        r.ConstantItem(50).AlignRight().Text((data.References?.Count ?? 0).ToString()).FontSize(10).Bold();
                    });
                });
            });
        });

        if ((data.References?.Count ?? 0) > 0)
        {
            col.Item().PaddingTop(14).Text("Referenced Evidence").FontSize(10).Bold();
            col.Item().PaddingTop(6).Row(chipRow =>
            {
                chipRow.Spacing(8);
                foreach (var reference in data.References!)
                {
                    chipRow.AutoItem().Background(Colors.Blue.Lighten4).PaddingVertical(4).PaddingHorizontal(10)
                        .Text(reference).FontSize(9).Bold().FontColor(Colors.Blue.Darken2);
                }
            });
        }

        col.Item().PaddingTop(18).Background(Colors.Grey.Lighten4)
            .Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
        {
            c.Item().Text("STATEMENT CERTIFICATION").FontSize(10).Bold()
                .FontColor(Colors.Grey.Darken4);
            c.Item().PaddingTop(6).Text("Witness affirms the accuracy of the statement below and acknowledges cooperative status with investigators.")
                .FontSize(9.5f).LineHeight(1.4f);
        });

        AddRightsAcknowledgements(col);
    }

    private void RenderLegacyCoverPage(ColumnDescriptor col, string? caseId, string? docId)
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
        
        AddRightsAcknowledgements(col);
    }

    private static void AddRightsAcknowledgements(ColumnDescriptor col)
    {
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
        
        RenderOfficerVerification(col);
    }

    private void RenderStructuredWitnessStatementContent(ColumnDescriptor col, WitnessStatementData data, string? caseId, string? docId)
    {
        RenderStructuredSummary(col, data, caseId, docId);
        RenderNarrativeSection(col, data.Narrative);
        RenderTimelineSection(col, data.Timeline ?? Array.Empty<WitnessTimelineEntry>());
        RenderReferenceSection(col, data.References ?? Array.Empty<string>());
        RenderWitnessSignatureSection(col, data);
        RenderOfficerVerification(col);
    }

    private void RenderStructuredSummary(ColumnDescriptor col, WitnessStatementData data, string? caseId, string? docId)
    {
        col.Item().Border(1).BorderColor(Colors.Blue.Lighten2).Padding(12).Column(summary =>
        {
            summary.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Witness").FontSize(9).FontColor(Colors.Grey.Darken2);
                    left.Item().Text(data.WitnessName ?? "Name not provided").FontSize(12).Bold();
                    if (!string.IsNullOrWhiteSpace(data.WitnessRole))
                    {
                        left.Item().Text(data.WitnessRole).FontSize(10).FontColor(Colors.Grey.Darken2);
                    }
                });

                row.ConstantItem(12);

                row.RelativeItem().Column(right =>
                {
                    right.Item().Text("Case Reference").FontSize(9).FontColor(Colors.Grey.Darken2);
                    right.Item().Text(caseId ?? data.CaseId ?? "N/A").FontSize(11).Bold();
                    right.Item().Text(docId ?? data.DocumentId ?? string.Empty).FontSize(9.5f)
                        .FontColor(Colors.Grey.Darken2);
                });
            });

            if (!string.IsNullOrWhiteSpace(data.Identification))
            {
                summary.Item().PaddingTop(8).Background(Colors.Grey.Lighten4).Padding(8)
                    .Text(data.Identification).FontSize(9.5f).LineHeight(1.35f);
            }
        });
    }

    private void RenderNarrativeSection(ColumnDescriptor col, string? narrative)
    {
        if (string.IsNullOrWhiteSpace(narrative))
        {
            return;
        }

        col.Item().PaddingTop(15).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(12).Column(section =>
        {
            section.Item().Text("STATEMENT NARRATIVE").FontSize(10.5f).Bold()
                .FontColor(Colors.Grey.Darken3);
            section.Item().PaddingTop(6).Text(narrative).FontSize(10)
                .LineHeight(1.45f);
        });
    }

    private void RenderTimelineSection(ColumnDescriptor col, IReadOnlyList<WitnessTimelineEntry> timeline)
    {
        if (timeline == null || timeline.Count == 0)
        {
            return;
        }

        col.Item().PaddingTop(15).Text("KEY TIMES").FontSize(10.5f).Bold()
            .FontColor(Colors.Grey.Darken3);
        col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(3);
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Lighten4).Padding(6)
                    .BorderBottom(1).BorderColor(Colors.Blue.Lighten2)
                    .Text("Timestamp").FontSize(9.5f).Bold();
                header.Cell().Background(Colors.Blue.Lighten4).Padding(6)
                    .BorderBottom(1).BorderColor(Colors.Blue.Lighten2)
                    .Text("Description").FontSize(9.5f).Bold();
            });

            foreach (var entry in timeline)
            {
                var timestampText = entry.Timestamp?.ToString("MMM dd HH:mm") ?? "—";
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                    .Padding(6).Text(timestampText).FontSize(9.5f);
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                    .Padding(6).Text(entry.Description).FontSize(9.5f).LineHeight(1.35f);
            }
        });
    }

    private void RenderReferenceSection(ColumnDescriptor col, IReadOnlyList<string> references)
    {
        if (references == null || references.Count == 0)
        {
            return;
        }

        col.Item().PaddingTop(15).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(12).Column(section =>
        {
            section.Item().Text("ATTACHMENTS & REFERENCES").FontSize(10.5f).Bold()
                .FontColor(Colors.Grey.Darken3);

            section.Item().PaddingTop(6).Row(row =>
            {
                row.Spacing(8);
                foreach (var reference in references)
                {
                    row.AutoItem().Background(Colors.Blue.Lighten4).PaddingVertical(4)
                        .PaddingHorizontal(10).Text(reference).FontSize(9).Bold()
                        .FontColor(Colors.Blue.Darken2);
                }
            });
        });
    }

    private void RenderWitnessSignatureSection(ColumnDescriptor col, WitnessStatementData data)
    {
        var signature = data.Signature ?? new WitnessSignatureInfo();
        var signatureName = signature.Name ?? data.WitnessName ?? "Witness";
        var signatureRole = signature.Role ?? data.WitnessRole ?? "";
        var signedAt = signature.SignedAt?.ToString("MMM dd, yyyy HH:mm") ?? "Pending";

        col.Item().PaddingTop(25).Border(2).BorderColor(Colors.Blue.Darken2)
            .Padding(15).Column(c =>
        {
            c.Item().PaddingBottom(15).Text("WITNESS SIGNATURE").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken4);

            c.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(sigCol =>
                {
                    sigCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .Height(30).AlignBottom().Text(signatureName).FontSize(10).Bold();
                    var roleLabel = string.IsNullOrWhiteSpace(signatureRole) ? "Witness Signature" : signatureRole;
                    sigCol.Item().PaddingTop(2).Text(roleLabel).FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(20);

                row.RelativeItem().Column(dateCol =>
                {
                    dateCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .Height(30).AlignBottom().Text(signedAt).FontSize(10);
                    dateCol.Item().PaddingTop(2).Text("Date / Time").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }

    private void RenderOfficerVerification(ColumnDescriptor col)
    {
        col.Item().PaddingTop(20).Border(1).BorderColor(Colors.Grey.Darken2)
            .Padding(12).Column(c =>
        {
            c.Item().PaddingBottom(15).Text("OFFICER VERIFICATION").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken4);
            
            c.Item().PaddingBottom(10).Text("I certify that this statement was given voluntarily and that the witness was advised of their rights and obligations.")
                .FontSize(9).Italic().LineHeight(1.4f);
            
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
