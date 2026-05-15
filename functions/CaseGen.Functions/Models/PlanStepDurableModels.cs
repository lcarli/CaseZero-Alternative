using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models;

public record PlanStepDurableInput
{
    [JsonPropertyName("request")]
    public CaseGenerationRequest Request { get; init; } = new();

    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
}

public record PlanCaseActivityResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("planBlobPath")]
    public required string PlanBlobPath { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("difficulty")]
    public required string Difficulty { get; init; }

    [JsonPropertyName("timezone")]
    public required string Timezone { get; init; }
}

public record PlanStepDurableResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("instanceId")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime CompletedAtUtc { get; init; }

    [JsonPropertyName("planBlobPath")]
    public required string PlanBlobPath { get; init; }

    [JsonPropertyName("difficulty")]
    public required string Difficulty { get; init; }

    [JsonPropertyName("timezone")]
    public required string Timezone { get; init; }
}

public record ExpandStepDurableInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
}

public record ExpandCaseActivityResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("expandBlobPath")]
    public required string ExpandBlobPath { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("filesSaved")]
    public IReadOnlyList<string> FilesSaved { get; init; } = Array.Empty<string>();
}

public record ExpandStepDurableResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("instanceId")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime CompletedAtUtc { get; init; }

    [JsonPropertyName("expandBlobPath")]
    public required string ExpandBlobPath { get; init; }

    [JsonPropertyName("filesSaved")]
    public IReadOnlyList<string> FilesSaved { get; init; } = Array.Empty<string>();
}

public record DesignStepDurableInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
}

public record DesignCaseActivityResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("designBlobPath")]
    public required string DesignBlobPath { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("filesSaved")]
    public IReadOnlyList<string> FilesSaved { get; init; } = Array.Empty<string>();
}

public record DesignStepDurableResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("instanceId")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime CompletedAtUtc { get; init; }

    [JsonPropertyName("designBlobPath")]
    public required string DesignBlobPath { get; init; }

    [JsonPropertyName("filesSaved")]
    public IReadOnlyList<string> FilesSaved { get; init; } = Array.Empty<string>();
}

public record GenerateStepDurableInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("generateImages")]
    public bool GenerateImages { get; init; }

    [JsonPropertyName("renderFiles")]
    public bool RenderFiles { get; init; }

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
}

public record GenerateDocumentTaskSummary
{
    [JsonPropertyName("docId")]
    public string DocId { get; init; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }
}

public record GenerateMediaTaskSummary
{
    [JsonPropertyName("evidenceId")]
    public string EvidenceId { get; init; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }
}

public record GenerateStepDurableResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("instanceId")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime CompletedAtUtc { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("documentsRequested")]
    public int DocumentsRequested { get; init; }

    [JsonPropertyName("mediaRequested")]
    public int MediaRequested { get; init; }

    [JsonPropertyName("documents")]
    public IReadOnlyList<GenerateDocumentTaskSummary> Documents { get; init; } = Array.Empty<GenerateDocumentTaskSummary>();

    [JsonPropertyName("media")]
    public IReadOnlyList<GenerateMediaTaskSummary> Media { get; init; } = Array.Empty<GenerateMediaTaskSummary>();

    [JsonPropertyName("generateImages")]
    public bool GenerateImages { get; init; }

    [JsonPropertyName("renderFiles")]
    public bool RenderFiles { get; init; }

    [JsonPropertyName("renderedDocuments")]
    public IReadOnlyList<RenderedArtifactSummary> RenderedDocuments { get; init; } = Array.Empty<RenderedArtifactSummary>();

    [JsonPropertyName("renderedMedia")]
    public IReadOnlyList<RenderedArtifactSummary> RenderedMedia { get; init; } = Array.Empty<RenderedArtifactSummary>();
}

public record LoadDesignSpecsActivityInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;
}

public record GenerateDocumentDurableActivityInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("spec")]
    public required DocumentSpec Spec { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("difficultyOverride")]
    public string? DifficultyOverride { get; init; }
}

public record NormalizeStepDurableInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("timezone")]
    public string Timezone { get; init; } = "UTC";

    [JsonPropertyName("difficulty")]
    public string? Difficulty { get; init; }

    [JsonPropertyName("maxQaIterations")]
    public int MaxQaIterations { get; init; } = 3;
}

public record NormalizeCaseDurableActivityResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("documentsLoaded")]
    public int DocumentsLoaded { get; init; }

    [JsonPropertyName("mediaLoaded")]
    public int MediaLoaded { get; init; }

    [JsonPropertyName("normalizedJson")]
    public string NormalizedJson { get; init; } = string.Empty;

    [JsonPropertyName("manifest")]
    public required CaseManifest Manifest { get; init; }

    [JsonPropertyName("log")]
    public required NormalizationLog Log { get; init; }

    [JsonPropertyName("documentsPersisted")]
    public int DocumentsPersisted { get; init; }

    [JsonPropertyName("mediaPersisted")]
    public int MediaPersisted { get; init; }

    [JsonPropertyName("normalizedContextPath")]
    public string? NormalizedContextPath { get; init; }

    [JsonPropertyName("manifestContextPath")]
    public string? ManifestContextPath { get; init; }

    [JsonPropertyName("filesSaved")]
    public IReadOnlyList<string> FilesSaved { get; init; } = Array.Empty<string>();
}

public record NormalizeStepDurableResult
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }

    [JsonPropertyName("instanceId")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime CompletedAtUtc { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("documentsLoaded")]
    public int DocumentsLoaded { get; init; }

    [JsonPropertyName("mediaLoaded")]
    public int MediaLoaded { get; init; }

    [JsonPropertyName("manifest")]
    public required CaseManifest Manifest { get; init; }

    [JsonPropertyName("log")]
    public required NormalizationLog Log { get; init; }

    [JsonPropertyName("filesSaved")]
    public IReadOnlyList<string> FilesSaved { get; init; } = Array.Empty<string>();

    [JsonPropertyName("validation")]
    public ValidationPhaseResult Validation { get; init; } = new();

    [JsonPropertyName("quality")]
    public QaPhaseResult Quality { get; init; } = new();

    [JsonPropertyName("packaging")]
    public PackagingPhaseResult Packaging { get; init; } = new();
}

public record ValidationPhaseResult
{
    [JsonPropertyName("completed")]
    public bool Completed { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }
}

public record QaIterationSummary
{
    [JsonPropertyName("iteration")]
    public int Iteration { get; init; }

    [JsonPropertyName("issuesFound")]
    public int IssuesFound { get; init; }

    [JsonPropertyName("fixesApplied")]
    public int FixesApplied { get; init; }

    [JsonPropertyName("remainingIssues")]
    public IReadOnlyList<string> RemainingIssues { get; init; } = Array.Empty<string>();

    [JsonPropertyName("isCleanAfterIteration")]
    public bool IsCleanAfterIteration { get; init; }
}

public record QaPhaseResult
{
    [JsonPropertyName("requestedIterations")]
    public int RequestedIterations { get; init; }

    [JsonPropertyName("executedIterations")]
    public int ExecutedIterations { get; init; }

    [JsonPropertyName("isCaseClean")]
    public bool IsCaseClean { get; init; }

    [JsonPropertyName("iterations")]
    public IReadOnlyList<QaIterationSummary> Iterations { get; init; } = Array.Empty<QaIterationSummary>();
}

public record PackagingPhaseResult
{
    [JsonPropertyName("completed")]
    public bool Completed { get; init; }

    [JsonPropertyName("bundlePath")]
    public string? BundlePath { get; init; }

    [JsonPropertyName("manifestPath")]
    public string? ManifestPath { get; init; }

    [JsonPropertyName("output")]
    public CaseGenerationOutput? Output { get; init; }
}

public record GenerateMediaDurableActivityInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("spec")]
    public required MediaSpec Spec { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("difficultyOverride")]
    public string? DifficultyOverride { get; init; }
}

public record RenderGeneratedDocumentInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("docId")]
    public string DocId { get; init; } = string.Empty;
}

public record RenderGeneratedMediaInput
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("evidenceId")]
    public string EvidenceId { get; init; } = string.Empty;
}

public record RenderedArtifactSummary
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;
}
