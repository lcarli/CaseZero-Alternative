using System;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions;

public class DesignStepDurableOrchestrator
{
    private readonly ILogger<DesignStepDurableOrchestrator> _logger;

    public DesignStepDurableOrchestrator(ILogger<DesignStepDurableOrchestrator> logger)
    {
        _logger = logger;
    }

    [Function(nameof(DesignStepDurableOrchestrator))]
    public async Task<DesignStepDurableResult> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<DesignStepDurableInput>()
            ?? throw new InvalidOperationException("DesignStepDurableOrchestrator requires DesignStepDurableInput");

        var replayLogger = context.CreateReplaySafeLogger<DesignStepDurableOrchestrator>();
        replayLogger.LogInformation("[DesignStep] Orchestration started for case {CaseId}", input.CaseId);

        var activityResult = await context.CallActivityAsync<DesignCaseActivityResult>(
            nameof(DesignStepDurableActivity),
            input);

        var completedAt = context.CurrentUtcDateTime;
        var measuredDuration = activityResult.DurationSeconds > 0
            ? activityResult.DurationSeconds
            : (completedAt - input.RequestedAtUtc).TotalSeconds;

        var output = new DesignStepDurableResult
        {
            CaseId = input.CaseId,
            InstanceId = context.InstanceId,
            RequestedAtUtc = input.RequestedAtUtc,
            CompletedAtUtc = completedAt,
            DurationSeconds = measuredDuration,
            DesignBlobPath = activityResult.DesignBlobPath,
            FilesSaved = activityResult.FilesSaved
        };

        replayLogger.LogInformation(
            "[DesignStep] Orchestration finished for case {CaseId} in {DurationSeconds:N2}s",
            input.CaseId,
            measuredDuration);

        return output;
    }
}

public class DesignStepDurableActivity
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<DesignStepDurableActivity> _logger;

    public DesignStepDurableActivity(
        ICaseGenerationService caseGenerationService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        ILogger<DesignStepDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function(nameof(DesignStepDurableActivity))]
    public async Task<DesignCaseActivityResult> RunAsync(
        [ActivityTrigger] DesignStepDurableInput input)
    {
        var startTime = DateTime.UtcNow;
        var traceId = input.TraceId;

        try
        {
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                Activity = nameof(DesignStepDurableActivity),
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "DesignStepDurableActivity started",
                Data = new
                {
                    filesToLoad = new[]
                    {
                        $"cases/{input.CaseId}/plan.json",
                        $"cases/{input.CaseId}/expand.json"
                    }
                }
            }).ConfigureAwait(false);

            var planPath = $"{input.CaseId}/plan.json";
            var expandPath = $"{input.CaseId}/expand.json";

            var planJson = await LoadFileAsync(planPath, input, "plan.json").ConfigureAwait(false);
            var expandJson = await LoadFileAsync(expandPath, input, "expand.json").ConfigureAwait(false);

            var designResult = await _caseGenerationService.DesignCaseAsync(planJson, expandJson, input.CaseId)
                ?? throw new InvalidOperationException("Design generation returned null response");

            var designPath = $"{input.CaseId}/design.json";
            await _storageService.SaveFileAsync("cases", designPath, designResult).ConfigureAwait(false);
            _logger.LogInformation("[DesignStep] Saved design.json for case {CaseId}", input.CaseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.Payload,
                Step = "DesignStepByCaseId",
                TraceId = traceId,
                Status = "PayloadSaved",
                Message = "design.json saved via DesignStepDurableActivity",
                PayloadReference = $"cases/{designPath}"
            }).ConfigureAwait(false);

            var duration = DateTime.UtcNow - startTime;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                Activity = nameof(DesignStepDurableActivity),
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "DesignStepDurableActivity completed",
                Data = new
                {
                    filesLoaded = new[] { "plan.json", "expand.json" },
                    filesSaved = new[] { "design.json" }
                }
            }).ConfigureAwait(false);

            return new DesignCaseActivityResult
            {
                CaseId = input.CaseId,
                DesignBlobPath = $"cases/{designPath}",
                DurationSeconds = duration.TotalSeconds,
                FilesSaved = new[] { "design.json" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DesignStep] Activity failed for case {CaseId}", input.CaseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                Activity = nameof(DesignStepDurableActivity),
                TraceId = traceId,
                Status = "Failed",
                Message = "DesignStepDurableActivity failed",
                Error = ex.Message
            }).ConfigureAwait(false);

            throw;
        }
    }

    private async Task<string> LoadFileAsync(string relativePath, DesignStepDurableInput input, string friendlyName)
    {
        try
        {
            var content = await _storageService.GetFileAsync("cases", relativePath).ConfigureAwait(false);
            _logger.LogInformation("[DesignStep] Loaded {File} for case {CaseId}", friendlyName, input.CaseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.Metadata,
                Step = "DesignStepByCaseId",
                TraceId = input.TraceId,
                Status = "PayloadLoaded",
                Message = $"{friendlyName} loaded",
                PayloadReference = $"cases/{relativePath}"
            }).ConfigureAwait(false);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DesignStep] Failed to load {File} for case {CaseId}", friendlyName, input.CaseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Design,
                Step = "DesignStepByCaseId",
                Activity = nameof(DesignStepDurableActivity),
                TraceId = input.TraceId,
                Status = "Failed",
                Message = $"Failed to load {friendlyName}",
                Error = ex.Message
            }).ConfigureAwait(false);

            throw;
        }
    }
}
