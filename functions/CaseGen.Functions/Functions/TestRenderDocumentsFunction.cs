using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Test function to render all documents (JSON to PDF) for an existing case
/// </summary>
public class TestRenderDocumentsFunction
{
    private readonly ILogger<TestRenderDocumentsFunction> _logger;
    private readonly IContextManager _contextManager;
    private readonly ICaseGenerationService _caseGenerationService;

    public TestRenderDocumentsFunction(
        ILogger<TestRenderDocumentsFunction> logger,
        IContextManager contextManager,
        ICaseGenerationService caseGenerationService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _caseGenerationService = caseGenerationService;
    }

    [Function("TestRenderDocuments")]
    public async Task<HttpResponseData> TestRenderDocuments(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("TestRenderDocuments function triggered");

        try
        {
            // Parse request body to get caseId
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Received request body: {Body}", requestBody);
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var requestData = JsonSerializer.Deserialize<TestRenderDocumentsRequest>(requestBody, options);

            if (string.IsNullOrEmpty(requestData?.CaseId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync($"CaseId is required in request body. Received: {requestBody}");
                return badResponse;
            }

            var caseId = requestData.CaseId;
            _logger.LogInformation("Starting document rendering test for case {CaseId}", caseId);

            // Query all documents from bundles/{caseId}/documents/*
            _logger.LogInformation("=== Querying bundles documents ===");
            var documentsQueryRaw = await _contextManager.QueryContextAsync<string>(
                caseId, 
                "documents/*");
            var documentsList = documentsQueryRaw.ToList();

            if (!documentsList.Any())
            {
                _logger.LogWarning("No documents found for case {CaseId}", caseId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"No documents found for case {caseId}. Make sure the case has documents/*.json files in bundles container.");
                return notFoundResponse;
            }

            _logger.LogInformation("Found {Count} documents", documentsList.Count);

            // Parse documents and render to PDF
            var results = new List<DocumentRenderResultDto>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var documentData in documentsList)
            {
                try
                {
                    var docJson = documentData.Data;
                    var docElement = JsonDocument.Parse(docJson).RootElement;
                    
                    var docId = docElement.TryGetProperty("docId", out var docIdProp) 
                        ? docIdProp.GetString() ?? "unknown" 
                        : "unknown";
                    var title = docElement.TryGetProperty("title", out var titleProp) 
                        ? titleProp.GetString() 
                        : null;
                    var type = docElement.TryGetProperty("type", out var typeProp) 
                        ? typeProp.GetString() 
                        : null;

                    _logger.LogInformation("Rendering document {DocId}: {Title} (type: {Type})", docId, title, type);

                    // Render the document to PDF
                    var pdfPath = await _caseGenerationService.RenderDocumentFromJsonAsync(
                        docId, 
                        docJson, 
                        caseId);

                    results.Add(new DocumentRenderResultDto
                    {
                        DocId = docId,
                        Title = title,
                        Type = type,
                        Success = true,
                        PdfPath = pdfPath
                    });
                    successCount++;

                    _logger.LogInformation("Successfully rendered document {DocId} to {PdfPath}", docId, pdfPath);
                }
                catch (Exception ex)
                {
                    var docId = "unknown";
                    try
                    {
                        var docElement = JsonDocument.Parse(documentData.Data).RootElement;
                        docId = docElement.TryGetProperty("docId", out var docIdProp) 
                            ? docIdProp.GetString() ?? "unknown" 
                            : "unknown";
                    }
                    catch { }

                    _logger.LogError(ex, "Error rendering document {DocId}", docId);
                    results.Add(new DocumentRenderResultDto
                    {
                        DocId = docId,
                        Success = false,
                        Error = ex.Message
                    });
                    failureCount++;
                }
            }

            // Create success response
            _logger.LogInformation("=== Document Rendering Summary ===");
            _logger.LogInformation("Total documents: {Total}", documentsList.Count);
            _logger.LogInformation("Successful: {Success}", successCount);
            _logger.LogInformation("Failed: {Failed}", failureCount);

            var response = req.CreateResponse(HttpStatusCode.OK);
            
            var responseData = new
            {
                caseId = caseId,
                totalDocuments = documentsList.Count,
                successCount = successCount,
                failureCount = failureCount,
                results = results
            };

            await response.WriteAsJsonAsync(responseData);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestRenderDocuments");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}\n\nStack: {ex.StackTrace}");
            return errorResponse;
        }
    }
}

// Request DTO
public record TestRenderDocumentsRequest
{
    public string? CaseId { get; init; }
}

// Response DTO
public record DocumentRenderResultDto
{
    public required string DocId { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public bool Success { get; init; }
    public string? PdfPath { get; init; }
    public string? Error { get; init; }
}
