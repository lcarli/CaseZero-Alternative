using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services;

public class PdfRenderingService : IPdfRenderingService
{
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PdfRenderingService> _logger;

    public PdfRenderingService(
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        IConfiguration configuration,
        ILogger<PdfRenderingService> logger)
    {
        _storageService = storageService;
        _caseLogging = caseLogging;
        _configuration = configuration;
        _logger = logger;

        // Configure QuestPDF for realistic document generation
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> RenderDocumentFromJsonAsync(string docId, string documentJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rendering document [{DocId}] from JSON to MD and PDF", docId);

        try
        {
            using var doc = JsonDocument.Parse(documentJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("docId", out var docIdProp) ||
                !root.TryGetProperty("title", out var titleProp) ||
                !root.TryGetProperty("sections", out var sectionsProp))
            {
                throw new InvalidOperationException($"Invalid document JSON structure for {docId}");
            }

            var title = titleProp.GetString() ?? "Untitled Document";
            var sections = sectionsProp.EnumerateArray();

            // Generate Markdown content
            var markdownBuilder = new StringBuilder();
            markdownBuilder.AppendLine($"# {title}");
            markdownBuilder.AppendLine();

            foreach (var section in sections)
            {
                if (section.TryGetProperty("title", out var sectionTitle) &&
                    section.TryGetProperty("content", out var sectionContent))
                {
                    markdownBuilder.AppendLine($"## {sectionTitle.GetString()}");
                    markdownBuilder.AppendLine();
                    markdownBuilder.AppendLine(sectionContent.GetString());
                    markdownBuilder.AppendLine();
                }
            }

            var markdownContent = markdownBuilder.ToString();

            // Determine document type from title
            var documentType = DetermineDocumentType(title);

            // Generate realistic PDF using QuestPDF
            var pdfBytes = GenerateRealisticPdf(title, markdownContent, documentType, caseId, docId);

            // Save files to bundles container
            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var mdPath = $"{caseId}/documents/{docId}.md";
            var pdfPath = $"{caseId}/documents/{docId}.pdf";

            await _storageService.SaveFileAsync(bundlesContainer, mdPath, markdownContent, cancellationToken);
            await _storageService.SaveFileAsync(bundlesContainer, pdfPath, pdfBytes, cancellationToken);

            // Log the rendering step
            await _caseLogging.LogStepResponseAsync(caseId, $"render/{docId}",
                JsonSerializer.Serialize(new
                {
                    docId,
                    markdownPath = mdPath,
                    pdfPath = pdfPath,
                    wordCount = markdownContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
                }, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);

            _logger.LogInformation("Successfully generated and saved document [{DocId}] as MD and PDF", docId);

            // Return a summary of the rendering operation  
            return JsonSerializer.Serialize(new
            {
                docId,
                status = "rendered",
                files = new { markdown = mdPath, pdf = pdfPath }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render document [{DocId}] from JSON", docId);
            throw;
        }
    }

    public Task<byte[]> GenerateTestPdfAsync(string title, string markdownContent, string documentType = "general", CancellationToken cancellationToken = default)
    {
        var actualDocumentType = DetermineDocumentType(title);
        if (!string.IsNullOrEmpty(documentType) && documentType != "general")
        {
            actualDocumentType = documentType;
        }

        return Task.FromResult(GenerateRealisticPdf(title, markdownContent, actualDocumentType));
    }

    private byte[] GenerateRealisticPdf(string title, string markdownContent, string documentType = "general", string? caseId = null, string? docId = null)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            var docTypeLabel = GetDocumentTypeLabel(documentType);
            var (bandBg, bandText) = GetThemeColors(documentType);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));

                    // Soft watermark
                    page.Background().Element(e => AddWatermark(e, classification));

                    // Institutional letterhead
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => BuildClassificationBand(h, classification));
                    });

                    // Content
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderByType(col, documentType, markdownContent, caseId, docId);
                    });

                    // Footer with pagination and classification
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                        t.Span("   •   ");
                        t.CurrentPageNumber().FontSize(9);
                        t.Span(" / ");
                        t.TotalPages().FontSize(9);
                    });
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateRealisticPdf");
            throw;
        }
    }

    private static string GetDocumentTypeLabel(string documentType)
    {
        return documentType.ToLower() switch
        {
            "cover_page" => "CASE FILE COVER",
            "police_report" => "INCIDENT REPORT",
            "forensics_report" => "FORENSIC REPORT",
            "interview" => "INTERVIEW TRANSCRIPT",
            "evidence_log" => "EVIDENCE CATALOG & CHAIN OF CUSTODY",
            "memo" or "memo_admin" => "INVESTIGATIVE MEMORANDUM",
            "witness_statement" => "WITNESS STATEMENT",
            _ => "INVESTIGATIVE DOCUMENT"
        };
    }

    private void AddWatermark(QuestPDF.Infrastructure.IContainer c, string text)
    {
        c.AlignCenter()
         .AlignMiddle()
         .PaddingHorizontal(50)
         .Rotate(315)
         .Text(text)
            .FontSize(48)
            .Bold()
            .FontColor(QuestPDF.Helpers.Colors.Grey.Lighten3);
    }

    private void BuildLetterhead(QuestPDF.Infrastructure.IContainer c, string docTypeLabel, string title, string? caseId, string? docId)
    {
        c.Column(mainCol =>
        {
            mainCol.Item().PaddingBottom(6).Row(row =>
        {
            // Left side - Department name only (no coat of arms square)
            row.RelativeItem(1).Column(col =>
            {
                col.Item().Text("MUNICIPAL POLICE DEPARTMENT")
                          .Bold().FontSize(11.5f);
            });

            row.RelativeItem(1).AlignRight().Column(col =>
            {
                col.Item().Text(docTypeLabel).Bold().FontSize(13);
                if (!string.IsNullOrWhiteSpace(docId))
                    col.Item().Text($"DocId: {docId}").FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(caseId))
                    col.Item().Text($"CaseId: {caseId}").FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                col.Item().Text($"Issued on: {DateTimeOffset.Now:yyyy-MM-dd HH:mm (zzz)}")
                          .FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
            });
        });

            // Title
            mainCol.Item().PaddingTop(2).Text(title).FontSize(14).Bold();
        });
    }

    private void BuildClassificationBand(IContainer c, string classification)
    {
        c.PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Row(r =>
        {
            r.RelativeItem().Text(classification).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
        });
    }

    private void BuildClassificationBand(IContainer c, string classification, string bandBg, string textColor)
    {
        c.PaddingTop(4).Background(bandBg).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Row(r =>
        {
            r.RelativeItem().Text(classification).FontSize(9.5f).FontColor(textColor);
        });
    }

    private (string BandBg, string BandText) GetThemeColors(string documentType)
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

    private string GetDocumentHeader(string documentType, string title)
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

    private string FormatMarkdownContent(string markdown)
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

    private void RenderMarkdownContent(QuestPDF.Fluent.ColumnDescriptor column, string markdownContent)
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

            // Markdown table
            if (IsTableLine(line) && i + 1 < lines.Length && IsTableSeparatorLine(lines[i + 1]))
            {
                var tableLines = new List<string> { line, lines[i + 1] };
                i += 2;
                while (i < lines.Length && IsTableLine(lines[i])) { tableLines.Add(lines[i]); i++; }
                RenderTable(column, tableLines);
                continue;
            }

            // Blockquote
            if (line.StartsWith("> "))
            {
                var quote = line.Substring(2);
                column.Item().Background(QuestPDF.Helpers.Colors.Grey.Lighten4)
                             .BorderLeft(3).BorderColor(QuestPDF.Helpers.Colors.Grey.Medium)
                             .Padding(6)
                             .Text(quote).FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                i++;
                continue;
            }

            // Headers
            if (line.StartsWith("## "))
            {
                var h = line.Substring(3);
                column.Item().PaddingTop(8).Text(h).FontSize(12).Bold();
                i++;
                continue;
            }
            if (line.StartsWith("# "))
            {
                var h = line.Substring(2);
                column.Item().PaddingTop(10).Text(h).FontSize(13).Bold();
                i++;
                continue;
            }

            // Numbered list (simple render)
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\.\s+"))
            {
                var text = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+\.\s+", "");
                column.Item().Row(r =>
                {
                    r.ConstantItem(12).Text("•");
                    r.RelativeItem().Text(text);
                });
                i++;
                continue;
            }

            // Bulleted list
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                var text = line.Substring(2);
                column.Item().Row(r =>
                {
                    r.ConstantItem(12).Text("•");
                    r.RelativeItem().Text(text);
                });
                i++;
                continue;
            }

            // Normal text
            column.Item().Text(line);
            i++;
        }
    }

    private void RenderByType(ColumnDescriptor col, string documentType, string markdownContent, string? caseId, string? docId)
    {
        switch (documentType.ToLower())
        {
            case "cover_page":
                RenderCoverPage(col, caseId, markdownContent);
                break;
            case "police_report":
                RenderPoliceReport(col, markdownContent, caseId, docId);
                break;
            case "forensics_report":
                RenderForensicsReport(col, markdownContent);
                break;
            case "interview":
                RenderInterview(col, markdownContent);
                break;
            case "evidence_log":
                RenderEvidenceLog(col, markdownContent);
                break;
            default:
                RenderGeneric(col, markdownContent);
                break;
        }
    }

    // ========================================
    // COVER PAGE RENDERING
    // ========================================

    private void RenderCoverPage(ColumnDescriptor col, string? caseId, string markdownContent)
    {
        // Extract title from markdown (first line after # header)
        var title = ExtractTitleFromMarkdown(markdownContent);
        
        // Agency seal at top center
        col.Item().AlignCenter().PaddingTop(20).Element(e => RenderAgencySeal(e));
        
        // "CASE FILE" title
        col.Item().PaddingTop(30).AlignCenter().Text("CASE FILE")
            .FontSize(28).Bold().FontColor(Colors.Grey.Darken3);
        
        // Classification banner
        col.Item().PaddingTop(15).Background(Colors.Red.Darken2).Padding(10).AlignCenter()
            .Text("CONFIDENTIAL • LAW ENFORCEMENT SENSITIVE")
            .FontSize(14).Bold().FontColor(Colors.White);
        
        // Case information block
        col.Item().PaddingTop(40).AlignCenter().Width(450).Element(e => RenderCaseInfoBlock(e, caseId, title));
        
        // Document stamps
        col.Item().PaddingTop(50).Element(e => RenderDocumentStamps(e));
        
        // Barcode at bottom
        col.Item().PaddingTop(60).AlignRight().Element(e => RenderBarcode(e, caseId));
        
        // Footer disclaimer
        col.Item().PaddingTop(40).AlignCenter()
            .Text("This document contains confidential law enforcement information")
            .FontSize(9).FontColor(Colors.Grey.Darken1).Italic();
    }

    private void RenderAgencySeal(IContainer container)
    {
        container.Column(col =>
        {
            // Load and render the actual Metro Police logo (transparent version)
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "assets", "LogoMetroPolice_transparent.png");
            
            // Normalize path for different OS
            logoPath = Path.GetFullPath(logoPath);
            
            if (File.Exists(logoPath))
            {
                try
                {
                    var logoBytes = File.ReadAllBytes(logoPath);
                    
                    // Render logo with controlled size (max 150px width to avoid oversizing)
                    // Using new API to avoid obsolete warning
                    col.Item().AlignCenter().MaxWidth(150).Image(logoBytes).FitWidth();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load MetroPolice logo, using fallback");
                    RenderFallbackSeal(col);
                }
            }
            else
            {
                _logger.LogWarning("MetroPolice logo not found at {LogoPath}, using fallback", logoPath);
                RenderFallbackSeal(col);
            }
            
            // Text below seal
            col.Item().PaddingTop(10).AlignCenter().Column(textCol =>
            {
                textCol.Item().Text("METRO POLICE DEPARTMENT")
                    .FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                textCol.Item().Text("Cold Case Investigation Unit")
                    .FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private void RenderFallbackSeal(ColumnDescriptor col)
    {
        // Fallback seal if logo file not found
        col.Item().AlignCenter().Width(120).Height(120).Border(3).BorderColor(Colors.Grey.Darken3)
            .Background(Colors.Grey.Lighten4).Padding(10).Column(innerCol =>
        {
            innerCol.Item().AlignCenter().Width(100).Height(100).Border(2).BorderColor(Colors.Grey.Darken2)
                .Background(Colors.White).Padding(10).AlignMiddle().Column(starCol =>
            {
                starCol.Item().AlignCenter().Width(60).Height(60).Border(2).BorderColor(Colors.Grey.Darken2)
                    .Background(Colors.Grey.Lighten3).AlignMiddle().AlignCenter()
                    .Text("★").FontSize(36).FontColor(Colors.Grey.Darken3);
            });
        });
    }

    private void RenderCaseInfoBlock(IContainer container, string? caseId, string title)
    {
        container.Border(2).BorderColor(Colors.Grey.Darken2).Padding(20).Column(col =>
        {
            // Case number (prominent)
            col.Item().PaddingBottom(10).AlignCenter().Text($"Case Number: {caseId ?? "UNKNOWN"}")
                .FontSize(14).Bold().FontColor(Colors.Grey.Darken3);
            
            col.Item().PaddingBottom(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            
            // Case details table
            col.Item().PaddingTop(8).Column(detailCol =>
            {
                RenderInfoRow(detailCol, "Case Title:", title);
                RenderInfoRow(detailCol, "Classification:", "Homicide - Cold Case");
                RenderInfoRow(detailCol, "Date Opened:", DateTime.Now.ToString("MMMM dd, yyyy"));
                RenderInfoRow(detailCol, "Current Status:", "Active Investigation");
            });
            
            col.Item().PaddingTop(10).PaddingBottom(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            
            // Investigator information
            col.Item().PaddingTop(8).Column(invCol =>
            {
                RenderInfoRow(invCol, "Lead Investigator:", "[REDACTED]");
                RenderInfoRow(invCol, "Badge Number:", "[####]");
                RenderInfoRow(invCol, "Unit:", "Major Crimes Division");
                RenderInfoRow(invCol, "Contact:", "[PHONE] ext. [###]");
            });
        });
    }

    private void RenderInfoRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(4).Row(row =>
        {
            row.ConstantItem(150).Text(label).FontSize(10).Bold().FontColor(Colors.Grey.Darken2);
            row.RelativeItem().Text(value).FontSize(10).FontColor(Colors.Grey.Darken3);
        });
    }

    private void RenderDocumentStamps(IContainer container)
    {
        container.Row(row =>
        {
            // Red "RECEIVED" stamp
            row.RelativeItem().PaddingLeft(50).Element(e => 
                RenderStamp(e, "RECEIVED", DateTime.Now.ToString("MMM dd yyyy").ToUpper(), 
                    Colors.Red.Darken2, 8, isOval: true));
            
            // Blue "FILE OPENED" stamp  
            row.RelativeItem().AlignRight().PaddingRight(50).Element(e => 
                RenderStamp(e, "FILE OPENED", DateTime.Now.ToString("MMM dd yyyy").ToUpper(), 
                    Colors.Blue.Darken2, -5, isOval: false));
        });
    }

    private void RenderStamp(IContainer container, string topText, string bottomText, 
        string color, float rotationDegrees, bool isOval)
    {
        // Note: QuestPDF's RotateLeft() doesn't take degrees parameter in this version
        // Using a simpler approach without rotation for now
        container.Width(120).Height(isOval ? 60 : 50)
            .Border(3).BorderColor(color)
            .Padding(8).AlignMiddle().Column(col =>
        {
            col.Item().AlignCenter().Text(topText)
                .FontSize(11).Bold().FontColor(color);
            col.Item().AlignCenter().Text(bottomText)
                .FontSize(9).FontColor(color);
        });
    }

    private void RenderBarcode(IContainer container, string? caseId)
    {
        var barcodeValue = caseId ?? "UNKNOWN";
        
        container.Column(col =>
        {
            // Simple barcode representation using bars
            col.Item().Width(150).Height(40).Border(1).BorderColor(Colors.Black)
                .Padding(5).AlignMiddle().AlignCenter()
                .Text($"||||| {barcodeValue} |||||")
                .FontSize(8).FontFamily("Courier New").FontColor(Colors.Black);
            
            col.Item().PaddingTop(3).AlignCenter()
                .Text("For official tracking use only")
                .FontSize(7).FontColor(Colors.Grey.Darken1).Italic();
        });
    }

    private string ExtractTitleFromMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return "Untitled Case";
        
        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# "))
                return trimmed.Substring(2).Trim();
        }
        
        return "Untitled Case";
    }

    // ========================================
    // DOCUMENT TYPE RENDERERS
    // ========================================

    private void RenderPoliceReport(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Header box with unit/agent/report info (enhanced)
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
                c.Item().Text($"Case: {(caseId ?? "________")}").FontSize(10).FontColor(Colors.Grey.Darken2);
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
        
        // Classification details table
        RenderFormTable(col, "CLASSIFICATION DETAILS", new Dictionary<string, string>
        {
            { "Case Type", "Cold Case Investigation" },
            { "Priority Level", "High" },
            { "Status", "Active Investigation" },
            { "Lead Unit", "Major Crimes Division" }
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
            RenderMarkdownContent(contentCol, md);
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

    private void RenderForensicsReport(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Indigo.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontSize(9.5f).FontColor(Colors.Indigo.Darken2));
               t.Span("This report follows forensic protocols. ");
               t.Span("Always record chain of custody at the end.").SemiBold();
           });

        RenderMarkdownContent(col, md);

        col.Item().PaddingTop(6).BorderTop(1).BorderColor(Colors.Indigo.Lighten2)
           .Text("— End of Report / Chain of Custody above —").FontSize(9).FontColor(Colors.Grey.Darken1);
    }

    private void RenderInterview(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Amber.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontSize(9.5f).FontColor(Colors.Amber.Darken3));
               t.Span("Complete transcript, without interviewer comments. ");
               t.Span("Labeling: ").FontColor(Colors.Amber.Darken3);
               t.Span("**Interviewer:** / **Interviewee:**").SemiBold();
           });

        RenderMarkdownContent(col, md);
    }

    private void RenderEvidenceLog(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Teal.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontColor(Colors.Teal.Darken2));
               t.Span("Item catalog and chain of custody. ");
               t.Span("Fields: ItemId, Collected on, Collected by, Description, Storage, Transfers.")
                .FontSize(9.5f);
           });

        RenderMarkdownContent(col, md);
    }

    // --- fallback: generic ---
    private void RenderGeneric(ColumnDescriptor col, string md) => RenderMarkdownContent(col, md);

    private bool IsTableLine(string line)
    {
        return line.Contains("|") && line.Split('|').Length > 2;
    }

    private bool IsTableSeparatorLine(string line)
    {
        return line.Contains("|") && line.Contains("-");
    }

    private void RenderTable(ColumnDescriptor column, List<string> tableLines)
    {
        if (tableLines.Count < 3) return;

        var headerLine = tableLines[0];
        var dataLines = tableLines.Skip(2).ToList();

        var headers = headerLine.Split('|')
                                .Skip(1)
                                .TakeWhile(_ => true)
                                .ToArray();

        // Clean empty borders
        headers = headers.Take(headers.Length - 1).Select(h => h.Trim()).ToArray();
        var colCount = headers.Length;
        if (colCount == 0) return;

        column.Item().PaddingVertical(4);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                for (int i = 0; i < colCount; i++)
                    cols.RelativeColumn();
            });

            // Header
            table.Header(h =>
            {
                for (int i = 0; i < colCount; i++)
                {
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(6)
                        .BorderBottom(1).BorderColor(Colors.Grey.Medium)
                        .Text(headers[i]).FontSize(10).Bold();
                }
            });

            // Data
            foreach (var dataLine in dataLines)
            {
                var cells = dataLine.Split('|')
                                   .Skip(1)
                                   .Take(colCount)
                                   .Select(c => c.Trim())
                                   .ToArray();

                for (int i = 0; i < colCount; i++)
                {
                    var cellContent = i < cells.Length ? cells[i] : "";
                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                        .Text(cellContent).FontSize(9.5f);
                }
            }
        });

        column.Item().PaddingBottom(4);
    }

    private string DetermineDocumentType(string title)
    {
        var titleLower = title.ToLower();

        if (titleLower.Contains("cover") || titleLower.Contains("front page"))
            return "cover_page";
        if (titleLower.Contains("police") || titleLower.Contains("incident") || titleLower.Contains("report"))
            return "police_report";
        if (titleLower.Contains("forensic") || titleLower.Contains("lab") || titleLower.Contains("analysis"))
            return "forensics_report";
        if (titleLower.Contains("interview") || titleLower.Contains("interrogation"))
            return "interview";
        if (titleLower.Contains("evidence") || titleLower.Contains("log") || titleLower.Contains("inventory"))
            return "evidence_log";
        if (titleLower.Contains("memo") || titleLower.Contains("memorandum"))
            return "memo";
        if (titleLower.Contains("witness") || titleLower.Contains("statement"))
            return "witness_statement";

        return "general";
    }
}