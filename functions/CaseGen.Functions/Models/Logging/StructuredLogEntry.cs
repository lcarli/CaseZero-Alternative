using System;

namespace CaseGen.Functions.Models;

public enum LogCategory
{
    WorkflowStep,
    PhaseTransition,
    LlmInteraction,
    Detailed,
    ExecutiveSummary,
    Metadata,
    Payload,
    StatusSnapshot
}

public sealed record TokenUsageSummary
{
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
}

public sealed record StructuredLogEntry
{
    public string CaseId { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public LogCategory Category { get; init; }
    public string Level { get; init; } = "Information";
    public string? Source { get; init; }
    public string? Phase { get; init; }
    public string? Step { get; init; }
    public string? Activity { get; init; }
    public string? TraceId { get; init; }
    public double? DurationMs { get; init; }
    public double? Progress { get; init; }
    public TokenUsageSummary? Tokens { get; init; }
    public string? Status { get; init; }
    public string? Message { get; init; }
    public object? Data { get; init; }
    public string? PayloadReference { get; init; }
    public string? Error { get; init; }
}
