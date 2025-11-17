using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public interface ICaseLoggingService
{
    /// <summary>
    /// Logs an orchestrator step for console output (clean and readable)
    /// </summary>
    void LogOrchestratorStep(string caseId, string step, string details = "");

    /// <summary>
    /// Logs orchestrator progress for console output
    /// </summary>
    void LogOrchestratorProgress(string caseId, int currentStep, int totalSteps, string stepName);

    /// <summary>
    /// Logs detailed information to blob storage (verbose logging)
    /// </summary>
    Task LogDetailedAsync(string caseId, string source, string level, string message, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a structured log entry (NDJSON friendly) to blob storage
    /// </summary>
    Task LogStructuredAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs LLM interaction details to blob storage
    /// </summary>
    Task LogLLMInteractionAsync(string caseId, string provider, string promptType, string prompt, string response, int? tokenCount = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the detailed log content for a case
    /// </summary>
    Task<string> GetDetailedLogAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a step response as formatted JSON in case folder structure
    /// </summary>
    Task LogStepResponseAsync(string caseId, string stepName, string jsonResponse, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs step metadata as JSON in case folder structure
    /// </summary>
    Task LogStepMetadataAsync(string caseId, string stepName, object metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a workflow step with clear phase tracking for better readability
    /// </summary>
    Task LogWorkflowStepAsync(string caseId, string phase, string step, object? stepData = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs workflow phase transitions with summary data
    /// </summary>
    Task LogPhaseTransitionAsync(string caseId, string fromPhase, string toPhase, object? phaseData = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an executive summary of the workflow execution for easy review
    /// </summary>
    Task CreateExecutiveSummaryAsync(string caseId, object summaryData, CancellationToken cancellationToken = default);
}
