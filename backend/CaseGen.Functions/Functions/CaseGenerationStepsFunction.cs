using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;
using System.Net;

namespace CaseGen.Functions.Functions;

public class CaseGenerationStepsFunction
{
    private readonly ILogger<CaseGenerationStepsFunction> _logger;
    private readonly ICaseGenerationService _caseGenerationService;

    public CaseGenerationStepsFunction(
        ILogger<CaseGenerationStepsFunction> logger,
        ICaseGenerationService caseGenerationService)
    {
        _logger = logger;
        _caseGenerationService = caseGenerationService;
    }

    [Function("PlanStep")]
    public async Task<HttpResponseData> PlanStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<CaseGenerationRequest>(requestBody ?? "{}");

            if (request == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body");
                return errorResponse;
            }

            var caseId = $"CASE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
            _logger.LogInformation("Starting plan step for case {CaseId}", caseId);

            var planResult = await _caseGenerationService.PlanCaseAsync(request, caseId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId,
                step = "plan",
                result = JsonSerializer.Deserialize<object>(planResult),
                raw = planResult
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute plan step");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("ExpandStep")]
    public async Task<HttpResponseData> ExpandStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<ExpandStepRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.PlanJson) || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId, planJson");
                return errorResponse;
            }

            _logger.LogInformation("Starting expand step for case {CaseId}", request.CaseId);

            var expandResult = await _caseGenerationService.ExpandCaseAsync(request.PlanJson, request.CaseId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "expand",
                result = JsonSerializer.Deserialize<object>(expandResult),
                raw = expandResult
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute expand step");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("DesignStep")]
    public async Task<HttpResponseData> DesignStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<DesignStepRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.PlanJson) || 
                string.IsNullOrEmpty(request.ExpandedJson) || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId, planJson, expandedJson");
                return errorResponse;
            }

            _logger.LogInformation("Starting design step for case {CaseId}", request.CaseId);

            var designResult = await _caseGenerationService.DesignCaseAsync(
                request.PlanJson, request.ExpandedJson, request.CaseId, request.Difficulty);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "design",
                result = JsonSerializer.Deserialize<object>(designResult),
                raw = designResult
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute design step");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateDocumentsStep")]
    public async Task<HttpResponseData> GenerateDocumentsStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<GenerateDocumentsStepRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.DesignJson) || 
                string.IsNullOrEmpty(request.CaseId) || request.DocumentSpecs == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId, designJson, documentSpecs");
                return errorResponse;
            }

            _logger.LogInformation("Starting generate documents step for case {CaseId}", request.CaseId);

            var documents = new List<object>();

            foreach (var spec in request.DocumentSpecs)
            {
                var documentJson = await _caseGenerationService.GenerateDocumentFromSpecAsync(
                    spec, request.DesignJson, request.CaseId, request.PlanJson, request.ExpandJson, request.Difficulty);
                
                documents.Add(new
                {
                    docId = spec.DocId,
                    result = JsonSerializer.Deserialize<object>(documentJson),
                    raw = documentJson
                });
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "generateDocuments",
                documentCount = documents.Count,
                documents
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute generate documents step");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateMediaStep")]
    public async Task<HttpResponseData> GenerateMediaStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<GenerateMediaStepRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.DesignJson) || 
                string.IsNullOrEmpty(request.CaseId) || request.MediaSpecs == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId, designJson, mediaSpecs");
                return errorResponse;
            }

            _logger.LogInformation("Starting generate media step for case {CaseId}", request.CaseId);

            var media = new List<object>();

            foreach (var spec in request.MediaSpecs)
            {
                var mediaJson = await _caseGenerationService.GenerateMediaFromSpecAsync(
                    spec, request.DesignJson, request.CaseId, request.PlanJson, request.ExpandJson, request.Difficulty);
                
                media.Add(new
                {
                    evidenceId = spec.EvidenceId,
                    result = JsonSerializer.Deserialize<object>(mediaJson),
                    raw = mediaJson
                });
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "generateMedia",
                mediaCount = media.Count,
                media
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute generate media step");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("NormalizeStep")]
    public async Task<HttpResponseData> NormalizeStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<NormalizeStepRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId) || 
                request.Documents == null || request.Media == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId, documents[], media[]");
                return errorResponse;
            }

            _logger.LogInformation("Starting normalize step for case {CaseId}", request.CaseId);

            var normalizationInput = new NormalizationInput
            {
                CaseId = request.CaseId,
                Documents = request.Documents,
                Media = request.Media,
                Difficulty = request.Difficulty,
                Timezone = request.Timezone ?? "UTC"
            };

            var normalizeResult = await _caseGenerationService.NormalizeCaseDeterministicAsync(normalizationInput);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "normalize",
                result = normalizeResult
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute normalize step");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}

// Request models for the step endpoints
public record ExpandStepRequest(string CaseId, string PlanJson);

public record DesignStepRequest(string CaseId, string PlanJson, string ExpandedJson, string? Difficulty = null);

public record GenerateDocumentsStepRequest(
    string CaseId, 
    string DesignJson, 
    DocumentSpec[] DocumentSpecs,
    string? PlanJson = null,
    string? ExpandJson = null,
    string? Difficulty = null);

public record GenerateMediaStepRequest(
    string CaseId, 
    string DesignJson, 
    MediaSpec[] MediaSpecs,
    string? PlanJson = null,
    string? ExpandJson = null,
    string? Difficulty = null);

public record NormalizeStepRequest(
    string CaseId, 
    string[] Documents, 
    string[] Media,
    string? Difficulty = null,
    string? Timezone = null);
