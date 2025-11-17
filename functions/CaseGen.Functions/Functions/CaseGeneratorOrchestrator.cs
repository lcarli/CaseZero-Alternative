using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;
using System.Linq;

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
            CompletedSteps = Array.Empty<string>(),
            StepDurationsSeconds = new Dictionary<string, double>()
        };

        var stepDurations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var logger = context.CreateReplaySafeLogger<CaseGeneratorOrchestrator>();
            logger.LogInformation("Starting case generation orchestration for case {CaseId}", caseId);
            _caseLogging.LogOrchestratorStep(caseId, "WORKFLOW_START", "Beginning case generation workflow");

            // Step 1: Plan (Hierarchical - 4 sub-activities)
            status = status with { CurrentStep = CaseGenerationSteps.Plan, Progress = 0.05, StepDurationsSeconds = SnapshotStepDurations(stepDurations) };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_START", "Creating hierarchical case plan");

            // 1.1: Generate core case structure
            await TrackActivityAsync(
                context,
                caseId,
                "PLAN_CORE",
                () => context.CallActivityAsync<string>(
                    "PlanCoreActivity",
                    new PlanCoreActivityModel { Request = request, CaseId = caseId }),
                stepDurations,
                phase: CaseGenerationSteps.Plan);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_CORE_COMPLETE", "Core structure generated");

            // 1.2: Generate suspects based on core
            status = status with { Progress = 0.08, StepDurationsSeconds = SnapshotStepDurations(stepDurations) };
            context.SetCustomStatus(status);
            await TrackActivityAsync(
                context,
                caseId,
                "PLAN_SUSPECTS",
                () => context.CallActivityAsync<string>(
                    "PlanSuspectsActivity",
                    new PlanSuspectsActivityModel { CorePlanRef = "@plan/core", CaseId = caseId }),
                stepDurations,
                phase: CaseGenerationSteps.Plan);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_SUSPECTS_COMPLETE", "Suspects generated");

            // 1.3: Generate timeline based on core + suspects
            status = status with { Progress = 0.09, StepDurationsSeconds = SnapshotStepDurations(stepDurations) };
            context.SetCustomStatus(status);
            await TrackActivityAsync(
                context,
                caseId,
                "PLAN_TIMELINE",
                () => context.CallActivityAsync<string>(
                    "PlanTimelineActivity",
                    new PlanTimelineActivityModel { CorePlanRef = "@plan/core", SuspectsRef = "@plan/suspects", CaseId = caseId }),
                stepDurations,
                phase: CaseGenerationSteps.Plan);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_TIMELINE_COMPLETE", "Timeline generated");

            // 1.4: Generate evidence plan based on all previous
            status = status with { Progress = 0.1, StepDurationsSeconds = SnapshotStepDurations(stepDurations) };
            context.SetCustomStatus(status);
            await TrackActivityAsync(
                context,
                caseId,
                "PLAN_EVIDENCE",
                () => context.CallActivityAsync<string>(
                    "PlanEvidenceActivity",
                    new PlanEvidenceActivityModel { CorePlanRef = "@plan/core", SuspectsRef = "@plan/suspects", TimelineRef = "@plan/timeline", CaseId = caseId }),
                stepDurations,
                phase: CaseGenerationSteps.Plan);
            
            completedSteps.Add(CaseGenerationSteps.Plan);
            _caseLogging.LogOrchestratorStep(caseId, "PLAN_COMPLETE", "Hierarchical plan completed");

            // Step 2: Expand (Hierarchical with fan-out parallelization)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Expand,
                Progress = 0.2,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_START", "Expanding plan with hierarchical fan-out");

            // Load plan suspects and evidence to determine what to expand
            // Note: Using activity to load since orchestrator can't directly access ContextManager
            var loadSuspectsResult = await context.CallActivityAsync<string>("LoadContextActivity",
                new LoadContextActivityModel { CaseId = caseId, Path = "plan/suspects" });
            var loadEvidenceResult = await context.CallActivityAsync<string>("LoadContextActivity",
                new LoadContextActivityModel { CaseId = caseId, Path = "plan/evidence" });

            // Parse to extract IDs for fan-out
            var suspectsData = JsonDocument.Parse(loadSuspectsResult);
            var suspects = suspectsData.RootElement.GetProperty("suspects");
            var suspectIds = new List<string>();
            foreach (var suspect in suspects.EnumerateArray())
            {
                suspectIds.Add(suspect.GetProperty("id").GetString()!);
            }

            var evidenceData = JsonDocument.Parse(loadEvidenceResult);
            var mainElements = evidenceData.RootElement.GetProperty("mainElements");
            var evidenceIds = new List<string>();
            for (int i = 0; i < mainElements.GetArrayLength(); i++)
            {
                evidenceIds.Add($"EV{(i + 1):D3}"); // EV001, EV002, etc.
            }

            // Fan-out: Expand all suspects in parallel
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_SUSPECTS_START", $"Expanding {suspectIds.Count} suspects in parallel");
            var suspectTasks = suspectIds.Select(suspectId =>
                context.CallActivityAsync<string>("ExpandSuspectActivity",
                    new ExpandSuspectActivityModel { CaseId = caseId, SuspectId = suspectId })
            ).ToList();
            await Task.WhenAll(suspectTasks);
            
            status = status with { Progress = 0.22 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_SUSPECTS_COMPLETE", $"Completed {suspectIds.Count} suspects");

            // Fan-out: Expand all evidence in parallel
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_EVIDENCE_START", $"Expanding {evidenceIds.Count} evidence items in parallel");
            var evidenceTasks = evidenceIds.Select(evidenceId =>
                context.CallActivityAsync<string>("ExpandEvidenceActivity",
                    new ExpandEvidenceActivityModel { CaseId = caseId, EvidenceId = evidenceId })
            ).ToList();
            await Task.WhenAll(evidenceTasks);
            
            status = status with { Progress = 0.24 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_EVIDENCE_COMPLETE", $"Completed {evidenceIds.Count} evidence items");

            // Sequential: Expand timeline (needs all suspects/evidence context)
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_TIMELINE_START", "Expanding timeline with cross-references");
            var expandTimelineResult = await context.CallActivityAsync<string>("ExpandTimelineActivity",
                new ExpandTimelineActivityModel { CaseId = caseId });
            
            status = status with { Progress = 0.26 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_TIMELINE_COMPLETE", "Timeline expanded");

            // Sequential: Synthesize relations (needs timeline + all suspects/evidence)
            _caseLogging.LogOrchestratorStep(caseId, "SYNTHESIZE_RELATIONS_START", "Synthesizing relationship graph");
            var synthesizeResult = await context.CallActivityAsync<string>("SynthesizeRelationsActivity",
                new SynthesizeRelationsActivityModel { CaseId = caseId });
            
            status = status with { Progress = 0.28 };
            context.SetCustomStatus(status);
            
            completedSteps.Add(CaseGenerationSteps.Expand);
            _caseLogging.LogOrchestratorStep(caseId, "EXPAND_COMPLETE", $"Hierarchical expand completed: {suspectIds.Count} suspects, {evidenceIds.Count} evidence, timeline, relations");

            // Step 3: Design (Phase 4 - hierarchical by type)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Design,
                Progress = 0.3,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_START", "Creating document and media specifications by type");

            // Define document types to design
            var docTypes = new[] 
            { 
                "police_report", 
                "interview", 
                "forensics_report", 
                "evidence_log", 
                "witness_statement", 
                "memo_admin" 
            };
            
            // Define media types to design
            var mediaTypes = new[] 
            { 
                "crime_scene_photo", 
                "mugshot", 
                "evidence_photo", 
                "forensic_photo" 
            };
            
            // Fan-out: Design all document types in parallel
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_DOCUMENTS_START", $"Designing {docTypes.Length} document types in parallel");
            var designDocTasks = docTypes.Select(docType =>
                context.CallActivityAsync<string>("DesignDocumentTypeActivity", 
                    new DesignDocumentTypeActivityModel { CaseId = caseId, DocType = docType })
            ).ToList();
            
            status = status with { Progress = 0.31 };
            context.SetCustomStatus(status);
            
            var designDocResults = await Task.WhenAll(designDocTasks);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_DOCUMENTS_COMPLETE", $"Completed {designDocResults.Length} document type designs");
            
            // Fan-out: Design all media types in parallel
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_MEDIA_START", $"Designing {mediaTypes.Length} media types in parallel");
            var designMediaTasks = mediaTypes.Select(mediaType =>
                context.CallActivityAsync<string>("DesignMediaTypeActivity", 
                    new DesignMediaTypeActivityModel { CaseId = caseId, MediaType = mediaType })
            ).ToList();
            
            status = status with { Progress = 0.32 };
            context.SetCustomStatus(status);
            
            var designMediaResults = await Task.WhenAll(designMediaTasks);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_MEDIA_COMPLETE", $"Completed {designMediaResults.Length} media type designs");
            
            // Aggregate all design results into DocumentAndMediaSpecs
            var allDocSpecs = new List<DocumentSpec>();
            var allMediaSpecs = new List<MediaSpec>();
            
            // Parse document type results
            foreach (var docResult in designDocResults)
            {
                try
                {
                    using var docTypeDoc = JsonDocument.Parse(docResult);
                    if (docTypeDoc.RootElement.TryGetProperty("specifications", out var docSpecifications) && 
                        docSpecifications.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var spec in docSpecifications.EnumerateArray())
                        {
                            var docSpec = JsonSerializer.Deserialize<DocumentSpec>(spec.GetRawText(), 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (docSpec != null) allDocSpecs.Add(docSpec);
                        }
                    }

                }
                catch (Exception ex)
                {
                    _caseLogging.LogOrchestratorStep(caseId, "DESIGN_DOC_PARSE_ERROR", $"Failed to parse doc result: {ex.Message}");
                }
            }
            
            // Parse media type results
            foreach (var mediaDesignResult in designMediaResults)
            {
                try
                {
                    using var mediaTypeDoc = JsonDocument.Parse(mediaDesignResult);
                    if (mediaTypeDoc.RootElement.TryGetProperty("specifications", out var mediaSpecifications) && 
                        mediaSpecifications.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var spec in mediaSpecifications.EnumerateArray())
                        {
                            var mediaSpec = JsonSerializer.Deserialize<MediaSpec>(spec.GetRawText(), 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (mediaSpec != null) allMediaSpecs.Add(mediaSpec);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _caseLogging.LogOrchestratorStep(caseId, "DESIGN_MEDIA_PARSE_ERROR", $"Failed to parse media result: {ex.Message}");
                }
            }
            
            // Create aggregated design result
            var designResult = JsonSerializer.Serialize(new DocumentAndMediaSpecs
            {
                DocumentSpecs = allDocSpecs.ToArray(),
                MediaSpecs = allMediaSpecs.ToArray(),
                CaseId = caseId,
                Version = "v2-hierarchical"
            }, new JsonSerializerOptions { WriteIndented = true });
            
            status = status with { Progress = 0.33 };
            context.SetCustomStatus(status);
            
            completedSteps.Add(CaseGenerationSteps.Design);
            _caseLogging.LogOrchestratorStep(caseId, "DESIGN_COMPLETE", $"Design aggregated: {allDocSpecs.Count} documents, {allMediaSpecs.Count} media items");

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

            // Phase 5: Pass only minimal context references - service will load via ContextManager
            var documentTasks = new List<Task<string>>();
            foreach (var docSpec in specs.DocumentSpecs)
            {
                var input = new GenerateDocumentItemInput 
                { 
                    CaseId = caseId, 
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

            // Phase 5: Pass only minimal context references - service will load via ContextManager
            var mediaTasks = new List<Task<string>>();
            foreach (var mediaSpec in specs.MediaSpecs)
            {
                var input = new GenerateMediaItemInput 
                { 
                    CaseId = caseId, 
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

            // Step 6.1: Normalize Entities (suspects, evidence, witnesses)
            var entitiesResult = await context.CallActivityAsync<string>("NormalizeEntitiesActivity", new NormalizeEntitiesActivityModel 
            { 
                CaseId = caseId
            });
            status = status with { Progress = 0.62 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_ENTITIES_COMPLETE", $"Normalized entities: {entitiesResult}");

            // Step 6.2: Normalize Documents (with entity references)
            var documentIds = documentsResult.Select(docJson => 
            {
                using var jsonDoc = JsonDocument.Parse(docJson);
                return jsonDoc.RootElement.GetProperty("docId").GetString() ?? string.Empty;
            }).Where(id => !string.IsNullOrEmpty(id)).ToArray();
            
            var docsResult = await context.CallActivityAsync<string>("NormalizeDocumentsActivity", new NormalizeDocumentsActivityModel 
            { 
                CaseId = caseId,
                DocIds = documentIds
            });
            status = status with { Progress = 0.64 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_DOCUMENTS_COMPLETE", $"Normalized documents: {docsResult}");

            // Step 6.3: Create Manifest (index with references)
            var manifestResult = await context.CallActivityAsync<string>("NormalizeManifestActivity", new NormalizeManifestActivityModel 
            { 
                CaseId = caseId
            });
            status = status with { Progress = 0.66 };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_MANIFEST_COMPLETE", $"Created manifest: {manifestResult.Length} chars");

            completedSteps.Add(CaseGenerationSteps.Normalize);
            _caseLogging.LogOrchestratorStep(caseId, "NORMALIZE_COMPLETE", "All normalization steps completed");

            // Step 7: Rule Validate (simplified - no index needed)
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RuleValidate,
                Progress = 0.7,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);
            _caseLogging.LogOrchestratorStep(caseId, "VALIDATE_START", "Validating case against business rules");

            var validateResult = await context.CallActivityAsync<string>("ValidateRulesActivity", new ValidateActivityModel { NormalizedJson = manifestResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.RuleValidate);
            _caseLogging.LogOrchestratorStep(caseId, "VALIDATE_COMPLETE", $"Validation completed: {validateResult.Length} chars");

            // Step 8: Granular Quality Assurance Loop (Scan → DeepDive → Fix → Verify → repeat)
            const int maxIterations = 3;
            var iteration = 1;
            var allIssuesFound = new List<string>(); // Track all issues across iterations

            _caseLogging.LogOrchestratorStep(caseId, "QA_LOOP_START", $"Starting granular quality assurance pipeline (max {maxIterations} iterations)");

            while (iteration <= maxIterations)
            {
                // Step 8.1: Scan for Issues (lightweight ~5KB)
                status = status with
                {
                    CurrentStep = CaseGenerationSteps.RedTeam,
                    Progress = 0.7 + (iteration - 1) * 0.025, // 0.7, 0.725, 0.75
                    CompletedSteps = completedSteps.ToArray()
                };
                context.SetCustomStatus(status);
                _caseLogging.LogOrchestratorStep(caseId, "QA_SCAN_START", $"Scanning for issues - iteration {iteration}");

                var scanResultJson = await context.CallActivityAsync<string>(
                    "QA_ScanIssuesActivity", 
                    new QA_ScanIssuesActivityModel { CaseId = caseId }
                );

                // Parse scan result
                List<QaScanIssue> issues;
                try
                {
                    var scanResult = JsonSerializer.Deserialize<JsonElement>(scanResultJson);
                    var issuesArray = scanResult.GetProperty("issues");
                    issues = JsonSerializer.Deserialize<List<QaScanIssue>>(issuesArray.GetRawText()) ?? new List<QaScanIssue>();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse scan result, assuming no issues");
                    issues = new List<QaScanIssue>();
                }

                _caseLogging.LogOrchestratorStep(caseId, "QA_SCAN_COMPLETE", $"Scan iteration {iteration} found {issues.Count} issue(s)");

                // Break if no issues found
                if (issues.Count == 0)
                {
                    _caseLogging.LogOrchestratorStep(caseId, "QA_LOOP_COMPLETE", $"Quality assurance completed - no issues found after {iteration} iteration(s)");
                    break;
                }

                // Track issues
                allIssuesFound.AddRange(issues.Select(i => i.Area));

                // Step 8.2: Deep Dive Analysis (parallel, ~15KB each)
                status = status with
                {
                    Progress = 0.7 + (iteration - 1) * 0.025 + 0.0083, // +0.0083
                    CompletedSteps = completedSteps.ToArray()
                };
                context.SetCustomStatus(status);
                _caseLogging.LogOrchestratorStep(caseId, "QA_DEEPDIVE_START", $"Deep dive analysis on {issues.Count} issue(s) - iteration {iteration}");

                var deepDiveTasks = issues.Select(issue => 
                    context.CallActivityAsync<string>(
                        "QA_DeepDiveActivity",
                        new QA_DeepDiveActivityModel 
                        { 
                            CaseId = caseId, 
                            IssueArea = issue.Area 
                        }
                    )
                ).ToArray();

                var deepDiveResultsJson = await Task.WhenAll(deepDiveTasks);
                _caseLogging.LogOrchestratorStep(caseId, "QA_DEEPDIVE_COMPLETE", $"Deep dive completed for {deepDiveResultsJson.Length} issue(s) - iteration {iteration}");

                // Parse deep dive results
                var deepDiveResults = new List<(string EntityId, string ProblemDetails, string SuggestedFix)>();
                foreach (var resultJson in deepDiveResultsJson)
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);
                        var entityId = result.TryGetProperty("affectedEntities", out var entities) && entities.GetArrayLength() > 0
                            ? entities[0].GetString() ?? ""
                            : "";
                        var problemDetails = result.TryGetProperty("problemDetails", out var pd) ? pd.GetString() ?? "" : "";
                        var suggestedFix = result.TryGetProperty("suggestedFix", out var sf) ? sf.GetString() ?? "" : "";
                        
                        if (!string.IsNullOrEmpty(entityId))
                        {
                            deepDiveResults.Add((entityId, problemDetails, suggestedFix));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to parse deep dive result");
                    }
                }

                // Step 8.3: Apply Fixes (parallel, ~20KB each)
                status = status with
                {
                    CurrentStep = CaseGenerationSteps.Fix,
                    Progress = 0.7 + (iteration - 1) * 0.025 + 0.0167, // +0.0167
                    CompletedSteps = completedSteps.ToArray()
                };
                context.SetCustomStatus(status);
                _caseLogging.LogOrchestratorStep(caseId, "QA_FIX_START", $"Applying fixes for {deepDiveResults.Count} issue(s) - iteration {iteration}");

                var fixTasks = deepDiveResults
                    .Select(analysis => 
                        context.CallActivityAsync<string>(
                            "FixEntityActivity",
                            new FixEntityActivityModel 
                            { 
                                CaseId = caseId, 
                                EntityId = analysis.EntityId,
                                IssueDescription = $"{analysis.ProblemDetails} | Fix: {analysis.SuggestedFix}"
                            }
                        )
                    ).ToArray();

                var fixResultsJson = await Task.WhenAll(fixTasks);
                
                // Count successful fixes
                var successfulFixes = 0;
                foreach (var fixJson in fixResultsJson)
                {
                    try
                    {
                        var fixResult = JsonSerializer.Deserialize<JsonElement>(fixJson);
                        if (fixResult.TryGetProperty("success", out var success) && success.GetBoolean())
                        {
                            successfulFixes++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to parse fix result");
                    }
                }
                
                _caseLogging.LogOrchestratorStep(caseId, "QA_FIX_COMPLETE", $"Fixes applied: {successfulFixes}/{fixResultsJson.Length} successful - iteration {iteration}");

                // Step 8.4: Verify Case is Clean (~1KB)
                status = status with
                {
                    Progress = 0.7 + (iteration - 1) * 0.025 + 0.025, // +0.025 total per iteration
                    CompletedSteps = completedSteps.ToArray()
                };
                context.SetCustomStatus(status);
                _caseLogging.LogOrchestratorStep(caseId, "QA_VERIFY_START", $"Verifying case quality - iteration {iteration}");

                var verifyResultJson = await context.CallActivityAsync<string>(
                    "CheckCaseCleanActivityV2",
                    new CheckCaseCleanActivityV2Model 
                    { 
                        CaseId = caseId,
                        IssueAreas = issues.Select(i => i.Area).ToArray()
                    }
                );

                // Parse verification result
                bool isClean = false;
                string[] remainingIssues = Array.Empty<string>();
                try
                {
                    var verifyResult = JsonSerializer.Deserialize<JsonElement>(verifyResultJson);
                    isClean = verifyResult.TryGetProperty("isClean", out var ic) && ic.GetBoolean();
                    if (verifyResult.TryGetProperty("remainingIssues", out var ri))
                    {
                        remainingIssues = JsonSerializer.Deserialize<string[]>(ri.GetRawText()) ?? Array.Empty<string>();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse verification result, assuming not clean");
                }

                _caseLogging.LogOrchestratorStep(caseId, "QA_VERIFY_COMPLETE", 
                    $"Verification iteration {iteration}: isClean={isClean}, remaining={remainingIssues.Length}");

                // Break if clean or max iterations
                if (isClean)
                {
                    _caseLogging.LogOrchestratorStep(caseId, "QA_LOOP_COMPLETE", 
                        $"Quality assurance completed - case is clean after {iteration} iteration(s)");
                    break;
                }

                if (iteration == maxIterations)
                {
                    _caseLogging.LogOrchestratorStep(caseId, "QA_LOOP_MAX_ITERATIONS", 
                        $"Quality assurance stopped at max iterations ({maxIterations}). Remaining issues: {string.Join(", ", remainingIssues)}");
                    break;
                }

                iteration++;
            }

            completedSteps.Add(CaseGenerationSteps.RedTeam);
            if (iteration > 1) completedSteps.Add(CaseGenerationSteps.Fix);
            
            _caseLogging.LogOrchestratorStep(caseId, "QA_FINAL", $"Quality assurance complete after {iteration} iteration(s). Total issues addressed: {allIssuesFound.Count}");

            // Reload manifest for packaging (QA modified entities, need fresh manifest)
            var finalManifest = await context.CallActivityAsync<string>("NormalizeManifestActivity", new NormalizeManifestActivityModel { CaseId = caseId });
            
            logger.LogInformation("MANIFEST DEBUG: finalManifest length = {Length} chars, starts with: {Start}", 
                finalManifest?.Length ?? 0, finalManifest?.Length > 200 ? finalManifest[..200] : finalManifest);
            
            _caseLogging.LogOrchestratorStep(caseId, "QA_FINAL", $"Final case ready for packaging after {iteration - 1} iteration(s)");

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

            logger.LogInformation("PACKAGE DEBUG: About to call PackageActivity with finalManifest of {Length} chars", finalManifest?.Length ?? 0);
            
            // Convert finalManifest to Base64 to avoid Durable Task JSON inspection issues
            var finalManifestBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(finalManifest ?? string.Empty));
            logger.LogInformation("PACKAGE DEBUG: Converted to Base64, length = {Base64Length}", finalManifestBase64.Length);
            
            var packageResult = await context.CallActivityAsync<CaseGenerationOutput>("PackageActivity", new PackageActivityModel { FinalJson = finalManifestBase64, CaseId = caseId }) ?? throw new InvalidOperationException("PackageActivity returned null");
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

    private async Task<T> TrackActivityAsync<T>(
        TaskOrchestrationContext context,
        string caseId,
        string stepKey,
        Func<Task<T>> activityCall,
        IDictionary<string, double> stepDurations,
        string? phase = null)
    {
        var traceId = context.NewGuid().ToString("N");
        var start = context.CurrentUtcDateTime;
        var result = await activityCall();
        var completedAt = context.CurrentUtcDateTime;
        var durationSeconds = (completedAt - start).TotalSeconds;
        stepDurations[stepKey] = durationSeconds;
        _caseLogging.LogOrchestratorStep(caseId, $"{stepKey}_DURATION", $"{durationSeconds:F2}s");

        var entry = new StructuredLogEntry
        {
            CaseId = caseId,
            Category = LogCategory.WorkflowStep,
            Phase = phase,
            Step = stepKey,
            Activity = stepKey,
            TraceId = traceId,
            DurationMs = System.Math.Round(durationSeconds * 1000, 2),
            Status = "Completed",
            Message = "Activity completed",
            TimestampUtc = completedAt,
            Data = new
            {
                startedAt = start,
                completedAt,
                durationSeconds
            }
        };

        await context.CallActivityAsync("RecordStructuredLogActivity", entry);
        return result;
    }

    private static Dictionary<string, double> SnapshotStepDurations(IDictionary<string, double> source)
    {
        return source.ToDictionary(kvp => kvp.Key, kvp => Math.Round(kvp.Value, 3));
    }
}
