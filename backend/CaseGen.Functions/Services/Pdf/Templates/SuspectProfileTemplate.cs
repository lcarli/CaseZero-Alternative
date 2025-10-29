using System.Linq;
using CaseGen.Functions.Services.Pdf;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.Pdf.Templates;

/// <summary>
/// Template for generating 3-page Suspect/Witness Profile documents
/// </summary>
public class SuspectProfileTemplate
{
    private readonly ILogger _logger;

    public SuspectProfileTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public byte[] Generate(string title, string markdownContent, string documentType, string? caseId, string? docId)
    {
        try
        {
            var classification = "CONFIDENTIAL • INTERNAL USE ONLY";
            var docTypeLabel = PdfCommonComponents.GetDocumentTypeLabel(documentType);
            var (bandBg, bandText) = PdfCommonComponents.GetThemeColors(documentType);
            var mugshotPath = FindMugshotPath(docId, markdownContent);

            return Document.Create(container =>
            {
                // PAGE 1: Cover with Photo and Personal Data
                container.Page(page =>
                {
                    ConfigurePage(page, classification);
                    
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => PdfCommonComponents.BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => PdfCommonComponents.BuildClassificationBand(h, classification));
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderPage1(col, markdownContent, mugshotPath);
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        RenderFooter(t, classification);
                    });
                });

                // PAGE 2: Criminal History and Interview Log
                container.Page(page =>
                {
                    ConfigurePage(page, classification);
                    
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => PdfCommonComponents.BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => PdfCommonComponents.BuildClassificationBand(h, classification));
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderPage2(col, markdownContent);
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        RenderFooter(t, classification);
                    });
                });

                // PAGE 3: Alibi and Risk Assessment
                container.Page(page =>
                {
                    ConfigurePage(page, classification);
                    
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => PdfCommonComponents.BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => PdfCommonComponents.BuildClassificationBand(h, classification));
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderPage3(col, markdownContent);
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        RenderFooter(t, classification);
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

    private void ConfigurePage(PageDescriptor page, string classification)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));
        page.Background().Element(e => PdfCommonComponents.AddWatermark(e, classification));
    }

    private void RenderFooter(TextDescriptor t, string classification)
    {
        t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
        t.Span("   •   Page ");
        t.CurrentPageNumber().FontSize(9);
        t.Span(" of ");
        t.TotalPages().FontSize(9);
    }

    // PAGE 1: Photo and Personal Data
    private void RenderPage1(ColumnDescriptor col, string markdownContent, string? mugshotPath = null)
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
    private void RenderPage2(ColumnDescriptor col, string markdownContent)
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
                    header.Cell().Background(Colors.Grey.Lighten3).Border(1)
                        .BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text("Date").FontSize(9).Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Border(1)
                        .BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text("Interviewer").FontSize(9).Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Border(1)
                        .BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text("Location").FontSize(9).Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Border(1)
                        .BorderColor(Colors.Grey.Lighten1).Padding(5)
                        .Text("Duration").FontSize(9).Bold();
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
    private void RenderPage3(ColumnDescriptor col, string markdownContent)
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

    // Helper methods for extracting data from markdown
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
        var history = new System.Text.StringBuilder();

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

    private List<(string Date, string Time, string Interviewer, string Location, string Duration)> ExtractInterviews(string markdown)
    {
        // For now, return sample data. Could be enhanced to parse from markdown
        return new List<(string, string, string, string, string)>
        {
            ("09/15/2023", "10:00 AM", "Det. Johnson #5678", "Station Room 2", "45 min")
        };
    }

    private string ExtractAlibi(string markdown)
    {
        // Look for alibi section
        var lines = markdown.Split('\n');
        var inSection = false;
        var alibi = new System.Text.StringBuilder();

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

    private (bool FlightRisk, bool ViolentHistory, bool Armed) ExtractRiskAssessment(string markdown)
    {
        var flightRisk = false;
        var violentHistory = false;
        var armed = false;
        
        // Look for risk assessment markers in markdown
        if (markdown.Contains("flight risk", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("likely to flee", StringComparison.OrdinalIgnoreCase))
        {
            flightRisk = true;
        }

        if (markdown.Contains("violent history", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("violent", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("assault", StringComparison.OrdinalIgnoreCase))
        {
            violentHistory = true;
        }

        if (markdown.Contains("armed", StringComparison.OrdinalIgnoreCase) ||
            markdown.Contains("weapon", StringComparison.OrdinalIgnoreCase))
        {
            armed = true;
        }

        return (flightRisk, violentHistory, armed);
    }

    private void RenderAgencySeal(IContainer c)
    {
        var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "assets", "LogoMetroPolice_transparent.png");
        
        if (File.Exists(logoPath))
        {
            c.Height(80).Image(logoPath);
        }
        else
        {
            c.Height(80).Border(2).BorderColor(Colors.Grey.Lighten1)
                .AlignCenter().AlignMiddle()
                .Text("[AGENCY SEAL]").FontSize(10).FontColor(Colors.Grey.Darken2);
        }
    }

    private void RenderMugshotPlaceholder(ColumnDescriptor col)
    {
        col.Item().PaddingTop(10).AlignCenter().Width(150).Height(200)
            .Border(2).BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten4)
            .AlignCenter().AlignMiddle()
            .Text("PHOTO\nUNAVAILABLE").FontSize(12).FontColor(Colors.Grey.Darken2);
    }

    private void RenderFormTable(ColumnDescriptor col, string title, Dictionary<string, string> data)
    {
        col.Item().PaddingTop(20).Column(c =>
        {
            c.Item().Text(title).FontSize(12).Bold().FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(2).LineHorizontal(2).LineColor(Colors.Grey.Darken2);
            
            c.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                });

                foreach (var kvp in data)
                {
                    table.Cell().Background(Colors.Grey.Lighten4).Border(1)
                        .BorderColor(Colors.Grey.Lighten1).Padding(8)
                        .Text(kvp.Key).FontSize(9).Bold();
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8)
                        .Text(kvp.Value).FontSize(9);
                }
            });
        });
    }

}
