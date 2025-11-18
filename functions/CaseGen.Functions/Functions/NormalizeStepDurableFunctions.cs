using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions;

public class NormalizeStepDurableOrchestrator
{
    private readonly ILogger<NormalizeStepDurableOrchestrator> _logger;

    public NormalizeStepDurableOrchestrator(ILogger<NormalizeStepDurableOrchestrator> logger)
    {
        _logger = logger;
    }

    [Function(nameof(NormalizeStepDurableOrchestrator))]
    public async Task<NormalizeStepDurableResult> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<NormalizeStepDurableInput>()
            ?? throw new InvalidOperationException("NormalizeStepDurableOrchestrator requires NormalizeStepDurableInput");

        var replayLogger = context.CreateReplaySafeLogger<NormalizeStepDurableOrchestrator>();
        replayLogger.LogInformation("[NormalizeStep] Orchestration started for case {CaseId}", input.CaseId);

        var activityResult = await context.CallActivityAsync<NormalizeCaseDurableActivityResult>(
            nameof(NormalizeStepDurableActivity),
            input);

        var completedAt = context.CurrentUtcDateTime;
        var duration = activityResult.DurationSeconds > 0
            ? activityResult.DurationSeconds
            : (completedAt - input.RequestedAtUtc).TotalSeconds;

        var output = new NormalizeStepDurableResult
        {
            CaseId = input.CaseId,
            InstanceId = context.InstanceId,
            RequestedAtUtc = input.RequestedAtUtc,
            CompletedAtUtc = completedAt,
            DurationSeconds = duration,
            DocumentsLoaded = activityResult.DocumentsLoaded,
            MediaLoaded = activityResult.MediaLoaded,
            Manifest = activityResult.Manifest,
            Log = activityResult.Log,
            FilesSaved = activityResult.FilesSaved
        };

        replayLogger.LogInformation(
            "[NormalizeStep] Orchestration finished for case {CaseId} in {DurationSeconds:N2}s",
            input.CaseId,
            duration);

        return output;
    }
}

public class NormalizeStepDurableActivity
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<NormalizeStepDurableActivity> _logger;

    public NormalizeStepDurableActivity(
        ICaseGenerationService caseGenerationService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        ILogger<NormalizeStepDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function(nameof(NormalizeStepDurableActivity))]
    public async Task<NormalizeCaseDurableActivityResult> RunAsync(
        [ActivityTrigger] NormalizeStepDurableInput input)
    {
        var caseId = input.CaseId;
        var traceId = input.TraceId;
        var startTime = DateTime.UtcNow;

        try
        {
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                Activity = nameof(NormalizeStepDurableActivity),
                TraceId = traceId,
                Status = "Started",
                TimestampUtc = startTime,
                Message = "NormalizeStepDurableActivity started",
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
            }).ConfigureAwait(false);

            var planJson = await TryLoadFileAsync(caseId, "plan.json", traceId).ConfigureAwait(false);
            var expandJson = await TryLoadFileAsync(caseId, "expand.json", traceId).ConfigureAwait(false);
            var designJson = await TryLoadFileAsync(caseId, "design.json", traceId).ConfigureAwait(false);

            var documents = await LoadCaseArtifactsAsync(caseId, "documents", traceId).ConfigureAwait(false);
            var media = await LoadCaseArtifactsAsync(caseId, "media", traceId).ConfigureAwait(false);

            if (documents.Count == 0 && media.Count == 0)
            {
                var message = "No generated content found";
                await _caseLogging.LogStructuredAsync(new StructuredLogEntry
                {
                    CaseId = caseId,
                    Category = LogCategory.WorkflowStep,
                    Phase = CaseGenerationSteps.Normalize,
                    Step = "NormalizeStepByCaseId",
                    TraceId = traceId,
                    Status = "Failed",
                    Message = message
                }).ConfigureAwait(false);

                throw new InvalidOperationException($"No generated content found for case {caseId}. Run GenerateStepByCaseId first.");
            }

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Metadata,
                Step = "NormalizeStepByCaseId",
                TraceId = traceId,
                Message = "Normalization input prepared",
                Data = new
                {
                    documents = documents.Count,
                    media = media.Count,
                    hasPlan = planJson != null,
                    hasExpand = expandJson != null,
                    hasDesign = designJson != null
                }
            }).ConfigureAwait(false);

            var normalizationInput = new NormalizationInput
            {
                CaseId = caseId,
                Documents = documents.ToArray(),
                Media = media.ToArray(),
                PlanJson = planJson,
                ExpandedJson = expandJson,
                DesignJson = designJson,
                Difficulty = input.Difficulty,
                Timezone = input.Timezone
            };

            _logger.LogInformation(
                "[NormalizeStep] Starting deterministic normalization for case {CaseId}. Docs: {DocCount}, Media: {MediaCount}",
                caseId,
                documents.Count,
                media.Count);

            var normalizationResult = await _caseGenerationService
                .NormalizeCaseDeterministicAsync(normalizationInput)
                .ConfigureAwait(false);

            var duration = DateTime.UtcNow - startTime;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                Activity = nameof(NormalizeStepDurableActivity),
                TraceId = traceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "Normalization completed via Durable activity",
                Data = new
                {
                    manifestDocuments = normalizationResult.Manifest.Documents.Length,
                    manifestMedia = normalizationResult.Manifest.Media.Length,
                    bundleCount = normalizationResult.Manifest.BundlePaths.Length,
                    validationEntries = normalizationResult.Log.ValidationResults.Length
                }
            }).ConfigureAwait(false);

            return new NormalizeCaseDurableActivityResult
            {
                CaseId = caseId,
                DurationSeconds = duration.TotalSeconds,
                DocumentsLoaded = documents.Count,
                MediaLoaded = media.Count,
                Manifest = normalizationResult.Manifest,
                Log = normalizationResult.Log,
                FilesSaved = new[] { "bundle.zip", "manifest.json" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NormalizeStep] Failed to normalize case {CaseId}", caseId);

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                Activity = nameof(NormalizeStepDurableActivity),
                TraceId = traceId,
                Status = "Failed",
                Message = "NormalizeStepDurableActivity failed",
                Error = ex.Message
            }).ConfigureAwait(false);

            throw;
        }
    }

    private async Task<string?> TryLoadFileAsync(string caseId, string fileName, string traceId)
    {
        var relativePath = $"{caseId}/{fileName}";
        try
        {
            var content = await _storageService.GetFileAsync("cases", relativePath).ConfigureAwait(false);
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Metadata,
                Step = "NormalizeStepByCaseId",
                TraceId = traceId,
                Message = $"{fileName} loaded",
                PayloadReference = $"cases/{relativePath}"
            }).ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NormalizeStep] Failed to load {File} for case {CaseId}", fileName, caseId);
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                TraceId = traceId,
                Status = "Warning",
                Message = $"Failed to load {fileName}",
                Error = ex.Message
            }).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<List<string>> LoadCaseArtifactsAsync(string caseId, string folder, string traceId)
    {
        var items = new List<string>();
        try
        {
            var prefix = $"{caseId}/generate/{folder}/";
            var fileNames = await _storageService.ListFilesAsync("cases", prefix).ConfigureAwait(false);
            foreach (var fileName in fileNames.Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                var content = await _storageService.GetFileAsync("cases", fileName).ConfigureAwait(false);
                items.Add(content);
            }

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.Metadata,
                Step = "NormalizeStepByCaseId",
                TraceId = traceId,
                Message = $"Generated {folder} loaded",
                Data = new
                {
                    prefix = $"cases/{prefix}",
                    count = items.Count
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NormalizeStep] Failed to load generated {Folder} for case {CaseId}", folder, caseId);
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = caseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.Normalize,
                Step = "NormalizeStepByCaseId",
                TraceId = traceId,
                Status = "Warning",
                Message = $"Failed to load generated {folder}",
                Error = ex.Message
            }).ConfigureAwait(false);
        }

        return items;
    }
}
