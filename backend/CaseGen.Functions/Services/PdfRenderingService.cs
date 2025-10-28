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
        
        if (documentType.ToLower() == "evidence_log" || documentType.ToLower() == "evidence_catalog")
        {
            return GenerateMultiPageEvidenceLog(title, markdownContent, documentType, caseId, docId);
        }
        
        if (documentType.ToLower() == "forensics_report" || documentType.ToLower() == "lab_report")
        {
            return GenerateMultiPageForensicsReport(title, markdownContent, documentType, caseId, docId);
        }
        
        if (documentType.ToLower() == "interview" || documentType.ToLower() == "interview_transcript")
        {
            return GenerateMultiPageInterview(title, markdownContent, documentType, caseId, docId);
        }
        
        if (documentType.ToLower() == "memo" || documentType.ToLower() == "memo_admin" || documentType.ToLower() == "internal_memo")
        {
            return GenerateMultiPageMemo(title, markdownContent, documentType, caseId, docId);
        }
        
        if (documentType.ToLower() == "witness_statement" || documentType.ToLower() == "statement")
        {
            return GenerateMultiPageWitnessStatement(title, markdownContent, documentType, caseId, docId);
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

    private byte[] GenerateMultiPageEvidenceLog(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            
            return Document.Create(container =>
            {
                // PAGE 1: Cover Page
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
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
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
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

    private byte[] GenerateMultiPageForensicsReport(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            
            return Document.Create(container =>
            {
                // PAGE 1: Cover Page
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
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
                            .Text("FORENSIC ANALYSIS REPORT").FontSize(24).Bold();
                        headerCol.Item().AlignCenter()
                            .Text("CRIME LABORATORY").FontSize(18).FontColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(40).Column(col =>
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
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
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

    private byte[] GenerateMultiPageInterview(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            
            return Document.Create(container =>
            {
                // PAGE 1: Cover Page
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
                    page.Header().Column(headerCol =>
                    {
                        // Logo at top
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
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
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

    private byte[] GenerateMultiPageMemo(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
                    page.Header().Column(headerCol =>
                    {
                        // Logo and department header
                        headerCol.Item().Row(r =>
                        {
                            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                                "assets", "LogoMetroPolice_transparent.png");
                            
                            if (File.Exists(logoPath))
                            {
                                r.AutoItem().Height(60).Image(logoPath);
                            }
                            
                            r.RelativeItem().PaddingLeft(15).AlignMiddle().Column(c =>
                            {
                                c.Item().Text("MUNICIPAL POLICE DEPARTMENT").FontSize(14).Bold();
                                c.Item().Text("Interdepartmental Communication").FontSize(11)
                                    .FontColor(Colors.Grey.Darken2);
                            });
                        });
                        
                        headerCol.Item().PaddingTop(15).AlignCenter()
                            .Text("M E M O R A N D U M").FontSize(18).Bold();
                        
                        headerCol.Item().PaddingTop(10).BorderBottom(2).BorderColor(Colors.Grey.Darken2);
                    });
                    
                    page.Content().PaddingTop(20).Column(col =>
                    {
                        // Memo header block
                        col.Item().Column(memoHeader =>
                        {
                            // TO field
                            memoHeader.Item().PaddingBottom(10).Row(r =>
                            {
                                r.ConstantItem(120).Text("TO:").FontSize(11).Bold();
                                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .PaddingBottom(3).Text("[Recipient Name / Unit]").FontSize(11);
                            });
                            
                            // FROM field
                            memoHeader.Item().PaddingBottom(10).Row(r =>
                            {
                                r.ConstantItem(120).Text("FROM:").FontSize(11).Bold();
                                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .PaddingBottom(3).Text("[Sender Name / Unit] - Badge #[####]").FontSize(11);
                            });
                            
                            // DATE field
                            memoHeader.Item().PaddingBottom(10).Row(r =>
                            {
                                r.ConstantItem(120).Text("DATE:").FontSize(11).Bold();
                                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .PaddingBottom(3).Text(DateTimeOffset.Now.ToString("MMMM dd, yyyy")).FontSize(11);
                            });
                            
                            // CASE REF field
                            memoHeader.Item().PaddingBottom(10).Row(r =>
                            {
                                r.ConstantItem(120).Text("CASE REF:").FontSize(11).Bold();
                                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .PaddingBottom(3).Text(caseId ?? "N/A").FontSize(11).FontColor(Colors.Blue.Darken2);
                            });
                            
                            // SUBJECT field (RE:)
                            memoHeader.Item().PaddingBottom(10).Row(r =>
                            {
                                r.ConstantItem(120).Text("RE:").FontSize(11).Bold();
                                r.RelativeItem().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .PaddingBottom(3).Text(title).FontSize(11).Bold();
                            });
                        });
                        
                        // Priority indicator (if applicable)
                        col.Item().PaddingTop(15).PaddingBottom(15).Row(priorityRow =>
                        {
                            priorityRow.AutoItem().Text("PRIORITY: ").FontSize(10).Bold();
                            priorityRow.AutoItem().Background(Colors.Orange.Lighten3).PaddingVertical(3).PaddingHorizontal(8)
                                .Text("☐ ROUTINE  ☐ URGENT  ☑ TIME SENSITIVE").FontSize(9).Bold()
                                .FontColor(Colors.Orange.Darken3);
                        });
                        
                        // Horizontal line separator
                        col.Item().PaddingBottom(15).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                        
                        // Memo body content
                        RenderMemoContent(col, markdownContent, caseId, docId);
                    });
                    
                    page.Footer().Column(footerCol =>
                    {
                        footerCol.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(8);
                        footerCol.Item().AlignCenter().Text(t =>
                        {
                            t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                            t.Span("   •   Page ");
                            t.CurrentPageNumber().FontSize(9);
                        });
                    });
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating multi-page memo: {Message}", ex.Message);
            throw;
        }
    }

    private byte[] GenerateMultiPageWitnessStatement(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • LAW ENFORCEMENT SENSITIVE";
            
            return Document.Create(container =>
            {
                // PAGE 1: Cover page with witness information
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
                    page.Header().Column(headerCol =>
                    {
                        // Large centered logo
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
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
                    
                    page.Background().Element(e => AddWatermark(e, classification));
                    
                    page.Header().Column(col =>
                    {
                        col.Item().Element(e => BuildLetterhead(e, "WITNESS STATEMENT", title, caseId, docId));
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
        RenderMarkdownContent(col, md);
        
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
        RenderMarkdownContent(col, md);
        
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
            
            // Lab Director approval
            certCol.Item().PaddingTop(15).Row(dirRow =>
            {
                dirRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Laboratory Director Signature").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                dirRow.ConstantItem(20);
                
                dirRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Ph.D., Certification").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                dirRow.ConstantItem(20);
                
                dirRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________").FontSize(9);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
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
        RenderMarkdownContent(col, md);
        
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
            
            // Subject acknowledgment
            certCol.Item().PaddingTop(15).Row(subjRow =>
            {
                subjRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Subject Signature").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                subjRow.ConstantItem(20);
                
                subjRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text($"{DateTimeOffset.Now:MM/dd/yyyy}").FontSize(9);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                subjRow.ConstantItem(20);
                
                subjRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text(DateTimeOffset.Now.ToString("HH:mm")).FontSize(9);
                    c.Item().PaddingTop(2).Text("Time").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
            
            // Interviewer certification
            certCol.Item().PaddingTop(15).Row(intRow =>
            {
                intRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Interviewing Officer Signature").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                intRow.ConstantItem(20);
                
                intRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                intRow.ConstantItem(20);
                
                intRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text($"{DateTimeOffset.Now:MM/dd/yyyy}").FontSize(9);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
            
            // Witness officer (if present)
            certCol.Item().PaddingTop(15).Row(witRow =>
            {
                witRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Witness Officer Signature (if present)").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                witRow.ConstantItem(20);
                
                witRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(30);
                    c.Item().PaddingTop(2).Text("Badge Number").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                witRow.ConstantItem(20);
                
                witRow.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________").FontSize(9);
                    c.Item().PaddingTop(2).Text("Date").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }

    private void RenderMemoContent(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        // Parse markdown sections
        var lines = md.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sections = new Dictionary<string, List<string>>
        {
            ["BACKGROUND"] = new List<string>(),
            ["DISCUSSION"] = new List<string>(),
            ["RECOMMENDATIONS"] = new List<string>(),
            ["ACTION ITEMS"] = new List<string>()
        };
        
        string currentSection = "";
        foreach (var line in lines)
        {
            var upper = line.Trim().ToUpper();
            if (upper.Contains("BACKGROUND"))
                currentSection = "BACKGROUND";
            else if (upper.Contains("DISCUSSION") || upper.Contains("FINDINGS"))
                currentSection = "DISCUSSION";
            else if (upper.Contains("RECOMMENDATION"))
                currentSection = "RECOMMENDATIONS";
            else if (upper.Contains("ACTION ITEM"))
                currentSection = "ACTION ITEMS";
            else if (!string.IsNullOrWhiteSpace(currentSection) && !string.IsNullOrWhiteSpace(line))
                sections[currentSection].Add(line);
        }

        // Render sections
        foreach (var section in sections)
        {
            if (section.Value.Count == 0) continue;

            col.Item().PaddingTop(15).PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Darken2)
                    .Padding(6).AlignMiddle()
                    .Text(section.Key).FontSize(11).Bold()
                    .FontColor(Colors.Grey.Darken4);
            });

            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(10).Column(c =>
            {
                foreach (var line in section.Value)
                {
                    c.Item().PaddingBottom(4).Text(line.Trim()).FontSize(10)
                        .LineHeight(1.4f);
                }
            });
        }

        // If no sections found, render all content
        if (sections.All(s => s.Value.Count == 0))
        {
            col.Item().PaddingTop(15).Border(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(12).Column(c =>
            {
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    c.Item().PaddingBottom(4).Text(line.Trim()).FontSize(10)
                        .LineHeight(1.4f);
                }
            });
        }

        // ACKNOWLEDGMENT section
        col.Item().PaddingTop(30).Border(1).BorderColor(Colors.Grey.Darken2)
            .Padding(12).Column(c =>
        {
            c.Item().PaddingBottom(10).Text("ACKNOWLEDGMENT").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken4);

            // Prepared by
            c.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(prepCol =>
                {
                    prepCol.Item().Text("PREPARED BY:").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken3);
                    prepCol.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________________________________").FontSize(9);
                    prepCol.Item().PaddingTop(2).Text("Officer Name / Rank").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                row.ConstantItem(20);
                
                row.RelativeItem().Column(dateCol =>
                {
                    dateCol.Item().Text("DATE:").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken3);
                    dateCol.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________").FontSize(9);
                    dateCol.Item().PaddingTop(2).Text("MM/DD/YYYY").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });

            // Reviewed by
            c.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(revCol =>
                {
                    revCol.Item().Text("REVIEWED BY:").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken3);
                    revCol.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________________________________").FontSize(9);
                    revCol.Item().PaddingTop(2).Text("Supervisor Name").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                row.ConstantItem(20);
                
                row.RelativeItem().Column(dateCol =>
                {
                    dateCol.Item().Text("DATE:").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken3);
                    dateCol.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________").FontSize(9);
                    dateCol.Item().PaddingTop(2).Text("MM/DD/YYYY").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });

            // Approved by
            c.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(appCol =>
                {
                    appCol.Item().Text("APPROVED BY:").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken3);
                    appCol.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________________________________").FontSize(9);
                    appCol.Item().PaddingTop(2).Text("Commanding Officer").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
                
                row.ConstantItem(20);
                
                row.RelativeItem().Column(dateCol =>
                {
                    dateCol.Item().Text("DATE:").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken3);
                    dateCol.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________").FontSize(9);
                    dateCol.Item().PaddingTop(2).Text("MM/DD/YYYY").FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
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
        
        // Notary public section
        col.Item().PaddingTop(20).Background(Colors.Grey.Lighten3)
            .Border(2).BorderColor(Colors.Grey.Darken3)
            .Padding(12).Column(c =>
        {
            c.Item().PaddingBottom(15).Text("NOTARY PUBLIC ACKNOWLEDGMENT").FontSize(11).Bold()
                .FontColor(Colors.Grey.Darken4);
            
            c.Item().PaddingBottom(10).Text("State of: ________________     County of: ________________")
                .FontSize(9);
            
            c.Item().PaddingBottom(10).Text("Subscribed and sworn to (or affirmed) before me on this _____ day of _____________, 20___, by ________________________________ (name of witness), proved to me on the basis of satisfactory evidence to be the person who appeared before me.")
                .FontSize(9).LineHeight(1.4f);
            
            // Notary signature
            c.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(sigCol =>
                {
                    sigCol.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1)
                        .AlignMiddle().Text("__________________________________________").FontSize(9);
                    sigCol.Item().PaddingTop(2).Text("Notary Public Signature").FontSize(9).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });
            });
            
            c.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Text("Commission Number: ________________").FontSize(9);
                row.ConstantItem(20);
                row.RelativeItem().Text("My Commission Expires: ________________").FontSize(9);
            });
            
            c.Item().PaddingTop(10).AlignCenter().Border(1).BorderColor(Colors.Grey.Darken2)
                .Padding(8).Text("[NOTARY SEAL]").FontSize(9).Italic()
                .FontColor(Colors.Grey.Darken2);
        });
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