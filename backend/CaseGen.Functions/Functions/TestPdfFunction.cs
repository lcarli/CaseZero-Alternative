using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;

namespace CaseGen.Functions.Functions;

public class TestPdfFunction
{
    private readonly ILogger<TestPdfFunction> _logger;
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IStorageService _storageService;
    private readonly IPdfRenderingService _pdfRenderingService;

    public TestPdfFunction(
        ILogger<TestPdfFunction> logger, 
        ICaseGenerationService caseGenerationService, 
        IStorageService storageService,
        IPdfRenderingService pdfRenderingService)
    {
        _logger = logger;
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
        _pdfRenderingService = pdfRenderingService;
    }

    /// <summary>
    /// Test endpoint for PDF generation using real case data from storage
    /// GET /api/test/pdf/real?caseId=CASE-20251027-b442dc10&docId=doc_police_001
    /// </summary>
    [Function("TestPdfGenerationFromRealCase")]
    public async Task<HttpResponseData> TestPdfGenerationFromRealCase(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "test/pdf/real")] HttpRequestData req)
    {
        _logger.LogInformation("TestPdfGenerationFromRealCase function processed a request.");

        try
        {
            // Parse query parameters
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var caseId = query["caseId"];
            var docId = query["docId"];

            if (string.IsNullOrEmpty(caseId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Missing required parameter: caseId");
                return badResponse;
            }

            _logger.LogInformation("Testing PDF generation for case: {CaseId}, document: {DocId}", caseId, docId);

            // If no docId specified, list available documents
            if (string.IsNullOrEmpty(docId))
            {
                return await ListAvailableDocuments(req, caseId);
            }

            // Load document from storage
            var documentJson = await LoadDocumentFromStorage(caseId, docId);
            
            if (string.IsNullOrEmpty(documentJson))
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Document not found: {docId} in case {caseId}");
                return notFoundResponse;
            }

            // Parse document JSON to extract title, content, and type
            var doc = JsonDocument.Parse(documentJson);
            var root = doc.RootElement;
            
            var title = root.GetProperty("title").GetString() ?? "Untitled Document";
            var documentType = root.GetProperty("type").GetString() ?? "general";
            
            // Build markdown content from sections
            var markdownBuilder = new StringBuilder();
            if (root.TryGetProperty("sections", out var sections))
            {
                foreach (var section in sections.EnumerateArray())
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
            }
            var markdownContent = markdownBuilder.ToString();

            _logger.LogInformation("Generating PDF for document type: {DocumentType}, title: {Title}", documentType, title);

            // Generate PDF using the test PDF method (returns bytes directly)
            var pdfBytes = await _pdfRenderingService.GenerateTestPdfAsync(title, markdownContent, documentType);

            _logger.LogInformation("PDF generated successfully, size: {Size} bytes", pdfBytes.Length);

            // Save PDF to test-pdfs container
            var fileName = $"test-real-{caseId}-{docId}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
            var containerName = "test-pdfs";
            var blobUrl = await _storageService.SaveFileAsync(containerName, fileName, pdfBytes);

            _logger.LogInformation("PDF generated from real case data and saved to: {BlobUrl}", blobUrl);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var result = new
            {
                success = true,
                caseId,
                docId,
                pdfBlobUrl = blobUrl,
                fileName,
                message = "PDF generated successfully from real case data"
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestPdfGenerationFromRealCase");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}\n\nStack: {ex.StackTrace}");
            return errorResponse;
        }
    }

    private async Task<HttpResponseData> ListAvailableDocuments(HttpRequestData req, string caseId)
    {
        try
        {
            var documents = await LoadAvailableDocuments(caseId);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var result = new
            {
                caseId,
                availableDocuments = documents,
                message = "Specify a docId parameter to generate PDF for a specific document",
                example = $"/api/test/pdf/real?caseId={caseId}&docId={documents.FirstOrDefault()}"
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error listing documents: {ex.Message}");
            return errorResponse;
        }
    }

    private async Task<string?> LoadDocumentFromStorage(string caseId, string docId)
    {
        try
        {
            // Load from bundles container (Azure Storage / Azurite)
            var containerName = "bundles";
            var blobPath = $"{caseId}/documents/{docId}.json";
            
            _logger.LogInformation("Attempting to load document from blob: container={Container}, path={Path}", 
                containerName, blobPath);
            
            try
            {
                var content = await _storageService.GetFileAsync(containerName, blobPath);
                if (!string.IsNullOrEmpty(content))
                {
                    _logger.LogInformation("Successfully loaded document from blob storage: {Path}", blobPath);
                    return content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load from blob storage container {Container}, path {Path}", 
                    containerName, blobPath);
            }

            _logger.LogWarning("Document not found in blob storage: {DocId} in case {CaseId}", docId, caseId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading document from storage");
            throw;
        }
    }

    private async Task<List<string>> LoadAvailableDocuments(string caseId)
    {
        var documents = new List<string>();

        try
        {
            // Try local test-output path first
            var localPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "../../../../test-output/bundles",
                caseId,
                "documents"
            );

            if (Directory.Exists(localPath))
            {
                var files = Directory.GetFiles(localPath, "*.json");
                documents.AddRange(files.Select(f => Path.GetFileNameWithoutExtension(f)));
                _logger.LogInformation("Found {Count} documents in local path: {Path}", documents.Count, localPath);
            }
            else
            {
                _logger.LogWarning("Local documents directory not found: {Path}", localPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available documents");
        }

        return documents;
    }

    /// <summary>
    /// Original test endpoint for PDF generation using custom test data
    /// POST /api/test/pdf
    /// </summary>
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
