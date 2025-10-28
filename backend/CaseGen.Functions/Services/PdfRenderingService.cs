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
        // Check if this is a multi-page document type
        if (documentType.ToLower() == "suspect_profile" || documentType.ToLower() == "witness_profile")
        {
            return GenerateMultiPageSuspectProfile(title, markdownContent, documentType, caseId, docId);
        }

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

    private byte[] GenerateMultiPageSuspectProfile(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            var docTypeLabel = GetDocumentTypeLabel(documentType);
            var (bandBg, bandText) = GetThemeColors(documentType);
            var mugshotPath = FindMugshotPath(docId, markdownContent);

            return Document.Create(container =>
            {
                // PAGE 1: Cover with Photo and Personal Data
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));

                    page.Background().Element(e => AddWatermark(e, classification));

                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => BuildClassificationBand(h, classification));
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderSuspectProfilePage1(col, markdownContent, mugshotPath);
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

                // PAGE 2: Criminal History and Interview Log
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));

                    page.Background().Element(e => AddWatermark(e, classification));

                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => BuildClassificationBand(h, classification));
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderSuspectProfilePage2(col, markdownContent);
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

                // PAGE 3: Alibi and Risk Assessment
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));

                    page.Background().Element(e => AddWatermark(e, classification));

                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => BuildClassificationBand(h, classification));
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderSuspectProfilePage3(col, markdownContent);
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
            _logger.LogError(ex, "Error generating multi-page suspect profile: {Message}", ex.Message);
            throw;
        }
    }

    private static string GetDocumentTypeLabel(string documentType)
    {
        return documentType.ToLower() switch
        {
            "cover_page" => "CASE FILE COVER",
            "suspect_profile" => "SUSPECT INFORMATION",
            "witness_profile" => "WITNESS INFORMATION",
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
            // Left side - Police Logo only (replaces department name text)
            row.RelativeItem(1).Row(leftRow =>
            {
                var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "assets", "LogoMetroPolice_transparent.png");
                
                if (File.Exists(logoPath))
                {
                    leftRow.AutoItem().Height(50).Image(logoPath);
                }
                else
                {
                    // Fallback to text if logo not found
                    leftRow.RelativeItem().Text("MUNICIPAL POLICE DEPARTMENT")
                        .Bold().FontSize(11.5f);
                }
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
            case "suspect_profile":
            case "witness_profile":
                var mugshotPath = FindMugshotPath(docId, markdownContent);
                RenderSuspectProfile(col, markdownContent, mugshotPath);
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

    private string? FindMugshotPath(string? docId, string markdownContent)
    {
        try
        {
            // Try to extract suspect name from markdown
            var suspectName = ExtractSuspectName(markdownContent);
            
            // Look for mugshot in test-output directory
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var testOutputPath = Path.Combine(baseDir, "../../../../test-output/bundles");
            
            if (Directory.Exists(testOutputPath))
            {
                // Search for case directories
                var caseDirs = Directory.GetDirectories(testOutputPath);
                foreach (var caseDir in caseDirs)
                {
                    var mediaPath = Path.Combine(caseDir, "media");
                    if (Directory.Exists(mediaPath))
                    {
                        // Look for mugshot files matching suspect name
                        var files = Directory.GetFiles(mediaPath, "*.png");
                        
                        // Try to find by docId pattern (e.g., SUSP-MARCO-002 -> marco)
                        if (!string.IsNullOrEmpty(docId))
                        {
                            var docIdLower = docId.ToLower();
                            var mugshotFile = files.FirstOrDefault(f => 
                                f.ToLower().Contains("mugshot") && 
                                (f.ToLower().Contains(suspectName.ToLower()) ||
                                 docIdLower.Contains(Path.GetFileNameWithoutExtension(f).ToLower().Split('_').LastOrDefault() ?? "")));
                            
                            if (mugshotFile != null)
                            {
                                _logger.LogInformation("Found mugshot: {Path}", mugshotFile);
                                return mugshotFile;
                            }
                        }
                        
                        // Fallback: try by suspect name
                        if (!string.IsNullOrEmpty(suspectName))
                        {
                            var mugshotFile = files.FirstOrDefault(f => 
                                f.ToLower().Contains("mugshot") && 
                                f.ToLower().Contains(suspectName.ToLower()));
                            
                            if (mugshotFile != null)
                            {
                                _logger.LogInformation("Found mugshot by name: {Path}", mugshotFile);
                                return mugshotFile;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not locate mugshot file");
        }
        
        return null;
    }

    private string ExtractSuspectName(string markdownContent)
    {
        // Try to extract first name from "Name: LastName, FirstName" pattern
        var lines = markdownContent.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("Name:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    var namePart = parts[1].Trim();
                    // Extract first name (after comma)
                    if (namePart.Contains(','))
                    {
                        var nameComponents = namePart.Split(',');
                        if (nameComponents.Length > 1)
                        {
                            return nameComponents[1].Trim().Split(' ')[0]; // First name only
                        }
                    }
                }
            }
        }
        return "";
    }

    // ========================================
    // COVER PAGE RENDERING
    // ========================================
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
    // SUSPECT/WITNESS PROFILE RENDERING (MULTI-PAGE)
    // ========================================

    // PAGE 1: Photo and Personal Data
    private void RenderSuspectProfilePage1(ColumnDescriptor col, string markdownContent, string? mugshotPath = null)
    {
        // Agency seal at top
        col.Item().AlignCenter().PaddingTop(10).PaddingBottom(10)
            .Element(e => RenderAgencySeal(e));

        // Header box with title and mugshot
        col.Item().Border(2).BorderColor(Colors.Grey.Darken2).Padding(15).Column(headerCol =>
        {
            headerCol.Item().AlignCenter().Text("SUSPECT INFORMATION")
                .FontSize(14).Bold();

            // Mugshot: load from path if provided, else placeholder
            if (!string.IsNullOrEmpty(mugshotPath) && File.Exists(mugshotPath))
            {
                try
                {
                    var imgBytes = File.ReadAllBytes(mugshotPath);
                    headerCol.Item().PaddingTop(10).AlignCenter()
                        .Width(150).Height(200).Image(imgBytes).FitArea();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load mugshot from {Path}, using placeholder", mugshotPath);
                    RenderMugshotPlaceholder(headerCol);
                }
            }
            else
            {
                RenderMugshotPlaceholder(headerCol);
            }

            // Suspect ID
            var suspectId = ExtractSuspectId(markdownContent);
            headerCol.Item().PaddingTop(5).AlignCenter()
                .Text($"ID: {suspectId}")
                .FontSize(10).FontFamily("Courier New");
        });

        // Personal data table
        var personalData = ExtractPersonalData(markdownContent);
        RenderFormTable(col, "PERSONAL DATA", personalData);
    }

    // PAGE 2: Criminal History and Interview Log
    private void RenderSuspectProfilePage2(ColumnDescriptor col, string markdownContent)
    {
        // Criminal history
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text("CRIMINAL HISTORY").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(2).LineHorizontal(2).LineColor(Colors.Grey.Darken2);
            c.Item().PaddingTop(10).Text(ExtractCriminalHistory(markdownContent))
                .FontSize(10).LineHeight(1.5f);
        });

        // Interview log table
        col.Item().PaddingTop(30).Column(c =>
        {
            c.Item().Text("INTERVIEW LOG").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(2).LineHorizontal(2).LineColor(Colors.Grey.Darken2);
            
            c.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1); // Date
                    cols.RelativeColumn(1.2f); // Interviewer
                    cols.RelativeColumn(1); // Location
                    cols.RelativeColumn(0.8f); // Duration
                });

                table.Header(header =>
                {
                    RenderTableHeader(header.Cell(), "Date");
                    RenderTableHeader(header.Cell(), "Interviewer");
                    RenderTableHeader(header.Cell(), "Location");
                    RenderTableHeader(header.Cell(), "Duration");
                });

                // Extract and render interview entries
                var interviews = ExtractInterviews(markdownContent);
                foreach (var interview in interviews)
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Column(innerCol =>
                        {
                            innerCol.Item().Text(interview.Date).FontSize(9);
                            innerCol.Item().Text(interview.Time).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text(interview.Interviewer).FontSize(9);
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text(interview.Location).FontSize(9);
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text(interview.Duration).FontSize(9);
                }
            });
        });
    }

    // PAGE 3: Alibi and Risk Assessment
    private void RenderSuspectProfilePage3(ColumnDescriptor col, string markdownContent)
    {
        // Alibi verification
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text("ALIBI VERIFICATION").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(2).LineHorizontal(2).LineColor(Colors.Grey.Darken2);
            c.Item().PaddingTop(10).Text(ExtractAlibi(markdownContent))
                .FontSize(10).LineHeight(1.5f);
        });

        // Risk assessment
        col.Item().PaddingTop(40).Column(c =>
        {
            c.Item().Text("RISK ASSESSMENT").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(2).LineHorizontal(2).LineColor(Colors.Grey.Darken2);
            
            c.Item().PaddingTop(15).Border(2).BorderColor(Colors.Grey.Darken1)
                .Background(Colors.Grey.Lighten4).Padding(20).Column(riskCol =>
            {
                var riskAssessment = ExtractRiskAssessment(markdownContent);
                
                riskCol.Item().PaddingBottom(10).Row(row =>
                {
                    row.AutoItem().Width(30).Text(riskAssessment.FlightRisk ? "☑" : "☐")
                        .FontSize(16).FontColor(Colors.Grey.Darken3);
                    row.RelativeItem().Text("Flight Risk").FontSize(11).Bold();
                });
                
                riskCol.Item().PaddingBottom(10).Row(row =>
                {
                    row.AutoItem().Width(30).Text(riskAssessment.ViolentHistory ? "☑" : "☐")
                        .FontSize(16).FontColor(Colors.Grey.Darken3);
                    row.RelativeItem().Text("Violent History").FontSize(11).Bold();
                });
                
                riskCol.Item().Row(row =>
                {
                    row.AutoItem().Width(30).Text(riskAssessment.Armed ? "☑" : "☐")
                        .FontSize(16).FontColor(Colors.Grey.Darken3);
                    row.RelativeItem().Text("Armed").FontSize(11).Bold();
                });
            });
        });

        // Notes section
        col.Item().PaddingTop(30).Column(c =>
        {
            c.Item().Text("INVESTIGATOR NOTES").FontSize(12).Bold()
                .FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(2).LineHorizontal(2).LineColor(Colors.Grey.Darken2);
            c.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1)
                .Padding(15).MinHeight(150)
                .Text("[Space for investigator notes and observations]")
                .FontSize(9).FontColor(Colors.Grey.Darken1).Italic();
        });
    }

    // Legacy single-page method (kept for compatibility with other document types)
    private void RenderSuspectProfile(ColumnDescriptor col, string markdownContent, string? mugshotPath = null)
    {
        // Agency seal at top
        col.Item().AlignCenter().PaddingTop(10).PaddingBottom(10)
            .Element(e => RenderAgencySeal(e));

        // Header box with title and mugshot
        col.Item().Border(2).BorderColor(Colors.Grey.Darken2).Padding(15).Column(headerCol =>
        {
            headerCol.Item().AlignCenter().Text("SUSPECT INFORMATION")
                .FontSize(14).Bold();

            // Mugshot: load from path if provided, else placeholder
            if (!string.IsNullOrEmpty(mugshotPath) && File.Exists(mugshotPath))
            {
                try
                {
                    var imgBytes = File.ReadAllBytes(mugshotPath);
                    headerCol.Item().PaddingTop(10).AlignCenter()
                        .Width(150).Height(200).Image(imgBytes).FitArea();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load mugshot from {Path}, using placeholder", mugshotPath);
                    RenderMugshotPlaceholder(headerCol);
                }
            }
            else
            {
                RenderMugshotPlaceholder(headerCol);
            }

            // Suspect ID
            var suspectId = ExtractSuspectId(markdownContent);
            headerCol.Item().PaddingTop(5).AlignCenter()
                .Text($"ID: {suspectId}")
                .FontSize(10).FontFamily("Courier New");
        });

        // Personal data table
        var personalData = ExtractPersonalData(markdownContent);
        RenderFormTable(col, "PERSONAL DATA", personalData);

        // Criminal history
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text("CRIMINAL HISTORY").FontSize(11).Bold();
            c.Item().PaddingTop(5).Text(ExtractCriminalHistory(markdownContent))
                .FontSize(10).LineHeight(1.4f);
        });

        // Interview log table
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text("INTERVIEW LOG").FontSize(11).Bold();
            c.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1); // Date
                    cols.RelativeColumn(1.2f); // Interviewer
                    cols.RelativeColumn(1); // Location
                    cols.RelativeColumn(0.8f); // Duration
                });

                table.Header(header =>
                {
                    RenderTableHeader(header.Cell(), "Date");
                    RenderTableHeader(header.Cell(), "Interviewer");
                    RenderTableHeader(header.Cell(), "Location");
                    RenderTableHeader(header.Cell(), "Duration");
                });

                // Extract and render interview entries
                var interviews = ExtractInterviews(markdownContent);
                foreach (var interview in interviews)
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Column(innerCol =>
                        {
                            innerCol.Item().Text(interview.Date).FontSize(9);
                            innerCol.Item().Text(interview.Time).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text(interview.Interviewer).FontSize(9);
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text(interview.Location).FontSize(9);
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text(interview.Duration).FontSize(9);
                }
            });
        });

        // Alibi verification
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text("ALIBI VERIFICATION").FontSize(11).Bold();
            c.Item().PaddingTop(5).Text(ExtractAlibi(markdownContent))
                .FontSize(10).LineHeight(1.4f);
        });

        // Risk assessment
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text("RISK ASSESSMENT").FontSize(11).Bold();
            c.Item().PaddingTop(5).Row(row =>
            {
                var riskAssessment = ExtractRiskAssessment(markdownContent);
                row.AutoItem().Text($"{(riskAssessment.FlightRisk ? "☑" : "☐")} Flight Risk    ").FontSize(10);
                row.AutoItem().Text($"{(riskAssessment.ViolentHistory ? "☑" : "☐")} Violent History    ").FontSize(10);
                row.AutoItem().Text($"{(riskAssessment.Armed ? "☑" : "☐")} Armed").FontSize(10);
            });
        });
    }

    private void RenderMugshotPlaceholder(ColumnDescriptor col)
    {
        col.Item().PaddingTop(10).AlignCenter()
            .Width(150).Height(200)
            .Border(1).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .AlignMiddle().AlignCenter()
            .Text("[PHOTO]").FontSize(12).FontColor(Colors.Grey.Darken1);
    }

    private void RenderTableHeader(IContainer cell, string text)
    {
        cell.Border(1).BorderColor(Colors.Grey.Darken1)
            .Background(Colors.Grey.Lighten2).Padding(5)
            .Text(text).FontSize(9).Bold();
    }

    private string ExtractSuspectId(string markdown)
    {
        // Look for suspect ID in markdown
        var lines = markdown.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("ID:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                    return parts[1].Trim();
            }
        }
        return "SUSP-001-2024";
    }

    private Dictionary<string, string> ExtractPersonalData(string markdown)
    {
        // Parse personal data from markdown or return defaults
        var data = new Dictionary<string, string>
        {
            { "Name", "[LAST], [FIRST] [MIDDLE]" },
            { "AKA", "[ALIASES]" },
            { "DOB", "MM/DD/YYYY (Age: ##)" },
            { "Sex/Race", "Male / White" },
            { "Height/Weight", "6'0\" / 180 lbs" },
            { "Eyes/Hair", "Brown / Brown" },
            { "Marks/Scars", "None reported" },
            { "Address", "[REDACTED]" },
            { "SSN", "###-##-####" }
        };

        // Try to extract actual data from markdown sections
        var lines = markdown.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("Name:", StringComparison.OrdinalIgnoreCase))
                data["Name"] = ExtractValue(trimmed);
            else if (trimmed.Contains("AKA:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Alias:", StringComparison.OrdinalIgnoreCase))
                data["AKA"] = ExtractValue(trimmed);
            else if (trimmed.Contains("DOB:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Date of Birth:", StringComparison.OrdinalIgnoreCase))
                data["DOB"] = ExtractValue(trimmed);
            else if (trimmed.Contains("Sex:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Race:", StringComparison.OrdinalIgnoreCase))
                data["Sex/Race"] = ExtractValue(trimmed);
            else if (trimmed.Contains("Height:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Weight:", StringComparison.OrdinalIgnoreCase))
                data["Height/Weight"] = ExtractValue(trimmed);
            else if (trimmed.Contains("Eyes:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Hair:", StringComparison.OrdinalIgnoreCase))
                data["Eyes/Hair"] = ExtractValue(trimmed);
            else if (trimmed.Contains("Marks:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Scars:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Tattoos:", StringComparison.OrdinalIgnoreCase))
                data["Marks/Scars"] = ExtractValue(trimmed);
        }

        return data;
    }

    private string ExtractValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < line.Length - 1)
            return line.Substring(colonIndex + 1).Trim();
        return line;
    }

    private string ExtractCriminalHistory(string markdown)
    {
        // Look for criminal history section
        var lines = markdown.Split('\n');
        var inSection = false;
        var history = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.Contains("Criminal History", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Prior Arrests", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (inSection)
            {
                if (line.StartsWith("#") || line.Contains("Interview", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Alibi", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                if (!string.IsNullOrWhiteSpace(line))
                    history.AppendLine(line.Trim());
            }
        }

        var result = history.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "No prior criminal record on file." : result;
    }

    private List<InterviewEntry> ExtractInterviews(string markdown)
    {
        // For now, return sample data. Could be enhanced to parse from markdown
        return new List<InterviewEntry>
        {
            new InterviewEntry
            {
                Date = "09/15/2023",
                Time = "10:00 AM",
                Interviewer = "Det. Johnson #5678",
                Location = "Station Room 2",
                Duration = "45 min"
            }
        };
    }

    private string ExtractAlibi(string markdown)
    {
        // Look for alibi section
        var lines = markdown.Split('\n');
        var inSection = false;
        var alibi = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.Contains("Alibi", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (inSection)
            {
                if (line.StartsWith("#") || line.Contains("Risk", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                if (!string.IsNullOrWhiteSpace(line))
                    alibi.AppendLine(line.Trim());
            }
        }

        var result = alibi.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "No alibi provided or verification pending." : result;
    }

    private RiskAssessment ExtractRiskAssessment(string markdown)
    {
        var assessment = new RiskAssessment();
        
        // Look for risk assessment markers in markdown
        if (markdown.Contains("flight risk", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("likely to flee", StringComparison.OrdinalIgnoreCase))
        {
            assessment.FlightRisk = true;
        }

        if (markdown.Contains("violent history", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("violent", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("assault", StringComparison.OrdinalIgnoreCase))
        {
            assessment.ViolentHistory = true;
        }

        if (markdown.Contains("armed", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("weapon", StringComparison.OrdinalIgnoreCase))
        {
            assessment.Armed = true;
        }

        return assessment;
    }

    private class InterviewEntry
    {
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string Interviewer { get; set; } = "";
        public string Location { get; set; } = "";
        public string Duration { get; set; } = "";
    }

    private class RiskAssessment
    {
        public bool FlightRisk { get; set; }
        public bool ViolentHistory { get; set; }
        public bool Armed { get; set; }
    }

    // ========================================
    // POLICE REPORT RENDERING
    // ========================================

    private void RenderPoliceReport(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Header box with unit/agent/report info (without logo - logo is in page header)
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
                // Case number highlighted
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
            RenderMarkdownContent(contentCol, md);
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
                
                sigRow.ConstantItem(20); // Spacing
                
                // Badge number
                sigRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .AlignMiddle().Text("Badge #: __________").FontSize(9);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                
                sigRow.ConstantItem(20); // Spacing
                
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
                        .Height(30); // Space for signature
                    c.Item().PaddingTop(2).Text("Supervisor Signature (if required)").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                
                supRow.ConstantItem(20); // Spacing
                
                supRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
                        .AlignMiddle().Text("Badge #: __________").FontSize(9);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                
                supRow.ConstantItem(20); // Spacing
                
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
        if (titleLower.Contains("suspect") || titleLower.Contains("person of interest") || titleLower.Contains("poi"))
            return "suspect_profile";
        if (titleLower.Contains("witness") && (titleLower.Contains("profile") || titleLower.Contains("information")))
            return "witness_profile";
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