using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using Microsoft.DurableTask.Client;

namespace CaseGen.Functions.Functions;

public class CaseGenerationStepsFunction
{
    private readonly ILogger<CaseGenerationStepsFunction> _logger;
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;

    public CaseGenerationStepsFunction(
        ILogger<CaseGenerationStepsFunction> logger,
        ICaseGenerationService caseGenerationService,
        IStorageService storageService,
        ICaseLoggingService caseLogging)
    {
        _logger = logger;
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
        _caseLogging = caseLogging;
    }

    private static string BuildStatusUri(HttpRequestData req, string route, string instanceId)
    {
        var baseUri = new Uri($"{req.Url.Scheme}://{req.Url.Authority}");
        var statusUri = new Uri(baseUri, $"/api/{route}/{instanceId}");
        return statusUri.ToString();
    }

    [Function("PlanStepStatus")]
    public async Task<HttpResponseData> PlanStepStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "PlanStepStatus/{instanceId}")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient durableClient)
    {
        return await BuildStatusResponseAsync<PlanStepDurableResult>(req, instanceId, durableClient, "PlanStep");
    }

    [Function("ExpandStepStatus")]
    public async Task<HttpResponseData> ExpandStepStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ExpandStepStatus/{instanceId}")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient durableClient)
    {
        return await BuildStatusResponseAsync<ExpandStepDurableResult>(req, instanceId, durableClient, "ExpandStep");
    }

    [Function("DesignStepStatus")]
    public async Task<HttpResponseData> DesignStepStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "DesignStepStatus/{instanceId}")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient durableClient)
    {
        return await BuildStatusResponseAsync<DesignStepDurableResult>(req, instanceId, durableClient, "DesignStep");
    }

    [Function("GenerateStepStatus")]
    public async Task<HttpResponseData> GenerateStepStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GenerateStepStatus/{instanceId}")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient durableClient)
    {
        return await BuildStatusResponseAsync<GenerateStepDurableResult>(req, instanceId, durableClient, "GenerateStep");
    }

    [Function("NormalizeStepStatus")]
    public async Task<HttpResponseData> NormalizeStepStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "NormalizeStepStatus/{instanceId}")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient durableClient)
    {
        return await BuildStatusResponseAsync<NormalizeStepDurableResult>(req, instanceId, durableClient, "NormalizeStep");
    }

    private static async Task<HttpResponseData> BuildStatusResponseAsync<TResult>(
        HttpRequestData req,
        string instanceId,
        DurableTaskClient durableClient,
        string stepName)
    {
        try
        {
            var metadata = await durableClient.GetInstanceAsync(instanceId);
            if (metadata == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"{stepName} instance {instanceId} not found");
                return notFound;
            }

            TResult? output = default;
            if (!string.IsNullOrEmpty(metadata.SerializedOutput))
            {
                output = JsonSerializer.Deserialize<TResult>(metadata.SerializedOutput);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                metadata.InstanceId,
                runtimeStatus = metadata.RuntimeStatus.ToString(),
                metadata.CreatedAt,
                metadata.LastUpdatedAt,
                metadata.SerializedCustomStatus,
                output
            });
            return response;
        }
        catch (Exception ex)
        {
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync($"Failed to query {stepName} status: {ex.Message}");
            return error;
        }
    }

    [Function("PlanStepOnly")]
    public async Task<HttpResponseData> PlanStepOnly(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        CaseGenerationRequest? request = null;
        string? caseId = null;
        string? traceId = null;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            request = JsonSerializer.Deserialize<CaseGenerationRequest>(requestBody ?? "{}");

            if (request == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body");
                return errorResponse;
            }

            caseId = $"CASE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
            traceId = Guid.NewGuid().ToString("N");
            var requestedAt = DateTime.UtcNow;
            var difficulty = request.Difficulty ?? "Rookie";
            var timezone = request.Timezone ?? "UTC";

            _logger.LogInformation(
                "[STEP-BY-STEP] Queueing PlanStepOnly for case {CaseId} ({Difficulty}/{Timezone})",
                caseId,
                difficulty,
                timezone);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Plan,
                Step = "PlanStepOnly",
                Activity = nameof(PlanStepDurableOrchestrator),
                TraceId = traceId,
                Status = "Queued",
                TimestampUtc = requestedAt,
                Message = "PlanStepOnly enqueued for Durable execution",
                Data = new
                {
                    request.Title,
                    request.Difficulty,
                    request.Timezone
                }
            });

            var input = new PlanStepDurableInput
            {
                CaseId = caseId,
                TraceId = traceId,
                RequestedAtUtc = requestedAt,
                Request = request
            };

            var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(PlanStepDurableOrchestrator),
                input);

            var statusUri = BuildStatusUri(req, "PlanStepStatus", instanceId);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                message = "Plan step accepted. Poll the status URL to track progress.",
                caseId,
                instanceId,
                statusQueryGetUri = statusUri,
                nextStep = "Call ExpandStepByCaseId once plan status reports Completed."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to queue PlanStepOnly");
            if (!string.IsNullOrEmpty(caseId))
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Plan,
                    Step = "PlanStepOnly",
                    Activity = nameof(PlanStepDurableOrchestrator),
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "PlanStepOnly queueing failed",
                    Error = ex.Message
                });
            }

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
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

    [Function("ExpandStepByCaseId")]
    public async Task<HttpResponseData> ExpandStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        string? caseId = null;
        string? traceId = null;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<NormalizeStepDurableRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            caseId = request.CaseId;
            traceId = Guid.NewGuid().ToString("N");
            var requestedAt = DateTime.UtcNow;

            _logger.LogInformation("[STEP-BY-STEP] Queueing ExpandStepByCaseId for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Expand,
                Step = "ExpandStepByCaseId",
                Activity = nameof(ExpandStepDurableOrchestrator),
                TraceId = traceId,
                Status = "Queued",
                TimestampUtc = requestedAt,
                Message = "ExpandStepByCaseId enqueued for Durable execution",
                Data = new
                {
                    filesToLoad = new[]
                    {
                        $"cases/{caseId}/plan.json"
                    }
                }
            });

            var input = new ExpandStepDurableInput
            {
                CaseId = caseId,
                TraceId = traceId,
                RequestedAtUtc = requestedAt
            };

            var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(ExpandStepDurableOrchestrator),
                input);

            var statusUri = BuildStatusUri(req, "ExpandStepStatus", instanceId);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                message = "Expand step accepted. Poll the status URL to track progress.",
                caseId,
                instanceId,
                statusQueryGetUri = statusUri,
                nextStep = "Call DesignStepByCaseId after the expand status reports Completed."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to queue ExpandStepByCaseId");
            if (!string.IsNullOrEmpty(caseId))
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Expand,
                    Step = "ExpandStepByCaseId",
                    Activity = nameof(ExpandStepDurableOrchestrator),
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "ExpandStepByCaseId queueing failed",
                    Error = ex.Message
                });
            }

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("DesignStepByCaseId")]
    public async Task<HttpResponseData> DesignStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        string? caseId = null;
        string? traceId = null;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<CaseIdOnlyRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            caseId = request.CaseId;
            traceId = Guid.NewGuid().ToString("N");
            var requestedAt = DateTime.UtcNow;

            _logger.LogInformation("[STEP-BY-STEP] Queueing DesignStepByCaseId for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                Activity = nameof(DesignStepDurableOrchestrator),
                TraceId = traceId,
                Status = "Queued",
                TimestampUtc = requestedAt,
                Message = "DesignStepByCaseId enqueued for Durable execution",
                Data = new
                {
                    filesToLoad = new[]
                    {
                        $"cases/{caseId}/plan.json",
                        $"cases/{caseId}/expand.json"
                    }
                }
            });

            var input = new DesignStepDurableInput
            {
                CaseId = caseId,
                TraceId = traceId,
                RequestedAtUtc = requestedAt
            };

            var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(DesignStepDurableOrchestrator),
                input);

            _logger.LogInformation(
                "[STEP-BY-STEP] DesignStepByCaseId orchestration started for case {CaseId} (InstanceId: {InstanceId})",
                caseId,
                instanceId);

            var statusUri = BuildStatusUri(req, "DesignStepStatus", instanceId);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                message = "Design step accepted. Poll the status URL to track progress.",
                caseId,
                instanceId,
                statusQueryGetUri = statusUri,
                nextStep = "Call GenerateStepByCaseId after the design status reports Completed."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to queue DesignStepByCaseId");
            if (!string.IsNullOrEmpty(caseId))
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Design,
                    Step = "DesignStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "DesignStepByCaseId queueing failed",
                    Error = ex.Message
                });
            }
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateStepByCaseId")]
    public async Task<HttpResponseData> GenerateStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        string? caseId = null;
        string? traceId = null;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<GenerateStepByCaseIdRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            caseId = request.CaseId;
            traceId = Guid.NewGuid().ToString("N");
            var requestedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "[STEP-BY-STEP] Queueing GenerateStepByCaseId for case {CaseId} (generateImages: {GenerateImages}, renderFiles: {RenderFiles})",
                caseId,
                request.GenerateImages,
                request.RenderFiles);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                Activity = nameof(GenerateStepDurableOrchestrator),
                TraceId = traceId,
                Status = "Queued",
                TimestampUtc = requestedAt,
                Message = "GenerateStepByCaseId enqueued for Durable execution",
                Data = new
                {
                    filesToLoad = new[] { $"cases/{caseId}/design.json" },
                    options = new
                    {
                        request.GenerateImages,
                        request.RenderFiles
                    }
                }
            });

            var inputPayload = new GenerateStepDurableInput
            {
                CaseId = caseId,
                TraceId = traceId,
                GenerateImages = request.GenerateImages,
                RenderFiles = request.RenderFiles,
                RequestedAtUtc = requestedAt
            };

            var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(GenerateStepDurableOrchestrator),
                inputPayload);

            var statusUri = BuildStatusUri(req, "GenerateStepStatus", instanceId);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                message = "Generate step accepted. Poll the status URL to track progress.",
                caseId,
                instanceId,
                statusQueryGetUri = statusUri,
                options = new
                {
                    request.GenerateImages,
                    request.RenderFiles
                },
                nextStep = "Call NormalizeStepByCaseId after the generate status reports Completed."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute GenerateStepByCaseId");
            if (!string.IsNullOrEmpty(caseId))
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.GenDocs,
                    Step = "GenerateStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "GenerateStepByCaseId failed",
                    Error = ex.Message
                });
            }
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

    [Function("GetGenerateProgressByCaseId")]
    public async Task<HttpResponseData> GetGenerateProgressByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<CaseIdOnlyRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            _logger.LogInformation("[STEP-BY-STEP] Checking generate progress for case {CaseId}", request.CaseId);

            // Load design.json to get total counts
            string designJson;
            try
            {
                designJson = await _storageService.GetFileAsync("cases", $"{request.CaseId}/design.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to load design.json for case {CaseId}", request.CaseId);
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Design not found for case {request.CaseId}");
                return errorResponse;
            }

            // Parse design
            var specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (specs == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Failed to parse design.json");
                return errorResponse;
            }

            // Count generated files
            var documentFiles = await _storageService.ListFilesAsync("cases", $"{request.CaseId}/generate/documents/");
            var mediaFiles = await _storageService.ListFilesAsync("cases", $"{request.CaseId}/generate/media/");

            var documentsGenerated = documentFiles.Count();
            var mediaGenerated = mediaFiles.Count();
            var totalDocuments = specs.DocumentSpecs.Length;
            var totalMedia = specs.MediaSpecs.Length;

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                progress = new
                {
                    documents = new
                    {
                        generated = documentsGenerated,
                        total = totalDocuments,
                        complete = documentsGenerated >= totalDocuments,
                        percentage = totalDocuments > 0 ? (documentsGenerated * 100.0 / totalDocuments) : 0
                    },
                    media = new
                    {
                        generated = mediaGenerated,
                        total = totalMedia,
                        complete = mediaGenerated >= totalMedia,
                        percentage = totalMedia > 0 ? (mediaGenerated * 100.0 / totalMedia) : 0
                    },
                    overall = new
                    {
                        generated = documentsGenerated + mediaGenerated,
                        total = totalDocuments + totalMedia,
                        complete = documentsGenerated >= totalDocuments && mediaGenerated >= totalMedia,
                        percentage = (totalDocuments + totalMedia) > 0 
                            ? ((documentsGenerated + mediaGenerated) * 100.0 / (totalDocuments + totalMedia)) 
                            : 0
                    }
                },
                nextStep = (documentsGenerated >= totalDocuments && mediaGenerated >= totalMedia)
                    ? "All generation complete. Next: NormalizeStepByCaseId"
                    : "Generation in progress. Keep polling GenerateStepStatus and re-run GetGenerateProgressByCaseId for updates."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to get generate progress");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("NormalizeStepByCaseId")]
    public async Task<HttpResponseData> NormalizeStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        string? caseId = null;
        string? traceId = null;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<CaseIdOnlyRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            caseId = request.CaseId;
            traceId = Guid.NewGuid().ToString("N");
            var requestedAt = DateTime.UtcNow;
            var timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone!.Trim();
            var maxQaIterations = Math.Clamp(request.MaxQaIterations ?? 3, 1, 5);
            var difficulty = request.Difficulty;

            _logger.LogInformation("[STEP-BY-STEP] Queueing NormalizeStepByCaseId for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                Activity = nameof(NormalizeStepDurableOrchestrator),
                TraceId = traceId,
                Status = "Queued",
                TimestampUtc = requestedAt,
                Message = "NormalizeStepByCaseId enqueued for Durable execution",
                Data = new
                {
                    filesToLoad = new[]
                    {
                        $"cases/{caseId}/plan.json",
                        $"cases/{caseId}/expand.json",
                        $"cases/{caseId}/design.json",
                        $"cases/{caseId}/generate/documents/*",
                        $"cases/{caseId}/generate/media/*"
                    },
                    timezone,
                    difficulty,
                    maxQaIterations
                }
            });

            var inputPayload = new NormalizeStepDurableInput
            {
                CaseId = caseId,
                TraceId = traceId,
                RequestedAtUtc = requestedAt,
                Timezone = timezone,
                Difficulty = difficulty,
                MaxQaIterations = maxQaIterations
            };

            var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(NormalizeStepDurableOrchestrator),
                inputPayload);

            _logger.LogInformation(
                "[STEP-BY-STEP] NormalizeStepByCaseId orchestration started for case {CaseId} (InstanceId: {InstanceId})",
                caseId,
                instanceId);

            var statusUri = BuildStatusUri(req, "NormalizeStepStatus", instanceId);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                message = "Normalize step accepted. Poll the status URL to track progress.",
                caseId,
                instanceId,
                statusQueryGetUri = statusUri,
                options = new
                {
                    timezone,
                    difficulty = difficulty ?? "auto",
                    maxQaIterations
                },
                filesNeeded = new[]
                {
                    "plan.json",
                    "expand.json",
                    "design.json",
                    "generate/documents/*.json",
                    "generate/media/*.json"
                }
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to queue NormalizeStepByCaseId");
            if (!string.IsNullOrEmpty(caseId))
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Normalize,
                    Step = "NormalizeStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "NormalizeStepByCaseId queueing failed",
                    Error = ex.Message
                });
            }
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}

// Request models for the step endpoints
public record CaseIdOnlyRequest([property: JsonPropertyName("caseId")] string CaseId);

public record GenerateStepByCaseIdRequest(
    [property: JsonPropertyName("caseId")] string CaseId,
    [property: JsonPropertyName("generateImages")] bool GenerateImages = false,
    [property: JsonPropertyName("renderFiles")] bool RenderFiles = false);

public record NormalizeStepDurableRequest(
    [property: JsonPropertyName("caseId")] string CaseId,
    [property: JsonPropertyName("timezone")] string? Timezone = null,
    [property: JsonPropertyName("difficulty")] string? Difficulty = null,
    [property: JsonPropertyName("maxQaIterations")] int? MaxQaIterations = null);

public record ExpandStepRequest(string CaseId, string PlanJson);

public record VisualRegistryStepRequest([property: JsonPropertyName("caseId")] string CaseId);

public record MasterReferencesStepRequest([property: JsonPropertyName("caseId")] string CaseId);

public record DesignStepRequest(string CaseId, string PlanJson, string ExpandedJson, string? Difficulty = null);

public record NormalizeStepRequest(
    string CaseId, 
    string[] Documents, 
    string[] Media,
    string? Difficulty = null,
    string? Timezone = null);
