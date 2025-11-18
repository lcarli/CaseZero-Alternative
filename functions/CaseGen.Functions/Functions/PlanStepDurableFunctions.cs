using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions;

public class PlanStepDurableOrchestrator
{
    private readonly ILogger<PlanStepDurableOrchestrator> _logger;

    public PlanStepDurableOrchestrator(ILogger<PlanStepDurableOrchestrator> logger)
    {
        _logger = logger;
    }

    [Function(nameof(PlanStepDurableOrchestrator))]
    public async Task<PlanStepDurableResult> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<PlanStepDurableInput>() ??
            throw new InvalidOperationException("PlanStepDurableOrchestrator requires PlanStepDurableInput");

        var replaySafeLogger = context.CreateReplaySafeLogger<PlanStepDurableOrchestrator>();
        replaySafeLogger.LogInformation("[PlanStep] Orchestration started for case {CaseId}", input.CaseId);

        var activityResult = await context.CallActivityAsync<PlanCaseActivityResult>(
            nameof(PlanStepDurableActivity),
            input);

        var completedAt = context.CurrentUtcDateTime;
        var measuredDuration = activityResult.DurationSeconds > 0
            ? activityResult.DurationSeconds
            : (completedAt - input.RequestedAtUtc).TotalSeconds;

        var output = new PlanStepDurableResult
        {
            CaseId = input.CaseId,
            InstanceId = context.InstanceId,
            RequestedAtUtc = input.RequestedAtUtc,
            CompletedAtUtc = completedAt,
            DurationSeconds = measuredDuration,
            PlanBlobPath = activityResult.PlanBlobPath,
            Difficulty = activityResult.Difficulty,
            Timezone = activityResult.Timezone
        };

        replaySafeLogger.LogInformation(
            "[PlanStep] Orchestration finished for case {CaseId} in {DurationSeconds:N2}s",
            input.CaseId,
            measuredDuration);

        return output;
    }
}

public class PlanStepDurableActivity
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<PlanStepDurableActivity> _logger;

    public PlanStepDurableActivity(
        ICaseGenerationService caseGenerationService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        ILogger<PlanStepDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function(nameof(PlanStepDurableActivity))]
    public async Task<PlanCaseActivityResult> RunAsync(
        [ActivityTrigger] PlanStepDurableInput input)
    {
        var request = input.Request ?? new CaseGenerationRequest();
        var difficulty = request.Difficulty ?? "Rookie";
        var timezone = request.Timezone ?? "UTC";
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("[PlanStep] Activity started for case {CaseId} ({Difficulty}/{Timezone})",
                input.CaseId, difficulty, timezone);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Plan,
                Step = "PlanStepOnly",
                Activity = nameof(PlanStepDurableActivity),
                TraceId = input.TraceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "PlanStepDurableActivity started",
                Data = new { difficulty, timezone }
            }).ConfigureAwait(false);

            var planResult = await _caseGenerationService.PlanCaseAsync(request, input.CaseId)
                ?? throw new InvalidOperationException("Plan generation returned null response");

            var planPath = $"{input.CaseId}/plan.json";
            await _storageService.SaveFileAsync("cases", planPath, planResult).ConfigureAwait(false);
            _logger.LogInformation("[PlanStep] Saved plan.json for case {CaseId}", input.CaseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.Payload,
                Step = "PlanStepOnly",
                TraceId = input.TraceId,
                Status = "PayloadSaved",
                Message = "plan.json saved via PlanStepDurableActivity",
                PayloadReference = $"cases/{planPath}"
            }).ConfigureAwait(false);

            var duration = DateTime.UtcNow - startTime;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Plan,
                Step = "PlanStepOnly",
                Activity = nameof(PlanStepDurableActivity),
                TraceId = input.TraceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "PlanStepDurableActivity completed",
                Data = new { difficulty, timezone }
            }).ConfigureAwait(false);

            return new PlanCaseActivityResult
            {
                CaseId = input.CaseId,
                PlanBlobPath = $"cases/{planPath}",
                DurationSeconds = duration.TotalSeconds,
                Difficulty = difficulty,
                Timezone = timezone
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlanStep] Activity failed for case {CaseId}", input.CaseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Plan,
                Step = "PlanStepOnly",
                Activity = nameof(PlanStepDurableActivity),
                TraceId = input.TraceId,
                Status = "Failed",
                Message = "PlanStepDurableActivity failed",
                Error = ex.Message
            }).ConfigureAwait(false);

            throw;
        }
    }
}
