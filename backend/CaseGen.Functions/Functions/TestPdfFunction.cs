using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;

namespace CaseGen.Functions.Functions;

public class TestPdfFunction
{
    private readonly ILogger<TestPdfFunction> _logger;
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IStorageService _storageService;

    public TestPdfFunction(ILogger<TestPdfFunction> logger, ICaseGenerationService caseGenerationService, IStorageService storageService)
    {
        _logger = logger;
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
    }

    [Function("TestPdfGeneration")]
    public async Task<HttpResponseData> TestPdfGeneration(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "test/pdf")] HttpRequestData req)
    {
        // This is a TEST FUNCTION ONLY - all actual PDF generation logic is in the main orchestrator
        // via CaseGenerationService.RenderDocumentFromJsonAsync()
        _logger.LogInformation("TestPdfGeneration function processed a request.");

        try
        {
            // Read the request body
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<DocumentTestRequest>(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (request == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request format");
                return badResponse;
            }

            _logger.LogInformation("Generating PDF for document: {Title}", request.Title);

            // Convert sections to markdown
            var markdownContent = ConvertToMarkdown(request);

            // Test the PDF generation
            var pdfResult = await TestPdfGenerationInternal(request.Title, markdownContent, request.Type);

            // Save PDF to blob storage
            var fileName = $"test-pdf-{DateTime.Now:yyyyMMdd-HHmmss}-{request.DocId}.pdf";
            var containerName = "test-pdfs";
            
            // Convert base64 back to bytes for blob storage
            var pdfBytes = Convert.FromBase64String(pdfResult.PdfBase64);
            var blobUrl = await _storageService.SaveFileAsync(containerName, fileName, pdfBytes);

            _logger.LogInformation("PDF saved to blob: {BlobUrl}", blobUrl);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var result = new DocumentTestResponse
            {
                MarkdownContent = markdownContent,
                PdfBlobUrl = blobUrl,
                DocumentType = pdfResult.DocumentType,
                FileName = fileName
            };

            var jsonResponse = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await response.WriteStringAsync(jsonResponse);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestPdfGeneration");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    private string ConvertToMarkdown(DocumentTestRequest request)
    {
        // Simple conversion for testing - the real logic is in the service
        var markdown = new System.Text.StringBuilder();
        
        markdown.AppendLine($"# {request.Title}");
        markdown.AppendLine();
        markdown.AppendLine($"**Documento ID:** {request.DocId}");
        markdown.AppendLine($"**Tipo:** {request.Type}");
        markdown.AppendLine($"**Palavras:** {request.Words}");
        markdown.AppendLine();

        foreach (var section in request.Sections)
        {
            markdown.AppendLine($"## {section.Title}");
            markdown.AppendLine();
            markdown.AppendLine(section.Content);
            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private async Task<(string PdfBase64, string DocumentType)> TestPdfGenerationInternal(string title, string markdownContent, string documentType)
    {
        try
        {
            // Simply call the service method that is now used in the main orchestrator
            var pdfBytes = await _caseGenerationService.GenerateTestPdfAsync(title, markdownContent, documentType);
            var pdfBase64 = Convert.ToBase64String(pdfBytes);
            
            return (pdfBase64, documentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GenerateTestPdfAsync");
            throw;
        }
    }
}
