using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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

        var normalizationPhase = await context.CallActivityAsync<NormalizeCaseDurableActivityResult>(
            nameof(NormalizeStepDurableActivity),
            input);

        var validationPhase = await RunValidationPhaseAsync(context, input.CaseId, normalizationPhase.NormalizedJson);

        // Build initial manifest so QA activities can rely on fresh context
        await context.CallActivityAsync<string>(
            "NormalizeManifestActivity",
            new NormalizeManifestActivityModel { CaseId = input.CaseId });

        var qualityPhase = await RunQualityPhaseAsync(context, input.CaseId, input.MaxQaIterations, replayLogger);

        var latestManifestJson = await context.CallActivityAsync<string>(
            "NormalizeManifestActivity",
            new NormalizeManifestActivityModel { CaseId = input.CaseId });

        var packagingPhase = await RunPackagingPhaseAsync(
            context,
            input.CaseId,
            latestManifestJson,
            normalizationPhase.ManifestContextPath);

        var completedAt = context.CurrentUtcDateTime;
        var duration = (completedAt - input.RequestedAtUtc).TotalSeconds;

        var output = new NormalizeStepDurableResult
        {
            CaseId = input.CaseId,
            InstanceId = context.InstanceId,
            RequestedAtUtc = input.RequestedAtUtc,
            CompletedAtUtc = completedAt,
            DurationSeconds = duration,
            DocumentsLoaded = normalizationPhase.DocumentsLoaded,
            MediaLoaded = normalizationPhase.MediaLoaded,
            Manifest = normalizationPhase.Manifest,
            Log = normalizationPhase.Log,
            FilesSaved = normalizationPhase.FilesSaved,
            Validation = validationPhase,
            Quality = qualityPhase,
            Packaging = packagingPhase
        };

        replayLogger.LogInformation(
            "[NormalizeStep] Orchestration finished for case {CaseId} in {DurationSeconds:N2}s",
            input.CaseId,
            duration);

        return output;
    }

    private static async Task<ValidationPhaseResult> RunValidationPhaseAsync(
        TaskOrchestrationContext context,
        string caseId,
        string normalizedJson)
    {
        var start = context.CurrentUtcDateTime;

        var output = await context.CallActivityAsync<string>(
            "ValidateRulesActivity",
            new ValidateActivityModel
            {
                CaseId = caseId,
                NormalizedJson = normalizedJson
            });

        var completed = context.CurrentUtcDateTime;
        return new ValidationPhaseResult
        {
            Completed = true,
            DurationSeconds = (completed - start).TotalSeconds,
            Output = output
        };
    }

    private static async Task<QaPhaseResult> RunQualityPhaseAsync(
        TaskOrchestrationContext context,
        string caseId,
        int maxIterations,
        ILogger logger)
    {
        var iterationSummaries = new List<QaIterationSummary>();
        var iteration = 1;
        var isClean = false;

        while (iteration <= maxIterations)
        {
            var scanResultJson = await context.CallActivityAsync<string>(
                "QA_ScanIssuesActivity",
                new QA_ScanIssuesActivityModel { CaseId = caseId });

            var issues = ParseScanIssues(scanResultJson);
            if (issues.Count == 0)
            {
                iterationSummaries.Add(new QaIterationSummary
                {
                    Iteration = iteration,
                    IssuesFound = 0,
                    FixesApplied = 0,
                    RemainingIssues = Array.Empty<string>(),
                    IsCleanAfterIteration = true
                });
                isClean = true;
                break;
            }

            logger.LogInformation(
                "[NormalizeStep][QA] Iteration {Iteration} scanning found {Count} issue(s)",
                iteration,
                issues.Count);

            var deepDiveTasks = issues
                .Select(issue => context.CallActivityAsync<string>(
                    "QA_DeepDiveActivity",
                    new QA_DeepDiveActivityModel
                    {
                        CaseId = caseId,
                        IssueArea = issue.Area
                    }))
                .ToArray();

            var deepDiveResults = await Task.WhenAll(deepDiveTasks);
            var fixModels = BuildFixModels(caseId, deepDiveResults);
            var fixTasks = fixModels
                .Select(model => context.CallActivityAsync<string>("FixEntityActivity", model))
                .ToArray();
            var fixResults = await Task.WhenAll(fixTasks);
            var fixesApplied = CountSuccessfulFixes(fixResults);

            var verificationJson = await context.CallActivityAsync<string>(
                "CheckCaseCleanActivityV2",
                new CheckCaseCleanActivityV2Model
                {
                    CaseId = caseId,
                    IssueAreas = issues.Select(i => i.Area).ToArray()
                });

            var (iterationClean, remainingIssues) = ParseVerificationResult(
                verificationJson,
                issues.Select(i => i.Area).ToArray());

            iterationSummaries.Add(new QaIterationSummary
            {
                Iteration = iteration,
                IssuesFound = issues.Count,
                FixesApplied = fixesApplied,
                RemainingIssues = remainingIssues,
                IsCleanAfterIteration = iterationClean
            });

            if (iterationClean)
            {
                isClean = true;
                break;
            }

            iteration++;
        }

        return new QaPhaseResult
        {
            RequestedIterations = maxIterations,
            ExecutedIterations = iterationSummaries.Count,
            IsCaseClean = isClean,
            Iterations = iterationSummaries
        };
    }

    private static async Task<PackagingPhaseResult> RunPackagingPhaseAsync(
        TaskOrchestrationContext context,
        string caseId,
        string manifestJson,
        string? manifestContextPath)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return new PackagingPhaseResult();
        }

        var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJson));
        var packageOutput = await context.CallActivityAsync<CaseGenerationOutput>(
            "PackageActivity",
            new PackageActivityModel
            {
                CaseId = caseId,
                FinalJson = base64Payload
            });

        return new PackagingPhaseResult
        {
            Completed = true,
            BundlePath = packageOutput.BundlePath,
            ManifestPath = manifestContextPath,
            Output = packageOutput
        };
    }

    private static List<QaScanIssue> ParseScanIssues(string scanResultJson)
    {
        try
        {
            var document = JsonSerializer.Deserialize<JsonElement>(scanResultJson);
            if (document.TryGetProperty("issues", out var issuesElement) &&
                issuesElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<QaScanIssue>>(issuesElement.GetRawText())
                    ?? new List<QaScanIssue>();
            }
        }
        catch
        {
            // ignore parsing errors, return empty list
        }

        return new List<QaScanIssue>();
    }

    private static List<FixEntityActivityModel> BuildFixModels(string caseId, IReadOnlyList<string> deepDiveResults)
    {
        var models = new List<FixEntityActivityModel>();
        foreach (var resultJson in deepDiveResults)
        {
            try
            {
                var result = JsonSerializer.Deserialize<JsonElement>(resultJson);
                var entityId = result.TryGetProperty("affectedEntities", out var entities) && entities.ValueKind == JsonValueKind.Array && entities.GetArrayLength() > 0
                    ? entities[0].GetString() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrEmpty(entityId))
                {
                    continue;
                }

                var problemDetails = result.TryGetProperty("problemDetails", out var detailsProp)
                    ? detailsProp.GetString() ?? string.Empty
                    : string.Empty;
                var suggestedFix = result.TryGetProperty("suggestedFix", out var fixProp)
                    ? fixProp.GetString() ?? string.Empty
                    : string.Empty;

                var issueDescription = string.IsNullOrWhiteSpace(problemDetails) && string.IsNullOrWhiteSpace(suggestedFix)
                    ? "See QA analysis for details."
                    : $"{problemDetails} | Fix: {suggestedFix}".Trim();

                models.Add(new FixEntityActivityModel
                {
                    CaseId = caseId,
                    EntityId = entityId,
                    IssueDescription = issueDescription
                });
            }
            catch
            {
                // ignore malformed payloads
            }
        }

        return models;
    }

    private static int CountSuccessfulFixes(IEnumerable<string> fixResults)
    {
        var successes = 0;
        foreach (var fixJson in fixResults)
        {
            try
            {
                var result = JsonSerializer.Deserialize<JsonElement>(fixJson);
                if (result.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                {
                    successes++;
                }
            }
            catch
            {
                // ignore malformed payloads
            }
        }

        return successes;
    }

    private static (bool IsClean, IReadOnlyList<string> RemainingIssues) ParseVerificationResult(
        string verificationJson,
        IReadOnlyList<string> fallbackIssues)
    {
        try
        {
            var document = JsonSerializer.Deserialize<JsonElement>(verificationJson);
            var isClean = document.TryGetProperty("isClean", out var cleanProp) && cleanProp.GetBoolean();

            var remainingIssues = new List<string>();
            if (document.TryGetProperty("remainingIssues", out var issuesProp) && issuesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var issue in issuesProp.EnumerateArray())
                {
                    var value = issue.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        remainingIssues.Add(value);
                    }
                }
            }

            return (isClean, remainingIssues);
        }
        catch
        {
            // ignore parse errors and fall back
        }

        return (false, fallbackIssues.ToArray());
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
                NormalizedJson = normalizationResult.NormalizedJson,
                Manifest = normalizationResult.Manifest,
                Log = normalizationResult.Log,
                DocumentsPersisted = documents.Count,
                MediaPersisted = media.Count,
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
