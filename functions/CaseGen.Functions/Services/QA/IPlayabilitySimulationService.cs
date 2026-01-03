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
}
