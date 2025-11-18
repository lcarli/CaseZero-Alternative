using System;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions;

public class ExpandStepDurableOrchestrator
{
    private readonly ILogger<ExpandStepDurableOrchestrator> _logger;

    public ExpandStepDurableOrchestrator(ILogger<ExpandStepDurableOrchestrator> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ExpandStepDurableOrchestrator))]
    public async Task<ExpandStepDurableResult> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<ExpandStepDurableInput>()
            ?? throw new InvalidOperationException("ExpandStepDurableOrchestrator requires ExpandStepDurableInput");

        var replayLogger = context.CreateReplaySafeLogger<ExpandStepDurableOrchestrator>();
        replayLogger.LogInformation("[ExpandStep] Orchestration started for case {CaseId}", input.CaseId);

        var activityResult = await context.CallActivityAsync<ExpandCaseActivityResult>(
            nameof(ExpandStepDurableActivity),
            input);

        var completedAt = context.CurrentUtcDateTime;
        var measuredDuration = activityResult.DurationSeconds > 0
            ? activityResult.DurationSeconds
            : (completedAt - input.RequestedAtUtc).TotalSeconds;

        var output = new ExpandStepDurableResult
        {
            CaseId = input.CaseId,
            InstanceId = context.InstanceId,
            RequestedAtUtc = input.RequestedAtUtc,
            CompletedAtUtc = completedAt,
            DurationSeconds = measuredDuration,
            ExpandBlobPath = activityResult.ExpandBlobPath,
            FilesSaved = activityResult.FilesSaved
        };

        replayLogger.LogInformation(
            "[ExpandStep] Orchestration finished for case {CaseId} in {DurationSeconds:N2}s",
            input.CaseId,
            measuredDuration);

        return output;
    }
}

public class ExpandStepDurableActivity
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<ExpandStepDurableActivity> _logger;

    public ExpandStepDurableActivity(
        ICaseGenerationService caseGenerationService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        ILogger<ExpandStepDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function(nameof(ExpandStepDurableActivity))]
    public async Task<ExpandCaseActivityResult> RunAsync(
        [ActivityTrigger] ExpandStepDurableInput input)
    {
        var startTime = DateTime.UtcNow;
        var traceId = input.TraceId;

        try
        {
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Expand,
                Step = "ExpandStepByCaseId",
                Activity = nameof(ExpandStepDurableActivity),
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "ExpandStepDurableActivity started"
            }).ConfigureAwait(false);

            var planPath = $"{input.CaseId}/plan.json";
            string planJson;
            try
            {
                planJson = await _storageService.GetFileAsync("cases", planPath).ConfigureAwait(false);
                _logger.LogInformation("[ExpandStep] Loaded plan.json for case {CaseId}", input.CaseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpandStep] Failed to load plan.json for case {CaseId}", input.CaseId);

                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = input.CaseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Expand,
                    Step = "ExpandStepByCaseId",
                    Activity = nameof(ExpandStepDurableActivity),
                    TraceId = traceId,
                    Status = "Failed",
                    Message = "Failed to load plan.json",
                    Error = ex.Message
                }).ConfigureAwait(false);

                throw;
            }

            var expandResult = await _caseGenerationService.ExpandCaseAsync(planJson, input.CaseId)
                ?? throw new InvalidOperationException("Expand generation returned null response");

            var expandPath = $"{input.CaseId}/expand.json";
            await _storageService.SaveFileAsync("cases", expandPath, expandResult).ConfigureAwait(false);
            _logger.LogInformation("[ExpandStep] Saved expand.json for case {CaseId}", input.CaseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.Payload,
                Step = "ExpandStepByCaseId",
                TraceId = traceId,
                Status = "PayloadSaved",
                Message = "expand.json saved via ExpandStepDurableActivity",
                PayloadReference = $"cases/{expandPath}"
            }).ConfigureAwait(false);

            var duration = DateTime.UtcNow - startTime;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Expand,
                Step = "ExpandStepByCaseId",
                Activity = nameof(ExpandStepDurableActivity),
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "ExpandStepDurableActivity completed"
            }).ConfigureAwait(false);

            var filesSaved = new[]
            {
                "expand/suspects.json",
                "expand/evidence.json",
                "expand/timeline.json",
                "expand/witnesses.json"
            };

            return new ExpandCaseActivityResult
            {
                CaseId = input.CaseId,
                ExpandBlobPath = $"cases/{expandPath}",
                DurationSeconds = duration.TotalSeconds,
                FilesSaved = filesSaved
            };
        }
        catch
        {
            throw;
        }
    }
}
