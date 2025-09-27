using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

public class CaseGeneratorOrchestrator
{
    private readonly ILogger<CaseGeneratorOrchestrator> _logger;
    private readonly ICaseLoggingService _caseLogging;

    public CaseGeneratorOrchestrator(ILogger<CaseGeneratorOrchestrator> logger, ICaseLoggingService caseLogging)
    {
        _logger = logger;
        _caseLogging = caseLogging;
    }

    [Function("StartCaseGeneration")]
    public async Task<HttpResponseData> StartCaseGeneration(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<CaseGenerationRequest>(requestBody ?? "{}");

            if (request == null)
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request body");
                return errorResponse;
            }

            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("CaseGenerationOrchestrator", request);

            _logger.LogInformation("Started case generation orchestration {InstanceId} for {Title}", instanceId, request.Title);

            var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new { instanceId, status = "Started" });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start case generation");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GetCaseGenerationStatus")]
    public async Task<HttpResponseData> GetCaseGenerationStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{instanceId}")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient client)
    {
        try
        {
            var metadata = await client.GetInstanceAsync(instanceId);

            if (metadata == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Instance not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                instanceId = metadata.InstanceId,
                runtimeStatus = metadata.RuntimeStatus.ToString(),
                createdAt = metadata.CreatedAt,
                lastUpdatedAt = metadata.LastUpdatedAt,
                customStatus = metadata.SerializedCustomStatus,
                output = metadata.SerializedOutput
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get case generation status for {InstanceId}", instanceId);
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("CaseGenerationOrchestrator")]
    public async Task<CaseGenerationStatus> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var request = context.GetInput<CaseGenerationRequest>() ?? throw new System.InvalidOperationException("Orchestration requires CaseGenerationRequest input.");
        var caseId = $"CASE-{context.CurrentUtcDateTime:yyyyMMdd}-{context.NewGuid().ToString("N")[..8]}";
        var startTime = context.CurrentUtcDateTime;
        var completedSteps = new List<string>();

        var status = new CaseGenerationStatus
        {
            CaseId = caseId,
            Status = "Running",
            StartTime = startTime,
            TotalSteps = CaseGenerationSteps.AllSteps.Length,
            CompletedSteps = Array.Empty<string>()
        };

        try
        {
            var logger = context.CreateReplaySafeLogger<CaseGeneratorOrchestrator>();
            logger.LogInformation("Starting case generation orchestration for case {CaseId}", caseId);
            _caseLogging.LogOrchestratorStep(caseId, "WORKFLOW_START", "Beginning case generation workflow");

            // Step 1: Plan
            status = status with { CurrentStep = CaseGenerationSteps.Plan, Progress = 0.1 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_START", "Creating initial case plan");

            var planResult = await context.CallActivityAsync<string>("PlanActivity", new PlanActivityModel { Request = request, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Plan);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_COMPLETE", $"Plan generated: {planResult.Length} chars");

            // Step 2: Expand
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Expand,
                Progress = 0.2,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_START", "Expanding plan into detailed content");

            var expandResult = await context.CallActivityAsync<string>("ExpandActivity", new ExpandActivityModel { PlanJson = planResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Expand);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_COMPLETE", $"Content expanded: {expandResult.Length} chars");

            // Step 3: Design
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Design,
                Progress = 0.3,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_START", "Creating document and media specifications");

            var designResult = await context.CallActivityAsync<string>("DesignActivity", new DesignActivityModel { PlanJson = planResult, ExpandedJson = expandResult, CaseId = caseId, Difficulty = request.Difficulty });
            completedSteps.Add(CaseGenerationSteps.Design);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_COMPLETE", $"Design created: {designResult.Length} chars");

            // Step 4: Generate Documents
            var specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Design result could not be parsed into DocumentAndMediaSpecs");

            status = status with
            {
                CurrentStep = CaseGenerationSteps.GenDocs,
                Progress = 0.4,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "GENDOCS_START", $"Generating {specs.DocumentSpecs.Length} documents");

            var documentTasks = new List<Task<string>>();
            foreach (var docSpec in specs.DocumentSpecs)
            {
                var input = new GenerateDocumentItemInput 
                { 
                    CaseId = caseId, 
                    PlanJson = planResult, 
                    ExpandedJson = expandResult, 
                    DesignJson = designResult, 
                    Spec = docSpec, 
                    DifficultyOverride = request.Difficulty 
                };
                documentTasks.Add(context.CallActivityAsync<string>("GenerateDocumentItemActivity", input));
            }

            var documentsResult = await Task.WhenAll(documentTasks);
            completedSteps.Add(CaseGenerationSteps.GenDocs);
            _caseLogging.LogOrchestratorStep(caseId, "GENDOCS_COMPLETE", $"Generated {documentsResult.Length} documents");

            // Step 5: Generate Media
            status = status with
            {
                CurrentStep = CaseGenerationSteps.GenMedia,
                Progress = 0.5,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "GENMEDIA_START", $"Generating {specs.MediaSpecs.Length} media items");

            var mediaTasks = new List<Task<string>>();
            foreach (var mediaSpec in specs.MediaSpecs)
            {
                var input = new GenerateMediaItemInput 
                { 
                    CaseId = caseId, 
                    PlanJson = planResult, 
                    ExpandedJson = expandResult, 
                    DesignJson = designResult, 
                    Spec = mediaSpec, 
                    DifficultyOverride = request.Difficulty 
                };
                mediaTasks.Add(context.CallActivityAsync<string>("GenerateMediaItemActivity", input));
            }

            var mediaResult = await Task.WhenAll(mediaTasks);
            completedSteps.Add(CaseGenerationSteps.GenMedia);
            _caseLogging.LogOrchestratorStep(caseId, "GENMEDIA_COMPLETE", $"Generated {mediaResult.Length} media items");

            // Step 6: Normalize (CORE GAME CONTENT - NO RENDERING YET)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Normalize,
                Progress = 0.6,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_START", $"Normalizing case data and creating manifest (core game content)");

            var normalizeResult = await context.CallActivityAsync<string>("NormalizeActivity", new NormalizeActivityModel 
            { 
                Documents = documentsResult, 
                Media = mediaResult, 
                CaseId = caseId,
                Difficulty = request.Difficulty,
                Timezone = request.Timezone,
                PlanJson = planResult,
                ExpandedJson = expandResult,
                DesignJson = designResult,
                RenderedDocs = Array.Empty<RenderedDocument>(), // No rendered files yet
                RenderedMedia = Array.Empty<RenderedMedia>()    // No rendered files yet
            });
            completedSteps.Add(CaseGenerationSteps.Normalize);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_COMPLETE", $"Normalization completed: {normalizeResult.Length} chars");

            // Step 7: Rule Validate (simplified - no index needed)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RuleValidate,
                Progress = 0.7,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "VALIDATE_START", "Validating case against business rules");

            var validateResult = await context.CallActivityAsync<string>("ValidateRulesActivity", new ValidateActivityModel { NormalizedJson = normalizeResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.RuleValidate);
            _caseLogging.LogOrchestratorStep(caseId, "VALIDATE_COMPLETE", $"Validation completed: {validateResult.Length} chars");

            // Step 8: Red Team (CORE COMPLETION BEFORE OPTIONAL RENDERING)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RedTeam,
                Progress = 0.8,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "REDTEAM_START", "Performing security analysis and red team review");

            var redTeamResult = await context.CallActivityAsync<string>("RedTeamActivity", new RedTeamActivityModel { ValidatedJson = normalizeResult, CaseId = caseId });
            
            // Save RedTeam analysis separately to logs container (not as case.json)
            await context.CallActivityAsync("SaveRedTeamAnalysisActivity", new SaveRedTeamAnalysisActivityModel { 
                CaseId = caseId, 
                RedTeamAnalysis = redTeamResult 
            });
            
            completedSteps.Add(CaseGenerationSteps.RedTeam);
            _caseLogging.LogOrchestratorStep(caseId, "REDTEAM_COMPLETE", $"Red team analysis completed and saved to logs: {redTeamResult.Length} chars");

            // OPTIONAL RENDERING PHASE (only if requested)
            var renderedDocs = new List<RenderedDocument>();
            var renderedMedia = new List<RenderedMedia>();

            if (request.RenderFiles)
            {
                logger.LogInformation("Rendering files requested - starting optional rendering phase for case {CaseId}", caseId);
                _caseLogging.LogOrchestratorStep(caseId, "RENDER_PHASE_START", "Starting optional file rendering phase");

                // Step 8.5: RenderDocuments (optional - fan-out JSON→MD→PDF)
                status = status with
                {
                    CurrentStep = CaseGenerationSteps.RenderDocs,
                    Progress = 0.85,
                    CompletedSteps = completedSteps.ToArray()
                };
                context.SetCustomStatus(status);
                _caseLogging.LogOrchestratorStep(caseId, "RENDER_DOCS_START", $"Rendering {documentsResult.Length} documents to PDF");

                var renderTasks = new List<Task<string>>();
                var docIdList = new List<string>(); // Track docIds for rendered documents
                foreach (var docJson in documentsResult)
                {
                    // Parse docId from the JSON result
                    string docId = "unknown";
                    try
                    {
                        using var doc = JsonDocument.Parse(docJson);
                        if (doc.RootElement.TryGetProperty("docId", out var docIdProp))
                        {
                            docId = docIdProp.GetString() ?? "unknown";
                        }
                    }
                    catch (JsonException)
                    {
                        // If parsing fails, generate a fallback docId
                        docId = $"doc_{renderTasks.Count + 1}";
                    }

                    docIdList.Add(docId); // Store the docId for later mapping
                    var renderInput = new RenderDocumentItemInput 
                    { 
                        CaseId = caseId, 
                        DocId = docId, 
                        DocumentJson = docJson 
                    };
                    renderTasks.Add(context.CallActivityAsync<string>("RenderDocumentItemActivity", renderInput));
                }

                var renderResults = await Task.WhenAll(renderTasks);
                completedSteps.Add(CaseGenerationSteps.RenderDocs);
                _caseLogging.LogOrchestratorStep(caseId, "RENDER_DOCS_COMPLETE", $"Rendered {renderResults.Length} documents");

                // Step 8.7: RenderImages (optional - fan-out for actual image generation)
                status = status with
                {
                    CurrentStep = CaseGenerationSteps.RenderImages,
                    Progress = 0.9,
                    CompletedSteps = completedSteps.ToArray()
                };
                context.SetCustomStatus(status);
                _caseLogging.LogOrchestratorStep(caseId, "RENDER_IMAGES_START", $"Rendering {mediaResult.Length} images");

                var renderImageTasks = new List<Task<string>>();
                var evidenceIdList = new List<string>(); // Track evidenceIds for rendered media
                foreach (var mediaJson in mediaResult)
                {
                    // Parse the media spec from the JSON result
                    try
                    {
                        var mediaSpec = JsonSerializer.Deserialize<MediaSpec>(mediaJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (mediaSpec != null)
                        {
                            evidenceIdList.Add(mediaSpec.EvidenceId); // Store the evidenceId for later mapping
                            var renderImageInput = new RenderMediaItemInput 
                            { 
                                CaseId = caseId, 
                                Spec = mediaSpec
                            };
                            renderImageTasks.Add(context.CallActivityAsync<string>("RenderMediaItemActivity", renderImageInput));
                        }
                    }
                    catch (JsonException ex)
                    {
                        logger.LogWarning(ex, "Failed to parse media spec JSON for image rendering: {MediaJson}", mediaJson);
                    }
                }

                var renderImageResults = await Task.WhenAll(renderImageTasks);
                completedSteps.Add(CaseGenerationSteps.RenderImages);
                _caseLogging.LogOrchestratorStep(caseId, "RENDER_IMAGES_COMPLETE", $"Rendered {renderImageResults.Length} images");

                // Build rendered artifacts arrays
                for (int i = 0; i < renderResults.Length && i < docIdList.Count; i++)
                {
                    var filePath = renderResults[i];
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        renderedDocs.Add(new RenderedDocument
                        {
                            DocId = docIdList[i],
                            FilePath = filePath,
                            FileSize = 0, // Could be calculated if needed
                            Sha256Hash = null, // Could be calculated if needed
                            ContentType = "application/pdf"
                        });
                    }
                }

                for (int i = 0; i < renderImageResults.Length && i < evidenceIdList.Count; i++)
                {
                    var filePath = renderImageResults[i];
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        renderedMedia.Add(new RenderedMedia
                        {
                            EvidenceId = evidenceIdList[i],
                            FilePath = filePath,
                            FileSize = 0, // Could be calculated if needed
                            Sha256Hash = null, // Could be calculated if needed
                            ContentType = "image/png",
                            Kind = "photo"
                        });
                    }
                }

                _caseLogging.LogOrchestratorStep(caseId, "RENDER_PHASE_COMPLETE", $"Optional rendering completed: {renderedDocs.Count} PDFs, {renderedMedia.Count} images");
            }
            else
            {
                logger.LogInformation("Rendering files not requested - skipping optional rendering phase for case {CaseId}", caseId);
                _caseLogging.LogOrchestratorStep(caseId, "RENDER_PHASE_SKIPPED", "File rendering skipped (renderFiles=false)");
            }

            // Step 9: Package (FINAL - with or without rendered files)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Package,
                Progress = 0.95,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "PACKAGE_START", "Creating final case package and delivery artifacts");

            var packageResult = await context.CallActivityAsync<CaseGenerationOutput>("PackageActivity", new PackageActivityModel { FinalJson = normalizeResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Package);
            _caseLogging.LogOrchestratorStep(caseId, "PACKAGE_COMPLETE", "Final packaging completed successfully");

            var endTime = context.CurrentUtcDateTime;
            var totalDuration = endTime - startTime;
            _caseLogging.LogOrchestratorStep(caseId, "WORKFLOW_COMPLETE", $"Case generation completed in {Math.Round(totalDuration.TotalMinutes, 2)} minutes");

            // Complete
            status = status with
            {
                Status = "Completed",
                CurrentStep = "Completed",
                Progress = 1.0,
                CompletedSteps = completedSteps.ToArray(),
                Output = packageResult,
                EstimatedCompletion = context.CurrentUtcDateTime
            };

            logger.LogInformation("Completed case generation orchestration for case {CaseId}", caseId);
            return status;
        }
        catch (Exception ex)
        {
            var logger = context.CreateReplaySafeLogger<CaseGeneratorOrchestrator>();
            logger.LogError(ex, "Failed case generation orchestration for case {CaseId}", caseId);
            _caseLogging.LogOrchestratorStep(caseId, "WORKFLOW_FAILED", $"Error: {ex.Message}");

            var endTime = context.CurrentUtcDateTime;
            var totalDuration = endTime - startTime;

            return status with
            {
                Status = "Failed",
                Error = ex.Message,
                EstimatedCompletion = context.CurrentUtcDateTime
            };
        }
    }
}
