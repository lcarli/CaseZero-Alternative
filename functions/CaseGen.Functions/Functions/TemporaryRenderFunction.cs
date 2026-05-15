using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions;

public class TemporaryRenderFunction
{
    private readonly IStorageService _storage;
    private readonly IPdfRenderingService _pdfRenderer;
    private readonly ILogger<TemporaryRenderFunction> _logger;

    public TemporaryRenderFunction(
        IStorageService storage,
        IPdfRenderingService pdfRenderer,
        ILogger<TemporaryRenderFunction> logger)
    {
        _storage = storage;
        _pdfRenderer = pdfRenderer;
        _logger = logger;
    }

    [Function("TempRenderDocument")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "TempRenderDocument")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body is required");
            }

            TempRenderRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<TempRenderRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize TempRenderRequest");
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request payload");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.BlobPath))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "blobPath is required");
            }

            var (container, path) = ParseBlobPath(request.BlobPath);
            if (string.IsNullOrEmpty(container) || string.IsNullOrEmpty(path))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "blobPath must be in the format 'container/path/to/file.json'");
            }

            _logger.LogInformation("[TEMP-RENDER] Reading blob {Container}/{Path}", container, path);
            var blobContent = await _storage.GetFileAsync(container, path);
            if (string.IsNullOrWhiteSpace(blobContent))
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Blob not found or empty");
            }

            var docId = Path.GetFileNameWithoutExtension(path);
            var caseId = request.CaseId ?? ExtractCaseIdFromPath(path);

            var renderResult = await _pdfRenderer.RenderDocumentFromJsonAsync(docId, blobContent, caseId ?? "TEST-CASE");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(renderResult);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TEMP-RENDER] Failed to render document");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteStringAsync(JsonSerializer.Serialize(new { error = message }));
        return response;
    }

    private static (string Container, string Path) ParseBlobPath(string blobPath)
    {
        var trimmed = blobPath.Trim('/');
        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex < 0)
        {
            return (string.Empty, string.Empty);
        }

        var container = trimmed[..slashIndex];
        var path = trimmed[(slashIndex + 1)..];
        return (container, path);
    }

    private static string? ExtractCaseIdFromPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[0] : null;
    }

    private record TempRenderRequest(string BlobPath, string? CaseId);
}
