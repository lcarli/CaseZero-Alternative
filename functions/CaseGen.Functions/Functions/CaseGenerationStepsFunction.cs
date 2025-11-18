using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;

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

    [Function("PlanStepOnly")]
    public async Task<HttpResponseData> PlanStepOnly(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        string? caseId = null;
        string? traceId = null;
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

            caseId = $"CASE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
            var startTime = DateTime.UtcNow;
            var difficulty = request.Difficulty ?? "Rookie";
            var timezone = request.Timezone ?? "UTC";
            traceId = Guid.NewGuid().ToString("N");
            
            _logger.LogInformation("[STEP-BY-STEP] Starting PlanStepOnly for case {CaseId} - Difficulty: {Difficulty}", 
                caseId, difficulty);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Plan,
                Step = "PlanStepOnly",
                Activity = "PlanCaseAsync",
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "PlanStepOnly invoked",
                Data = new
                {
                    difficulty,
                    timezone,
                    requestTitle = request.Title,
                    requestDifficulty = request.Difficulty,
                    requestTimezone = request.Timezone
                }
            });

            var planCallStart = DateTime.UtcNow;
            var planResult = await _caseGenerationService.PlanCaseAsync(request, caseId)
                ?? throw new InvalidOperationException("Plan generation returned null response");
            var planCallDuration = DateTime.UtcNow - planCallStart;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Plan,
                Step = "PlanStepOnly",
                Activity = "PlanCaseAsync",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(planCallDuration.TotalMilliseconds, 2),
                Message = "PlanStepOnly generation finished",
                Data = new
                {
                    difficulty,
                    timezone,
                    outputLength = planResult.Length
                }
            });
            
            // Save plan.json to storage
            var planPath = $"{caseId}/plan.json";
            await _storageService.SaveFileAsync("cases", planPath, planResult);
            _logger.LogInformation("[STEP-BY-STEP] Saved plan.json to storage for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Payload,
                Step = "PlanStepOnly",
                Message = "plan.json saved via PlanStepOnly",
                PayloadReference = $"cases/{planPath}"
            });
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[STEP-BY-STEP] PlanStepOnly completed for case {CaseId} - Duration: {Duration}s", 
                caseId, duration.TotalSeconds);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Plan,
                Step = "PlanStepOnly",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "PlanStepOnly request completed",
                Data = new
                {
                    durationSeconds = duration.TotalSeconds,
                    difficulty,
                    timezone
                }
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId,
                step = "plan",
                difficulty = request.Difficulty ?? "Rookie",
                timezone = request.Timezone ?? "UTC",
                durationSeconds = duration.TotalSeconds,
                result = JsonSerializer.Deserialize<object>(planResult),
                raw = planResult,
                nextStep = $"Use this caseId in ExpandStepByCaseId: {caseId}"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute PlanStepOnly");
            if (!string.IsNullOrWhiteSpace(caseId))
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Plan,
                    Step = "PlanStepOnly",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "PlanStepOnly failed",
                    Error = ex.Message
                });
            }
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

    [Function("ExpandStepByCaseId")]
    public async Task<HttpResponseData> ExpandStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
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
            var startTime = DateTime.UtcNow;
            traceId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("[STEP-BY-STEP] Starting ExpandStepByCaseId for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Expand,
                Step = "ExpandStepByCaseId",
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "ExpandStepByCaseId invoked",
                Data = new
                {
                    filesToLoad = new[] { $"cases/{caseId}/plan.json" }
                }
            });

            // Load plan.json from storage
            string planJson;
            var planPath = $"{caseId}/plan.json";
            try
            {
                planJson = await _storageService.GetFileAsync("cases", planPath);
                _logger.LogInformation("[STEP-BY-STEP] Loaded plan.json from storage for case {CaseId}", caseId);

                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "ExpandStepByCaseId",
                    Message = "plan.json loaded",
                    PayloadReference = $"cases/{planPath}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to load plan.json for case {CaseId}", caseId);
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Expand,
                    Step = "ExpandStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "Failed to load plan.json",
                    Error = ex.Message
                });
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Plan not found for case {caseId}. Make sure PlanStepOnly was executed first.");
                return errorResponse;
            }

            // Execute expand
            var expandCallStart = DateTime.UtcNow;
            var expandResult = await _caseGenerationService.ExpandCaseAsync(planJson, caseId)
                ?? throw new InvalidOperationException("Expand generation returned null response");
            var expandCallDuration = DateTime.UtcNow - expandCallStart;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Expand,
                Step = "ExpandStepByCaseId",
                Activity = "ExpandCaseAsync",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(expandCallDuration.TotalMilliseconds, 2),
                Message = "ExpandCaseAsync completed",
                Data = new
                {
                    outputLength = expandResult.Length
                }
            });
            
            // Save expand.json to storage
            var expandPath = $"{caseId}/expand.json";
            await _storageService.SaveFileAsync("cases", expandPath, expandResult);
            _logger.LogInformation("[STEP-BY-STEP] Saved expand.json to storage for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Payload,
                Step = "ExpandStepByCaseId",
                Message = "expand.json saved via ExpandStepByCaseId",
                PayloadReference = $"cases/{expandPath}"
            });

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[STEP-BY-STEP] ExpandStepByCaseId completed for case {CaseId} - Duration: {Duration}s", 
                caseId, duration.TotalSeconds);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Expand,
                Step = "ExpandStepByCaseId",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "ExpandStepByCaseId request completed",
                Data = new
                {
                    durationSeconds = duration.TotalSeconds,
                    filesLoaded = new[] { "plan.json" },
                    filesSaved = new[] { "expand.json" }
                }
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId,
                step = "expand",
                durationSeconds = duration.TotalSeconds,
                filesLoaded = new[] { "plan.json" },
                filesSaved = new[] { "expand/suspects.json", "expand/evidence.json", "expand/timeline.json", "expand/witnesses.json" },
                result = JsonSerializer.Deserialize<object>(expandResult),
                raw = expandResult,
                nextStep = $"Use this caseId in DesignStepByCaseId: {caseId}"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute ExpandStepByCaseId");
            if (!string.IsNullOrEmpty(caseId))
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Expand,
                    Step = "ExpandStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "ExpandStepByCaseId failed",
                    Error = ex.Message
                });
            }
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("DesignVisualRegistryStep")]
    public async Task<HttpResponseData> DesignVisualRegistryStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<VisualRegistryStepRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            _logger.LogInformation("Starting design visual registry step for case {CaseId}", request.CaseId);

            var registryResult = await _caseGenerationService.DesignVisualConsistencyRegistryAsync(request.CaseId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "designVisualRegistry",
                result = JsonSerializer.Deserialize<object>(registryResult),
                raw = registryResult
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute design visual registry step");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateMasterReferencesStep")]
    public async Task<HttpResponseData> GenerateMasterReferencesStep(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<MasterReferencesStepRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            _logger.LogInformation("Starting generate master references step for case {CaseId}", request.CaseId);

            var generatedCount = await _caseGenerationService.GenerateMasterReferencesAsync(request.CaseId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "generateMasterReferences",
                generatedCount,
                message = $"Successfully generated {generatedCount} master reference images"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute generate master references step");
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

    [Function("DesignStepByCaseId")]
    public async Task<HttpResponseData> DesignStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
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
            var startTime = DateTime.UtcNow;
            traceId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("[STEP-BY-STEP] Starting DesignStepByCaseId for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "DesignStepByCaseId invoked",
                Data = new
                {
                    filesToLoad = new[] 
                    {
                        $"cases/{caseId}/plan.json",
                        $"cases/{caseId}/expand.json"
                    }
                }
            });

            // Load plan.json from storage
            var planPath = $"{caseId}/plan.json";
            string planJson;
            try
            {
                planJson = await _storageService.GetFileAsync("cases", planPath);
                _logger.LogInformation("[STEP-BY-STEP] Loaded plan.json from storage for case {CaseId}", caseId);

                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "DesignStepByCaseId",
                    Message = "plan.json loaded",
                    PayloadReference = $"cases/{planPath}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to load plan.json for case {CaseId}", caseId);
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Design,
                    Step = "DesignStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "Failed to load plan.json",
                    Error = ex.Message
                });
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Plan not found for case {caseId}. Make sure PlanStepOnly was executed first.");
                return errorResponse;
            }

            // Load expand.json from storage
            var expandPath = $"{caseId}/expand.json";
            string expandJson;
            try
            {
                expandJson = await _storageService.GetFileAsync("cases", expandPath);
                _logger.LogInformation("[STEP-BY-STEP] Loaded expand.json from storage for case {CaseId}", caseId);

                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "DesignStepByCaseId",
                    Message = "expand.json loaded",
                    PayloadReference = $"cases/{expandPath}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to load expand.json for case {CaseId}", caseId);
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Design,
                    Step = "DesignStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "Failed to load expand.json",
                    Error = ex.Message
                });
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Expand not found for case {caseId}. Make sure ExpandStepByCaseId was executed first.");
                return errorResponse;
            }

            // Execute design
            var designCallStart = DateTime.UtcNow;
            var designResult = await _caseGenerationService.DesignCaseAsync(planJson, expandJson, caseId)
                ?? throw new InvalidOperationException("Design generation returned null response");
            var designCallDuration = DateTime.UtcNow - designCallStart;
            
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                Activity = "DesignCaseAsync",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(designCallDuration.TotalMilliseconds, 2),
                Message = "DesignCaseAsync completed",
                Data = new
                {
                    outputLength = designResult.Length
                }
            });
            
            // Save design.json to storage
            var designPath = $"{caseId}/design.json";
            await _storageService.SaveFileAsync("cases", designPath, designResult);
            _logger.LogInformation("[STEP-BY-STEP] Saved design.json to storage for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Payload,
                Step = "DesignStepByCaseId",
                Message = "design.json saved via DesignStepByCaseId",
                PayloadReference = $"cases/{designPath}"
            });

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[STEP-BY-STEP] DesignStepByCaseId completed for case {CaseId} - Duration: {Duration}s", 
                caseId, duration.TotalSeconds);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "DesignStepByCaseId request completed",
                Data = new
                {
                    durationSeconds = duration.TotalSeconds,
                    filesLoaded = new[] { "plan.json", "expand.json" },
                    filesSaved = new[] { "design.json" }
                }
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId,
                step = "design",
                durationSeconds = duration.TotalSeconds,
                filesLoaded = new[] { "plan.json", "expand.json" },
                filesSaved = new[] { "design.json" },
                result = JsonSerializer.Deserialize<object>(designResult),
                raw = designResult,
                nextStep = $"Use this caseId in GenerateStepByCaseId: {caseId}"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute DesignStepByCaseId");
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
                    Message = "DesignStepByCaseId failed",
                    Error = ex.Message
                });
            }
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

    [Function("GenerateStepByCaseId")]
    public async Task<HttpResponseData> GenerateStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
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
            var startTime = DateTime.UtcNow;
            traceId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("[STEP-BY-STEP] Starting GenerateStepByCaseId for case {CaseId} - generateImages: {GenerateImages}, renderFiles: {RenderFiles}", 
                caseId, request.GenerateImages, request.RenderFiles);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "GenerateStepByCaseId invoked",
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

            var designPath = $"{caseId}/design.json";
            string designJson;
            try
            {
                designJson = await _storageService.GetFileAsync("cases", designPath);
                _logger.LogInformation("[STEP-BY-STEP] Loaded design.json from storage for case {CaseId}", caseId);

                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "GenerateStepByCaseId",
                    Message = "design.json loaded",
                    PayloadReference = $"cases/{designPath}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to load design.json for case {CaseId}", caseId);
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.GenDocs,
                    Step = "GenerateStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "Failed to load design.json",
                    Error = ex.Message
                });
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Design not found for case {caseId}. Make sure DesignStepByCaseId was executed first.");
                return errorResponse;
            }

            DocumentAndMediaSpecs? specs;
            try
            {
                specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (specs == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to parse design.json");
                    return errorResponse;
                }
                
                _logger.LogInformation("[STEP-BY-STEP] Parsed design: {DocCount} documents, {MediaCount} media items", 
                    specs.DocumentSpecs.Length, specs.MediaSpecs.Length);

                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "GenerateStepByCaseId",
                    Message = "Design parsed for generation",
                    Data = new
                    {
                        documents = specs.DocumentSpecs.Length,
                        media = specs.MediaSpecs.Length
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to parse design.json for case {CaseId}", caseId);
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.GenDocs,
                    Step = "GenerateStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "Invalid design.json format",
                    Error = ex.Message
                });
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Invalid design.json format: {ex.Message}");
                return errorResponse;
            }

            var generatedDocs = 0;
            var generatedMedia = 0;

            _logger.LogInformation("[STEP-BY-STEP] Generating {Count} documents...", specs.DocumentSpecs.Length);
            var documentsStart = DateTime.UtcNow;
            foreach (var docSpec in specs.DocumentSpecs)
            {
                try
                {
                    var documentJson = await _caseGenerationService.GenerateDocumentFromSpecAsync(
                        docSpec, string.Empty, caseId, null, null, null);
                    generatedDocs++;
                    
                    if (generatedDocs % 5 == 0)
                    {
                        _logger.LogInformation("[STEP-BY-STEP] Generated {Current}/{Total} documents", generatedDocs, specs.DocumentSpecs.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[STEP-BY-STEP] Failed to generate document {DocId}", docSpec.DocId);
                    await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                    {
                        CaseId = caseId,
                        Category = LogCategory.WorkflowStep,
                        Phase = CaseGenerationSteps.GenDocs,
                        Step = "GenerateStepByCaseId",
                        TraceId = traceId,
                        Status = "Warning",
                        Message = $"Failed to generate document {docSpec.DocId}",
                        Error = ex.Message
                    });
                }
            }
            var documentsDuration = DateTime.UtcNow - documentsStart;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                Activity = "GenerateDocumentBatch",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(documentsDuration.TotalMilliseconds, 2),
                Message = "Document generation batch completed",
                Data = new
                {
                    requested = specs.DocumentSpecs.Length,
                    generated = generatedDocs,
                    failures = Math.Max(0, specs.DocumentSpecs.Length - generatedDocs)
                }
            });

            if (request.GenerateImages)
            {
                _logger.LogInformation("[STEP-BY-STEP] Generating {Count} media items...", specs.MediaSpecs.Length);
                var mediaStart = DateTime.UtcNow;
                foreach (var mediaSpec in specs.MediaSpecs)
                {
                    try
                    {
                        var mediaJson = await _caseGenerationService.GenerateMediaFromSpecAsync(
                            mediaSpec, string.Empty, caseId, null, null, null);
                        generatedMedia++;
                        
                        if (generatedMedia % 5 == 0)
                        {
                            _logger.LogInformation("[STEP-BY-STEP] Generated {Current}/{Total} media items", generatedMedia, specs.MediaSpecs.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[STEP-BY-STEP] Failed to generate media {EvidenceId}", mediaSpec.EvidenceId);
                        await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                        {
                            CaseId = caseId,
                            Category = LogCategory.WorkflowStep,
                            Phase = CaseGenerationSteps.GenMedia,
                            Step = "GenerateStepByCaseId",
                            TraceId = traceId,
                            Status = "Warning",
                            Message = $"Failed to generate media {mediaSpec.EvidenceId}",
                            Error = ex.Message
                        });
                    }
                }
                var mediaDuration = DateTime.UtcNow - mediaStart;

                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.GenMedia,
                    Step = "GenerateStepByCaseId",
                    Activity = "GenerateMediaBatch",
                    TraceId = traceId,
                    Status = "Completed",
                    DurationMs = Math.Round(mediaDuration.TotalMilliseconds, 2),
                    Message = "Media generation batch completed",
                    Data = new
                    {
                        requested = specs.MediaSpecs.Length,
                        generated = generatedMedia,
                        failures = Math.Max(0, specs.MediaSpecs.Length - generatedMedia)
                    }
                });
            }
            else
            {
                _logger.LogInformation("[STEP-BY-STEP] Skipping image generation (generateImages=false)");
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "GenerateStepByCaseId",
                    Message = "Image generation skipped",
                    Data = new { request.GenerateImages }
                });
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[STEP-BY-STEP] GenerateStepByCaseId completed for case {CaseId} - Duration: {Duration}s", 
                caseId, duration.TotalSeconds);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = request.GenerateImages ? CaseGenerationSteps.GenMedia : CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "GenerateStepByCaseId request completed",
                Data = new
                {
                    durationSeconds = duration.TotalSeconds,
                    filesLoaded = new[] { "design.json" },
                    generated = new
                    {
                        documents = generatedDocs,
                        media = generatedMedia,
                        total = generatedDocs + generatedMedia
                    },
                    options = new
                    {
                        request.GenerateImages,
                        request.RenderFiles
                    }
                }
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId,
                step = "generate",
                durationSeconds = duration.TotalSeconds,
                filesLoaded = new[] { "design.json" },
                generated = new
                {
                    documents = generatedDocs,
                    media = generatedMedia,
                    total = generatedDocs + generatedMedia
                },
                options = new
                {
                    generateImages = request.GenerateImages,
                    renderFiles = request.RenderFiles
                },
                nextStep = $"Use this caseId in NormalizeStepByCaseId: {caseId}"
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

    [Function("GenerateSingleDocumentByCaseId")]
    public async Task<HttpResponseData> GenerateSingleDocumentByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<GenerateSingleDocumentRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId) || request.DocumentIndex < 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId, documentIndex (0-based)");
                return errorResponse;
            }

            var startTime = DateTime.UtcNow;
            _logger.LogInformation("[STEP-BY-STEP] Starting GenerateSingleDocumentByCaseId for case {CaseId} - index: {Index}", 
                request.CaseId, request.DocumentIndex);

            // Load design.json from storage
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

            // Parse design to get specs
            DocumentAndMediaSpecs? specs;
            try
            {
                specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (specs == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to parse design.json");
                    return errorResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to parse design.json for case {CaseId}", request.CaseId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Invalid design.json format: {ex.Message}");
                return errorResponse;
            }

            // Validate index
            if (request.DocumentIndex >= specs.DocumentSpecs.Length)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Invalid documentIndex {request.DocumentIndex}. Total documents: {specs.DocumentSpecs.Length}");
                return errorResponse;
            }

            var docSpec = specs.DocumentSpecs[request.DocumentIndex];
            _logger.LogInformation("[STEP-BY-STEP] Generating document {Index}/{Total}: {DocId} ({Type})", 
                request.DocumentIndex + 1, specs.DocumentSpecs.Length, docSpec.DocId, docSpec.Type);

            // Generate the document
            string documentJson;
            try
            {
                documentJson = await _caseGenerationService.GenerateDocumentFromSpecAsync(
                    docSpec, string.Empty, request.CaseId, null, null, null);
                _logger.LogInformation("[STEP-BY-STEP] Successfully generated document {DocId}", docSpec.DocId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to generate document {DocId}", docSpec.DocId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Failed to generate document {docSpec.DocId}: {ex.Message}");
                return errorResponse;
            }

            var duration = DateTime.UtcNow - startTime;
            var hasMore = request.DocumentIndex + 1 < specs.DocumentSpecs.Length;
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "generate-single-document",
                durationSeconds = duration.TotalSeconds,
                document = new
                {
                    index = request.DocumentIndex,
                    docId = docSpec.DocId,
                    type = docSpec.Type,
                    title = docSpec.Title,
                    generated = true
                },
                progress = new
                {
                    current = request.DocumentIndex + 1,
                    total = specs.DocumentSpecs.Length,
                    percentage = ((request.DocumentIndex + 1) * 100.0 / specs.DocumentSpecs.Length),
                    hasMore = hasMore
                },
                nextStep = hasMore 
                    ? $"Generate next document: index {request.DocumentIndex + 1}"
                    : "All documents complete. Next: GenerateSingleMediaByCaseId (optional) or NormalizeStepByCaseId"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute GenerateSingleDocumentByCaseId");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateSingleMediaByCaseId")]
    public async Task<HttpResponseData> GenerateSingleMediaByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<GenerateSingleMediaRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId) || request.MediaIndex < 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId, mediaIndex (0-based)");
                return errorResponse;
            }

            var startTime = DateTime.UtcNow;
            _logger.LogInformation("[STEP-BY-STEP] Starting GenerateSingleMediaByCaseId for case {CaseId} - index: {Index}", 
                request.CaseId, request.MediaIndex);

            // Load design.json from storage
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

            // Parse design to get specs
            DocumentAndMediaSpecs? specs;
            try
            {
                specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (specs == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to parse design.json");
                    return errorResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to parse design.json for case {CaseId}", request.CaseId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Invalid design.json format: {ex.Message}");
                return errorResponse;
            }

            // Validate index
            if (request.MediaIndex >= specs.MediaSpecs.Length)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Invalid mediaIndex {request.MediaIndex}. Total media: {specs.MediaSpecs.Length}");
                return errorResponse;
            }

            var mediaSpec = specs.MediaSpecs[request.MediaIndex];
            _logger.LogInformation("[STEP-BY-STEP] Generating media {Index}/{Total}: {EvidenceId} ({Kind})", 
                request.MediaIndex + 1, specs.MediaSpecs.Length, mediaSpec.EvidenceId, mediaSpec.Kind);

            // Generate the media
            string mediaJson;
            try
            {
                mediaJson = await _caseGenerationService.GenerateMediaFromSpecAsync(
                    mediaSpec, string.Empty, request.CaseId, null, null, null);
                _logger.LogInformation("[STEP-BY-STEP] Successfully generated media {EvidenceId}", mediaSpec.EvidenceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to generate media {EvidenceId}", mediaSpec.EvidenceId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Failed to generate media {mediaSpec.EvidenceId}: {ex.Message}");
                return errorResponse;
            }

            var duration = DateTime.UtcNow - startTime;
            var hasMore = request.MediaIndex + 1 < specs.MediaSpecs.Length;
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "generate-single-media",
                durationSeconds = duration.TotalSeconds,
                media = new
                {
                    index = request.MediaIndex,
                    evidenceId = mediaSpec.EvidenceId,
                    kind = mediaSpec.Kind,
                    title = mediaSpec.Title,
                    generated = true
                },
                progress = new
                {
                    current = request.MediaIndex + 1,
                    total = specs.MediaSpecs.Length,
                    percentage = ((request.MediaIndex + 1) * 100.0 / specs.MediaSpecs.Length),
                    hasMore = hasMore
                },
                nextStep = hasMore 
                    ? $"Generate next media: index {request.MediaIndex + 1}"
                    : "All media complete. Next: NormalizeStepByCaseId"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute GenerateSingleMediaByCaseId");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateDocumentsStepByCaseId")]
    public async Task<HttpResponseData> GenerateDocumentsStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<GenerateDocumentsBatchRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            var startTime = DateTime.UtcNow;
            _logger.LogInformation("[STEP-BY-STEP] Starting GenerateDocumentsStepByCaseId for case {CaseId} - batch: {BatchStart}-{BatchEnd}", 
                request.CaseId, request.BatchStart, request.BatchEnd);

            // Load design.json from storage
            string designJson;
            try
            {
                designJson = await _storageService.GetFileAsync("cases", $"{request.CaseId}/design.json");
                _logger.LogInformation("[STEP-BY-STEP] Loaded design.json from storage for case {CaseId}", request.CaseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to load design.json for case {CaseId}", request.CaseId);
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Design not found for case {request.CaseId}. Make sure DesignStepByCaseId was executed first.");
                return errorResponse;
            }

            // Parse design to get specs
            DocumentAndMediaSpecs? specs;
            try
            {
                specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (specs == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to parse design.json");
                    return errorResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to parse design.json for case {CaseId}", request.CaseId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Invalid design.json format: {ex.Message}");
                return errorResponse;
            }

            // Determine batch range
            var totalDocs = specs.DocumentSpecs.Length;
            var batchStart = request.BatchStart ?? 0;
            var batchEnd = request.BatchEnd ?? totalDocs;
            
            // Validate range
            if (batchStart < 0 || batchStart >= totalDocs || batchEnd <= batchStart || batchEnd > totalDocs)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Invalid batch range. Total documents: {totalDocs}, requested: {batchStart}-{batchEnd}");
                return errorResponse;
            }

            var generatedDocs = 0;
            var docsBatch = specs.DocumentSpecs.Skip(batchStart).Take(batchEnd - batchStart).ToArray();
            
            _logger.LogInformation("[STEP-BY-STEP] Generating documents {Start}-{End} of {Total}...", 
                batchStart, batchEnd, totalDocs);

            foreach (var docSpec in docsBatch)
            {
                try
                {
                    var documentJson = await _caseGenerationService.GenerateDocumentFromSpecAsync(
                        docSpec, string.Empty, request.CaseId, null, null, null);
                    generatedDocs++;
                    
                    _logger.LogInformation("[STEP-BY-STEP] Generated document {DocId} ({Current}/{Total} in batch)", 
                        docSpec.DocId, generatedDocs, docsBatch.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[STEP-BY-STEP] Failed to generate document {DocId}", docSpec.DocId);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[STEP-BY-STEP] GenerateDocumentsStepByCaseId completed for case {CaseId} - Duration: {Duration}s", 
                request.CaseId, duration.TotalSeconds);

            var hasMoreDocuments = batchEnd < totalDocs;
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "generate-documents",
                durationSeconds = duration.TotalSeconds,
                batch = new
                {
                    start = batchStart,
                    end = batchEnd,
                    generated = generatedDocs,
                    total = totalDocs,
                    hasMore = hasMoreDocuments
                },
                nextStep = hasMoreDocuments 
                    ? $"Continue with GenerateDocumentsStepByCaseId batch {batchEnd}-{Math.Min(batchEnd + (batchEnd - batchStart), totalDocs)}"
                    : "Documents complete. Next: GenerateMediaStepByCaseId (optional) or NormalizeStepByCaseId"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute GenerateDocumentsStepByCaseId");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateMediaStepByCaseId")]
    public async Task<HttpResponseData> GenerateMediaStepByCaseId(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<GenerateMediaBatchRequest>(requestBody ?? "{}");

            if (request == null || string.IsNullOrEmpty(request.CaseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body. Required: caseId");
                return errorResponse;
            }

            var startTime = DateTime.UtcNow;
            _logger.LogInformation("[STEP-BY-STEP] Starting GenerateMediaStepByCaseId for case {CaseId} - batch: {BatchStart}-{BatchEnd}", 
                request.CaseId, request.BatchStart, request.BatchEnd);

            // Load design.json from storage
            string designJson;
            try
            {
                designJson = await _storageService.GetFileAsync("cases", $"{request.CaseId}/design.json");
                _logger.LogInformation("[STEP-BY-STEP] Loaded design.json from storage for case {CaseId}", request.CaseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to load design.json for case {CaseId}", request.CaseId);
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Design not found for case {request.CaseId}. Make sure DesignStepByCaseId was executed first.");
                return errorResponse;
            }

            // Parse design to get specs
            DocumentAndMediaSpecs? specs;
            try
            {
                specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (specs == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to parse design.json");
                    return errorResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to parse design.json for case {CaseId}", request.CaseId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Invalid design.json format: {ex.Message}");
                return errorResponse;
            }

            // Determine batch range
            var totalMedia = specs.MediaSpecs.Length;
            var batchStart = request.BatchStart ?? 0;
            var batchEnd = request.BatchEnd ?? totalMedia;
            
            // Validate range
            if (batchStart < 0 || batchStart >= totalMedia || batchEnd <= batchStart || batchEnd > totalMedia)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Invalid batch range. Total media: {totalMedia}, requested: {batchStart}-{batchEnd}");
                return errorResponse;
            }

            var generatedMedia = 0;
            var mediaBatch = specs.MediaSpecs.Skip(batchStart).Take(batchEnd - batchStart).ToArray();
            
            _logger.LogInformation("[STEP-BY-STEP] Generating media {Start}-{End} of {Total}...", 
                batchStart, batchEnd, totalMedia);

            foreach (var mediaSpec in mediaBatch)
            {
                try
                {
                    var mediaJson = await _caseGenerationService.GenerateMediaFromSpecAsync(
                        mediaSpec, string.Empty, request.CaseId, null, null, null);
                    generatedMedia++;
                    
                    _logger.LogInformation("[STEP-BY-STEP] Generated media {EvidenceId} ({Current}/{Total} in batch)", 
                        mediaSpec.EvidenceId, generatedMedia, mediaBatch.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[STEP-BY-STEP] Failed to generate media {EvidenceId}", mediaSpec.EvidenceId);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[STEP-BY-STEP] GenerateMediaStepByCaseId completed for case {CaseId} - Duration: {Duration}s", 
                request.CaseId, duration.TotalSeconds);

            var hasMoreMedia = batchEnd < totalMedia;
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = request.CaseId,
                step = "generate-media",
                durationSeconds = duration.TotalSeconds,
                batch = new
                {
                    start = batchStart,
                    end = batchEnd,
                    generated = generatedMedia,
                    total = totalMedia,
                    hasMore = hasMoreMedia
                },
                nextStep = hasMoreMedia 
                    ? $"Continue with GenerateMediaStepByCaseId batch {batchEnd}-{Math.Min(batchEnd + (batchEnd - batchStart), totalMedia)}"
                    : "Media complete. Next: NormalizeStepByCaseId"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute GenerateMediaStepByCaseId");
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
                    : documentsGenerated < totalDocuments
                        ? $"Continue documents: GenerateDocumentsStepByCaseId batch {documentsGenerated}-{Math.Min(documentsGenerated + 3, totalDocuments)}"
                        : $"Continue media: GenerateMediaStepByCaseId batch {mediaGenerated}-{Math.Min(mediaGenerated + 3, totalMedia)}"
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
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
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
            var startTime = DateTime.UtcNow;
            traceId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("[STEP-BY-STEP] Starting NormalizeStepByCaseId for case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "NormalizeStepByCaseId invoked",
                Data = new
                {
                    filesToLoad = new[]
                    {
                        $"cases/{caseId}/plan.json",
                        $"cases/{caseId}/expand.json",
                        $"cases/{caseId}/design.json",
                        $"cases/{caseId}/generate/documents/*",
                        $"cases/{caseId}/generate/media/*"
                    }
                }
            });

            string? planJson = null;
            string? expandJson = null;
            string? designJson = null;

            async Task<string?> TryGetFileAsync(string relativePath, string label)
            {
                try
                {
                    var content = await _storageService.GetFileAsync("cases", relativePath);
                    await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                    {
                        CaseId = caseId,
                        Category = LogCategory.Metadata,
                        Step = "NormalizeStepByCaseId",
                        Message = $"{label} loaded",
                        PayloadReference = $"cases/{relativePath}"
                    });
                    return content;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[STEP-BY-STEP] Failed to load {Label} for case {CaseId}", label, caseId);
                    await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                    {
                        CaseId = caseId,
                        Category = LogCategory.WorkflowStep,
                        Phase = CaseGenerationSteps.Normalize,
                        Step = "NormalizeStepByCaseId",
                        TraceId = traceId,
                        Status = "Warning",
                        Message = $"Failed to load {label}",
                        Error = ex.Message
                    });
                    return null;
                }
            }

            planJson = await TryGetFileAsync($"{caseId}/plan.json", "plan.json");
            expandJson = await TryGetFileAsync($"{caseId}/expand.json", "expand.json");
            designJson = await TryGetFileAsync($"{caseId}/design.json", "design.json");

            var documentsList = new List<string>();
            try
            {
                var prefix = $"{caseId}/generate/documents/";
                var fileNames = await _storageService.ListFilesAsync("cases", prefix);
                foreach (var fileName in fileNames)
                {
                    if (fileName.EndsWith(".json"))
                    {
                        var content = await _storageService.GetFileAsync("cases", fileName);
                        documentsList.Add(content);
                    }
                }

                _logger.LogInformation("[STEP-BY-STEP] Found {Count} generated documents", documentsList.Count);
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "NormalizeStepByCaseId",
                    Message = "Generated documents loaded",
                    Data = new
                    {
                        prefix = $"cases/{prefix}",
                        count = documentsList.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to list generated documents");
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Normalize,
                    Step = "NormalizeStepByCaseId",
                    TraceId = traceId,
                    Status = "Warning",
                    Message = "Failed to list generated documents",
                    Error = ex.Message
                });
            }

            var mediaList = new List<string>();
            try
            {
                var prefix = $"{caseId}/generate/media/";
                var fileNames = await _storageService.ListFilesAsync("cases", prefix);
                foreach (var fileName in fileNames)
                {
                    if (fileName.EndsWith(".json"))
                    {
                        var content = await _storageService.GetFileAsync("cases", fileName);
                        mediaList.Add(content);
                    }
                }

                _logger.LogInformation("[STEP-BY-STEP] Found {Count} generated media items", mediaList.Count);
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.Metadata,
                    Step = "NormalizeStepByCaseId",
                    Message = "Generated media loaded",
                    Data = new
                    {
                        prefix = $"cases/{prefix}",
                        count = mediaList.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STEP-BY-STEP] Failed to list generated media");
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Normalize,
                    Step = "NormalizeStepByCaseId",
                    TraceId = traceId,
                    Status = "Warning",
                    Message = "Failed to list generated media",
                    Error = ex.Message
                });
            }

            if (documentsList.Count == 0 && mediaList.Count == 0)
            {
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Normalize,
                    Step = "NormalizeStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "No generated content found"
                });
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"No generated content found for case {caseId}. Make sure GenerateStepByCaseId was executed first.");
                return errorResponse;
            }

            var normalizationInput = new NormalizationInput
            {
                CaseId = caseId,
                Documents = documentsList.ToArray(),
                Media = mediaList.ToArray(),
                PlanJson = planJson,
                ExpandedJson = expandJson,
                DesignJson = designJson,
                Difficulty = null,
                Timezone = "UTC"
            };

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Metadata,
                Step = "NormalizeStepByCaseId",
                Message = "Normalization input prepared",
                Data = new
                {
                    documents = documentsList.Count,
                    media = mediaList.Count,
                    hasPlan = planJson != null,
                    hasExpand = expandJson != null,
                    hasDesign = designJson != null
                }
            });

            _logger.LogInformation("[STEP-BY-STEP] Starting normalization with {DocCount} docs and {MediaCount} media", 
                documentsList.Count, mediaList.Count);

            var normalizeStart = DateTime.UtcNow;
            var normalizeResult = await _caseGenerationService.NormalizeCaseDeterministicAsync(normalizationInput);
            var normalizeDuration = DateTime.UtcNow - normalizeStart;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                Activity = "NormalizeCaseDeterministicAsync",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(normalizeDuration.TotalMilliseconds, 2),
                Message = "Normalization completed",
                Data = new
                {
                    manifestDocuments = normalizeResult.Manifest.Documents.Length,
                    manifestMedia = normalizeResult.Manifest.Media.Length,
                    bundleCount = normalizeResult.Manifest.BundlePaths.Length,
                    validationEntries = normalizeResult.Log.ValidationResults.Length
                }
            });

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[STEP-BY-STEP] NormalizeStepByCaseId completed for case {CaseId} - Duration: {Duration}s", 
                caseId, duration.TotalSeconds);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "NormalizeStepByCaseId request completed",
                Data = new
                {
                    durationSeconds = duration.TotalSeconds,
                    filesLoaded = new[] { "plan.json", "expand.json", "design.json" },
                    generatedCounts = new { documents = documentsList.Count, media = mediaList.Count },
                    filesSaved = new[] { "bundle.zip", "manifest.json" }
                }
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = caseId,
                step = "normalize",
                durationSeconds = duration.TotalSeconds,
                filesLoaded = new[] { "plan.json", "expand.json", "design.json", $"{documentsList.Count} documents", $"{mediaList.Count} media" },
                filesSaved = new[] { "bundle.zip", "manifest.json" },
                result = new
                {
                    manifest = normalizeResult.Manifest,
                    log = normalizeResult.Log
                },
                message = "Case normalized and bundle created successfully"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STEP-BY-STEP] Failed to execute NormalizeStepByCaseId");
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
                    Message = "NormalizeStepByCaseId failed",
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

public record GenerateSingleDocumentRequest(
    [property: JsonPropertyName("caseId")] string CaseId,
    [property: JsonPropertyName("documentIndex")] int DocumentIndex);

public record GenerateSingleMediaRequest(
    [property: JsonPropertyName("caseId")] string CaseId,
    [property: JsonPropertyName("mediaIndex")] int MediaIndex);

public record GenerateDocumentsBatchRequest(
    [property: JsonPropertyName("caseId")] string CaseId,
    [property: JsonPropertyName("batchStart")] int? BatchStart = null,
    [property: JsonPropertyName("batchEnd")] int? BatchEnd = null);

public record GenerateMediaBatchRequest(
    [property: JsonPropertyName("caseId")] string CaseId,
    [property: JsonPropertyName("batchStart")] int? BatchStart = null,
    [property: JsonPropertyName("batchEnd")] int? BatchEnd = null);

public record ExpandStepRequest(string CaseId, string PlanJson);

public record VisualRegistryStepRequest([property: JsonPropertyName("caseId")] string CaseId);

public record MasterReferencesStepRequest([property: JsonPropertyName("caseId")] string CaseId);

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
