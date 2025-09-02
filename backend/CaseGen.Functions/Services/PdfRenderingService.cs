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

    public async Task<byte[]> GenerateTestPdfAsync(string title, string markdownContent, string documentType = "general", CancellationToken cancellationToken = default)
    {
        var actualDocumentType = DetermineDocumentType(title);
        if (!string.IsNullOrEmpty(documentType) && documentType != "general")
        {
            actualDocumentType = documentType;
        }

        return GenerateRealisticPdf(title, markdownContent, actualDocumentType);
    }

    private byte[] GenerateRealisticPdf(string title, string markdownContent, string documentType = "general", string? caseId = null, string? docId = null)
    {
        try
        {
            var classification = "CONFIDENCIAL • USO INTERNO";
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

                    // Marca d'água suave
                    page.Background().Element(e => AddWatermark(e, classification));

                    // Letterhead institucional
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => BuildClassificationBand(h, classification));
                    });

                    // Conteúdo
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderByType(col, documentType, markdownContent, caseId, docId);
                    });

                    // Rodapé com paginação e sigilo
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
            "police_report" => "RELATÓRIO DE OCORRÊNCIA",
            "forensics_report" => "LAUDO PERICIAL",
            "interview" => "TRANSCRIÇÃO DE ENTREVISTA",
            "evidence_log" => "CATÁLOGO & CADEIA DE CUSTÓDIA",
            "memo" or "memo_admin" => "MEMORANDO INVESTIGATIVO",
            "witness_statement" => "DECLARAÇÃO DE TESTEMUNHA",
            _ => "DOCUMENTO INVESTIGATIVO"
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
            // "Brasão" genérico (sem Radius)
            row.RelativeItem(1).Column(col =>
            {
                col.Item().Height(36).Width(36).Background(QuestPDF.Helpers.Colors.Grey.Darken2);
                col.Item().Text("DEPARTAMENTO DE POLÍCIA MUNICIPAL")
                          .Bold().FontSize(11.5f);
            });

            row.RelativeItem(1).AlignRight().Column(col =>
            {
                col.Item().Text(docTypeLabel).Bold().FontSize(13);
                if (!string.IsNullOrWhiteSpace(docId))
                    col.Item().Text($"DocId: {docId}").FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(caseId))
                    col.Item().Text($"CaseId: {caseId}").FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                col.Item().Text($"Emitido em: {DateTimeOffset.Now:yyyy-MM-dd HH:mm (zzz)}")
                          .FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
            });
        });

            // Título
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
            column.Item().Text("Sem conteúdo.");
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

            // Ignore primeiro H1 (já está no letterhead)
            if (line.StartsWith("# ") && !skippedFirstH1)
            {
                skippedFirstH1 = true;
                i++;
                continue;
            }

            // Tabela markdown
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

            // Cabeçalhos
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

            // Lista numerada (render simples)
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

            // Lista com bullets
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

            // Texto normal
            column.Item().Text(line);
            i++;
        }
    }

    private void RenderByType(ColumnDescriptor col, string documentType, string markdownContent, string? caseId, string? docId)
    {
        switch (documentType.ToLower())
        {
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

    private void RenderPoliceReport(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        col.Item().PaddingBottom(6).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(r =>
        {
            r.RelativeItem().Column(c =>
            {
                c.Item().Text("Unidade/Agente: __________________").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                c.Item().Text("Contato: ________________________").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
            });
            r.RelativeItem().Column(c =>
            {
                c.Item().Text($"Nº B.O.: {(docId ?? "________")}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                c.Item().Text($"Data/Hora: {DateTimeOffset.Now:yyyy-MM-dd HH:mm (zzz)}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
            });
            r.RelativeItem().AlignRight().Column(c =>
            {
                c.Item().Text($"CaseId: {(caseId ?? "________")}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                c.Item().Text("Classificação: Confidencial").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
            });
        });

        RenderMarkdownContent(col, md);
    }

    private void RenderForensicsReport(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Indigo.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontSize(9.5f).FontColor(Colors.Indigo.Darken2));
               t.Span("Este laudo segue protocolos periciais. ");
               t.Span("Sempre registrar cadeia de custódia ao final.").SemiBold();
           });

        RenderMarkdownContent(col, md);

        col.Item().PaddingTop(6).BorderTop(1).BorderColor(Colors.Indigo.Lighten2)
           .Text("— Fim do Laudo / Cadeia de Custódia acima —").FontSize(9).FontColor(Colors.Grey.Darken1);
    }

    private void RenderInterview(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Amber.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontSize(9.5f).FontColor(Colors.Amber.Darken3));
               t.Span("Transcrição integral, sem comentários do entrevistador. ");
               t.Span("Rotulagem: ").FontColor(Colors.Amber.Darken3);
               t.Span("**Entrevistador:** / **Entrevistado(a):**").SemiBold();
           });

        RenderMarkdownContent(col, md);
    }

    private void RenderEvidenceLog(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Teal.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontColor(Colors.Teal.Darken2));
               t.Span("Catálogo de itens e cadeia de custódia. ");
               t.Span("Campos: ItemId, Coleta em, Coletado por, Descrição, Armazenamento, Transferências.")
                .FontSize(9.5f);
           });

        RenderMarkdownContent(col, md);
    }

    // --- fallback: genérico ---
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

        // Limpa bordas vazias
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

            // Cabeçalho
            table.Header(h =>
            {
                for (int i = 0; i < colCount; i++)
                {
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(6)
                        .BorderBottom(1).BorderColor(Colors.Grey.Medium)
                        .Text(headers[i]).FontSize(10).Bold();
                }
            });

            // Dados
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