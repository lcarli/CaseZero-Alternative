using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services.CaseGeneration;

/// <summary>
/// Service responsible for generating initial case plans (Phase 2: Hierarchical Plan).
/// Handles core structure, suspects, timeline, evidence planning, and legacy monolithic plan.
/// </summary>
public class PlanGenerationService
{
    private readonly ILLMService _llmService;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly IContextManager _contextManager;
    private readonly ILogger<PlanGenerationService> _logger;

    public PlanGenerationService(
        ILLMService llmService,
        IJsonSchemaProvider schemaProvider,
        IContextManager contextManager,
        ILogger<PlanGenerationService> logger)
    {
        _llmService = llmService;
        _schemaProvider = schemaProvider;
        _contextManager = contextManager;
        _logger = logger;
    }

    /// <summary>
    /// Generates the core case structure (Phase 2.1).
    /// </summary>
    public async Task<string> PlanCoreAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken = default)
    {
        var actualDifficulty = request.Difficulty ?? DifficultyLevels.AllLevels[Random.Shared.Next(DifficultyLevels.AllLevels.Length)];
        var difficultyProfile = DifficultyLevels.GetProfile(actualDifficulty);

        _logger.LogInformation("PLAN-CORE: Generating core case structure for {CaseId}, difficulty={Difficulty}", caseId, actualDifficulty);

        var systemPrompt = $@"
You are a master architect of investigative cold cases. Generate the CORE STRUCTURE of a case plan.

DIFFICULTY PROFILE: {actualDifficulty}
Description: {difficultyProfile?.Description}

COMPLEXITY GUIDELINES:
- Suspects: {difficultyProfile?.Suspects.Min}-{difficultyProfile?.Suspects.Max}
- Documents: {difficultyProfile?.Documents.Min}-{difficultyProfile?.Documents.Max}
- Evidence items: {difficultyProfile?.Evidences.Min}-{difficultyProfile?.Evidences.Max}
- False leads: {difficultyProfile?.RedHerrings}
- Gated documents: {difficultyProfile?.GatedDocuments}
- Forensics complexity: {difficultyProfile?.ForensicsComplexity}
- Estimated duration: {difficultyProfile?.EstimatedDurationMinutes.Min}-{difficultyProfile?.EstimatedDurationMinutes.Max} minutes

GEOGRAPHY/NAMING POLICY:
- No real street names, numbers, coordinates, or real brands/companies
- If a real city is needed, limit to City/State only; prefer abstract locations
- All names must be plausible and fictitious

OUTPUT: ONLY valid JSON conforming to PlanCore schema. No comments or extra text.";

        var userPrompt = $@"
Generate the CORE STRUCTURE for a new investigative case.

INPUT:
- Difficulty: {actualDifficulty}
- Timezone: {request.Timezone}
- Generate images (metadata only): {request.GenerateImages}

MANDATORY CONTENT (PlanCore schema):
- caseId: ""{caseId}""
- title: Strong, specific, engaging case title
- location: Plausible abstract location (no real addresses)
- incidentType: Coherent with difficulty
- difficulty: ""{actualDifficulty}""
- timezone: ""{request.Timezone}""
- estimatedDuration: Based on difficulty profile
- overview: Clear investigative scope without revealing solution
- learningObjectives[]: Concrete, observable, testable objectives
- minDetectiveRank: ""{actualDifficulty}""
- profileApplied: Numeric ranges from difficulty profile

OUTPUT FORMAT: ONLY JSON valid by PlanCore schema.";

        var jsonSchema = _schemaProvider.GetSchema("PlanCore");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
        
        // Save to context storage
        await _contextManager.SaveContextAsync(caseId, "plan/core", result, cancellationToken);
        
        return result;
    }

    /// <summary>
    /// Generates initial suspect list (Phase 2.2).
    /// </summary>
    public async Task<string> PlanSuspectsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PLAN-SUSPECTS: Generating suspect profiles for {CaseId}", caseId);

        // Load core plan from context
        var coreSnapshot = await _contextManager.BuildSnapshotAsync(caseId, new[] { "@plan/core" }, cancellationToken);
        var corePlan = coreSnapshot.Items["plan/core"] as string ?? throw new InvalidOperationException("Core plan not found");

        var coreData = JsonDocument.Parse(corePlan);
        var difficulty = coreData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);

        var systemPrompt = $@"
You are a specialist in developing suspect profiles for investigative cases.

DIFFICULTY: {difficulty}
SUSPECT RANGE: {difficultyProfile.Suspects.Min}-{difficultyProfile.Suspects.Max}

Generate an initial list of suspects with:
- Unique IDs (S001, S002, etc.)
- Plausible fictitious names
- Clear roles/relationships to the case
- Initial motivations that will be expanded later

IMPORTANT: Do NOT reveal the culprit. Create plausible suspects where investigation is needed.

OUTPUT: ONLY valid JSON conforming to PlanSuspects schema.";

        var userPrompt = $@"
Based on this core case plan, generate the initial suspect list:

{corePlan}

Generate between {difficultyProfile.Suspects.Min} and {difficultyProfile.Suspects.Max} suspects.
Each suspect must have: id (S001 format), name, role, initialMotivation.

OUTPUT FORMAT: ONLY JSON valid by PlanSuspects schema.";

        var jsonSchema = _schemaProvider.GetSchema("PlanSuspects");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
        
        // Save to context storage
        await _contextManager.SaveContextAsync(caseId, "plan/suspects", result, cancellationToken);
        
        return result;
    }

    /// <summary>
    /// Generates chronological timeline of events (Phase 2.3).
    /// </summary>
    public async Task<string> PlanTimelineAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PLAN-TIMELINE: Generating timeline for {CaseId}", caseId);

        // Load core plan and suspects from context
        var snapshot = await _contextManager.BuildSnapshotAsync(caseId, new[] { "@plan/core", "@plan/suspects" }, cancellationToken);
        var corePlan = snapshot.Items["plan/core"] as string ?? throw new InvalidOperationException("Core plan not found");
        var suspects = snapshot.Items["plan/suspects"] as string ?? throw new InvalidOperationException("Suspects not found");

        var coreData = JsonDocument.Parse(corePlan);
        var timezone = coreData.RootElement.GetProperty("timezone").GetString();

        var systemPrompt = $@"
You are a timeline architect for investigative cases.

TIMEZONE: {timezone}

Create a chronologically ordered timeline of events with:
- Unique event IDs (E001, E002, etc.)
- ISO-8601 timestamps with timezone offset ({timezone})
- Brief but specific event titles
- Locations (can be abstract)
- References to involved suspects

TEMPORAL CONSISTENCY (CRITICAL):
- ALL timestamps MUST use ISO-8601 with timezone offset
- Events must be chronologically ordered
- NO overlapping or conflicting timestamps
- Each event gets a unique, logical time slot

OUTPUT: ONLY valid JSON conforming to PlanTimeline schema.";

        var userPrompt = $@"
Based on this case core and suspects, generate a chronological timeline:

CORE PLAN:
{corePlan}

SUSPECTS:
{suspects}

Create 5-10 key events that:
- Use timezone {timezone}
- Reference suspect IDs where relevant
- Are chronologically ordered
- Have realistic time spacing

OUTPUT FORMAT: ONLY JSON valid by PlanTimeline schema.";

        var jsonSchema = _schemaProvider.GetSchema("PlanTimeline");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
        
        // Save to context storage
        await _contextManager.SaveContextAsync(caseId, "plan/timeline", result, cancellationToken);
        
        return result;
    }

    /// <summary>
    /// Generates evidence plan with golden truth facts (Phase 2.4).
    /// </summary>
    public async Task<string> PlanEvidenceAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PLAN-EVIDENCE: Generating evidence plan for {CaseId}", caseId);

        // Load all previous plan components from context
        var snapshot = await _contextManager.BuildSnapshotAsync(caseId, 
            new[] { "@plan/core", "@plan/suspects", "@plan/timeline" }, 
            cancellationToken);
        
        var corePlan = snapshot.Items["plan/core"] as string ?? throw new InvalidOperationException("Core plan not found");
        var suspects = snapshot.Items["plan/suspects"] as string ?? throw new InvalidOperationException("Suspects not found");
        var timeline = snapshot.Items["plan/timeline"] as string ?? throw new InvalidOperationException("Timeline not found");

        var coreData = JsonDocument.Parse(corePlan);
        var difficulty = coreData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);

        var systemPrompt = $@"
You are an evidence architect for investigative cases.

DIFFICULTY: {difficulty}
EVIDENCE RANGE: {difficultyProfile.Evidences.Min}-{difficultyProfile.Evidences.Max}
FALSE LEADS: {difficultyProfile.RedHerrings}

Generate:
1) mainElements[]: Core evidence types that will be developed
   - Examples: witness statements, logs, receipts, CCTV snapshots, forensic reports, etc.
   - Must align with difficulty level

2) goldenTruth.facts[]: Sealed true facts that MUST be supported by evidence
   - Each fact needs minSupports ≥ 2
   - Should reference suspects/events
   - Do NOT reveal the culprit
   - Must be verifiable through heterogeneous sources

OUTPUT: ONLY valid JSON conforming to PlanEvidence schema.";

        var userPrompt = $@"
Based on this case structure, generate the evidence plan:

CORE PLAN:
{corePlan}

SUSPECTS:
{suspects}

TIMELINE:
{timeline}

Generate:
- mainElements: {difficultyProfile.Evidences.Min}-{difficultyProfile.Evidences.Max} evidence types
- goldenTruth.facts: 3-7 key facts that must be proven
  - Each with minSupports: 2-4
  - Reference suspect/event IDs where relevant
  - heterogeneous: true for varied sources

OUTPUT FORMAT: ONLY JSON valid by PlanEvidence schema.";

        var jsonSchema = _schemaProvider.GetSchema("PlanEvidence");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
        
        // Save to context storage
        await _contextManager.SaveContextAsync(caseId, "plan/evidence", result, cancellationToken);
        
        return result;
    }

    /// <summary>
    /// LEGACY: Original monolithic plan generation method (Phase 1).
    /// Generates complete plan in one LLM call. Use hierarchical methods (PlanCore -> PlanSuspects -> etc.) for new cases.
    /// </summary>
    public async Task<string> PlanCaseAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken = default)
    {
        // Resolve difficulty/profile com fallback robusto
        var requestedDiff = request.Difficulty;
        var difficultyProfile = DifficultyLevels.GetProfile(requestedDiff);
        var actualDifficulty = request.Difficulty ?? DifficultyLevels.AllLevels[Random.Shared.Next(DifficultyLevels.AllLevels.Length)];

        _logger.LogInformation("PLAN: Auto-generating case with difficulty={Difficulty}, timezone={Timezone}, images={GenerateImages}", actualDifficulty, request.Timezone, request.GenerateImages);

        var systemPrompt = $@"
            You are a master architect of investigative cold cases. Produce a FULLY AUTOMATED INITIAL PLAN
            strictly aligned with the DIFFICULTY PROFILE and designed to be expanded without contradictions.

            DIFFICULTY PROFILE: {actualDifficulty}
            Description: {difficultyProfile?.Description}

            COMPLEXITY GUIDELINES (HARD CONSTRAINTS):
            - Suspects: {difficultyProfile?.Suspects.Min}-{difficultyProfile?.Suspects.Max}
            - Documents (later): {difficultyProfile?.Documents.Min}-{difficultyProfile?.Documents.Max}
            - Evidence items (later): {difficultyProfile?.Evidences.Min}-{difficultyProfile?.Evidences.Max}
            - False leads: {difficultyProfile?.RedHerrings}
            - Gated documents (later): {difficultyProfile?.GatedDocuments}
            - Forensics complexity: {difficultyProfile?.ForensicsComplexity}
            - Complexity factors: {string.Join(", ", difficultyProfile?.ComplexityFactors ?? Array.Empty<string>())}
            - Estimated duration: {difficultyProfile?.EstimatedDurationMinutes.Min}-{difficultyProfile?.EstimatedDurationMinutes.Max} minutes

            GEOGRAPHY/NAMING POLICY:
            - No real street names, numbers, coordinates, or real brands/companies.
            - If a real city is needed, limit to City/State only; prefer abstract locations (e.g., ""neighborhood bakery"").
            - All names must be plausible and fictitious.

            TEMPORAL CONSISTENCY (CRITICAL):
            - ALL timestamps MUST use ISO-8601 with timezone offset: {request.Timezone}
            - Establish a clear timeline baseline that will be consistently used across ALL documents and evidence
            - All timeline events must be chronologically ordered and logically spaced
            - NO conflicting or overlapping timestamps - each event must have a unique, consistent time
            - Document creation times, evidence collection times, and incident timestamps must align logically

            EXPANSION READINESS (NON-NEGOTIABLE):
            - mainElements[] must already anticipate the minimum entities that will exist later (e.g., ""witness statement"", ""key control log"", ""CCTV collage"", ""receipt"", etc.).
            - goldenTruth.facts must be verifiable by at least two heterogeneous later items (documents/media), without revealing any culprit.
            - learningObjectives must be concrete, observable, and testable during gameplay.

            OUTPUT: ONLY valid JSON conforming to the Plan schema. No comments or extra text.";


        var userPrompt = $@"
            Generate a FULLY AUTOMATED PLAN for a new investigative case.

            INPUT:
            - Difficulty: {actualDifficulty}
            - Generate images (metadata only): {request.GenerateImages}
            - Timezone for timestamps: {request.Timezone}

            MANDATORY CONTENT (Plan schema only):
            - Unique caseId, strong specific title, plausible abstract location, incidentType coherent with difficulty
            - overview that sets clear investigative scope (no solution)
            - learningObjectives[] directly tied to actions the player will perform
            - mainElements[] that pre-declare the core sources to be expanded (witness statements, logs, receipts, cctv snapshot collage, forensics inspection, etc.)
            - estimatedDuration aligned with difficulty
            - timeline[] canonical events with ISO-8601 + offset (use {request.Timezone}) - MUST be chronologically ordered with NO overlaps or conflicts
            - minDetectiveRank = {requestedDiff}
            - profileApplied with numeric ranges actually used
            - goldenTruth.facts with minSupports ≥ 2 and heterogeneous sources (do not reveal culprit/solution)

            CONSISTENCY RULES:
            - Names/roles introduced here must remain verbatim in later stages (Expand/Design).
            - Do not include any information that would later require real addresses/brands.
            - Avoid over-specifying details that would conflict with the difficulty (e.g., no advanced forensics on Rookie).

            OUTPUT FORMAT:
            - ONLY JSON valid by the **Plan** schema (no comments).";

        var jsonSchema = _schemaProvider.GetSchema("Plan");
        return await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }
}
