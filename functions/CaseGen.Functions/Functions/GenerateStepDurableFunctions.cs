using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions;

public class GenerateStepDurableOrchestrator
{
    private readonly ILogger<GenerateStepDurableOrchestrator> _logger;

    public GenerateStepDurableOrchestrator(ILogger<GenerateStepDurableOrchestrator> logger)
    {
        _logger = logger;
    }

    [Function(nameof(GenerateStepDurableOrchestrator))]
    public async Task<GenerateStepDurableResult> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<GenerateStepDurableInput>()
            ?? throw new InvalidOperationException("GenerateStepDurableOrchestrator requires GenerateStepDurableInput");

        var replayLogger = context.CreateReplaySafeLogger<GenerateStepDurableOrchestrator>();
        replayLogger.LogInformation("[GenerateStep] Orchestration started for case {CaseId}", input.CaseId);

        var specs = await context.CallActivityAsync<DocumentAndMediaSpecs>(
            nameof(LoadDesignSpecsActivity),
            new LoadDesignSpecsActivityInput
            {
                CaseId = input.CaseId,
                TraceId = input.TraceId
            });

        var documentTasks = new List<Task<GenerateDocumentTaskSummary>>();
        for (var i = 0; i < specs.DocumentSpecs.Length; i++)
        {
            documentTasks.Add(context.CallActivityAsync<GenerateDocumentTaskSummary>(
                nameof(GenerateDocumentDurableActivity),
                new GenerateDocumentDurableActivityInput
                {
                    CaseId = input.CaseId,
                    Spec = specs.DocumentSpecs[i],
                    Index = i,
                    TraceId = input.TraceId,
                    DifficultyOverride = null
                }));
        }

        var documentResults = documentTasks.Count > 0
            ? await Task.WhenAll(documentTasks)
            : Array.Empty<GenerateDocumentTaskSummary>();

        IReadOnlyList<GenerateMediaTaskSummary> mediaResults = Array.Empty<GenerateMediaTaskSummary>();
        if (input.GenerateImages && specs.MediaSpecs.Length > 0)
        {
            var mediaTasks = new List<Task<GenerateMediaTaskSummary>>();
            for (var i = 0; i < specs.MediaSpecs.Length; i++)
            {
                mediaTasks.Add(context.CallActivityAsync<GenerateMediaTaskSummary>(
                    nameof(GenerateMediaDurableActivity),
                    new GenerateMediaDurableActivityInput
                    {
                        CaseId = input.CaseId,
                        Spec = specs.MediaSpecs[i],
                        Index = i,
                        TraceId = input.TraceId,
                        DifficultyOverride = null
                    }));
            }

            mediaResults = mediaTasks.Count > 0
                ? await Task.WhenAll(mediaTasks)
                : Array.Empty<GenerateMediaTaskSummary>();
        }

        var completedAt = context.CurrentUtcDateTime;
        var durationSeconds = (completedAt - input.RequestedAtUtc).TotalSeconds;

        var output = new GenerateStepDurableResult
        {
            CaseId = input.CaseId,
            InstanceId = context.InstanceId,
            RequestedAtUtc = input.RequestedAtUtc,
            CompletedAtUtc = completedAt,
            DurationSeconds = durationSeconds,
            DocumentsRequested = specs.DocumentSpecs.Length,
            MediaRequested = specs.MediaSpecs.Length,
            Documents = documentResults,
            Media = mediaResults,
            GenerateImages = input.GenerateImages,
            RenderFiles = input.RenderFiles
        };

        replayLogger.LogInformation(
            "[GenerateStep] Orchestration finished for case {CaseId}. Docs: {DocSuccess}/{DocTotal}, Media: {MediaSuccess}/{MediaTotal}",
            input.CaseId,
            documentResults.Count(r => r.Success),
            specs.DocumentSpecs.Length,
            mediaResults.Count(r => r.Success),
            specs.MediaSpecs.Length);

        return output;
    }
}

public class LoadDesignSpecsActivity
{
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<LoadDesignSpecsActivity> _logger;

    public LoadDesignSpecsActivity(
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        ILogger<LoadDesignSpecsActivity> logger)
    {
        _storageService = storageService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function(nameof(LoadDesignSpecsActivity))]
    public async Task<DocumentAndMediaSpecs> RunAsync([ActivityTrigger] LoadDesignSpecsActivityInput input)
    {
        var path = $"{input.CaseId}/design.json";
        try
        {
            var json = await _storageService.GetFileAsync("cases", path).ConfigureAwait(false);
            var specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("design.json is invalid or empty");

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.Metadata,
                Step = "GenerateStepByCaseId",
                TraceId = input.TraceId,
                Status = "DesignLoaded",
                Message = "design.json loaded for durable generation",
                PayloadReference = $"cases/{path}",
                Data = new
                {
                    documents = specs.DocumentSpecs.Length,
                    media = specs.MediaSpecs.Length
                }
            }).ConfigureAwait(false);

            return specs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GenerateStep] Failed to load design.json for case {CaseId}", input.CaseId);
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                TraceId = input.TraceId,
                Status = "Failed",
                Message = "Failed to load design.json",
                Error = ex.Message
            }).ConfigureAwait(false);
            throw;
        }
    }
}

public class GenerateDocumentDurableActivity
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<GenerateDocumentDurableActivity> _logger;

    public GenerateDocumentDurableActivity(
        ICaseGenerationService caseGenerationService,
        ICaseLoggingService caseLogging,
        ILogger<GenerateDocumentDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function(nameof(GenerateDocumentDurableActivity))]
    public async Task<GenerateDocumentTaskSummary> RunAsync([ActivityTrigger] GenerateDocumentDurableActivityInput input)
    {
        var start = DateTime.UtcNow;
        try
        {
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                Activity = nameof(GenerateDocumentDurableActivity),
                TraceId = input.TraceId,
                Status = "Started",
                TimestampUtc = start,
                Message = "Document generation started",
                Data = new
                {
                    docId = input.Spec.DocId,
                    type = input.Spec.Type,
                    index = input.Index
                }
            }).ConfigureAwait(false);

            await _caseGenerationService.GenerateDocumentFromSpecAsync(
                input.Spec,
                designJson: string.Empty,
                input.CaseId,
                planJson: null,
                expandJson: null,
                input.DifficultyOverride).ConfigureAwait(false);

            var duration = DateTime.UtcNow - start;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                Activity = nameof(GenerateDocumentDurableActivity),
                TraceId = input.TraceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "Document generation completed",
                Data = new
                {
                    docId = input.Spec.DocId,
                    index = input.Index
                }
            }).ConfigureAwait(false);

            return new GenerateDocumentTaskSummary
            {
                DocId = input.Spec.DocId,
                Index = input.Index,
                Success = true,
                DurationSeconds = duration.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - start;
            _logger.LogError(ex, "[GenerateStep] Failed to generate document {DocId} for case {CaseId}", input.Spec.DocId, input.CaseId);
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenDocs,
                Step = "GenerateStepByCaseId",
                Activity = nameof(GenerateDocumentDurableActivity),
                TraceId = input.TraceId,
                Status = "Failed",
                Message = $"Failed to generate document {input.Spec.DocId}",
                Error = ex.Message
            }).ConfigureAwait(false);

            return new GenerateDocumentTaskSummary
            {
                DocId = input.Spec.DocId,
                Index = input.Index,
                Success = false,
                Error = ex.Message,
                DurationSeconds = duration.TotalSeconds
            };
        }
    }
}

public class GenerateMediaDurableActivity
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<GenerateMediaDurableActivity> _logger;

    public GenerateMediaDurableActivity(
        ICaseGenerationService caseGenerationService,
        ICaseLoggingService caseLogging,
        ILogger<GenerateMediaDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function(nameof(GenerateMediaDurableActivity))]
    public async Task<GenerateMediaTaskSummary> RunAsync([ActivityTrigger] GenerateMediaDurableActivityInput input)
    {
        var start = DateTime.UtcNow;
        try
        {
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenMedia,
                Step = "GenerateStepByCaseId",
                Activity = nameof(GenerateMediaDurableActivity),
                TraceId = input.TraceId,
                Status = "Started",
                TimestampUtc = start,
                Message = "Media generation started",
                Data = new
                {
                    evidenceId = input.Spec.EvidenceId,
                    kind = input.Spec.Kind,
                    index = input.Index
                }
            }).ConfigureAwait(false);

            await _caseGenerationService.GenerateMediaFromSpecAsync(
                input.Spec,
                designJson: string.Empty,
                input.CaseId,
                planJson: null,
                expandJson: null,
                input.DifficultyOverride).ConfigureAwait(false);

            var duration = DateTime.UtcNow - start;

            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenMedia,
                Step = "GenerateStepByCaseId",
                Activity = nameof(GenerateMediaDurableActivity),
                TraceId = input.TraceId,
                Status = "Completed",
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                Message = "Media generation completed",
                Data = new
                {
                    evidenceId = input.Spec.EvidenceId,
                    index = input.Index
                }
            }).ConfigureAwait(false);

            return new GenerateMediaTaskSummary
            {
                EvidenceId = input.Spec.EvidenceId,
                Index = input.Index,
                Success = true,
                DurationSeconds = duration.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - start;
            _logger.LogError(ex, "[GenerateStep] Failed to generate media {EvidenceId} for case {CaseId}", input.Spec.EvidenceId, input.CaseId);
            await _caseLogging.LogStructuredAsync(new StructuredLogEntry
            {
                CaseId = input.CaseId,
                Category = LogCategory.WorkflowStep,
                Phase = CaseGenerationSteps.GenMedia,
                Step = "GenerateStepByCaseId",
                Activity = nameof(GenerateMediaDurableActivity),
                TraceId = input.TraceId,
                Status = "Failed",
                Message = $"Failed to generate media {input.Spec.EvidenceId}",
                Error = ex.Message
            }).ConfigureAwait(false);

            return new GenerateMediaTaskSummary
            {
                EvidenceId = input.Spec.EvidenceId,
                Index = input.Index,
                Success = false,
                Error = ex.Message,
                DurationSeconds = duration.TotalSeconds
            };
        }
    }
}
