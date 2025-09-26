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

            // Configuration for retry logic
            const int maxRetries = 3;
            const int maxDocumentRetries = 2; // Lower retry count for individual documents to avoid excessive delays

            // Step 1: Plan (with retry)
            status = status with { CurrentStep = CaseGenerationSteps.Plan, Progress = 0.1 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_START", "Creating initial case plan");

            string planResult = "";
            for (int planAttempt = 1; planAttempt <= maxRetries; planAttempt++)
            {
                try
                {
                    planResult = await context.CallActivityAsync<string>("PlanActivity", new PlanActivityModel { Request = request, CaseId = caseId });
                    
                    if (IsValidContent(planResult, "Plan"))
                    {
                        logger.LogInformation("Plan generation succeeded on attempt {Attempt} for case {CaseId}", planAttempt, caseId);
                        break;
                    }
                    else
                    {
                        logger.LogWarning("Plan generation attempt {Attempt} returned invalid/empty content for case {CaseId}. Content length: {Length}", 
                            planAttempt, caseId, planResult?.Length ?? 0);
                        _caseLogging.LogOrchestratorStep(caseId, "PLAN_RETRY", $"Attempt {planAttempt} failed - invalid content, retrying...");
                        
                        if (planAttempt == maxRetries)
                        {
                            throw new InvalidOperationException($"Plan generation failed after {maxRetries} attempts - all attempts returned empty or invalid content");
                        }
                    }
                }
                catch (Exception ex) when (planAttempt < maxRetries)
                {
                    logger.LogWarning(ex, "Plan generation attempt {Attempt} failed for case {CaseId}, retrying...", planAttempt, caseId);
                    _caseLogging.LogOrchestratorStep(caseId, "PLAN_RETRY", $"Attempt {planAttempt} failed with error: {ex.Message}, retrying...");
                }
            }

            completedSteps.Add(CaseGenerationSteps.Plan);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_COMPLETE", $"Plan generated: {planResult.Length} chars");

            // Step 2: Expand (with retry)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Expand,
                Progress = 0.2,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_START", "Expanding plan into detailed content");

            string expandResult = "";
            for (int expandAttempt = 1; expandAttempt <= maxRetries; expandAttempt++)
            {
                try
                {
                    expandResult = await context.CallActivityAsync<string>("ExpandActivity", new ExpandActivityModel { PlanJson = planResult, CaseId = caseId });
                    
                    if (IsValidContent(expandResult, "Expand"))
                    {
                        logger.LogInformation("Expand generation succeeded on attempt {Attempt} for case {CaseId}", expandAttempt, caseId);
                        break;
                    }
                    else
                    {
                        logger.LogWarning("Expand generation attempt {Attempt} returned invalid/empty content for case {CaseId}. Content length: {Length}", 
                            expandAttempt, caseId, expandResult?.Length ?? 0);
                        _caseLogging.LogOrchestratorStep(caseId, "EXPAND_RETRY", $"Attempt {expandAttempt} failed - invalid content, retrying...");
                        
                        if (expandAttempt == maxRetries)
                        {
                            throw new InvalidOperationException($"Expand generation failed after {maxRetries} attempts - all attempts returned empty or invalid content");
                        }
                    }
                }
                catch (Exception ex) when (expandAttempt < maxRetries)
                {
                    logger.LogWarning(ex, "Expand generation attempt {Attempt} failed for case {CaseId}, retrying...", expandAttempt, caseId);
                    _caseLogging.LogOrchestratorStep(caseId, "EXPAND_RETRY", $"Attempt {expandAttempt} failed with error: {ex.Message}, retrying...");
                }
            }

            completedSteps.Add(CaseGenerationSteps.Expand);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_COMPLETE", $"Content expanded: {expandResult.Length} chars");

            // Step 3: Design (with retry)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Design,
                Progress = 0.3,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_START", "Creating document and media specifications");

            string designResult = "";
            for (int designAttempt = 1; designAttempt <= maxRetries; designAttempt++)
            {
                try
                {
                    designResult = await context.CallActivityAsync<string>("DesignActivity", new DesignActivityModel { PlanJson = planResult, ExpandedJson = expandResult, CaseId = caseId, Difficulty = request.Difficulty });
                    
                    if (IsValidContent(designResult, "Design"))
                    {
                        logger.LogInformation("Design generation succeeded on attempt {Attempt} for case {CaseId}", designAttempt, caseId);
                        break;
                    }
                    else
                    {
                        logger.LogWarning("Design generation attempt {Attempt} returned invalid/empty content for case {CaseId}. Content length: {Length}", 
                            designAttempt, caseId, designResult?.Length ?? 0);
                        _caseLogging.LogOrchestratorStep(caseId, "DESIGN_RETRY", $"Attempt {designAttempt} failed - invalid content, retrying...");
                        
                        if (designAttempt == maxRetries)
                        {
                            throw new InvalidOperationException($"Design generation failed after {maxRetries} attempts - all attempts returned empty or invalid content");
                        }
                    }
                }
                catch (Exception ex) when (designAttempt < maxRetries)
                {
                    logger.LogWarning(ex, "Design generation attempt {Attempt} failed for case {CaseId}, retrying...", designAttempt, caseId);
                    _caseLogging.LogOrchestratorStep(caseId, "DESIGN_RETRY", $"Attempt {designAttempt} failed with error: {ex.Message}, retrying...");
                }
            }

            completedSteps.Add(CaseGenerationSteps.Design);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_COMPLETE", $"Design created: {designResult.Length} chars");

            // Step 4: Generate Documents
            var specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Design result could not be parsed into DocumentAndMediaSpecs");

            // Step 4+5: GenDocs & GenMedia com validação individual e retry
            status = status with
            {
                CurrentStep = $"{CaseGenerationSteps.GenDocs}+{CaseGenerationSteps.GenMedia}",
                Progress = 0.45,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "GENERATION_START", $"Starting parallel generation with validation: {specs.DocumentSpecs.Length} docs, {specs.MediaSpecs.Length} media");

            // Generate documents with individual validation and retry
            var documentTasks = new List<Task<string>>();
            
            foreach (var docSpec in specs.DocumentSpecs)
            {
                documentTasks.Add(GenerateDocumentWithRetryAsync(context, docSpec, planResult, expandResult, designResult, request.Difficulty, caseId, maxDocumentRetries, logger));
            }

            // Generate media with individual validation and retry
            var mediaTasks = new List<Task<string>>();
            
            foreach (var mediaSpec in specs.MediaSpecs)
            {
                mediaTasks.Add(GenerateMediaWithRetryAsync(context, mediaSpec, planResult, expandResult, designResult, request.Difficulty, caseId, maxDocumentRetries, logger));
            }

            // Wait for all generation tasks to complete
            await Task.WhenAll(documentTasks.Concat(mediaTasks));

            var documentsResult = documentTasks.Select(t => t.Result).ToArray();
            var mediaResult = mediaTasks.Select(t => t.Result).ToArray();

            completedSteps.Add(CaseGenerationSteps.GenDocs);
            completedSteps.Add(CaseGenerationSteps.GenMedia);
            _caseLogging.LogOrchestratorStep(caseId, "GENERATION_COMPLETE", $"Generated {documentsResult.Length} docs, {mediaResult.Length} media with validation");

            // Step 5.5: RenderDocuments (fan-out JSON→MD→PDF)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RenderDocs,
                Progress = 0.55,
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

            // Step 5.8: RenderImages (fan-out for actual image generation)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RenderImages,
                Progress = 0.58,
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

            // Build rendered artifacts arrays for normalization
            var renderedDocs = new List<RenderedDocument>();
            for (int i = 0; i < renderResults.Length && i < docIdList.Count; i++)
            {
                var filePath = renderResults[i];
                if (!string.IsNullOrEmpty(filePath))
                {
                    // TODO: Get actual file size and SHA256 hash if needed
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

            var renderedMedia = new List<RenderedMedia>();
            for (int i = 0; i < renderImageResults.Length && i < evidenceIdList.Count; i++)
            {
                var filePath = renderImageResults[i];
                if (!string.IsNullOrEmpty(filePath))
                {
                    // TODO: Get actual file size and SHA256 hash if needed
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

            // Step 6: Normalize
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Normalize,
                Progress = 0.6,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_START", $"Normalizing case data and creating manifest");

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
                RenderedDocs = renderedDocs.ToArray(),
                RenderedMedia = renderedMedia.ToArray()
            });
            completedSteps.Add(CaseGenerationSteps.Normalize);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_COMPLETE", $"Normalization completed: {normalizeResult.Length} chars");

            // Step 7: Index
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Index,
                Progress = 0.7,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "INDEX_START", "Creating searchable index of case content");

            var indexResult = await context.CallActivityAsync<string>("IndexActivity", new IndexActivityModel { NormalizedJson = normalizeResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Index);
            _caseLogging.LogOrchestratorStep(caseId, "INDEX_COMPLETE", $"Index created: {indexResult.Length} chars");

            // Step 8: Rule Validate
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RuleValidate,
                Progress = 0.8,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "VALIDATE_START", "Validating case against business rules");

            var validateResult = await context.CallActivityAsync<string>("ValidateRulesActivity", new ValidateActivityModel { IndexedJson = indexResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.RuleValidate);
            _caseLogging.LogOrchestratorStep(caseId, "VALIDATE_COMPLETE", $"Validation completed: {validateResult.Length} chars");

            // Step 9: Red Team
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RedTeam,
                Progress = 0.9,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "REDTEAM_START", "Performing security analysis and red team review");

            var redTeamResult = await context.CallActivityAsync<string>("RedTeamActivity", new RedTeamActivityModel { ValidatedJson = validateResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.RedTeam);
            _caseLogging.LogOrchestratorStep(caseId, "REDTEAM_COMPLETE", $"Red team analysis completed: {redTeamResult.Length} chars");

            // Step 10: Package
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Package,
                Progress = 0.95,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "PACKAGE_START", "Creating final case package and delivery artifacts");

            var packageResult = await context.CallActivityAsync<CaseGenerationOutput>("PackageActivity", new PackageActivityModel { FinalJson = redTeamResult, CaseId = caseId });
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

    /// <summary>
    /// Validates if the content is not empty and contains valid data
    /// </summary>
    private static bool IsValidContent(string content, string stepName)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        // For JSON steps, validate JSON structure
        if (stepName.ToLowerInvariant().Contains("json") || 
            stepName == "Plan" || stepName == "Expand" || stepName == "Design" ||
            stepName == "Normalize" || stepName == "Index" || stepName == "Validate" || stepName == "RedTeam")
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                // Check if it's not just an empty object or array
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any())
                {
                    return true;
                }
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    return true;
                }
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // For other content, check minimum length
        return content.Length > 50; // Minimum meaningful content
    }

    /// <summary>
    /// Validates if a document JSON contains required fields and content
    /// </summary>
    private static bool IsValidDocumentContent(string documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(documentJson);
            var root = document.RootElement;

            // Check for required fields
            if (!root.TryGetProperty("docId", out var docId) || string.IsNullOrWhiteSpace(docId.GetString()))
                return false;

            if (!root.TryGetProperty("content", out var content) || string.IsNullOrWhiteSpace(content.GetString()))
                return false;

            if (!root.TryGetProperty("title", out var title) || string.IsNullOrWhiteSpace(title.GetString()))
                return false;

            // Check if content has meaningful length
            var contentText = content.GetString();
            return contentText != null && contentText.Length > 100; // Minimum meaningful document content
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates if a media JSON contains required fields and specifications
    /// </summary>
    private static bool IsValidMediaContent(string mediaJson)
    {
        if (string.IsNullOrWhiteSpace(mediaJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(mediaJson);
            var root = document.RootElement;

            // Check for required fields
            if (!root.TryGetProperty("evidenceId", out var evidenceId) || string.IsNullOrWhiteSpace(evidenceId.GetString()))
                return false;

            if (!root.TryGetProperty("description", out var description) || string.IsNullOrWhiteSpace(description.GetString()))
                return false;

            if (!root.TryGetProperty("kind", out var kind) || string.IsNullOrWhiteSpace(kind.GetString()))
                return false;

            // Check if description has meaningful length
            var descriptionText = description.GetString();
            return descriptionText != null && descriptionText.Length > 20; // Minimum meaningful media description
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a fallback document when generation fails
    /// </summary>
    private static string CreateFallbackDocument(DocumentSpec spec)
    {
        var fallbackContent = spec.Type.ToLowerInvariant() switch
        {
            "police_report" => $"Police Report #{spec.DocId}\n\nThis report is currently unavailable due to technical issues. Please contact the system administrator for assistance.\n\nReport ID: {spec.DocId}\nStatus: Placeholder\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            "witness_statement" => $"Witness Statement - {spec.Title}\n\nStatement currently unavailable due to system error.\n\nWitness: [Name Redacted]\nStatement ID: {spec.DocId}\nStatus: Placeholder\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            "forensics_report" => $"Forensic Analysis Report\n\nReport: {spec.Title}\n\nDetailed analysis is currently unavailable due to technical issues.\n\nEvidence ID: {spec.DocId}\nAnalysis Type: Forensic\nStatus: Placeholder\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            _ => $"Document: {spec.Title}\n\nContent is currently unavailable due to technical issues.\n\nDocument ID: {spec.DocId}\nType: {spec.Type}\nStatus: Placeholder\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
        };

        var fallbackDocument = new
        {
            docId = spec.DocId,
            type = spec.Type,
            title = $"{spec.Title} [FALLBACK]",
            content = fallbackContent,
            sections = spec.Sections ?? new[] { "Content" },
            lengthTarget = spec.LengthTarget ?? new[] { 200, 400 },
            gated = spec.Gated,
            gatingRule = spec.GatingRule,
            metadata = new Dictionary<string, object>
            {
                { "fallback", true },
                { "generationAttempts", "failed" },
                { "createdAt", DateTime.UtcNow.ToString("O") }
            }
        };

        return JsonSerializer.Serialize(fallbackDocument);
    }

    /// <summary>
    /// Creates a fallback media spec when generation fails
    /// </summary>
    private static string CreateFallbackMedia(MediaSpec spec)
    {
        var fallbackDescription = spec.Kind.ToLowerInvariant() switch
        {
            "photo" => $"Crime scene photograph #{spec.EvidenceId}. Image currently unavailable due to technical issues.",
            "video" => $"Surveillance video #{spec.EvidenceId}. Video content currently unavailable due to technical issues.",
            "audio" => $"Audio recording #{spec.EvidenceId}. Audio content currently unavailable due to technical issues.",
            _ => $"Media evidence #{spec.EvidenceId} of type {spec.Kind}. Content currently unavailable due to technical issues."
        };

        var fallbackMedia = new
        {
            evidenceId = spec.EvidenceId,
            kind = spec.Kind,
            title = $"{spec.Title} [FALLBACK]",
            prompt = $"{fallbackDescription} [FALLBACK]",
            constraints = spec.Constraints ?? new Dictionary<string, object>(),
            deferred = spec.Deferred,
            metadata = new Dictionary<string, object>
            {
                { "fallback", true },
                { "generationAttempts", "failed" },
                { "createdAt", DateTime.UtcNow.ToString("O") }
            }
        };

        return JsonSerializer.Serialize(fallbackMedia);
    }

    /// <summary>
    /// Generates a document with retry logic and fallback
    /// </summary>
    private async Task<string> GenerateDocumentWithRetryAsync(
        TaskOrchestrationContext context,
        DocumentSpec docSpec,
        string planResult,
        string expandResult,
        string designResult,
        string? difficulty,
        string caseId,
        int maxRetries,
        ILogger logger)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var input = new GenerateDocumentItemInput 
                { 
                    CaseId = caseId, 
                    PlanJson = planResult, 
                    ExpandedJson = expandResult, 
                    DesignJson = designResult, 
                    Spec = docSpec, 
                    DifficultyOverride = difficulty 
                };
                
                var docResult = await context.CallActivityAsync<string>("GenerateDocumentItemActivity", input);
                
                if (IsValidDocumentContent(docResult))
                {
                    logger.LogInformation("Document {DocId} generated successfully on attempt {Attempt}", docSpec.DocId, attempt);
                    return docResult;
                }
                else
                {
                    logger.LogWarning("Document {DocId} generation attempt {Attempt} returned invalid content. Content length: {Length}", 
                        docSpec.DocId, attempt, docResult?.Length ?? 0);
                    _caseLogging.LogOrchestratorStep(caseId, "DOC_RETRY", $"Document {docSpec.DocId} attempt {attempt} failed - invalid content, retrying...");
                    
                    if (attempt == maxRetries)
                    {
                        // Fallback: create a minimal placeholder document
                        var fallbackDoc = CreateFallbackDocument(docSpec);
                        logger.LogWarning("Document {DocId} generation failed after {MaxRetries} attempts - using fallback content", 
                            docSpec.DocId, maxRetries);
                        _caseLogging.LogOrchestratorStep(caseId, "DOC_FALLBACK", $"Document {docSpec.DocId} using fallback content after {maxRetries} failed attempts");
                        return fallbackDoc;
                    }
                }
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Document {DocId} generation attempt {Attempt} failed, retrying...", docSpec.DocId, attempt);
                _caseLogging.LogOrchestratorStep(caseId, "DOC_RETRY", $"Document {docSpec.DocId} attempt {attempt} failed with error: {ex.Message}, retrying...");
            }
            catch (Exception ex) when (attempt == maxRetries)
            {
                // Final attempt failed - create fallback
                logger.LogError(ex, "Document {DocId} generation failed after {MaxRetries} attempts - using fallback", docSpec.DocId, maxRetries);
                var fallbackDoc = CreateFallbackDocument(docSpec);
                _caseLogging.LogOrchestratorStep(caseId, "DOC_FALLBACK", $"Document {docSpec.DocId} using fallback after final attempt failed: {ex.Message}");
                return fallbackDoc;
            }
        }

        // Fallback if all retries exhausted (should not reach here)
        return CreateFallbackDocument(docSpec);
    }

    /// <summary>
    /// Generates media with retry logic and fallback
    /// </summary>
    private async Task<string> GenerateMediaWithRetryAsync(
        TaskOrchestrationContext context,
        MediaSpec mediaSpec,
        string planResult,
        string expandResult,
        string designResult,
        string? difficulty,
        string caseId,
        int maxRetries,
        ILogger logger)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var input = new GenerateMediaItemInput 
                { 
                    CaseId = caseId, 
                    PlanJson = planResult, 
                    ExpandedJson = expandResult, 
                    DesignJson = designResult, 
                    Spec = mediaSpec, 
                    DifficultyOverride = difficulty 
                };
                
                var mediaResult = await context.CallActivityAsync<string>("GenerateMediaItemActivity", input);
                
                if (IsValidMediaContent(mediaResult))
                {
                    logger.LogInformation("Media {EvidenceId} generated successfully on attempt {Attempt}", mediaSpec.EvidenceId, attempt);
                    return mediaResult;
                }
                else
                {
                    logger.LogWarning("Media {EvidenceId} generation attempt {Attempt} returned invalid content. Content length: {Length}", 
                        mediaSpec.EvidenceId, attempt, mediaResult?.Length ?? 0);
                    _caseLogging.LogOrchestratorStep(caseId, "MEDIA_RETRY", $"Media {mediaSpec.EvidenceId} attempt {attempt} failed - invalid content, retrying...");
                    
                    if (attempt == maxRetries)
                    {
                        // Fallback: create a minimal placeholder media spec
                        var fallbackMedia = CreateFallbackMedia(mediaSpec);
                        logger.LogWarning("Media {EvidenceId} generation failed after {MaxRetries} attempts - using fallback content", 
                            mediaSpec.EvidenceId, maxRetries);
                        _caseLogging.LogOrchestratorStep(caseId, "MEDIA_FALLBACK", $"Media {mediaSpec.EvidenceId} using fallback content after {maxRetries} failed attempts");
                        return fallbackMedia;
                    }
                }
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Media {EvidenceId} generation attempt {Attempt} failed, retrying...", mediaSpec.EvidenceId, attempt);
                _caseLogging.LogOrchestratorStep(caseId, "MEDIA_RETRY", $"Media {mediaSpec.EvidenceId} attempt {attempt} failed with error: {ex.Message}, retrying...");
            }
            catch (Exception ex) when (attempt == maxRetries)
            {
                // Final attempt failed - create fallback
                logger.LogError(ex, "Media {EvidenceId} generation failed after {MaxRetries} attempts - using fallback", mediaSpec.EvidenceId, maxRetries);
                var fallbackMedia = CreateFallbackMedia(mediaSpec);
                _caseLogging.LogOrchestratorStep(caseId, "MEDIA_FALLBACK", $"Media {mediaSpec.EvidenceId} using fallback after final attempt failed: {ex.Message}");
                return fallbackMedia;
            }
        }

        // Fallback if all retries exhausted (should not reach here)
        return CreateFallbackMedia(mediaSpec);
    }
}
