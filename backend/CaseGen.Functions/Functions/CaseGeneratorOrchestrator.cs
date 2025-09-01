using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

public class CaseGeneratorOrchestrator
{
    private readonly ILogger<CaseGeneratorOrchestrator> _logger;

    public CaseGeneratorOrchestrator(ILogger<CaseGeneratorOrchestrator> logger)
    {
        _logger = logger;
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

            var completedSteps = new List<string>();

            // Step 1: Plan
            status = status with { CurrentStep = CaseGenerationSteps.Plan, Progress = 0.1 };
            context.SetCustomStatus(status);

            var planResult = await context.CallActivityAsync<string>("PlanActivity", new PlanActivityModel { Request = request, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Plan);

            // Step 2: Expand
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Expand,
                Progress = 0.2,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var expandResult = await context.CallActivityAsync<string>("ExpandActivity", new ExpandActivityModel { PlanJson = planResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Expand);

            // Step 3: Design
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Design,
                Progress = 0.3,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var designResult = await context.CallActivityAsync<string>("DesignActivity", new DesignActivityModel { PlanJson = planResult, ExpandedJson = expandResult, CaseId = caseId, Difficulty = request.Difficulty });
            completedSteps.Add(CaseGenerationSteps.Design);

            // Step 4: Generate Documents
            var specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(designResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Design result could not be parsed into DocumentAndMediaSpecs");

            // Step 4+5: GenDocs & GenMedia em paralelo com FAN-OUT/FAN-IN
            status = status with
            {
                CurrentStep = $"{CaseGenerationSteps.GenDocs}+{CaseGenerationSteps.GenMedia}",
                Progress = 0.45,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var docTasks = new List<Task<string>>();
            foreach (var ds in specs.DocumentSpecs)
            {
                var input = new GenerateDocumentItemInput { CaseId = caseId, PlanJson = planResult, ExpandedJson = expandResult, DesignJson = designResult, Spec = ds, DifficultyOverride = request.Difficulty };
                docTasks.Add(context.CallActivityAsync<string>("GenerateDocumentItemActivity", input));
            }
            var mediaTasks = new List<Task<string>>();
            foreach (var ms in specs.MediaSpecs)
            {
                var input = new GenerateMediaItemInput { CaseId = caseId, PlanJson = planResult, ExpandedJson = expandResult, DesignJson = designResult, Spec = ms, DifficultyOverride = request.Difficulty };
                mediaTasks.Add(context.CallActivityAsync<string>("GenerateMediaItemActivity", input));
            }

            var docsWhenAll = Task.WhenAll(docTasks);
            var mediaWhenAll = Task.WhenAll(mediaTasks);
            await Task.WhenAll(docsWhenAll, mediaWhenAll);

            var documentsResult = docsWhenAll.Result;
            var mediaResult = mediaWhenAll.Result;

            completedSteps.Add(CaseGenerationSteps.GenDocs);
            completedSteps.Add(CaseGenerationSteps.GenMedia);

            // Step 6: Normalize
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Normalize,
                Progress = 0.6,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var normalizeResult = await context.CallActivityAsync<string>("NormalizeActivity", new NormalizeActivityModel { Documents = documentsResult, Media = mediaResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Normalize);

            // Step 7: Index
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Index,
                Progress = 0.7,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var indexResult = await context.CallActivityAsync<string>("IndexActivity", new IndexActivityModel { NormalizedJson = normalizeResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Index);

            // Step 8: Rule Validate
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RuleValidate,
                Progress = 0.8,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var validateResult = await context.CallActivityAsync<string>("ValidateRulesActivity", new ValidateActivityModel { IndexedJson = indexResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.RuleValidate);

            // Step 9: Red Team
            status = status with
            {
                CurrentStep = CaseGenerationSteps.RedTeam,
                Progress = 0.9,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var redTeamResult = await context.CallActivityAsync<string>("RedTeamActivity", new RedTeamActivityModel { ValidatedJson = validateResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.RedTeam);

            // Step 10: Package
            status = status with
            {
                CurrentStep = CaseGenerationSteps.Package,
                Progress = 0.95,
                CompletedSteps = completedSteps.ToArray()
            };
            context.SetCustomStatus(status);

            var packageResult = await context.CallActivityAsync<CaseGenerationOutput>("PackageActivity", new PackageActivityModel { FinalJson = redTeamResult, CaseId = caseId });
            completedSteps.Add(CaseGenerationSteps.Package);

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

            return status with
            {
                Status = "Failed",
                Error = ex.Message,
                EstimatedCompletion = context.CurrentUtcDateTime
            };
        }
    }
}