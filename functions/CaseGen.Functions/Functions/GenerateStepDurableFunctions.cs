using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
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

        var renderedDocumentSummaries = new List<RenderedArtifactSummary>();
        if (input.RenderFiles && documentResults.Length > 0)
        {
            var renderTasks = documentResults
                .Where(d => d.Success)
                .Select(d => context.CallActivityAsync<RenderedArtifactSummary>(
                    nameof(RenderGeneratedDocumentActivity),
                    new RenderGeneratedDocumentInput
                    {
                        CaseId = input.CaseId,
                        DocId = d.DocId
                    }))
                .ToList();

            if (renderTasks.Count > 0)
            {
                var renderedDocs = await Task.WhenAll(renderTasks);
                renderedDocumentSummaries.AddRange(renderedDocs);
            }

            replayLogger.LogInformation(
                "[GenerateStep] Rendered {RenderedCount} PDF files for case {CaseId}",
                renderedDocumentSummaries.Count,
                input.CaseId);
        }

        var renderedMediaSummaries = new List<RenderedArtifactSummary>();
        if (input.GenerateImages && mediaResults.Count > 0)
        {
            var renderMediaTasks = mediaResults
                .Where(m => m.Success)
                .Select(m => context.CallActivityAsync<RenderedArtifactSummary>(
                    nameof(RenderGeneratedMediaActivity),
                    new RenderGeneratedMediaInput
                    {
                        CaseId = input.CaseId,
                        EvidenceId = m.EvidenceId
                    }))
                .ToList();

            if (renderMediaTasks.Count > 0)
            {
                var renderedMedia = await Task.WhenAll(renderMediaTasks);
                renderedMediaSummaries.AddRange(renderedMedia);
            }

            replayLogger.LogInformation(
                "[GenerateStep] Generated {RenderedCount} image files for case {CaseId}",
                renderedMediaSummaries.Count,
                input.CaseId);
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
            RenderFiles = input.RenderFiles,
            RenderedDocuments = renderedDocumentSummaries,
            RenderedMedia = renderedMediaSummaries
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
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly string _casesContainer;
    private readonly ILogger<GenerateDocumentDurableActivity> _logger;

    public GenerateDocumentDurableActivity(
        ICaseGenerationService caseGenerationService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        IConfiguration configuration,
        ILogger<GenerateDocumentDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _casesContainer = configuration["CaseGeneratorStorage:CasesContainer"] ?? "cases";
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

            var documentJson = await _caseGenerationService.GenerateDocumentFromSpecAsync(
                input.Spec,
                designJson: string.Empty,
                input.CaseId,
                planJson: null,
                expandJson: null,
                input.DifficultyOverride).ConfigureAwait(false);

            var documentPath = $"{input.CaseId}/generate/documents/{input.Spec.DocId}.json";
            await _storageService.SaveFileAsync(_casesContainer, documentPath, documentJson).ConfigureAwait(false);
            _logger.LogInformation("[GenerateStep] Stored document JSON at {Path}", documentPath);

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
    private readonly IStorageService _storageService;
    private readonly string _casesContainer;
    private readonly ILogger<GenerateMediaDurableActivity> _logger;

    public GenerateMediaDurableActivity(
        ICaseGenerationService caseGenerationService,
        ICaseLoggingService caseLogging,
        IStorageService storageService,
        IConfiguration configuration,
        ILogger<GenerateMediaDurableActivity> logger)
    {
        _caseGenerationService = caseGenerationService;
        _caseLogging = caseLogging;
        _storageService = storageService;
        _casesContainer = configuration["CaseGeneratorStorage:CasesContainer"] ?? "cases";
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

            var mediaJson = await _caseGenerationService.GenerateMediaFromSpecAsync(
                input.Spec,
                designJson: string.Empty,
                input.CaseId,
                planJson: null,
                expandJson: null,
                input.DifficultyOverride).ConfigureAwait(false);

            var mediaPath = $"{input.CaseId}/generate/media/{input.Spec.EvidenceId}.json";
            await _storageService.SaveFileAsync(_casesContainer, mediaPath, mediaJson).ConfigureAwait(false);
            _logger.LogInformation("[GenerateStep] Stored media JSON at {Path}", mediaPath);

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

public class RenderGeneratedDocumentActivity
{
    private readonly IStorageService _storageService;
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly string _casesContainer;
    private readonly ILogger<RenderGeneratedDocumentActivity> _logger;

    public RenderGeneratedDocumentActivity(
        IStorageService storageService,
        ICaseGenerationService caseGenerationService,
        IConfiguration configuration,
        ILogger<RenderGeneratedDocumentActivity> logger)
    {
        _storageService = storageService;
        _caseGenerationService = caseGenerationService;
        _casesContainer = configuration["CaseGeneratorStorage:CasesContainer"] ?? "cases";
        _logger = logger;
    }

    [Function(nameof(RenderGeneratedDocumentActivity))]
    public async Task<RenderedArtifactSummary> RunAsync([ActivityTrigger] RenderGeneratedDocumentInput input)
    {
        var documentPath = $"{input.CaseId}/generate/documents/{input.DocId}.json";
        _logger.LogInformation("[GenerateStep] Loading document JSON from {Path}", documentPath);

        var documentJson = await _storageService.GetFileAsync(_casesContainer, documentPath).ConfigureAwait(false);
        var renderedPath = await _caseGenerationService.RenderDocumentFromJsonAsync(input.DocId, documentJson, input.CaseId).ConfigureAwait(false);

        return new RenderedArtifactSummary
        {
            Id = input.DocId,
            Path = renderedPath,
            Kind = "pdf"
        };
    }
}

public class RenderGeneratedMediaActivity
{
    private readonly IStorageService _storageService;
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly string _casesContainer;
    private readonly ILogger<RenderGeneratedMediaActivity> _logger;

    public RenderGeneratedMediaActivity(
        IStorageService storageService,
        ICaseGenerationService caseGenerationService,
        IConfiguration configuration,
        ILogger<RenderGeneratedMediaActivity> logger)
    {
        _storageService = storageService;
        _caseGenerationService = caseGenerationService;
        _casesContainer = configuration["CaseGeneratorStorage:CasesContainer"] ?? "cases";
        _logger = logger;
    }

    [Function(nameof(RenderGeneratedMediaActivity))]
    public async Task<RenderedArtifactSummary> RunAsync([ActivityTrigger] RenderGeneratedMediaInput input)
    {
        var mediaPath = $"{input.CaseId}/generate/media/{input.EvidenceId}.json";
        _logger.LogInformation("[GenerateStep] Loading media JSON from {Path}", mediaPath);

        var mediaJson = await _storageService.GetFileAsync(_casesContainer, mediaPath).ConfigureAwait(false);
        var mediaSpec = JsonSerializer.Deserialize<MediaSpec>(mediaJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Unable to parse media spec for {input.EvidenceId}");

        var renderedPath = await _caseGenerationService.RenderMediaFromJsonAsync(mediaSpec, input.CaseId).ConfigureAwait(false);

        return new RenderedArtifactSummary
        {
            Id = input.EvidenceId,
            Path = renderedPath,
            Kind = mediaSpec.Kind
        };
    }
}
