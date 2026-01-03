using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services.QA;

/// <summary>
/// EPIC 4.1: Service for simulating player reasoning and validating case solvability.
/// Acts as an experienced detective to identify dead ends, excessive ambiguity, and missing links.
/// </summary>
public class PlayabilitySimulationService : IPlayabilitySimulationService
{
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<PlayabilitySimulationService> _logger;

    public PlayabilitySimulationService(
        ILLMService llmService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        ILogger<PlayabilitySimulationService> logger)
    {
        _llmService = llmService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    /// <summary>
    /// Simulates detective reasoning to validate if the case is solvable without guessing.
    /// </summary>
    public async Task<PlayabilitySimulationResult> SimulatePlayabilityAsync(
        string caseId,
        string normalizedBundle,
        string? difficulty = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EPIC 4.1: Starting playability simulation for case {CaseId}", caseId);

        var systemPrompt = @"
You are an EXPERIENCED DETECTIVE and GAME DESIGN EVALUATOR. Your role is to assess if a cold case investigation game is SOLVABLE through logical reasoning alone (no guessing).

EVALUATION CRITERIA:
1. **Reasoning Paths**: Can a player deduce the solution through evidence analysis and logical inference?
2. **Dead Ends**: Are there investigation paths that lead nowhere without feedback?
3. **Ambiguity**: Is there excessive ambiguity that prevents definitive conclusions?
4. **Missing Links**: Are there logical gaps where critical connections are absent?
5. **Circular Reasoning**: Do any reasoning paths loop without progress?

YOUR TASK:
- Assume the role of a skilled detective playing this case
- Identify ALL possible reasoning paths a player might take
- For each path, determine if it leads to the solution, hits a dead end, or becomes ambiguous
- Flag CRITICAL issues that make the case unsolvable
- Provide specific, actionable recommendations for fixes

ASSESSMENT STANDARDS:
- **SOLVABLE**: At least ONE clear reasoning path exists from evidence to solution
- **DEAD END**: Investigation path with no way to progress or validate conclusions
- **EXCESSIVE AMBIGUITY**: Multiple equally valid interpretations with no way to narrow down
- **MISSING LINK**: Gap in logic where a connection should exist but doesn't
- **UNSUPPORTED CONCLUSION**: Required logical leap without supporting evidence

OUTPUT: JSON with structured analysis following PlayabilitySimulationResult schema.";

        var userPrompt = $@"
Analyze the following NORMALIZED CASE BUNDLE and determine if it is solvable by a player using logical reasoning.

CASE ID: {caseId}
DIFFICULTY LEVEL: {difficulty ?? "Unknown"}

NORMALIZED BUNDLE:
{normalizedBundle}

SIMULATION INSTRUCTIONS:
1. Read all documents and evidence carefully
2. Identify the KNOWN GROUND TRUTH (who is the perpetrator, how the crime occurred)
3. Map out ALL possible reasoning paths a player could take
4. For each path, evaluate:
   - What evidence/documents does it require?
   - Does it lead to the correct conclusion?
   - Are there dead ends or missing links?
   - Is the reasoning logically sound?
5. Identify specific playability issues:
   - Dead ends where investigation stalls
   - Excessive ambiguity with no resolution
   - Missing connections between evidence
   - Unsupported logical leaps
   - Circular reasoning that goes nowhere
6. Determine overall solvability (true/false)
7. Provide concrete recommendations for improvements

CRITICAL: Focus on whether a PLAYER (not you as an AI) could solve this through gameplay.

OUTPUT: Valid JSON matching PlayabilitySimulationResult schema:
{{
  ""caseId"": ""string"",
  ""isSolvable"": boolean,
  ""overallAssessment"": ""string"",
  ""reasoningPaths"": [
    {{
      ""pathId"": ""PATH001"",
      ""description"": ""string"",
      ""steps"": [""step1"", ""step2"", ...],
      ""outcome"": ""leads_to_solution"" | ""dead_end"" | ""ambiguous"",
      ""requiredEvidences"": [""EV001"", ...],
      ""requiredDocuments"": [""DOC001"", ...]
    }}
  ],
  ""issues"": [
    {{
      ""issueId"": ""ISSUE001"",
      ""type"": ""dead_end"" | ""excessive_ambiguity"" | ""missing_link"" | ""unsupported_conclusion"" | ""circular_reasoning"",
      ""severity"": ""critical"" | ""high"" | ""medium"" | ""low"",
      ""description"": ""string"",
      ""affectedEntities"": [""S001"", ""EV003"", ...],
      ""impact"": ""string"",
      ""suggestedFix"": ""string""
    }}
  ],
  ""recommendations"": [""string"", ...]
}}";

        try
        {
            var responseJson = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
            
            // Log the response
            await _caseLogging.LogStepResponseAsync(caseId, "playability_simulation", responseJson, cancellationToken);

            // Parse the result
            var result = JsonSerializer.Deserialize<PlayabilitySimulationResult>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new InvalidOperationException("Failed to deserialize playability simulation result");

            // Save the simulation result
            var bundlesContainer = "bundles"; // TODO: Get from configuration
            var simulationPath = $"{caseId}/qa/playability_simulation.json";
            await _storageService.SaveFileAsync(bundlesContainer, simulationPath, responseJson, cancellationToken);

            _logger.LogInformation(
                "EPIC 4.1: Playability simulation completed - Solvable: {IsSolvable}, Issues: {IssueCount}, Paths: {PathCount}",
                result.IsSolvable, result.Issues.Length, result.ReasoningPaths.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EPIC 4.1: Failed to simulate playability for case {CaseId}", caseId);
            
            // Return a failure result
            return new PlayabilitySimulationResult
            {
                CaseId = caseId,
                IsSolvable = false,
                OverallAssessment = $"Simulation failed: {ex.Message}",
                ReasoningPaths = Array.Empty<ReasoningPath>(),
                Issues = new[]
                {
                    new PlayabilityIssue
                    {
                        IssueId = "SIM_ERROR",
                        Type = "simulation_failure",
                        Severity = "critical",
                        Description = $"Playability simulation failed: {ex.Message}",
                        AffectedEntities = Array.Empty<string>(),
                        Impact = "Cannot determine if case is solvable",
                        SuggestedFix = "Review case structure and retry simulation"
                    }
                },
                Recommendations = new[] { "Fix simulation errors and retry", "Verify case bundle structure" }
            };
        }
    }
}
