using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services.QA;

/// <summary>
/// EPIC 4.1: Service for simulating player reasoning and validating case solvability.
/// </summary>
public interface IPlayabilitySimulationService
{
    /// <summary>
    /// Simulates detective reasoning to validate if the case is solvable without guessing.
    /// </summary>
    Task<PlayabilitySimulationResult> SimulatePlayabilityAsync(
        string caseId,
        string normalizedBundle,
        string? difficulty = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EPIC 4.2: Converts playability issues into QA issues that can be processed by the Fix loop.
    /// </summary>
    List<QaScanIssue> ConvertToQaIssues(PlayabilitySimulationResult simulationResult);
}
