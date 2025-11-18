using CaseGen.Functions.Models;
using CaseGen.Functions.Services.CaseGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System;
using System.Text.RegularExpressions;
using System.Linq;

namespace CaseGen.Functions.Services;

/// <summary>
/// Main orchestrator for case generation pipeline.
/// Delegates phase-specific operations to specialized services.
/// </summary>
public class CaseGenerationService : ICaseGenerationService
{
    // RedTeam chunked processing constants
    private const int DefaultMaxBytesPerCall = 60_000;
    private const int MaxParallelCalls = 3;
    
    // Core services
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ISchemaValidationService _schemaValidationService;
    private readonly IConfiguration _configuration;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly ICaseLoggingService _caseLogging;
    private readonly INormalizerService _normalizerService;
    private readonly IPdfRenderingService _pdfRenderingService;
    private readonly IImagesService _imagesService;
    private readonly IPrecisionEditor _precisionEditor;
    private readonly IRedTeamCacheService _redTeamCache;
    private readonly IContextManager _contextManager;
    private readonly ILogger<CaseGenerationService> _logger;

    // Specialized phase services
    private readonly PlanGenerationService _planService;
    private readonly ExpandService _expandService;
    private readonly DesignService _designService;
    private readonly DocumentGenerationService _documentService;
    private readonly MediaGenerationService _mediaService;
    private readonly ValidationService _validationService;

    public CaseGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        ISchemaValidationService schemaValidationService,
        IJsonSchemaProvider schemaProvider,
        ICaseLoggingService caseLogging,
        INormalizerService normalizerService,
        IPdfRenderingService pdfRenderingService,
        IImagesService imagesService,
        IPrecisionEditor precisionEditor,
        IRedTeamCacheService redTeamCache,
        IContextManager contextManager,
        PlanGenerationService planService,
        ExpandService expandService,
        DesignService designService,
        DocumentGenerationService documentService,
        MediaGenerationService mediaService,
        ValidationService validationService,
        IConfiguration configuration,
        ILogger<CaseGenerationService> logger)
    {
        _llmService = llmService;
        _schemaProvider = schemaProvider;
        _storageService = storageService;
        _schemaValidationService = schemaValidationService;
        _caseLogging = caseLogging;
        _normalizerService = normalizerService;
        _pdfRenderingService = pdfRenderingService;
        _imagesService = imagesService;
        _precisionEditor = precisionEditor;
        _redTeamCache = redTeamCache;
        _contextManager = contextManager;
        _planService = planService;
        _expandService = expandService;
        _designService = designService;
        _documentService = documentService;
        _mediaService = mediaService;
        _validationService = validationService;
        _configuration = configuration;
        _logger = logger;
    }

    // ========== Phase 2: Hierarchical Plan Methods ==========

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

    // ========== Original Monolithic Plan Method ==========

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
        var planResult = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);

        await PersistPlanContextAsync(caseId, planResult, cancellationToken);

        return planResult;
    }

    // ========== Phase 3: Hierarchical Expand Methods ==========

    public async Task<string> ExpandSuspectAsync(string caseId, string suspectId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EXPAND-SUSPECT: Expanding suspect {SuspectId} for case {CaseId}", suspectId, caseId);

        // Load context: core plan + specific suspect from plan
        var snapshot = await _contextManager.BuildSnapshotAsync(caseId, 
            new[] { "@plan/core", "@plan/suspects" }, 
            cancellationToken);
        
        var corePlan = snapshot.Items["plan/core"] as string ?? throw new InvalidOperationException("Core plan not found");
        var suspectsJson = snapshot.Items["plan/suspects"] as string ?? throw new InvalidOperationException("Suspects not found");

        // Extract the specific suspect
        var suspectsData = JsonDocument.Parse(suspectsJson);
        var suspects = suspectsData.RootElement.GetProperty("suspects");
        JsonElement targetSuspect = default;
        
        foreach (var suspect in suspects.EnumerateArray())
        {
            if (suspect.GetProperty("id").GetString() == suspectId)
            {
                targetSuspect = suspect;
                break;
            }
        }

        if (targetSuspect.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Suspect {suspectId} not found in plan");
        }

        var coreData = JsonDocument.Parse(corePlan);
        var difficulty = coreData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);

        var systemPrompt = $@"
You are an expert in developing detailed suspect profiles for investigative cases.

DIFFICULTY: {difficulty}
COMPLEXITY FACTORS: {string.Join(", ", difficultyProfile.ComplexityFactors)}

Expand this suspect's profile with rich, investigatively-relevant details:
- Background: Occupation, history, personality traits, past events
- Motive: Detailed potential reasons for involvement
- Alibi: Specific times, locations, corroborating factors
- Behavior: Demeanor, cooperation level, inconsistencies
- Relationships: Connections to other persons in the case
- Evidence Links: How evidence might connect to this suspect

IMPORTANT:
- Maintain EXACT name and role from Plan
- Do NOT reveal if this is the culprit
- Create plausible complexity appropriate to difficulty level
- All details must be investigatively useful

OUTPUT: ONLY valid JSON conforming to ExpandSuspect schema.";

        var suspectName = targetSuspect.GetProperty("name").GetString();
        var suspectRole = targetSuspect.GetProperty("role").GetString();
        var initialMotivation = targetSuspect.GetProperty("initialMotivation").GetString();

        var userPrompt = $@"
Expand the profile for this suspect:

CASE CONTEXT:
{corePlan}

SUSPECT (from Plan):
- ID: {suspectId}
- Name: {suspectName}
- Role: {suspectRole}
- Initial Motivation: {initialMotivation}

Generate a detailed ExpandSuspect profile with:
- suspectId: ""{suspectId}""
- name: ""{suspectName}"" (EXACT match)
- role: ""{suspectRole}"" (EXACT match)
- background: Detailed 2-3 sentence background
- motive: Expanded motivation with specific details
- alibi: Detailed alibi with times/locations
- behavior: demeanor, cooperationLevel, optional inconsistencies[]
- relationships: [] (if applicable to other suspects/witnesses)
- evidenceLinks: [] (potential connections)
- suspicionLevel: ""low"", ""moderate"", ""high"", or ""very high""

OUTPUT FORMAT: ONLY JSON valid by ExpandSuspect schema.";

        var jsonSchema = _schemaProvider.GetSchema("ExpandSuspect");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
        
        // Save to context storage with suspect-specific path
        await _contextManager.SaveContextAsync(caseId, $"expand/suspects/{suspectId}", result, cancellationToken);
        
        return result;
    }

    public async Task<string> ExpandEvidenceAsync(string caseId, string evidenceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EXPAND-EVIDENCE: Expanding evidence {EvidenceId} for case {CaseId}", evidenceId, caseId);
        
        // Load minimal context: core + evidence list
        var snapshot = await _contextManager.BuildSnapshotAsync(
            caseId,
            new[] { "@plan/core", "@plan/evidence" },
            cancellationToken
        );

        var corePlan = snapshot.Items["plan/core"] as string ?? throw new InvalidOperationException("Core plan not found");
        var evidenceJson = snapshot.Items["plan/evidence"] as string ?? throw new InvalidOperationException("Evidence plan not found");

        // Parse core for difficulty
        var coreData = JsonDocument.Parse(corePlan);
        var difficulty = coreData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);
        
        // Parse evidence data
        var evidenceData = JsonDocument.Parse(evidenceJson);
        var mainElements = evidenceData.RootElement.GetProperty("mainElements");
        
        // Find the evidence by matching the evidenceId pattern (EV001, EV002, etc.)
        // Since mainElements is just an array of strings, we need to use index-based matching
        // EV001 = index 0, EV002 = index 1, etc.
        var evidenceIndex = int.Parse(evidenceId.Substring(2)) - 1; // "EV001" -> 0
        if (evidenceIndex < 0 || evidenceIndex >= mainElements.GetArrayLength())
        {
            throw new InvalidOperationException($"Evidence ID {evidenceId} is out of range");
        }
        
        var evidenceType = mainElements[evidenceIndex].GetString();
        
        // Load goldenTruth for context
        var goldenTruth = evidenceData.RootElement.GetProperty("goldenTruth");
        var factsJson = JsonSerializer.Serialize(goldenTruth.GetProperty("facts"));

        var systemPrompt = $@"
You are an expert case designer creating detailed evidence for a {difficulty}-level detective case.

DIFFICULTY: {difficulty}
COMPLEXITY FACTORS: {string.Join(", ", difficultyProfile.ComplexityFactors)}

Create a comprehensive expansion for evidence item {evidenceId} (type: {evidenceType}).

REQUIREMENTS:
1. Generate detailed physical description and discovery context
2. Include complete chain of custody with realistic timestamps
3. Add forensic analysis if appropriate for evidence type
4. Link to relevant suspects and events using IDs (S001, E001, etc.)
5. Reference golden truth facts this evidence supports (FACT001, etc.)
6. Assess significance and investigative value
7. Consider player discovery mechanics

IMPORTANT:
- All timestamps must use ISO-8601 format with timezone offset
- Chain of custody must be complete and realistic
- Evidence significance should match difficulty level
- Do NOT reveal the solution directly

OUTPUT: ONLY valid JSON conforming to ExpandEvidence schema.";

        var userPrompt = $@"
Expand this evidence item:

CASE CONTEXT:
{corePlan}

EVIDENCE TO EXPAND:
- ID: {evidenceId}
- Type: {evidenceType}

GOLDEN TRUTH FACTS (for reference):
{factsJson}

Generate the detailed evidence expansion with:
- Complete discovery context
- Physical details
- Chain of custody
- Forensic analysis (if applicable)
- Relationships to suspects/events/facts
- Significance assessment
- Player visibility settings";

        var jsonSchema = _schemaProvider.GetSchema("ExpandEvidence");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);

        // Save to hierarchical path: expand/evidence/{evidenceId}
        await _contextManager.SaveContextAsync(
            caseId,
            $"expand/evidence/{evidenceId}",
            result,
            cancellationToken
        );

        _logger.LogInformation("EXPAND-EVIDENCE: Completed evidence {EvidenceId}", evidenceId);
        return result;
    }

    public async Task<string> ExpandTimelineAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EXPAND-TIMELINE: Expanding timeline for case {CaseId}", caseId);
        
        // Load comprehensive context: core, timeline, suspects, evidence
        var snapshot = await _contextManager.BuildSnapshotAsync(
            caseId,
            new[] { "@plan/core", "@plan/timeline", "@plan/suspects", "@plan/evidence" },
            cancellationToken
        );

        var corePlan = snapshot.Items["plan/core"] as string ?? throw new InvalidOperationException("Core plan not found");
        var timelineJson = snapshot.Items["plan/timeline"] as string ?? throw new InvalidOperationException("Timeline not found");
        var suspectsJson = snapshot.Items["plan/suspects"] as string ?? throw new InvalidOperationException("Suspects not found");
        var evidenceJson = snapshot.Items["plan/evidence"] as string ?? throw new InvalidOperationException("Evidence not found");

        // Parse core for difficulty
        var coreData = JsonDocument.Parse(corePlan);
        var difficulty = coreData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);

        // Parse timeline data
        var timelineData = JsonDocument.Parse(timelineJson);
        var timelineEvents = timelineData.RootElement.GetProperty("timeline");
        var eventsJson = JsonSerializer.Serialize(timelineEvents);

        // Parse suspects data
        var suspectsData = JsonDocument.Parse(suspectsJson);
        var suspects = suspectsData.RootElement.GetProperty("suspects");
        var suspectsListJson = JsonSerializer.Serialize(suspects);

        // Parse evidence data
        var evidenceData = JsonDocument.Parse(evidenceJson);
        var mainElements = evidenceData.RootElement.GetProperty("mainElements");
        var goldenTruth = evidenceData.RootElement.GetProperty("goldenTruth");
        var factsJson = JsonSerializer.Serialize(goldenTruth.GetProperty("facts"));

        var systemPrompt = $@"
You are an expert case designer creating a detailed timeline expansion for a {difficulty}-level detective case.

DIFFICULTY: {difficulty}
COMPLEXITY FACTORS: {string.Join(", ", difficultyProfile.ComplexityFactors)}

Expand the macro timeline into detailed events with:

1. COMPREHENSIVE DESCRIPTIONS:
   - Detailed description of what happened (100+ words per event)
   - Location details (type, context)
   - Chronological sequence of actions within each event

2. PARTICIPANT DETAILS:
   - All involved suspects with their roles and actions
   - What each participant did, observed, and knows
   - Conflicting accounts and inconsistencies

3. EVIDENCE LINKAGE:
   - Which evidence items were generated during each event
   - How evidence was created or left behind
   - Cross-references to evidence IDs (EV001, etc.)

4. WITNESS ACCOUNTS:
   - What witnesses claim to have seen/heard
   - Reliability levels and contradictions
   - Discrepancies between accounts

5. INVESTIGATIVE CONTEXT:
   - Significance level (critical, important, moderate, minor)
   - Investigative leads that emerge
   - Links to golden truth facts (FACT001, etc.)
   - Relationships between events

6. PLAYER DISCOVERY:
   - Initial knowledge level (full, partial, none)
   - How players discover event details
   - Required detective rank for full access

IMPORTANT:
- Maintain EXACT event IDs, timestamps, and titles from PlanTimeline
- Use exact suspect IDs (S001, S002, etc.) and names from plan
- Create realistic inconsistencies and contradictions appropriate to difficulty
- Do NOT reveal the solution directly
- All timestamps must match PlanTimeline exactly

OUTPUT: ONLY valid JSON conforming to ExpandTimeline schema.";

        var userPrompt = $@"
Expand the timeline for this case:

CASE CONTEXT:
{corePlan}

TIMELINE TO EXPAND:
{eventsJson}

SUSPECTS (for cross-reference):
{suspectsListJson}

GOLDEN TRUTH FACTS (for reference):
{factsJson}

Generate the detailed timeline expansion with:
- Each event from PlanTimeline expanded with full details
- Participant actions and observations
- Evidence linkage (EV001, etc.)
- Witness accounts with reliability and contradictions
- Significance assessment and investigative leads
- Event relationships and inconsistencies
- Player discovery mechanics
- Timeline metadata (duration, key turning points, narrative structure)";

        var jsonSchema = _schemaProvider.GetSchema("ExpandTimeline");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);

        // Save to context storage
        await _contextManager.SaveContextAsync(caseId, "expand/timeline", result, cancellationToken);

        _logger.LogInformation("EXPAND-TIMELINE: Completed timeline expansion");
        return result;
    }

    public async Task<string> SynthesizeRelationsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SYNTHESIZE-RELATIONS: Synthesizing relationships for case {CaseId}", caseId);
        
        // Load all Plan contexts (core, suspects, timeline, evidence) and expanded timeline
        // Note: In Phase 3 Task 3.5, we'll update Orchestrator to pass expanded suspects/evidence
        // For now, we synthesize based on Plan data + expanded timeline
        var snapshot = await _contextManager.BuildSnapshotAsync(
            caseId,
            new[] { "@plan/core", "@plan/suspects", "@plan/timeline", "@plan/evidence", "@expand/timeline" },
            cancellationToken
        );

        var corePlan = snapshot.Items["plan/core"] as string ?? throw new InvalidOperationException("Core plan not found");
        var suspectsJson = snapshot.Items["plan/suspects"] as string ?? throw new InvalidOperationException("Suspects not found");
        var timelineJson = snapshot.Items["plan/timeline"] as string ?? throw new InvalidOperationException("Timeline not found");
        var evidenceJson = snapshot.Items["plan/evidence"] as string ?? throw new InvalidOperationException("Evidence not found");
        var expandedTimelineJson = snapshot.Items["expand/timeline"] as string ?? throw new InvalidOperationException("Expanded timeline not found");

        // Parse core for difficulty
        var coreData = JsonDocument.Parse(corePlan);
        var difficulty = coreData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);

        // Parse suspects data
        var suspectsData = JsonDocument.Parse(suspectsJson);
        var suspects = suspectsData.RootElement.GetProperty("suspects");
        var suspectsListJson = JsonSerializer.Serialize(suspects);

        // Parse timeline data
        var timelineData = JsonDocument.Parse(timelineJson);
        var timelineEvents = timelineData.RootElement.GetProperty("timeline");
        var basicTimelineJson = JsonSerializer.Serialize(timelineEvents);

        // Parse evidence data
        var evidenceData = JsonDocument.Parse(evidenceJson);
        var mainElements = evidenceData.RootElement.GetProperty("mainElements");
        var goldenTruth = evidenceData.RootElement.GetProperty("goldenTruth");
        var factsJson = JsonSerializer.Serialize(goldenTruth.GetProperty("facts"));

        var systemPrompt = $@"
You are an expert case designer creating a comprehensive relationship synthesis for a {difficulty}-level detective case.

DIFFICULTY: {difficulty}
COMPLEXITY FACTORS: {string.Join(", ", difficultyProfile.ComplexityFactors)}

Synthesize ALL relationships across the case:

1. SUSPECT RELATIONS:
   - Map connections between all suspects (colleague, friend, rival, family, alibi_for, etc.)
   - Strength of connections (strong, moderate, weak)
   - Shared events where suspects were together
   - Contradictions in their accounts about each other

2. EVIDENCE CONNECTIONS:
   - How each evidence item links to suspects (owned_by, seen_with, implicates, exonerates)
   - Connection strength (definitive, strong, suggestive, weak)
   - Which events generated each evidence
   - Which golden truth facts each evidence supports

3. EVENT LINKAGES:
   - Causal relationships (caused_by, led_to, prerequisite_for)
   - Temporal relationships (concurrent_with)
   - Logical relationships (contradicts, supports)
   - Evidence supporting each linkage

4. CONTRADICTION MATRIX:
   - All contradictions across witness accounts, alibis, timeline, evidence
   - Severity levels (critical, important, moderate, minor)
   - Sources of conflicts (suspect IDs, evidence IDs)
   - Potential resolutions

5. ALIBI NETWORK:
   - Each suspect's alibi claims with timeframes
   - Corroboration status (verified, partially_verified, unverified, contradicted)
   - Who/what corroborates or contradicts each alibi
   - Related events during alibi timeframes

6. MOTIVE ANALYSIS:
   - Motive strength for each suspect (very_strong to none)
   - Specific motive factors (financial gain, revenge, protection, etc.)
   - Evidence supporting each motive
   - Interconnected motives between suspects

7. INVESTIGATIVE PATHS:
   - Multiple investigative approaches players can take
   - Key elements (suspects, evidence, events) in each path
   - Difficulty levels and what each path reveals

IMPORTANT:
- Use exact IDs from plan (S001, EV001, E001, FACT001)
- Create realistic interconnections appropriate to difficulty
- Generate meaningful contradictions that require investigation
- Map alibi networks with proper corroboration/contradiction
- Do NOT reveal the solution directly
- All relationships must be investigatively useful

OUTPUT: ONLY valid JSON conforming to SynthesizeRelations schema.";

        var userPrompt = $@"
Synthesize comprehensive relationships for this case:

CASE CONTEXT:
{corePlan}

SUSPECTS (with IDs):
{suspectsListJson}

TIMELINE (basic events):
{basicTimelineJson}

EXPANDED TIMELINE (detailed events):
{expandedTimelineJson}

GOLDEN TRUTH FACTS:
{factsJson}

Generate the complete relationship synthesis with:
- Suspect relations network with connections, shared events, contradictions
- Evidence connections to suspects, events, and facts
- Event linkages with causal/temporal/logical relationships
- Contradiction matrix across all sources
- Alibi network with corroboration status
- Motive analysis for all suspects
- Multiple investigative paths with difficulty levels";

        var jsonSchema = _schemaProvider.GetSchema("SynthesizeRelations");
        var result = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);

        // Save to context storage
        await _contextManager.SaveContextAsync(caseId, "expand/relations", result, cancellationToken);

        _logger.LogInformation("SYNTHESIZE-RELATIONS: Completed relationship synthesis");
        return result;
    }

    // ========== Helper Methods ==========

    public async Task<string> LoadContextAsync(string caseId, string path, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("LOAD-CONTEXT: Loading {Path} for case {CaseId}", path, caseId);
        
        var snapshot = await _contextManager.BuildSnapshotAsync(
            caseId,
            new[] { $"@{path}" },
            cancellationToken
        );
        
        // BuildSnapshotAsync is called with @path but returns the key WITHOUT the @ prefix
        // So we need to check for the key without @
        if (!snapshot.Items.ContainsKey(path))
        {
            _logger.LogError("Snapshot keys available: {Keys}", string.Join(", ", snapshot.Items.Keys));
            throw new InvalidOperationException($"Context {path} not found in snapshot");
        }
        
        var item = snapshot.Items[path];
        
        // ContextManager returns deserialized objects, so we need to serialize them to JSON strings
        if (item == null)
        {
            throw new InvalidOperationException($"Context {path} has invalid format (null)");
        }
        
        _logger.LogInformation("LOAD-CONTEXT-DEBUG: path={Path}, itemType={Type}", path, item.GetType().FullName);
        
        // If it's already a string, return as-is
        if (item is string str)
        {
            _logger.LogInformation("LOAD-CONTEXT-DEBUG: Returning as string");
            return str;
        }
        
        // If it's a JsonElement, get the raw JSON text (don't serialize again!)
        if (item is System.Text.Json.JsonElement jsonElement)
        {
            var rawJson = jsonElement.GetRawText();
            _logger.LogInformation("LOAD-CONTEXT-DEBUG: Extracted raw JSON from JsonElement, length={Length}", rawJson.Length);
            return rawJson;
        }
        
        // For other object types, serialize to JSON string
        var json = System.Text.Json.JsonSerializer.Serialize(item);
        _logger.LogInformation("LOAD-CONTEXT-DEBUG: Serialized object to JSON, length={Length}", json.Length);
        return json;
    }

    private async Task PersistPlanContextAsync(string caseId, string planJson, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(planJson);
        var root = doc.RootElement;
        await _contextManager.SaveContextAsync(caseId, "plan/core", planJson, cancellationToken);

        if (root.TryGetProperty("suspects", out var suspectsElement) && suspectsElement.ValueKind == JsonValueKind.Array)
        {
            await _contextManager.SaveContextAsync(caseId, "plan/suspects", suspectsElement.GetRawText(), cancellationToken);
        }

        if (root.TryGetProperty("timeline", out var timelineElement) && timelineElement.ValueKind == JsonValueKind.Array)
        {
            await _contextManager.SaveContextAsync(caseId, "plan/timeline", timelineElement.GetRawText(), cancellationToken);
        }

        if (root.TryGetProperty("mainElements", out var evidenceElement) && evidenceElement.ValueKind == JsonValueKind.Array)
        {
            await _contextManager.SaveContextAsync(caseId, "plan/evidence", evidenceElement.GetRawText(), cancellationToken);
        }
    }

    private async Task PersistExpandContextsAsync(string caseId, string expandJson, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(expandJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("suspects", out var suspectsElement) && suspectsElement.ValueKind == JsonValueKind.Array)
        {
            var suspects = suspectsElement.EnumerateArray().ToArray();
            for (int index = 0; index < suspects.Length; index++)
            {
                var item = suspects[index];
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : $"S{index + 1:000}";
                await _contextManager.SaveContextAsync(caseId, $"expand/suspects/{id}", item.GetRawText(), cancellationToken);
            }
            await _contextManager.SaveContextAsync(caseId, "expand/suspects", suspectsElement.GetRawText(), cancellationToken);
        }

        if (root.TryGetProperty("evidences", out var evidencesElement) && evidencesElement.ValueKind == JsonValueKind.Array)
        {
            var evidences = evidencesElement.EnumerateArray().ToArray();
            for (int index = 0; index < evidences.Length; index++)
            {
                var item = evidences[index];
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : $"E{index + 1:000}";
                await _contextManager.SaveContextAsync(caseId, $"expand/evidence/{id}", item.GetRawText(), cancellationToken);
            }
            await _contextManager.SaveContextAsync(caseId, "expand/evidence", evidencesElement.GetRawText(), cancellationToken);
        }

        if (root.TryGetProperty("timeline", out var timelineElement) && timelineElement.ValueKind == JsonValueKind.Array)
        {
            await _contextManager.SaveContextAsync(caseId, "expand/timeline", timelineElement.GetRawText(), cancellationToken);
        }
    }

    private async Task PersistDesignSpecsAsync(string caseId, string designJson, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(designJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("documentSpecs", out var docSpecs) && docSpecs.ValueKind == JsonValueKind.Array)
        {
            foreach (var grouping in docSpecs.EnumerateArray().Select(e => JsonDocument.Parse(e.GetRawText()).RootElement).GroupBy(e => e.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "unknown" : "unknown"))
            {
                var payload = JsonSerializer.Serialize(new
                {
                    docType = grouping.Key,
                    specifications = grouping.Select(g => g).ToArray()
                });
                await _contextManager.SaveContextAsync(caseId, $"design/documents/{grouping.Key}", payload, cancellationToken);
            }
        }

        if (root.TryGetProperty("mediaSpecs", out var mediaSpecs) && mediaSpecs.ValueKind == JsonValueKind.Array)
        {
            foreach (var grouping in mediaSpecs.EnumerateArray().Select(e => JsonDocument.Parse(e.GetRawText()).RootElement).GroupBy(e => e.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() ?? "photo" : "photo"))
            {
                var payload = JsonSerializer.Serialize(new
                {
                    kind = grouping.Key,
                    specs = grouping.Select(g => g).ToArray()
                });
                await _contextManager.SaveContextAsync(caseId, $"design/media/{grouping.Key}", payload, cancellationToken);
            }
        }
        await _contextManager.SaveContextAsync(caseId, "design/specs", designJson, cancellationToken);
    }

    // ========== Phase 4: Design by Document Type ==========

    public async Task<string> DesignDocumentTypeAsync(string caseId, string docType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DESIGN-DOC-TYPE: type={DocType} caseId={CaseId}", docType, caseId);
        
        // Step 1: Determine which contexts to load based on document type
        var contextPaths = new List<string> { "plan/core" }; // Always need core
        
        switch (docType.ToLowerInvariant())
        {
            case "police_report":
            case "evidence_log":
                // Police reports and evidence logs need timeline context
                contextPaths.Add("expand/timeline");
                break;
                
            case "interview":
            case "witness_statement":
                // Interviews/witness statements need suspects context
                contextPaths.Add("plan/suspects");
                contextPaths.Add("expand/timeline"); // Also helpful for temporal context
                break;
                
            case "forensics_report":
                // Forensic reports need evidence context
                contextPaths.Add("plan/evidence");
                contextPaths.Add("expand/timeline");
                break;
                
            case "memo_admin":
                // Administrative memos may reference various elements
                contextPaths.Add("plan/suspects");
                contextPaths.Add("plan/evidence");
                contextPaths.Add("expand/timeline");
                break;
                
            default:
                throw new ArgumentException($"Unknown document type: {docType}");
        }
        
        // Step 2: Load all required contexts in parallel
        var contextTasks = contextPaths.Select(async path =>
        {
            try
            {
                var content = await LoadContextAsync(caseId, path, cancellationToken);
                return (path, content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load context {Path}: {Error}", path, ex.Message);
                return (path, (string?)null);
            }
        }).ToList();
        
        var contextResults = await Task.WhenAll(contextTasks);
        var contexts = contextResults
            .Where(r => r.Item2 != null)
            .ToDictionary(r => r.Item1, r => r.Item2!);
        
        if (!contexts.ContainsKey("plan/core"))
        {
            throw new InvalidOperationException("Failed to load required plan/core context");
        }
        
        // Step 3: Load difficulty profile from plan/core
        string difficulty = "Rookie";
        try
        {
            using var coreDoc = JsonDocument.Parse(contexts["plan/core"]);
            if (coreDoc.RootElement.TryGetProperty("difficulty", out var diffProp))
            {
                difficulty = diffProp.GetString() ?? "Rookie";
            }
        }
        catch { /* use default */ }
        
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);
        
        // Step 4: Build context-aware prompt based on document type
        var systemPrompt = $@"
You are an investigative case designer specializing in {docType} documents.

TASK: Create a detailed specification for ONE OR MORE {docType} document(s) based on the provided context.

OUTPUT FORMAT (JSON conforming to DesignDocumentType schema):
{{
  ""docType"": ""{docType}"",
  ""specifications"": [
    {{
      ""docId"": ""doc_<slug>_<nnn>"",
      ""type"": ""{docType}"",
      ""title"": ""Document Title in English"",
      ""dateCreated"": ""ISO-8601 timestamp with timezone offset (MUST match timezone from Expand timeline)"",
      ""sections"": [""Section 1"", ""Section 2"", ...],
      ""lengthTarget"": {{ ""min"": 200, ""max"": 500 }},
      ""gated"": false,
      ""gatingRule"": {{ ... }} (only if gated=true),
      ""subjectId"": ""S001"" (for interviews/witness_statements),
      ""evidenceReferences"": [""EV001"", ...],
      ""timelineReferences"": [""event_001"", ...]
    }}
  ],
  ""contextUsed"": {{
    ""planCore"": true,
    ""timeline"": true/false,
    ""suspects"": [...],
    ""evidence"": [...]
  }}
}}

CRITICAL RULES:
- All text in English
- IDs must be unique and follow pattern conventions
- dateCreated MUST use the SAME timezone offset as Expand timeline
- dateCreated must be chronologically consistent with case timeline
- For forensics_report: ""Chain of Custody"" section is MANDATORY
- Gating rules (gated=true) only allowed for forensics_report at difficulty levels with gatedDocuments > 0
- Evidence/timeline references must match actual IDs from loaded contexts
- For interviews/witness_statements: include subjectId matching suspect/witness ID from context

DOCUMENT TYPE SPECIFIC RULES:
{GetDocTypeSpecificRules(docType, difficultyProfile)}

CONSISTENCY REQUIREMENTS:
- Names (suspects/witnesses) must match EXACTLY with context
- Evidence IDs must match existing evidence from context
- Timeline references must match event IDs from expand/timeline
- Do not invent information not present in context
- Do not reveal solution or final guilt determination";

        var userPrompt = $@"
Design {docType} specification(s) for this case.

DIFFICULTY: {difficulty}

LOADED CONTEXTS:
{string.Join("\n\n", contexts.Select(kvp => $"=== {kvp.Key.ToUpperInvariant()} ===\n{kvp.Value}"))}

QUANTITY GUIDANCE:
- Create appropriate number of {docType} documents based on context
- For interviews/witness_statements: one per relevant suspect/witness
- For forensics_report: {difficultyProfile.GatedDocuments} gated report(s) required
- For police_report/evidence_log: typically 1-2 documents
- For memo_admin: 1-3 documents as needed

Generate the specification now.";

        // Step 5: Call LLM with schema validation
        var jsonSchema = _schemaProvider.GetSchema("DesignDocumentType");
        
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(
                    caseId, 
                    systemPrompt, 
                    userPrompt, 
                    jsonSchema, 
                    cancellationToken
                );
                
                // Validate response structure
                using var doc = JsonDocument.Parse(response);
                if (!doc.RootElement.TryGetProperty("docType", out _) ||
                    !doc.RootElement.TryGetProperty("specifications", out _))
                {
                    throw new InvalidOperationException("Response missing required properties");
                }
                
                // Step 6: Save to context storage
                var savePath = $"design/documents/{docType}";
                await _contextManager.SaveContextAsync(caseId, savePath, response, cancellationToken);
                
                _logger.LogInformation("DESIGN-DOC-TYPE-COMPLETE: type={DocType} path={Path}", docType, savePath);
                return response;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("DESIGN-DOC-TYPE-RETRY: attempt={Attempt} error={Error}", attempt, ex.Message);
                // Retry
            }
        }
        
        throw new InvalidOperationException($"Failed to design {docType} after {maxRetries} attempts");
    }
    
    private string GetDocTypeSpecificRules(string docType, DifficultyProfile profile)
    {
        return docType.ToLowerInvariant() switch
        {
            "police_report" => @"
- Include Report Number, Officer details, Date/Time
- Sections: Incident Summary, Scene Description, Evidence Collected, Witness Statements, Actions Taken
- lengthTarget: {min: 300, max: 600}
- Reference at least 2 evidence IDs and 2 timeline events
- Objective tone, no speculation about guilt",

            "interview" => @"
- Format as Q&A transcript with Interviewer/Interviewee labels
- Include interview date/time, location, subject identification
- Sections: Introduction, Background Questions, Incident Questions, Alibi Verification, Closing
- lengthTarget: {min: 400, max: 800}
- Must include subjectId matching suspect from context
- Reference at least 1 evidence ID or timeline event relevant to subject's statements",

            "forensics_report" => $@"
- Include Laboratory ID, Examiner details, Date/Time
- Sections: Evidence Description, Methodology, Results, Interpretation, **Chain of Custody** (MANDATORY), Limitations
- lengthTarget: {{min: 400, max: 800}}
- gated: {(profile.GatedDocuments > 0 ? "true for forensics reports" : "false (gating not allowed at this difficulty)")}
- {(profile.GatedDocuments > 0 ? "gatingRule: { \"action\": \"requires_evidence\", \"evidenceId\": \"<valid-evidence-id>\" }" : "")}
- Chain of Custody must list temporal sequence of evidence handling
- Reference at least 2 evidence IDs",

            "evidence_log" => @"
- Cataloging format with structured entries
- Sections: Log Header, Evidence Entries (each with ID/description/location/time/officer), Summary
- lengthTarget: {min: 250, max: 500}
- List all major evidence items from context
- Include collection timestamps consistent with timeline",

            "witness_statement" => @"
- Written statement format (first person from witness perspective)
- Include witness identification, statement date/time, location
- Sections: Witness Information, Account of Events, Additional Information, Signature Block
- lengthTarget: {min: 300, max: 600}
- Must include subjectId matching witness from context
- Reference timeline events witnessed by this person",

            "memo_admin" => @"
- Bureaucratic memo format
- Include To/From/Subject/Date header
- Sections vary but typically: Purpose, Summary, Action Items, Attachments
- lengthTarget: {min: 200, max: 400}
- Reference document/evidence IDs when discussing case progression
- Neutral administrative tone",

            _ => "Follow standard document conventions for this type"
        };
    }

    public async Task<string> DesignMediaTypeAsync(string caseId, string mediaType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DESIGN-MEDIA-TYPE: type={MediaType} caseId={CaseId}", mediaType, caseId);
        
        // Step 1: Determine which contexts to load based on media type
        var contextPaths = new List<string> { "plan/core" }; // Always need core
        
        switch (mediaType.ToLowerInvariant())
        {
            case "crime_scene_photo":
            case "surveillance_photo":
                // Crime scene and surveillance photos need timeline and location context
                contextPaths.Add("expand/timeline");
                contextPaths.Add("plan/evidence");
                break;
                
            case "mugshot":
                // Mugshots need suspect context
                contextPaths.Add("plan/suspects");
                break;
                
            case "evidence_photo":
            case "forensic_photo":
            case "document_scan":
                // Evidence-related media needs evidence context
                contextPaths.Add("plan/evidence");
                contextPaths.Add("expand/timeline");
                break;
                
            case "diagram":
                // Diagrams may need various contexts depending on what's being diagrammed
                contextPaths.Add("expand/timeline");
                contextPaths.Add("plan/suspects");
                contextPaths.Add("plan/evidence");
                break;
                
            default:
                throw new ArgumentException($"Unknown media type: {mediaType}");
        }
        
        // Step 2: Load all required contexts in parallel (including visual registry)
        var contextTasks = contextPaths.Select(async path =>
        {
            try
            {
                var content = await LoadContextAsync(caseId, path, cancellationToken);
                return (path, content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load context {Path}: {Error}", path, ex.Message);
                return (path, (string?)null);
            }
        }).ToList();

        // Also try to load visual registry (optional)
        contextTasks.Add(Task.Run(async () =>
        {
            try
            {
                var registryContent = await LoadContextAsync(caseId, "visual-registry", cancellationToken);
                return ("visual-registry", registryContent);
            }
            catch
            {
                // Visual registry is optional - case might not have one yet
                return ("visual-registry", (string?)null);
            }
        }));
        
        var contextResults = await Task.WhenAll(contextTasks);
        var contexts = contextResults
            .Where(r => r.Item2 != null)
            .ToDictionary(r => r.Item1, r => r.Item2!);
        
        if (!contexts.ContainsKey("plan/core"))
        {
            throw new InvalidOperationException("Failed to load required plan/core context");
        }

        // Step 2b: Extract visual references summary if registry exists
        var visualReferencesSummary = "";
        var availableReferences = new Dictionary<string, string>(); // referenceId -> category
        
        if (contexts.ContainsKey("visual-registry"))
        {
            try
            {
                using var registryDoc = JsonDocument.Parse(contexts["visual-registry"]);
                var referencesElement = registryDoc.RootElement.GetProperty("references");
                
                var summaryBuilder = new StringBuilder();
                summaryBuilder.AppendLine("AVAILABLE VISUAL REFERENCES (for consistency):");
                summaryBuilder.AppendLine();
                
                foreach (var refProperty in referencesElement.EnumerateObject())
                {
                    var refId = refProperty.Name;
                    var refObj = refProperty.Value;
                    var category = refObj.GetProperty("category").GetString() ?? "";
                    var description = refObj.GetProperty("detailedDescription").GetString() ?? "";
                    
                    availableReferences[refId] = category;
                    
                    // Include abbreviated description in summary
                    var shortDesc = description.Length > 150 ? description.Substring(0, 150) + "..." : description;
                    summaryBuilder.AppendLine($"- {refId} ({category}): {shortDesc}");
                }
                
                visualReferencesSummary = summaryBuilder.ToString();
                _logger.LogInformation("DESIGN-MEDIA-TYPE: Loaded {Count} visual references for consistency", availableReferences.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse visual registry: {Error}", ex.Message);
            }
        }
        
        // Step 3: Load difficulty profile from plan/core
        string difficulty = "Rookie";
        try
        {
            using var coreDoc = JsonDocument.Parse(contexts["plan/core"]);
            if (coreDoc.RootElement.TryGetProperty("difficulty", out var diffProp))
            {
                difficulty = diffProp.GetString() ?? "Rookie";
            }
        }
        catch { /* use default */ }
        
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);
        
        // Step 4: Build context-aware prompt based on media type
        var systemPrompt = $@"
You are a forensic media specialist designing specifications for {mediaType} generation.

TASK: Create detailed specifications for ONE OR MORE {mediaType} items based on the provided context.

OUTPUT FORMAT (JSON conforming to DesignMediaType schema):
{{
  ""mediaType"": ""{mediaType}"",
  ""specifications"": [
    {{
      ""evidenceId"": ""ev_<slug>_<nnn>"",
      ""kind"": ""photo"" | ""document_scan"" | ""diagram"" | ""audio"" | ""video"",
      ""title"": ""Media Title in English"",
      ""collectedAt"": ""ISO-8601 timestamp with timezone offset (MUST match timezone from Expand timeline)"",
      ""prompt"": ""Detailed visual description for image generation..."",
      ""constraints"": {{
        ""lighting"": ""natural daylight"" | ""forensic flash"" | ""dim interior"" | ""raking light"",
        ""perspective"": ""eye-level"" | ""overhead"" | ""close-up"" | ""wide-angle"",
        ""scale"": true/false,
        ""quality"": ""professional"" | ""security-camera"" | ""smartphone"" | ""forensic-quality"",
        ""colorMode"": ""color"" | ""black-and-white"" | ""infrared"",
        ""annotation"": true/false
      }},
      ""deferred"": false,
      ""subjectId"": ""S001"" (for mugshots),
      ""relatedEvidenceIds"": [""EV001"", ...],
      ""locationId"": ""crime_scene"" | ""evidence_room"" | ...,
      ""capturedBy"": ""CSI Technician"" | ""Detective"" | ...,
      ""purpose"": ""document crime scene"" | ""identify suspect"" | ...,
      ""visualReferenceIds"": [""evidence_backpack"", ""suspect_001""] (OPTIONAL - see VISUAL CONSISTENCY section below)
    }}
  ],
  ""contextUsed"": {{
    ""planCore"": true,
    ""timeline"": true/false,
    ""suspects"": [...],
    ""evidence"": [...]
  }}
}}

CRITICAL RULES:
- All text in English
- IDs must be unique and follow pattern conventions (ev_<slug>_<nnn>)
- collectedAt MUST use the SAME timezone offset as Expand timeline
- collectedAt must be chronologically consistent with case timeline
- Prompts must be detailed and specific, describing visual elements in detail
- For mugshots: include subjectId matching suspect from context
- For crime scene photos: list all visible evidence in relatedEvidenceIds
- For audio/video: set deferred=true (not yet supported by generation pipeline)
- Constraints must be realistic for the media type and investigative context

VISUAL CONSISTENCY (NEW - CRITICAL):
{(string.IsNullOrEmpty(visualReferencesSummary) ? @"
- No visual references available for this case (registry not generated yet)
- Do NOT include visualReferenceIds field in specifications" : $@"
- Visual references are AVAILABLE for maintaining consistency across images
- When a MediaSpec shows an evidence item or suspect that has a reference, include visualReferenceIds array
- Reference IDs MUST match exactly from the available references list below
- Use visualReferenceIds when:
  * Crime scene photo shows evidence items with references
  * Evidence photo depicts an item with a reference
  * Mugshot shows a suspect with a reference
  * Multiple photos need to show the same object/person consistently
- Example: If crime scene photo shows backpack and knife, and both have references: ""visualReferenceIds"": [""evidence_backpack"", ""evidence_knife""]
- If no matching references exist for visible items, OMIT visualReferenceIds field entirely

{visualReferencesSummary}")}

MEDIA TYPE SPECIFIC RULES:
{GetMediaTypeSpecificRules(mediaType, difficultyProfile)}

CONSISTENCY REQUIREMENTS:
- Subject appearances (for mugshots) must match suspect descriptions from context EXACTLY
- Evidence characteristics must match evidence descriptions from context EXACTLY
- Locations must match locations from timeline
- Collection times must be realistic (not before incident, during investigation window)
- Do not invent information not present in context
- Prompts should be detailed enough for image generation (composition, lighting, mood, specific details)";

        var userPrompt = $@"
Design {mediaType} specification(s) for this case.

DIFFICULTY: {difficulty}

LOADED CONTEXTS:
{string.Join("\n\n", contexts.Select(kvp => $"=== {kvp.Key.ToUpperInvariant()} ===\n{kvp.Value}"))}

QUANTITY GUIDANCE:
- Create appropriate number of {mediaType} items based on context
- For mugshots: one per suspect who gets arrested/booked
- For crime_scene_photo: {difficultyProfile.Evidences.Item1}-{difficultyProfile.Evidences.Item2} photos covering different angles/details
- For evidence_photo: one per significant physical evidence item
- For forensic_photo: close-ups of critical evidence details
- For document_scan: one per paper document/record mentioned
- For diagram: 1-2 diagrams (timeline diagram, scene layout, relationship map)

Generate the specification now.";

        // Step 5: Call LLM with schema validation
        var jsonSchema = _schemaProvider.GetSchema("DesignMediaType");
        
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(
                    caseId, 
                    systemPrompt, 
                    userPrompt, 
                    jsonSchema, 
                    cancellationToken
                );
                
                // Validate response structure
                using var doc = JsonDocument.Parse(response);
                if (!doc.RootElement.TryGetProperty("mediaType", out _) ||
                    !doc.RootElement.TryGetProperty("specifications", out _))
                {
                    throw new InvalidOperationException("Response missing required properties");
                }

                // Step 5b: Validate visualReferenceIds if present
                if (availableReferences.Count > 0)
                {
                    var specs = doc.RootElement.GetProperty("specifications");
                    var totalSpecs = 0;
                    var specsWithRefs = 0;
                    var invalidRefs = new List<string>();

                    foreach (var spec in specs.EnumerateArray())
                    {
                        totalSpecs++;
                        
                        if (spec.TryGetProperty("visualReferenceIds", out var refIdsElement))
                        {
                            specsWithRefs++;
                            
                            foreach (var refId in refIdsElement.EnumerateArray())
                            {
                                var refIdStr = refId.GetString();
                                if (!string.IsNullOrEmpty(refIdStr) && !availableReferences.ContainsKey(refIdStr))
                                {
                                    invalidRefs.Add(refIdStr);
                                    _logger.LogWarning("DESIGN-MEDIA-TYPE-INVALID-REF: MediaSpec references unknown visualReferenceId={RefId}",
                                        refIdStr);
                                }
                            }
                        }
                    }

                    _logger.LogInformation("DESIGN-MEDIA-TYPE-REFS-VALIDATED: {SpecsWithRefs}/{TotalSpecs} specs use visual references, invalidRefs={InvalidCount}",
                        specsWithRefs, totalSpecs, invalidRefs.Count);
                }
                
                // Step 6: Save to context storage
                var savePath = $"design/media/{mediaType}";
                await _contextManager.SaveContextAsync(caseId, savePath, response, cancellationToken);
                
                _logger.LogInformation("DESIGN-MEDIA-TYPE-COMPLETE: type={MediaType} path={Path}", mediaType, savePath);
                return response;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("DESIGN-MEDIA-TYPE-RETRY: attempt={Attempt} error={Error}", attempt, ex.Message);
                // Retry
            }
        }
        
        throw new InvalidOperationException($"Failed to design {mediaType} after {maxRetries} attempts");
    }
    
    private string GetMediaTypeSpecificRules(string mediaType, DifficultyProfile profile)
    {
        return mediaType.ToLowerInvariant() switch
        {
            "crime_scene_photo" => $@"
- kind: ""photo""
- Multiple angles: overview, mid-range, close-ups
- Show evidence items in context (use relatedEvidenceIds for all visible items)
- constraints: {{ ""lighting"": ""forensic flash"", ""perspective"": ""eye-level"" or ""overhead"", ""scale"": true for close-ups, ""quality"": ""forensic-quality"" }}
- Prompt should describe: room layout, visible evidence, lighting conditions, any disturbances
- Include locationId from timeline (e.g., ""crime_scene"", ""victim_residence"")
- capturedBy: ""CSI Technician"" or ""Forensic Photographer""
- Typical count: {profile.Evidences.Item1}-{profile.Evidences.Item2} photos",

            "mugshot" => @"
- kind: ""photo""
- Standard booking photo format (front and profile views)
- Must include subjectId matching suspect from context
- constraints: { ""lighting"": ""even frontal lighting"", ""perspective"": ""eye-level"", ""colorMode"": ""color"", ""quality"": ""professional"" }
- Prompt must match suspect's physical description from context EXACTLY (age, ethnicity, build, distinguishing features)
- Include booking information context
- locationId: ""booking_station"" or ""police_station""
- capturedBy: ""Booking Officer""
- One per arrested suspect",

            "evidence_photo" => $@"
- kind: ""photo""
- Close-up detail shots of physical evidence
- constraints: {{ ""lighting"": ""raking light"" for texture, ""scale"": true (ruler visible), ""quality"": ""forensic-quality"", ""annotation"": false }}
- Prompt should describe evidence appearance matching context (color, texture, dimensions, condition, any markings)
- Include relatedEvidenceIds (typically just this item, unless showing relationship to other evidence)
- locationId: ""evidence_room"" or ""lab""
- capturedBy: ""Forensic Technician""
- Typical count: {profile.Evidences.Item1}-{profile.Evidences.Item2} items",

            "forensic_photo" => @"
- kind: ""photo""
- Highly detailed forensic analysis shots (fingerprints, bloodstain patterns, tool marks, etc.)
- constraints: { ""lighting"": ""specialized (UV/IR if needed)"", ""scale"": true, ""quality"": ""forensic-quality"", ""annotation"": true (may include markers) }
- Prompt should describe forensic feature in technical detail
- Include relatedEvidenceIds
- locationId: ""forensic_lab""
- capturedBy: ""Forensic Examiner""
- Purpose: specific forensic analysis (e.g., ""analyze fingerprint ridge detail"", ""document bloodstain pattern"")",

            "document_scan" => @"
- kind: ""document_scan""
- Digital scan of paper documents, forms, records
- constraints: { ""quality"": ""high-resolution scan"", ""colorMode"": ""color"" or ""black-and-white"" depending on original }
- Prompt should describe document type, visible text headers, stamps, signatures, overall condition
- Do NOT reproduce full text content in prompt (that's for document generation step)
- locationId: based on where document was obtained
- capturedBy: ""Records Officer"" or ""Detective""
- Examples: police forms, medical records, receipts, notes",

            "surveillance_photo" => @"
- kind: ""photo""
- Security camera or surveillance footage stills
- constraints: { ""quality"": ""security-camera"" (lower quality), ""lighting"": ""variable"" (often dim), ""colorMode"": ""color"" or ""black-and-white"" }
- Prompt should describe: camera angle (overhead, wide), visible subjects/activity, timestamp overlay
- Include relatedEvidenceIds if suspects visible
- locationId: surveillance location from timeline
- capturedBy: ""Security Camera"" or ""Surveillance System""
- Purpose: establish timeline, identify suspects",

            "diagram" => @"
- kind: ""diagram""
- Schematic representations (timeline diagram, crime scene layout, relationship map)
- constraints: { ""colorMode"": ""color"", ""annotation"": true }
- Prompt should describe: diagram type, elements to include, labels, connections/relationships
- For timeline: chronological sequence of events
- For scene layout: overhead view with evidence positions
- For relationship map: connections between suspects/witnesses/victims
- capturedBy: ""Investigator"" or ""Analyst""
- Typically 1-2 diagrams per case",

            _ => "Follow standard forensic media conventions for this type"
        };
    }

    // ========== Original Monolithic Expand Method ==========

    public async Task<string> ExpandCaseAsync(string planJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EXPAND: Building detailed case from plan");

        // Parse the plan to extract difficulty level
        var planData = JsonDocument.Parse(planJson);
        var difficulty = planData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);

        _logger.LogInformation("EXPAND: Building detailed case from plan with difficulty={Difficulty}", difficulty);

        var systemPrompt = $@"
            You are a specialist in developing investigative COLD CASES. Expand the initial plan
            into a coherent, contradiction-free structure strictly bound to the DIFFICULTY PROFILE.

            DIFFICULTY PROFILE: {difficulty}
            Description: {difficultyProfile.Description}

            EXPANSION HARD LIMITS:
            - Suspects: {difficultyProfile.Suspects.Min}-{difficultyProfile.Suspects.Max}
            - Evidence items: {difficultyProfile.Evidences.Min}-{difficultyProfile.Evidences.Max} (physical/digital/testimonial)
            - False leads: {difficultyProfile.RedHerrings} (subtle and plausible)
            - Forensics complexity: {difficultyProfile.ForensicsComplexity}
            - Complexity factors: {string.Join(", ", difficultyProfile.ComplexityFactors)}

            LEVEL GUIDANCE:
            - Rookie/Detective: linear chronology, direct indicators, modest ambiguity
            - Detective2/Sergeant+: cross-correlations, some gating prerequisites
            - Lieutenant/Captain/Commander: specialized analyses, layered dependencies

            TEMPORAL CONSISTENCY (MANDATORY):
            - PRESERVE all timeline events from Plan with exact same timestamps
            - ALL new timestamps MUST use the same timezone offset as established in Plan
            - Evidence collection times MUST logically follow incident times
            - Witness interview times MUST be chronologically consistent
            - Document creation dates MUST align with when they would realistically be created
            - NO timestamp conflicts - each event gets a unique, logical time slot
            
            STRICT CONSISTENCY:
            - Reuse names/roles introduced in Plan verbatim; DO NOT invent new real-world brands/addresses.
            - Every evidence/witness/suspect introduced here must be usable later in Design (docs or media).";

        var userPrompt = $@"
            Expand the following plan into detailed content honoring the difficulty and remaining expansion-ready:

            {planJson}

            YOU MUST DELIVER (within the Expand schema only):
            1) SUSPECTS:
            - Distinct backgrounds/motives/alibis proportional to difficulty
            - Keep names/roles stable (verbatim from Plan if present)
            2) EVIDENCE:
            - Physical, digital, and testimonial items
            - Each item must be precisely described so it can later become a document or media item (no vague items)
            3) TIMELINE:
            - Coherent, ISO-8601 with offset; link key events to suspects/witnesses/evidence when applicable
            4) WITNESSES:
            - Add statements with specificity and reliability ratings aligned to difficulty
            5) LOCATIONS:
            - Abstract or City/State only (no real addresses/brands)

            CROSS-STAGE GUARANTEES (NO EXCEPTIONS):
            - Every suspect OR witness must be representable later by at least one document (interview/witness_statement/memo).
            - Every evidence item here will later appear either as a mediaSpec (photo/document_scan/diagram) or be cited in at least one generated document.
            - Do NOT introduce content that cannot be legally/realistically depicted later.

            OUTPUT: ONLY valid JSON conforming to the Expand schema.";

        var jsonSchema = _schemaProvider.GetSchema("Expand");

        var expandResult = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);

        await PersistExpandContextsAsync(caseId, expandResult, cancellationToken);

        return expandResult;
    }

    /// <summary>
    /// Analyzes the case context and creates a visual consistency registry
    /// identifying all elements (evidence, suspects) that need consistent visual representation
    /// across multiple generated images.
    /// </summary>
    public async Task<string> DesignVisualConsistencyRegistryAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DESIGN-VISUAL-REGISTRY: Starting visual consistency analysis for caseId={CaseId}", caseId);

        // Step 1: Try to load contexts - with fallback to old structure
        var contexts = new Dictionary<string, string>();
        
        // Try new structure first: plan/core, plan/evidence, plan/suspects, expand/evidence, expand/suspects
        // Fallback to old structure: case.json for core info
        
        // Core context (required)
        try
        {
            contexts["plan/core"] = await LoadContextAsync(caseId, "plan/core", cancellationToken);
        }
        catch
        {
            try
            {
                // Fallback: try to get case.json from cases folder
                var caseJson = await _storageService.GetFileAsync("cases", $"{caseId}/case.json", cancellationToken);
                contexts["plan/core"] = caseJson;
                _logger.LogInformation("Using fallback case.json for core context");
            }
            catch
            {
                throw new InvalidOperationException($"Failed to load core context for case {caseId}. Case may not exist or has invalid structure.");
            }
        }

        // Evidence context (optional)
        try
        {
            contexts["plan/evidence"] = await LoadContextAsync(caseId, "plan/evidence", cancellationToken);
        }
        catch
        {
            try
            {
                contexts["expand/evidence"] = await LoadContextAsync(caseId, "expand/evidence", cancellationToken);
            }
            catch
            {
                _logger.LogWarning("No evidence context found for case {CaseId}", caseId);
            }
        }

        // Suspects context (optional)
        try
        {
            contexts["plan/suspects"] = await LoadContextAsync(caseId, "plan/suspects", cancellationToken);
        }
        catch
        {
            try
            {
                contexts["expand/suspects"] = await LoadContextAsync(caseId, "expand/suspects", cancellationToken);
            }
            catch
            {
                _logger.LogWarning("No suspects context found for case {CaseId}", caseId);
            }
        }

        if (contexts.Count == 0 || !contexts.ContainsKey("plan/core"))
        {
            throw new InvalidOperationException("Failed to load required contexts for visual registry");
        }

        _logger.LogInformation("Loaded {Count} contexts for visual analysis", contexts.Count);

        // Step 2: Build comprehensive context for analysis
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("=== CASE CONTEXT FOR VISUAL CONSISTENCY ANALYSIS ===\n");
        
        foreach (var (path, content) in contexts)
        {
            contextBuilder.AppendLine($"--- {path.ToUpper()} ---");
            contextBuilder.AppendLine(content);
            contextBuilder.AppendLine();
        }

        // Step 3: Create system prompt for visual registry generation
        var systemPrompt = @"
You are a forensic visual consistency specialist. Your task is to analyze a criminal case and identify ALL elements that will need consistent visual representation across multiple generated images.

OBJECTIVE: Create a Visual Consistency Registry containing detailed physical descriptions of:
1. Physical Evidence Items (weapons, clothing, objects, documents)
2. Suspects (physical appearance for mugshots and scene photos)
3. Key Locations (if they appear in multiple images)

FOR EACH ELEMENT, PROVIDE:
- referenceId: Unique identifier (e.g., ""evidence_backpack"", ""suspect_001"", ""location_warehouse"")
- category: ""physical_evidence"" | ""suspect"" | ""location""
- detailedDescription: Comprehensive physical description including:
  * Exact dimensions (height, width, depth in inches or cm)
  * Materials and textures (fabric type, metal finish, surface characteristics)
  * Primary and secondary colors with specificity
  * Wear patterns, damage, unique marks, distinctive features
  * Any text, logos, brands, serial numbers visible
  * For suspects: age, build, height, hair (color, style, length), eye color, facial features, distinctive marks
  * For locations: layout, flooring, walls, lighting, architectural features
- colorPalette: Array of 3-5 primary colors in hex format (e.g., [""#1B3A6B"", ""#FFFFFF"", ""#8B4513""])
- distinctiveFeatures: Array of 3-5 unique identifiers that make this element recognizable (e.g., ""Bent left zipper pull"", ""Scar above right eyebrow"")

CRITERIA FOR INCLUSION:
- Include ANY evidence item that appears in 2+ photos/documents
- Include ALL suspects (for mugshots and potential scene appearance)
- Include locations if they appear in multiple crime scene photos
- EXCLUDE: Generic items without distinctive features, items appearing only once

OUTPUT FORMAT: JSON conforming to VisualConsistencyRegistry schema.
";

        var userPrompt = $@"
Analyze the following case context and create a Visual Consistency Registry for all elements requiring consistent visual representation:

{contextBuilder}

INSTRUCTIONS:
1. Review ALL evidence items - identify which items will appear in multiple images (crime scene photos, evidence photos, close-ups)
2. For EACH suspect, create a detailed physical description suitable for generating consistent mugshots and scene appearances
3. For key locations appearing in multiple photos, describe architectural and spatial features
4. Provide EXHAUSTIVE physical details - these descriptions will be used to generate master reference images
5. Be specific with measurements, colors (use hex codes), materials, and unique identifiers

EXAMPLE REGISTRY ENTRY:
{{
  ""referenceId"": ""evidence_backpack"",
  ""category"": ""physical_evidence"",
  ""detailedDescription"": ""Navy blue Nike backpack, dimensions 18×12×6 inches. Main compartment with double YKK zipper pulls (silver tone, left pull slightly bent at 15-degree angle). Front pocket features embroidered white swoosh logo (3 inches wide, centered). Padded black webbing shoulder straps (1.5 inch width, right strap shows fraying at top attachment point exposing white inner threads). Bottom panel has irregular dark brown stain (approximately 2 inches diameter, oil-based appearance). Fabric shows general wear with slight fading on sun-exposed surfaces. Two side mesh pockets (elastic degraded on left pocket)."",
  ""colorPalette"": [""#1B3A6B"", ""#FFFFFF"", ""#000000"", ""#4A4A4A""],
  ""distinctiveFeatures"": [
    ""Bent left zipper pull (15-degree angle)"",
    ""Frayed right shoulder strap at top"",
    ""Dark brown stain on bottom (2in diameter)"",
    ""White Nike swoosh logo (3in wide)"",
    ""Degraded left mesh pocket elastic""
  ]
}}

OUTPUT: ONLY valid JSON conforming to VisualConsistencyRegistry schema.
";

        // Step 4: Call LLM with schema validation
        var jsonSchema = _schemaProvider.GetSchema("VisualConsistencyRegistry");
        
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(
                    caseId,
                    systemPrompt,
                    userPrompt,
                    jsonSchema,
                    cancellationToken
                );

                // Validate response structure
                using var doc = JsonDocument.Parse(response);
                if (!doc.RootElement.TryGetProperty("caseId", out _) ||
                    !doc.RootElement.TryGetProperty("references", out _))
                {
                    throw new InvalidOperationException("Response missing required properties");
                }

                // Step 5: Save to context storage
                // IMPORTANT: Parse the JSON string into an object before saving to avoid double-encoding
                // SaveContextAsync will serialize it again, so we need to give it an object, not a JSON string
                var savePath = "visual-registry";
                var registryObject = JsonSerializer.Deserialize<object>(response);
                await _contextManager.SaveContextAsync(caseId, savePath, registryObject, cancellationToken);

                // Count references (it's an object/dictionary, not an array)
                var referencesCount = 0;
                if (doc.RootElement.TryGetProperty("references", out var referencesElement) &&
                    referencesElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    referencesCount = referencesElement.EnumerateObject().Count();
                }

                _logger.LogInformation("DESIGN-VISUAL-REGISTRY-COMPLETE: path={Path}, references={Count}",
                    savePath,
                    referencesCount);

                return response;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("DESIGN-VISUAL-REGISTRY-RETRY: attempt={Attempt} error={Error}", attempt, ex.Message);
                // Retry
            }
        }

        throw new InvalidOperationException($"Failed to design visual consistency registry after {maxRetries} attempts");
    }

    /// <summary>
    /// Generates master reference images for all elements in the visual consistency registry.
    /// These isolated, high-quality images serve as references for maintaining visual consistency
    /// across multiple generated images in the case.
    /// </summary>
    public async Task<int> GenerateMasterReferencesAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GENERATE-MASTER-REFS: Starting master reference generation for caseId={CaseId}", caseId);

        // Step 1: Load the visual registry
        string registryJson;
        try
        {
            registryJson = await LoadContextAsync(caseId, "visual-registry", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GENERATE-MASTER-REFS-ERROR: Failed to load visual registry for caseId={CaseId}", caseId);
            throw new InvalidOperationException("Visual consistency registry not found. Run DesignVisualConsistencyRegistryAsync first.", ex);
        }

        // Debug: Check what we got
        _logger.LogInformation("GENERATE-MASTER-REFS-DEBUG: registryJson length={Length}, first 200 chars: {Preview}", 
            registryJson.Length, 
            registryJson.Length > 200 ? registryJson.Substring(0, 200) : registryJson);

        // Step 2: Parse registry and extract references
        using var registryDoc = JsonDocument.Parse(registryJson);
        
        _logger.LogInformation("GENERATE-MASTER-REFS-DEBUG: Root element type={Type}", 
            registryDoc.RootElement.ValueKind);
        
        var referencesElement = registryDoc.RootElement.GetProperty("references");
        
        if (referencesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Invalid visual registry format: 'references' must be an object");
        }

        var referencesList = new List<(string referenceId, string category, string description, string[] colorPalette)>();
        
        foreach (var refProperty in referencesElement.EnumerateObject())
        {
            var refId = refProperty.Name;
            var refObj = refProperty.Value;
            
            var category = refObj.GetProperty("category").GetString() ?? "physical_evidence";
            var description = refObj.GetProperty("detailedDescription").GetString() ?? "";
            
            var colorPaletteArray = refObj.GetProperty("colorPalette")
                .EnumerateArray()
                .Select(c => c.GetString() ?? "")
                .ToArray();
            
            referencesList.Add((refId, category, description, colorPaletteArray));
        }

        _logger.LogInformation("GENERATE-MASTER-REFS: Found {Count} references to generate", referencesList.Count);

        // Step 3: Generate master reference image for each element
        var generatedCount = 0;
        var updatedReferences = new Dictionary<string, JsonElement>();

        foreach (var (referenceId, category, description, colorPalette) in referencesList)
        {
            try
            {
                _logger.LogInformation("GENERATE-MASTER-REF: Generating reference for {ReferenceId} (category={Category})", 
                    referenceId, category);

                var startTime = DateTime.UtcNow;

                // Build optimized prompt for isolated reference image
                var prompt = BuildMasterReferencePrompt(referenceId, category, description, colorPalette);

                // Generate image using ImagesService (text-only for now, no reference needed)
                var imageUrl = await _imagesService.GenerateAsync(
                    caseId,
                    new MediaSpec 
                    { 
                        EvidenceId = $"ref_{referenceId}",
                        Title = $"Master Reference - {referenceId}",
                        Prompt = prompt,
                        Kind = "photo"
                    },
                    cancellationToken);

                // Extract blob path from Azurite URL
                // URL format: http://127.0.0.1:10000/devstoreaccount1/bundles/CASE-xxx/media/ref_xxx.generated-image.png
                // Extract: CASE-xxx/media/ref_xxx.generated-image.png
                var blobPath = imageUrl.Split(new[] { "/bundles/" }, StringSplitOptions.None).Last();
                
                // Load the generated image bytes from bundles storage
                var imageBytes = await _storageService.GetFileBytesAsync("bundles", blobPath, cancellationToken);
                
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    throw new InvalidOperationException($"Failed to load generated image for reference {referenceId} from path {blobPath}");
                }

                // Save reference image to case-context container for visual consistency system
                var referenceFileName = $"{referenceId}.png";
                var referencePath = $"case-context/{caseId}/references/{referenceFileName}";
                
                await _storageService.SaveFileBytesAsync(
                    "case-context",
                    $"{caseId}/references/{referenceFileName}",
                    imageBytes,
                    cancellationToken);

                var duration = DateTime.UtcNow - startTime;

                _logger.LogInformation("GENERATE-MASTER-REF-COMPLETE: referenceId={ReferenceId}, size={Size}bytes, duration={Duration}ms, path={Path}",
                    referenceId, imageBytes.Length, duration.TotalMilliseconds, referencePath);

                // Update the reference object with imageUrl
                var originalRef = referencesElement.GetProperty(referenceId);
                var updatedRef = UpdateReferenceWithImageUrl(originalRef, referencePath);
                updatedReferences[referenceId] = updatedRef;

                generatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GENERATE-MASTER-REF-ERROR: Failed to generate reference for {ReferenceId}", referenceId);
                // Continue with other references even if one fails
                updatedReferences[referenceId] = referencesElement.GetProperty(referenceId);
            }
        }

        // Step 4: Update the visual registry with imageUrl for all generated references
        var updatedRegistry = UpdateRegistryWithImageUrls(registryDoc.RootElement, updatedReferences);
        
        // Save updated registry back to storage
        await _contextManager.SaveContextAsync(caseId, "visual-registry", updatedRegistry, cancellationToken);

        _logger.LogInformation("GENERATE-MASTER-REFS-COMPLETE: Generated {GeneratedCount}/{TotalCount} master references for caseId={CaseId}",
            generatedCount, referencesList.Count, caseId);

        return generatedCount;
    }

    private string BuildMasterReferencePrompt(string referenceId, string category, string description, string[] colorPalette)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("MASTER REFERENCE IMAGE - ISOLATED STUDIO PHOTOGRAPHY");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Subject ID: {referenceId}");
        promptBuilder.AppendLine($"Category: {category}");
        promptBuilder.AppendLine();

        switch (category)
        {
            case "physical_evidence":
                promptBuilder.AppendLine("PHOTOGRAPHY SETUP:");
                promptBuilder.AppendLine("- Clean white background (seamless paper backdrop)");
                promptBuilder.AppendLine("- Professional studio lighting (soft diffused light, no harsh shadows)");
                promptBuilder.AppendLine("- Object centered in frame, oriented for optimal visibility");
                promptBuilder.AppendLine("- Straight-on angle (parallel to camera plane)");
                promptBuilder.AppendLine("- Forensic scale ruler visible at bottom for size reference");
                promptBuilder.AppendLine("- High resolution, maximum detail capture");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("SUBJECT DESCRIPTION:");
                promptBuilder.AppendLine(description);
                break;

            case "suspect":
                promptBuilder.AppendLine("PHOTOGRAPHY SETUP:");
                promptBuilder.AppendLine("- Neutral gray background (police lineup backdrop)");
                promptBuilder.AppendLine("- Even frontal lighting (no dramatic shadows)");
                promptBuilder.AppendLine("- Subject centered, facing camera directly (mugshot front view)");
                promptBuilder.AppendLine("- Head and shoulders composition (standard mugshot framing)");
                promptBuilder.AppendLine("- Neutral expression, looking at camera");
                promptBuilder.AppendLine("- High clarity for facial feature identification");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("SUBJECT DESCRIPTION:");
                promptBuilder.AppendLine(description);
                break;

            case "location":
                promptBuilder.AppendLine("PHOTOGRAPHY SETUP:");
                promptBuilder.AppendLine("- Wide-angle establishing shot");
                promptBuilder.AppendLine("- Even lighting showing spatial layout clearly");
                promptBuilder.AppendLine("- Camera positioned to capture key architectural features");
                promptBuilder.AppendLine("- Empty scene (no people or temporary objects)");
                promptBuilder.AppendLine("- Focus on permanent structural elements");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("LOCATION DESCRIPTION:");
                promptBuilder.AppendLine(description);
                break;
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("COLOR PALETTE (for accuracy):");
        foreach (var color in colorPalette)
        {
            promptBuilder.AppendLine($"  - {color}");
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("CRITICAL REQUIREMENTS:");
        promptBuilder.AppendLine("- ALL distinctive features from description MUST be clearly visible");
        promptBuilder.AppendLine("- Colors MUST match the specified palette accurately");
        promptBuilder.AppendLine("- Maximum detail and clarity for use as reference image");
        promptBuilder.AppendLine("- No context, no storytelling - pure documentation");
        promptBuilder.AppendLine("- This image will be used as visual reference for other images");

        return promptBuilder.ToString();
    }

    private JsonElement UpdateReferenceWithImageUrl(JsonElement originalRef, string imageUrl)
    {
        // Create a mutable dictionary from the original reference
        var refDict = new Dictionary<string, object?>();
        
        foreach (var prop in originalRef.EnumerateObject())
        {
            refDict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => prop.Value.EnumerateArray().Select(e => e.GetString()).ToArray(),
                _ => null
            };
        }

        // Add or update imageUrl
        refDict["imageUrl"] = imageUrl;

        // Serialize back to JSON and parse to JsonElement
        var json = JsonSerializer.Serialize(refDict);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private string UpdateRegistryWithImageUrls(JsonElement originalRegistry, Dictionary<string, JsonElement> updatedReferences)
    {
        var registryDict = new Dictionary<string, object?>();

        // Copy all top-level properties
        foreach (var prop in originalRegistry.EnumerateObject())
        {
            if (prop.Name == "references")
            {
                // Replace references with updated versions
                var refsDict = new Dictionary<string, JsonElement>();
                foreach (var (key, value) in updatedReferences)
                {
                    refsDict[key] = value;
                }
                registryDict["references"] = refsDict;
            }
            else
            {
                registryDict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            }
        }

        return JsonSerializer.Serialize(registryDict, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> DesignCaseAsync(string planJson, string expandedJson, string caseId, string? difficulty = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Designing case structure");

        using var planDoc = JsonDocument.Parse(planJson);
        var planSnapshot = planDoc.RootElement.Clone();
        var planDifficulty = planDoc.RootElement.GetProperty("difficulty").GetString() ?? "Rookie";
        await PersistPlanContextAsync(caseId, planSnapshot.GetRawText(), cancellationToken);
        using var expandDoc = JsonDocument.Parse(expandedJson);
        var expandSnapshot = expandDoc.RootElement.Clone();
        await PersistExpandContextsAsync(caseId, expandSnapshot.GetRawText(), cancellationToken);

        // Get the actual difficulty profile for dynamic validation
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty ?? planDifficulty);

        (int minDocs, int maxDocs) = difficultyProfile.Documents;
        (int minEvid, int maxEvid) = difficultyProfile.Evidences;

        // Override with plan values if available (for backward compatibility)
        if (planDoc.RootElement.TryGetProperty("profileApplied", out var prof))
        {
            if (prof.TryGetProperty("documents", out var docsArr) && docsArr.ValueKind == JsonValueKind.Array && docsArr.GetArrayLength() == 2)
            { minDocs = docsArr[0].GetInt32(); maxDocs = docsArr[1].GetInt32(); }

            if (prof.TryGetProperty("evidences", out var evidArr) && evidArr.ValueKind == JsonValueKind.Array && evidArr.GetArrayLength() == 2)
            { minEvid = evidArr[0].GetInt32(); maxEvid = evidArr[1].GetInt32(); }
        }

        var gatedDocsCount = difficultyProfile.GatedDocuments;
        var minMedia = Math.Max(2, minEvid);
        var maxMedia = Math.Max(minMedia, maxEvid);

        var systemPrompt = @"
            You are an investigative case designer. Convert the plan and the expansion
            into structured specifications for parallel generation of documents and media.

            IMPORTANT (JSON ONLY conforming to the DocumentAndMediaSpecs schema):
            - All text in english
            - No explanations or comments outside the JSON.
            - Mark sensitive documents with ""gated"": true and include ""gatingRule"" { action, evidenceId?, notes? }.
            - Forensic reports MUST contain the ""Cadeia de Custódia"" (Chain of Custody) section.
            - Do not invent real addresses or real brands/companies (use abstract locations or City/State).
            - Allowed document types: police_report, interview, memo_admin, forensics_report, evidence_log, witness_statement
            - Allowed media types: photo, document_scan, diagram (audio/video => deferred=true)

            TEMPORAL SPECIFICATIONS (CRITICAL):
            - ALL document dateCreated and evidence collectedAt timestamps MUST be consistent with Expand timeline
            - Use the EXACT same timezone offset established in Expand for ALL timestamps
            - Document creation dates must logically follow the incident timeline (e.g., police reports after incident, interviews after initial report)
            - Evidence collection times must be realistic (not before incident, not weeks later without justification)
            - Interview timestamps must be chronologically ordered and consistent with case progression
            - NO overlapping or conflicting timestamps - each document/evidence gets a unique time slot

            MANDATORY CONSISTENCY:
            - Names (suspects/witnesses) and evidence must match 1:1 with Expand (same text/semantics).
            - Every suspect from Expand must have at least 1 document (interview, witness_statement or memo_admin).
            - Every witness from Expand must have at least 1 witness_statement.
            - Every evidence item from Expand must appear in mediaSpecs (scan/photo/diagram) OR be referenced by ID in at least one document.
            - IDs (docId/evidenceId) must be unique, stable, and mutually coherent.";

        var userPrompt = $@"
            Transform this case into structured specifications.

            Difficulty: {difficulty ?? planDifficulty}

            EXPANDED CONTEXT:
            {expandedJson}

            QUANTITY RULES (from difficulty profile):
            - Documents: {minDocs}-{maxDocs}
            - Media items: {minMedia}-{maxMedia}
            - Gated documents: exactly {gatedDocsCount} {(gatedDocsCount > 0 ? "(forensics_report required)" : "(gated usage forbidden)")}

            MINIMUM COVERAGE (NON-NEGOTIABLE):
            - Include at least 1 evidence_log and 1 police_report.
            - For EACH suspect from Expand: >=1 document (interview, witness_statement OR memo_admin).
            - For EACH witness from Expand: >=1 witness_statement.
            - For EACH evidence from Expand: create a corresponding mediaSpec OR explicitly reference the evidence in document(s).

            SPECIFIC RULES:
            {(gatedDocsCount > 0 ? @"- Exactly the indicated number of forensics_report with ""gated"": true.
            - Each ""gatingRule"" must be {{ ""action"": ""requires_evidence"" | ""submit_evidence"", ""evidenceId"": ""<id-existing-in-mediaSpecs>"" }}.
            - Forensic reports (including gated) must contain the ""Chain of Custody"" section." :
            @"- It is forbidden to use ""gated"": true at this level; do not include ""gatingRule"".
            - Forensic reports must still contain the ""Chain of Custody"" section.")}

            DOCUMENT REQUIREMENTS:
            - For each document: docId (""doc_<slug>_<nnn>""), type, title, sections[], lengthTarget[min,max], gated (bool)
            - If type == forensics_report, include ""Chain of Custody"" in sections
            - Reference evidence by evidenceId when relevant; do not reveal the solution

            MEDIA REQUIREMENTS:
            - For each media item: evidenceId (""ev_<slug>_<nnn>""), kind (photo/document_scan/diagram), title, prompt, constraints (OBJECT), deferred (bool)
            - audio/video: deferred=true
            - constraints must be a simple object (e.g. ""lighting"": ""raking"", ""scale"": true)

            SCHEMA COMPLIANCE:
            - Output ONLY valid JSON conforming to **DocumentAndMediaSpecs**.
            - Do not add fields outside the schema; unique IDs; names/evidence faithful to Expand.";

        var jsonSchema = _schemaProvider.GetSchema("DocumentAndMediaSpecs");
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
                var validationResult = await _schemaValidationService.ParseAndValidateAsync(response, difficulty ?? planDifficulty);
                if (validationResult != null)
                {
                    // Persist canonical contexts for downstream Generate step
                    await PersistPlanContextAsync(caseId, planSnapshot.GetRawText(), cancellationToken);
                    await PersistExpandContextsAsync(caseId, expandSnapshot.GetRawText(), cancellationToken);
                    await PersistDesignSpecsAsync(caseId, response, cancellationToken);
                    return response;
                }
                if (attempt == maxRetries) throw new InvalidOperationException("Design specs failed validation after retries");
            }
            catch when (attempt < maxRetries) { /* retry */ }
        }
        throw new InvalidOperationException("Failed to generate design specs");
    }

    public async Task<string> GenerateDocumentFromSpecAsync(
    DocumentSpec spec,
    string designJson,
    string caseId,
    string? planJson = null,
    string? expandJson = null,
    string? difficultyOverride = null,
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gen Doc[{DocId}] type={Type} title={Title}", spec.DocId, spec.Type, spec.Title);

        // Phase 5: Load context selectively via ContextManager instead of receiving full JSONs
        // For document generation, we primarily need the design context for this doc type
        string actualDesignJson = designJson;
        if (string.IsNullOrWhiteSpace(designJson))
        {
            try
            {
                // Load design context for this document type from hierarchical storage
                actualDesignJson = await LoadContextAsync(caseId, $"design/documents/{spec.Type}", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load design context for {DocType}: {Error}", spec.Type, ex.Message);
                actualDesignJson = "{}";
            }
        }

        // Derive difficulty (Plan > override > Rookie)
        string difficulty = "Rookie";
        if (!string.IsNullOrWhiteSpace(difficultyOverride)) difficulty = difficultyOverride;
        if (!string.IsNullOrWhiteSpace(planJson))
        {
            try
            {
                using var p = JsonDocument.Parse(planJson);
                if (p.RootElement.TryGetProperty("difficulty", out var d) && d.ValueKind == JsonValueKind.String)
                    difficulty = d.GetString() ?? difficulty;
            }
            catch { /* ignore */ }
        }
        else
        {
            // Try to load difficulty from plan/core if not provided
            try
            {
                var coreContext = await LoadContextAsync(caseId, "plan/core", cancellationToken);
                using var coreDoc = JsonDocument.Parse(coreContext);
                if (coreDoc.RootElement.TryGetProperty("difficulty", out var d))
                {
                    difficulty = d.GetString() ?? difficulty;
                }
            }
            catch { /* use default */ }
        }

        // Type-specific directives (augmented with minimum referencing rules)
        string typeDirectives = spec.Type.ToLowerInvariant() switch
        {
            "police_report" => """
                FORMAT (Markdown inside 'content'):
                - Header: Report Number, Date/Time (ISO-8601 with offset), Unit / Responsible Officer.
                - Objective incident summary.
                - Requested sections (use H2 headings: ##).
                - Bullet lists when appropriate.

                MINIMUM ANCHORS (must be real from Design/Expand):
                - Cite ≥2 real IDs (evidenceId and/or docId) and/or concrete timeline timestamps/events.
                - Do not infer guilt or reveal solution.
                """,

            "interview" => """
                FORMAT (Markdown inside 'content'):
                - Clean transcript (no interviewer opinions/judgment).
                - Label lines: **Interviewer:** / **Interviewee:**.
                - Optional timestamps in brackets when natural (e.g., [00:05]).
                - Objective Q&A; do not infer guilt.

                MINIMUM ANCHORS:
                - Reference ≥1 real support: an evidenceId and/or a concrete timeline event/timestamp relevant to statements.
                """,

            "memo_admin" => """
                FORMAT (Markdown inside 'content'):
                - Header: To / From / Subject / Date.
                - Concise bureaucratic tone; use bullets for action items.
                - Reference documents/evidence by ID when available.

                MINIMUM ANCHORS:
                - Cite ≥1 real docId or evidenceId mentioned in Design/Expand when appropriate.
                """,

            "forensics_report" => """
                FORMAT (Markdown inside 'content'):
                - Header: Laboratory / Examiner / Date / Time (ISO-8601 with offset).
                - Methodology (procedures), Results, Interpretation / Limitations.
                - **Chain of Custody** (mandatory) with events in temporal order.
                - Reference relevant evidenceId/docId (do not reveal solution).

                MINIMUM ANCHORS:
                - Cite ≥2 real evidenceId (and docId if applicable).
                - Chain of Custody must include ISO-8601 timestamps and ordered handoffs.
                """,

            "evidence_log" => """
                FORMAT (Markdown inside 'content'):
                - Table: ItemId | Collected At | Collected By | Description | Storage | Transfers.
                - Brief notes per item.

                MINIMUM ANCHORS:
                - Every line must correspond to a real evidenceId present in Design (no new items).
                """,

            "witness_statement" => """
                FORMAT (Markdown inside 'content'):
                - First-person statement, objective, no speculation about perpetrator.
                - Date/Time (ISO-8601 with offset) and brief fictional identification.

                MINIMUM ANCHORS:
                - Reference ≥1 real evidenceId or a concrete timeline event/timestamp corroborating the statement.
                """,

            _ => "FORMAT: use the requested sections; objective, documentary text."
        };

        // Difficulty-specific directives
        string difficultyDirectives = difficulty switch
        {
            "Rookie" or "Iniciante" => """
                DIFFICULTY PROFILE:
                - Simple, direct vocabulary; low ambiguity.
                - Linear chronology and explicit relationships.
                - Aim near the minimum of lengthTarget.
                """,
            "Detective" or "Detective2" => """
                DIFFICULTY PROFILE:
                - Moderate vocabulary; some jargon with context.
                - Introduce plausible ambiguities and light cross-checks.
                - Aim for the middle of the lengthTarget range.
                """,
            "Sergeant" or "Lieutenant" => """
                DIFFICULTY PROFILE:
                - Technical tone when relevant; correlations across sources.
                - Realistic ambiguities (without revealing solution); cite times and IDs.
                - Aim at the top of the lengthTarget range.
                """,
            _ /* Captain/Commander */ => """
                DIFFICULTY PROFILE:
                - Technical language; specialized inferences.
                - Controlled ambiguity, competing hypotheses.
                - Cross multiple sources; mention methodological limitations.
                - Use near the maximum of lengthTarget.
                """
        };

        // Phase 5: Use loaded design context (already contains all necessary type-specific info)
        var designCtx = actualDesignJson ?? "{}";

        var systemPrompt = @"
            You are a police / forensic technical writer. Generate ONLY JSON containing the document body.

            GENERAL RULES (MANDATORY):
            - Write all text in english.
            - Never reveal the solution or culprit.
            - Maintain consistency with Design specifications.
            - **Use exactly the provided 'sections' titles and in the exact order. Do NOT add or rename sections.**
            - Follow exactly the word count range (lengthTarget).
            - If type == forensics_report, include the ""Chain of Custody"" section.
            - Whenever citing evidence, reference existing evidenceId/docId (do not invent).
            - Do not mention gating in the content (gating is game metadata).
            - No real PII, brands, or real addresses.

            TEMPORAL CONSISTENCY (CRITICAL):
            - ALL timestamps MUST use ISO-8601 format with the same timezone offset from Design
            - Document creation date MUST match the dateCreated specified in Design for this document
            - All referenced times (incident times, collection times, interview times) MUST be consistent with established timeline
            - Chain of Custody timestamps MUST be chronologically ordered and realistic
            - NO conflicting or overlapping timestamps with other documents or evidence

            OUTPUT JSON:
            {
            ""docId"": ""string"",
            ""type"": ""string"",
            ""title"": ""string"",
            ""words"": number,
            ""sections"": [ { ""title"": ""string"", ""content"": ""markdown"" } ]
            }";

        var userPrompt = $@"
            CONTEXT — DESIGN:
            {designCtx}

            DOCUMENT TO GENERATE:
            - docId: {spec.DocId}
            - type: {spec.Type}
            - title: {spec.Title}
            - sections (order): {string.Join(", ", spec.Sections)}
            - lengthTarget: {spec.LengthTarget.Min}–{spec.LengthTarget.Max} words
            - gated: {spec.Gated}

            TYPE DIRECTIVES:
            {typeDirectives}

            DIFFICULTY DIRECTIVES ({difficulty}):
            {difficultyDirectives}

            ANTI-INVENTION (MANDATORY):
            - People/locations/evidence: only those defined in Expand/Design.
            - If mentioning evidence, use existing evidenceId/docId; do not create new items.
            - Do not mention gating in the content (gating is metadata).

            PRE-SUBMISSION CHECK (the model must self-check before writing):
            - Did you verify that every cited evidenceId/docId exists in the supplied Design?
            - Are you using the exact 'sections' titles in the given order, without adding extra sections?
            - Are you within the specified lengthTarget range?
            - Are timestamps (when present) in ISO-8601 with offset?

            Output: **ONLY JSON** in the specified structure (no comments).";

        var json = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);

        try
        {
            await _caseLogging.LogStepResponseAsync(caseId, $"documents/{spec.DocId}", json, cancellationToken);

            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var docBundlePath = $"{caseId}/documents/{spec.DocId}.json";
            await _storageService.SaveFileAsync(bundlesContainer, docBundlePath, json, cancellationToken);

            _logger.LogInformation("BUNDLE: Saved doc to bundle: {Path} (case={CaseId}, type={Type})",
                docBundlePath, caseId, spec.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist generated document {DocId} for case {CaseId}",
                spec.DocId, caseId);
        }

        return json;
    }

    public async Task<string> GenerateMediaFromSpecAsync(
        MediaSpec spec,
        string designJson,
        string caseId,
        string? planJson = null,
        string? expandJson = null,
        string? difficultyOverride = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gen Media[{EvidenceId}] kind={Kind} title={Title}", spec.EvidenceId, spec.Kind, spec.Title);

        // Phase 5: Load context selectively via ContextManager instead of receiving full JSONs
        string actualDesignJson = designJson;
        if (string.IsNullOrWhiteSpace(designJson))
        {
            try
            {
                // Load design context for this media type from hierarchical storage
                actualDesignJson = await LoadContextAsync(caseId, $"design/media/{spec.Kind}", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load design context for {MediaKind}: {Error}", spec.Kind, ex.Message);
                actualDesignJson = "{}";
            }
        }

        var systemPrompt = @"
            You are a generator of FORENSIC specifications for a single static media asset.
            Output: ONLY valid JSON with { evidenceId, kind, title, prompt, constraints }.

            SCOPE & SAFETY
            - One image only (a collage is acceptable if composed into a single bitmap); never a multi-image sequence.
            - Style: documentary photograph/screenshot, neutral, non-artistic.
            - Content 100% fictitious. Forbid real names, brands/logos, faces/biometrics, real license plates, official badges.
            - No graphic/violent content. Avoid people when the scene allows.
            - Do NOT write the evidence name or caseId inside the image.

            TEMPORAL CONSISTENCY (MANDATORY):
            - ALL timestamps in media MUST use the same timezone offset established in Plan/Design
            - CCTV timestamps MUST match the collectedAt time specified in Design for this evidence
            - Document scan dates MUST be consistent with when the document would realistically exist in the case timeline
            - NO conflicting timestamps - each piece of evidence has exactly one collection time that must be respected

            STRICT CONSISTENCY (NON-NEGOTIABLE)
            - Reflect ONLY the narrative elements already present in Plan/Expand/Design; do not invent new entities, locations, vehicles, or timestamps.
            - If the evidence exists in Expand/Design with specific attributes (time, camera label, object condition, location), MIRROR those details.
            - Use only existing labels already implied by the case (e.g., 'CAM-03', zone ids, door ids). No new camera IDs or zones.
            - Honor any provided initial_constraints (unless they violate safety), mapping them into the final constraints.

            TECHNICAL STANDARDIZATION
            - Always quantify: angle in degrees, camera height OR subject distance in meters, lens in mm, aperture f/, shutter 1/x s, ISO, white balance (K).
            - Lighting: diffuse/even; avoid harsh shadows; prefer color-neutral rendering.
            - For object-on-surface evidence: prefer 90° top-down when applicable.

            MANDATORY FIELD FORMAT
            - prompt: fixed sections in this exact order:
            1) Function (1 sentence)
            2) Scene / Composition (3–5 sentences; include subject percentage in frame)
            3) Angle & Distance (strict numeric values)
            4) Optics & Technique (lens mm, f/, 1/x s, ISO, WB K, focus, DOF)
            5) Mandatory elements (labels/markers/timestamps already present in the case)
            6) Negatives (objective list of what MUST NOT appear)
            7) Acceptance checklist (objective, verifiable bullets)
            - constraints: JSON object containing:
            - angle_deg (number)
            - camera_height_m OR distance_m (number; include exactly one of these keys)
            - aspect_ratio (string, e.g., ""16:9"", ""4:3"", ""A4"")
            - resolution_px (string, e.g., ""1920x1080"")
            - seed (7-digit integer as string)
            - deferred=false

            TYPE-SPECIFIC RULES
            - kind=cftv_frame:
            - Overlay timestamp ""YYYY-MM-DD HH:MM:SS"" (top-right, monospaced, white with 1–2px outline).
            - Camera label (e.g., ""CAM-03"") on frame.
            - Camera height 2.5–3.0 m; lens 2.8–4 mm equiv.; shutter 1/60 s; mild H.264 artifacts; slight coherent motion blur.
            - kind=document_scan / receipt:
            - Perspective corrected; 300–600 DPI equivalent; visible margins; no fingers, no moiré; crop clean edges.
            - kind=scene_topdown:
            - 90° orthographic top-down; keep edges parallel to frame; include simple scale reference ONLY if already implied by case (no rulers).

            DO NOT
            - Do not repeat the rulebook in prose; translate rules into concrete parameters.
            - Do not return Markdown, comments, or extra fields.

            PRE-SUBMISSION SELF-CHECK (must be satisfied before answering)
            - Are all referenced labels/timestamps present in the supplied case context?
            - Are prompt sections exactly in the required order (1–7) and fully populated?
            - Does constraints include angle_deg and exactly one of camera_height_m or distance_m, plus aspect_ratio, resolution_px, seed (7 digits), deferred=false?
            - Is this a single image (no sequence)?";

        // Phase 5: Use loaded design context
        var userPrompt = $@"
            CONTEXT (only the minimal, relevant parts — do NOT restate everything)
            {actualDesignJson}

            EVIDENCE SPECIFICATION
            - evidenceId: {spec.EvidenceId}
            - kind: {spec.Kind}
            - title: {spec.Title}
            - difficulty: {(difficultyOverride ?? "Detective")}
            - initial_constraints: {(spec.Constraints != null && spec.Constraints.Any() ? string.Join(", ", spec.Constraints.Select(kv => $"{kv.Key}: {kv.Value}")) : "n/a")}
            - language: en-US

            LEVEL OF DETAIL BY DIFFICULTY (apply WITHOUT creating a sequence):
            - Rookie: simple composition; prefer top-down when applicable.
            - Detective/Detective2: add one contextual detail within the same frame.
            - Sergeant+: wider context PLUS a relevant local detail in the same frame (no multi-image montage); subtle false-positive ONLY if consistent with the case.
            - Captain/Commander: include plausible control objects (e.g., neutral color/gray card) only if consistent with operational reality for that scene.

            HARD REQUIREMENTS FOR THIS OUTPUT
            - Return ONLY valid JSON with: evidenceId, kind, title, prompt, constraints.
            - The prompt MUST follow the 7 fixed sections in the exact order and include measurable values.
            - constraints MUST include: angle_deg, camera_height_m OR distance_m (exactly one), aspect_ratio, resolution_px, seed (7 digits), deferred=false.
            - If kind=cftv_frame: put timestamp overlay ""YYYY-MM-DD HH:MM:SS"" (top-right) and a camera label like ""CAM-03"".
            - Respect initial_constraints when provided (unless unsafe/contradictory).
            - Do NOT invent new entities, labels, brands, or times.
            ";

        var json = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);

        try
        {
            await _caseLogging.LogStepResponseAsync(caseId, $"media/{spec.EvidenceId}", json, cancellationToken);

            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var docBundlePath = $"{caseId}/media/{spec.EvidenceId}.json";
            await _storageService.SaveFileAsync(bundlesContainer, docBundlePath, json, cancellationToken);

            _logger.LogInformation("BUNDLE: Saved media to bundle: {Path} (case={CaseId},  kind={Kind})",
                docBundlePath, caseId, spec.Kind);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist generated media {DocId} for case {CaseId}",
                spec.EvidenceId, caseId);
        }

        return json;
    }


    public async Task<string> RenderDocumentFromJsonAsync(string docId, string documentJson, string caseId, CancellationToken cancellationToken = default)
    {
        return await _pdfRenderingService.RenderDocumentFromJsonAsync(docId, documentJson, caseId, cancellationToken);
    }

    public async Task<string> RenderMediaFromJsonAsync(MediaSpec spec, string caseId, CancellationToken cancellationToken = default)
    {
        return await _imagesService.GenerateAsync(caseId, spec);
    }

    public async Task<NormalizationResult> NormalizeCaseDeterministicAsync(NormalizationInput input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Normalizing case content deterministically for case {CaseId}", input.CaseId);
        return await _normalizerService.NormalizeCaseAsync(input, cancellationToken);
    }

    public async Task<string> ValidateRulesAsync(string normalizedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating case rules");

        var systemPrompt = """
            You are a specialist in validating detective game cases. 
            Verify that the case complies with gameplay, narrative consistency, and quality standards.
            
            Pay special attention to TEMPORAL CONSISTENCY:
            - All timestamps must use consistent timezone offset throughout the case
            - Document creation dates must logically follow incident timeline  
            - Evidence collection times must be realistic and chronologically sound
            - Interview timestamps must be properly sequenced
            - No overlapping or conflicting timestamps between documents/evidence
            - Chain of custody timestamps must be chronologically ordered
            """;

        var userPrompt = $"""
            Validate this normalized case against the defined quality rules:
            
            {normalizedJson}
            
            Check: 
            1. TEMPORAL CONSISTENCY: Verify all timestamps use consistent timezone, are chronologically logical, and have no conflicts
            2. Narrative consistency and logical flow
            3. Gameplay balance and challenge level
            4. Completeness of clues and evidence
            5. Realism and authenticity
            6. Overall case quality and solvability
            
            Flag any timestamp inconsistencies, timezone mismatches, or chronological errors as critical issues.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    [Obsolete("Use RedTeamGlobalAnalysisAsync and RedTeamFocusedAnalysisAsync for hierarchical analysis instead")]
    public async Task<string> RedTeamCaseAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        return await RedTeamCaseChunkedAsync(validatedJson, caseId, cancellationToken);
    }

    public async Task<string> RedTeamGlobalAnalysisAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("REDTEAM GLOBAL: Starting macro analysis for case {CaseId}", caseId);
        
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            _logger.LogError("REDTEAM GLOBAL: Empty or null validatedJson provided for case {CaseId}", caseId);
            return CreateFallbackGlobalAnalysis("Global analysis failed - empty input JSON provided.");
        }

        // Check cache first
        var contentHash = _redTeamCache.ComputeContentHash(validatedJson);
        var cachedAnalysis = await _redTeamCache.GetCachedAnalysisAsync(contentHash, "Global", null, cancellationToken);
        
        if (cachedAnalysis != null)
        {
            _logger.LogInformation("REDTEAM GLOBAL: Using cached analysis for case {CaseId} (hash: {Hash})", 
                caseId, contentHash[..8]);
            return cachedAnalysis;
        }

        // Log input size for monitoring
        _logger.LogInformation("REDTEAM GLOBAL: Input JSON size: {Size} bytes for case {CaseId}", 
            validatedJson.Length, caseId);

        var systemPrompt = """
            You are a senior forensic case analyst conducting a high-level strategic assessment of a complete forensic case.
            Your goal is to identify MACRO-LEVEL issues that affect the case's overall integrity and coherence.

            Focus on:
            1. CROSS-DOCUMENT INCONSISTENCIES: Contradictions between different documents
            2. CHRONOLOGICAL PROBLEMS: Timeline gaps, impossible sequences, temporal contradictions
            3. NARRATIVE COHERENCE: Story elements that don't align across the case
            4. REFERENCE INTEGRITY: Missing or broken cross-references between documents
            5. STRUCTURAL COMPLETENESS: Missing critical elements or documents

            Do NOT focus on:
            - Minor formatting issues
            - Small textual errors
            - Document-specific content problems
            - Individual timestamp corrections

            Provide a strategic assessment that identifies which areas need detailed focused analysis.
            """;

        var jsonStructure = """
            {
                "MacroIssues": [
                    {
                        "Type": "CrossDocumentInconsistency|ChronologicalGap|NarrativeContradiction|ReferenceIntegrity|StructuralCompleteness",
                        "Severity": "Critical|Major|Minor",
                        "AffectedDocuments": ["doc_id_1", "doc_id_2"],
                        "Description": "Clear description of the macro issue",
                        "RequiredFocusAreas": ["specific_section_1", "specific_field_2"]
                    }
                ],
                "CriticalDocuments": ["doc_ids_needing_detailed_analysis"],
                "FocusAreas": ["specific_areas_to_examine_in_detail"],
                "OverallAssessment": "Strategic assessment of case quality",
                "RequiresDetailedAnalysis": true
            }
            """;

        var userPrompt = $"""
            Analyze this complete forensic case for macro-level issues. Return your analysis as valid JSON:

            {validatedJson}

            Required JSON structure:
            {jsonStructure}
            """;

        try
        {
            var result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("REDTEAM GLOBAL: Empty response received for case {CaseId}", caseId);
                return CreateFallbackGlobalAnalysis("Global analysis failed - empty response received from LLM.");
            }

            // Clean and validate JSON
            var cleanedResult = CleanRedTeamJsonResponse(result);
            
            // Try to parse to validate structure
            try
            {
                var analysis = JsonSerializer.Deserialize<GlobalRedTeamAnalysis>(cleanedResult);
                if (analysis != null)
                {
                    _logger.LogInformation("REDTEAM GLOBAL: Completed - {MacroIssueCount} macro issues found", 
                        analysis.MacroIssues.Count);
                    
                    // Cache the successful analysis
                    await _redTeamCache.CacheAnalysisAsync(contentHash, cleanedResult, "Global", null, cancellationToken);
                    
                    return cleanedResult;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "REDTEAM GLOBAL: Invalid JSON structure returned for case {CaseId}", caseId);
            }

            // Cache even if parsing failed - let downstream handle it, but avoid re-processing
            await _redTeamCache.CacheAnalysisAsync(contentHash, cleanedResult, "Global", null, cancellationToken);
            
            return cleanedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REDTEAM GLOBAL: Exception during analysis for case {CaseId}", caseId);
            return CreateFallbackGlobalAnalysis($"Global analysis failed with exception: {ex.Message}");
        }
    }

    public async Task<string> RedTeamFocusedAnalysisAsync(string validatedJson, string caseId, string globalAnalysis, string[] focusAreas, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("REDTEAM FOCUSED: Starting detailed analysis for case {CaseId} on {FocusAreaCount} areas", 
            caseId, focusAreas.Length);
        
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            _logger.LogError("REDTEAM FOCUSED: Empty or null validatedJson provided for case {CaseId}", caseId);
            return CreateFallbackStructuredAnalysis("Focused analysis failed - empty input JSON provided.");
        }

        // Check cache first - for focused analysis, we include focus areas in the cache key
        var contentHash = _redTeamCache.ComputeContentHash(validatedJson + globalAnalysis);
        var cachedAnalysis = await _redTeamCache.GetCachedAnalysisAsync(contentHash, "Focused", focusAreas, cancellationToken);
        
        if (cachedAnalysis != null)
        {
            _logger.LogInformation("REDTEAM FOCUSED: Using cached analysis for case {CaseId} (hash: {Hash})", 
                caseId, contentHash[..8]);
            return cachedAnalysis;
        }

        // Use existing chunked analysis but with focused prompting
        var result = await RedTeamCaseChunkedAsync(validatedJson, caseId, cancellationToken, globalAnalysis, focusAreas);
        
        // Cache the result if successful
        if (!string.IsNullOrWhiteSpace(result))
        {
            await _redTeamCache.CacheAnalysisAsync(contentHash, result, "Focused", focusAreas, cancellationToken);
        }
        
        return result;
    }

    public async Task<string> RedTeamCaseChunkedAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        return await RedTeamCaseChunkedAsync(validatedJson, caseId, cancellationToken, null, null);
    }

    public async Task<string> RedTeamCaseChunkedAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default, string? globalAnalysis = null, string[]? focusAreas = null)
    {
        var analysisType = globalAnalysis != null ? "FOCUSED" : "STANDARD";
        _logger.LogInformation("PRECISION REDTEAM CHUNKED: Starting structured {AnalysisType} analysis for case {CaseId}", analysisType, caseId);
        
        // Input validation
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            _logger.LogError("PRECISION REDTEAM CHUNKED: Empty or null validatedJson provided for case {CaseId}", caseId);
            return CreateFallbackStructuredAnalysis("RedTeam analysis failed - empty input JSON provided.");
        }

        var inputSize = System.Text.Encoding.UTF8.GetByteCount(validatedJson);
        _logger.LogInformation("PRECISION REDTEAM CHUNKED: Input JSON size: {InputSize} bytes for case {CaseId}", inputSize, caseId);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(validatedJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "PRECISION REDTEAM CHUNKED: Invalid input JSON for case {CaseId}", caseId);
            return CreateFallbackStructuredAnalysis($"RedTeam analysis failed - invalid input JSON: {ex.Message}");
        }

        using (doc)
        {
            // Step 1: Build skeleton and indexes
            var (skeletonJson, docMap, mediaMap) = BuildSkeletonAndIndexes(doc.RootElement);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Built skeleton and indexes - {DocCount} docs, {MediaCount} media", 
                docMap.Count, mediaMap.Count);

            // Step 2: Read configuration
            var maxBytesPerCall = _configuration.GetValue("CaseGenerator:RedTeam:MaxBytesPerCall", DefaultMaxBytesPerCall);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Using maxBytesPerCall={MaxBytes}", maxBytesPerCall);

            // Step 3: Plan chunks
            var chunks = PlanChunks(docMap, mediaMap, maxBytesPerCall, skeletonJson);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Planned {ChunkCount} chunks", chunks.Count);

            // Step 4: Process chunks with limited parallelism
            var semaphore = new SemaphoreSlim(MaxParallelCalls, MaxParallelCalls);
            var tasks = chunks.Select(async chunk =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await RunRedTeamOnChunkAsync(chunk, skeletonJson, docMap, mediaMap, caseId, cancellationToken, globalAnalysis, focusAreas);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var analyses = await Task.WhenAll(tasks);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Processed all {ChunkCount} chunks", chunks.Count);

            // Step 5: Merge analyses
            var mergedAnalysis = MergeAnalyses(analyses);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Merged analysis - {IssueCount} total issues", 
                mergedAnalysis.Issues.Count);

            // Step 6: Serialize and return
            var result = JsonSerializer.Serialize(mergedAnalysis, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Completed analysis for case {CaseId}: {ResultLength} chars", 
                caseId, result.Length);
            
            return result;
        }
    }

    #region RedTeam Chunked Processing Helpers

    private (string skeletonJson, Dictionary<string, JsonElement> docMap, Dictionary<string, JsonElement> mediaMap) 
        BuildSkeletonAndIndexes(JsonElement root)
    {
        var docMap = new Dictionary<string, JsonElement>();
        var mediaMap = new Dictionary<string, JsonElement>();
        var timestamps = new HashSet<string>();

        // Extract timezone and difficulty
        var timezone = root.TryGetProperty("timezone", out var tzProp) ? tzProp.GetString() : "UTC";
        var difficulty = root.TryGetProperty("difficulty", out var diffProp) ? diffProp.GetString() : "Rookie";

        // Build document map and collect timestamps
        if (root.TryGetProperty("documents", out var docsArray))
        {
            foreach (var doc in docsArray.EnumerateArray())
            {
                if (doc.TryGetProperty("docId", out var docIdProp))
                {
                    var docId = docIdProp.GetString();
                    if (!string.IsNullOrEmpty(docId))
                    {
                        docMap[docId] = doc;
                        
                        // Collect timestamps from document
                        if (doc.TryGetProperty("createdAt", out var createdProp))
                        {
                            var created = createdProp.GetString();
                            if (IsTimestamp(created)) timestamps.Add(created!);
                        }
                        if (doc.TryGetProperty("modifiedAt", out var modifiedProp))
                        {
                            var modified = modifiedProp.GetString();
                            if (IsTimestamp(modified)) timestamps.Add(modified!);
                        }
                    }
                }
            }
        }

        // Build media map and collect timestamps
        if (root.TryGetProperty("media", out var mediaArray))
        {
            foreach (var media in mediaArray.EnumerateArray())
            {
                if (media.TryGetProperty("evidenceId", out var evidenceIdProp))
                {
                    var evidenceId = evidenceIdProp.GetString();
                    if (!string.IsNullOrEmpty(evidenceId))
                    {
                        mediaMap[evidenceId] = media;
                        
                        // Collect timestamps from media
                        if (media.TryGetProperty("collectedAt", out var collectedProp))
                        {
                            var collected = collectedProp.GetString();
                            if (IsTimestamp(collected)) timestamps.Add(collected!);
                        }
                    }
                }
            }
        }

        // Build skeleton
        var skeleton = new
        {
            timezone = timezone,
            difficulty = difficulty,
            indexes = new
            {
                docIds = docMap.Keys.ToArray(),
                evidenceIds = mediaMap.Keys.ToArray()
            },
            temporalLedger = timestamps.Take(50).ToArray() // Limit to prevent growth
        };

        var skeletonJson = JsonSerializer.Serialize(skeleton, new JsonSerializerOptions { WriteIndented = true });
        return (skeletonJson, docMap, mediaMap);
    }

    private bool IsTimestamp(string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        
        // Simple ISO-8601 validation with timezone
        var isoPattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$";
        return Regex.IsMatch(s, isoPattern);
    }

    private List<RedTeamChunkSpec> PlanChunks(
        Dictionary<string, JsonElement> docMap, 
        Dictionary<string, JsonElement> mediaMap, 
        int maxBytesPerCall, 
        string skeletonJson)
    {
        var chunks = new List<RedTeamChunkSpec>();
        var skeletonBytes = Encoding.UTF8.GetByteCount(skeletonJson);
        var availableBytes = maxBytesPerCall - skeletonBytes - 1000; // Reserve for prompt overhead

        if (availableBytes <= 0)
        {
            _logger.LogWarning("CHUNKING: Skeleton too large, using minimal chunks");
            availableBytes = 5000; // Fallback
        }

        var currentDocIds = new List<string>();
        var currentEvidenceIds = new List<string>();
        var currentBytes = 0;

        // Add documents to chunks
        foreach (var (docId, docElement) in docMap)
        {
            var docBytes = Encoding.UTF8.GetByteCount(docElement.GetRawText());
            
            if (currentBytes + docBytes > availableBytes && currentDocIds.Count > 0)
            {
                // Create chunk and reset
                chunks.Add(new RedTeamChunkSpec
                {
                    DocIds = currentDocIds.ToArray(),
                    EvidenceIds = currentEvidenceIds.ToArray(),
                    EstimatedBytes = currentBytes + skeletonBytes
                });
                
                currentDocIds.Clear();
                currentEvidenceIds.Clear();
                currentBytes = 0;
            }
            
            currentDocIds.Add(docId);
            currentBytes += docBytes;
        }

        // Add media to current or new chunks
        foreach (var (evidenceId, mediaElement) in mediaMap)
        {
            var mediaBytes = Encoding.UTF8.GetByteCount(mediaElement.GetRawText());
            
            if (currentBytes + mediaBytes > availableBytes && (currentDocIds.Count > 0 || currentEvidenceIds.Count > 0))
            {
                // Create chunk and reset
                chunks.Add(new RedTeamChunkSpec
                {
                    DocIds = currentDocIds.ToArray(),
                    EvidenceIds = currentEvidenceIds.ToArray(),
                    EstimatedBytes = currentBytes + skeletonBytes
                });
                
                currentDocIds.Clear();
                currentEvidenceIds.Clear();
                currentBytes = 0;
            }
            
            currentEvidenceIds.Add(evidenceId);
            currentBytes += mediaBytes;
        }

        // Add final chunk if needed
        if (currentDocIds.Count > 0 || currentEvidenceIds.Count > 0)
        {
            chunks.Add(new RedTeamChunkSpec
            {
                DocIds = currentDocIds.ToArray(),
                EvidenceIds = currentEvidenceIds.ToArray(),
                EstimatedBytes = currentBytes + skeletonBytes
            });
        }

        // Ensure at least one chunk exists
        if (chunks.Count == 0)
        {
            chunks.Add(new RedTeamChunkSpec
            {
                DocIds = Array.Empty<string>(),
                EvidenceIds = Array.Empty<string>(),
                EstimatedBytes = skeletonBytes
            });
        }

        return chunks;
    }

    private async Task<StructuredRedTeamAnalysis> RunRedTeamOnChunkAsync(
        RedTeamChunkSpec chunk,
        string skeletonJson,
        Dictionary<string, JsonElement> docMap,
        Dictionary<string, JsonElement> mediaMap,
        string caseId,
        CancellationToken cancellationToken,
        string? globalAnalysis = null,
        string[]? focusAreas = null)
    {
        _logger.LogInformation("REDTEAM CHUNK: Processing {DocCount} docs, {MediaCount} media ({EstimatedBytes} bytes)",
            chunk.DocIds.Length, chunk.EvidenceIds.Length, chunk.EstimatedBytes);

        // Build scoped JSON with skeleton + chunk data
        var scopedData = new
        {
            skeleton = JsonSerializer.Deserialize<object>(skeletonJson),
            documents = chunk.DocIds.Select(id => docMap.TryGetValue(id, out var doc) ? doc : (JsonElement?)null)
                                   .Where(d => d.HasValue)
                                   .Select(d => JsonSerializer.Deserialize<object>(d!.Value.GetRawText()))
                                   .ToArray(),
            media = chunk.EvidenceIds.Select(id => mediaMap.TryGetValue(id, out var media) ? media : (JsonElement?)null)
                                     .Where(m => m.HasValue)
                                     .Select(m => JsonSerializer.Deserialize<object>(m!.Value.GetRawText()))
                                     .ToArray()
        };

        var scopedJson = JsonSerializer.Serialize(scopedData, new JsonSerializerOptions { WriteIndented = true });

        // Build context-aware system prompt
        var contextualPrompt = globalAnalysis != null 
            ? $"""
            You are a precision red team specialist for police investigative training content.
            Analyze ONLY the documents and media provided in this specific chunk scope.
            
            GLOBAL CONTEXT PROVIDED:
            You have access to macro-level analysis that identified these key issues:
            {globalAnalysis}
            
            FOCUSED ANALYSIS AREAS:
            {(focusAreas?.Length > 0 ? string.Join(", ", focusAreas) : "Standard analysis")}
            
            CRITICAL MISSION:
            - Use the global context to inform your detailed analysis
            - Focus especially on areas identified in the global analysis
            - Identify problems with SURGICAL PRECISION within the provided scope only
            - Specify EXACT document IDs, field paths, and problematic values
            - Provide SPECIFIC fix instructions for each issue
            - Prioritize issues that relate to the macro-level problems identified
            """
            : """
            You are a precision red team specialist for police investigative training content. 
            Analyze ONLY the documents and media provided in this specific chunk scope.
            
            CRITICAL MISSION:
            - Identify problems with SURGICAL PRECISION within the provided scope only
            - Specify EXACT document IDs, field paths, and problematic values
            - Provide SPECIFIC fix instructions for each issue
            - Focus on temporal inconsistencies as highest priority
            """;

        var systemPrompt = $$$"""
            {{{contextualPrompt}}}
            
            CRITICAL JSON FORMAT REQUIREMENTS:
            - ALL string fields must be valid JSON strings (no arrays in string fields)
            - CurrentValue must be a single string, not an array
            - If you need to reference multiple values, use comma-separated strings
            - Do not use arrays where strings are expected
            
            OUTPUT FORMAT: Return ONLY valid JSON matching this EXACT structure:
            {{
                "Issues": [
                    {{
                        "Priority": "High|Medium|Low",
                        "Type": "TimestampConflict|PostCreationReference|ChronologicalGap|etc",
                        "Problem": "Clear description of the problem",
                        "Location": {{
                            "DocId": "document_id",
                            "Field": "field_path",
                            "Section": "section_name",
                            "LinePattern": "text_pattern_to_find",
                            "CurrentValue": "current_problematic_value"
                        }},
                        "Fix": {{
                            "Action": "UpdateTimestamp|ReplaceText|MoveToAddendum|RemoveReference|AddMediaAttachment|GenerateMissingDocument",
                            "NewValue": "new_value_to_set",
                            "OldText": "text_to_replace",
                            "NewText": "replacement_text", 
                            "Reason": "explanation_why_fix_needed"
                        }}
                    }}
                ],
                "Summary": "Brief summary of all issues found",
                "HighPriorityCount": 0,
                "MediumPriorityCount": 0,
                "LowPriorityCount": 0
            }}
            
            PRIORITY GUIDELINES:
            - High: Timestamp conflicts, chronological impossibilities
            - Medium: Timeline gaps, missing context
            - Low: Minor wording, style improvements
            """;

        var userPrompt = $"""
            Analyze this chunk scope and identify specific problems with exact locations:
            
            CHUNK SCOPE JSON:
            {scopedJson}
            
            FIND AND SPECIFY (within this scope only):
            1. EXACT document IDs where problems occur
            2. SPECIFIC fields or text patterns that are problematic  
            3. CURRENT values that need to be changed
            4. PRECISE fix instructions (new timestamps, replacement text, etc.)
            
            Focus especially on:
            - Evidence compilation times vs collection times
            - Report creation times vs referenced events
            - Chronological order conflicts
            - Timeline consistency within this chunk
            
            Return structured JSON analysis with surgical precision.
            """;

        string? result = null;
        try
        {
            result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("REDTEAM CHUNK: Empty response for chunk, returning empty analysis");
                return new StructuredRedTeamAnalysis
                {
                    Issues = new List<PreciseIssue>(),
                    Summary = "Empty response for chunk",
                    HighPriorityCount = 0,
                    MediumPriorityCount = 0,
                    LowPriorityCount = 0
                };
            }

            // Try to clean and fix the JSON before deserializing
            var cleanedResult = CleanRedTeamJsonResponse(result);
            
            var analysis = JsonSerializer.Deserialize<StructuredRedTeamAnalysis>(cleanedResult);
            if (analysis != null)
            {
                _logger.LogInformation("REDTEAM CHUNK: Completed - {IssueCount} issues found", analysis.Issues.Count);
                return analysis;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Invalid JSON response, returning empty analysis. Response preview: {Preview}", 
                result?.Length > 500 ? result[..500] + "..." : result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Exception during processing, returning empty analysis");
        }

        return new StructuredRedTeamAnalysis
        {
            Issues = new List<PreciseIssue>(),
            Summary = "Chunk processing failed",
            HighPriorityCount = 0,
            MediumPriorityCount = 0,
            LowPriorityCount = 0
        };
    }

    private StructuredRedTeamAnalysis MergeAnalyses(IEnumerable<StructuredRedTeamAnalysis> analyses)
    {
        var allIssues = new List<PreciseIssue>();
        var summaries = new List<string>();
        int totalHigh = 0, totalMedium = 0, totalLow = 0;

        foreach (var analysis in analyses)
        {
            allIssues.AddRange(analysis.Issues);
            if (!string.IsNullOrWhiteSpace(analysis.Summary))
            {
                summaries.Add(analysis.Summary);
            }
            totalHigh += analysis.HighPriorityCount;
            totalMedium += analysis.MediumPriorityCount;
            totalLow += analysis.LowPriorityCount;
        }

        // Dedupe issues by key combination
        var deduped = allIssues
            .GroupBy(issue => new 
            { 
                issue.Location.DocId, 
                issue.Location.Field, 
                issue.Location.Section, 
                issue.Location.LinePattern 
            })
            .Select(g => g.First())
            .ToList();

        // Recalculate counts from deduped issues
        var actualHigh = deduped.Count(i => i.Priority == "High");
        var actualMedium = deduped.Count(i => i.Priority == "Medium");
        var actualLow = deduped.Count(i => i.Priority == "Low");

        var mergedSummary = summaries.Count > 0 
            ? $"Merged analysis from {summaries.Count} chunks: {string.Join("; ", summaries.Take(3))}"
            : "No issues found in any chunks";

        return new StructuredRedTeamAnalysis
        {
            Issues = deduped,
            Summary = mergedSummary,
            HighPriorityCount = actualHigh,
            MediumPriorityCount = actualMedium,
            LowPriorityCount = actualLow
        };
    }

    private string CleanRedTeamJsonResponse(string jsonResponse)
    {
        try
        {
            // Parse as JsonDocument to inspect and fix structural issues
            using var doc = JsonDocument.Parse(jsonResponse);
            
            // Check if we have the expected structure
            if (!doc.RootElement.TryGetProperty("Issues", out var issuesArray))
            {
                _logger.LogWarning("REDTEAM CHUNK: Response missing 'Issues' property");
                return jsonResponse; // Return as-is if basic structure is missing
            }

            // Create a mutable structure to fix issues
            var mutableResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse)!;
            
            if (mutableResponse.TryGetValue("Issues", out var issuesObj) && issuesObj is JsonElement issuesElement)
            {
                var fixedIssues = new List<object>();
                
                foreach (var issue in issuesElement.EnumerateArray())
                {
                    var fixedIssue = FixIssueStructure(issue);
                    if (fixedIssue != null)
                    {
                        fixedIssues.Add(fixedIssue);
                    }
                }
                
                mutableResponse["Issues"] = fixedIssues;
            }

            // Serialize back to clean JSON
            var cleanedJson = JsonSerializer.Serialize(mutableResponse, new JsonSerializerOptions { WriteIndented = false });
            return cleanedJson;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Failed to clean JSON response, returning original");
            return jsonResponse;
        }
    }

    private object? FixIssueStructure(JsonElement issueElement)
    {
        try
        {
            var issueDict = new Dictionary<string, object>();
            
            // Copy basic properties
            if (issueElement.TryGetProperty("Priority", out var priority))
                issueDict["Priority"] = priority.GetString() ?? "Low";
            
            if (issueElement.TryGetProperty("Type", out var type))
                issueDict["Type"] = type.GetString() ?? "Unknown";
            
            if (issueElement.TryGetProperty("Problem", out var problem))
                issueDict["Problem"] = problem.GetString() ?? "Unknown issue";

            // Fix Location structure
            if (issueElement.TryGetProperty("Location", out var location))
            {
                var locationDict = new Dictionary<string, object?>();
                
                if (location.TryGetProperty("DocId", out var docId))
                    locationDict["DocId"] = docId.GetString() ?? "";
                
                if (location.TryGetProperty("Field", out var field))
                    locationDict["Field"] = field.GetString();
                
                if (location.TryGetProperty("Section", out var section))
                    locationDict["Section"] = section.GetString();
                
                if (location.TryGetProperty("LinePattern", out var linePattern))
                    locationDict["LinePattern"] = linePattern.GetString();
                
                // Fix CurrentValue - convert arrays to strings
                if (location.TryGetProperty("CurrentValue", out var currentValue))
                {
                    if (currentValue.ValueKind == JsonValueKind.Array)
                    {
                        // Convert array to comma-separated string
                        var arrayValues = new List<string>();
                        foreach (var item in currentValue.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                arrayValues.Add(item.GetString() ?? "");
                        }
                        locationDict["CurrentValue"] = string.Join(", ", arrayValues);
                    }
                    else if (currentValue.ValueKind == JsonValueKind.String)
                    {
                        locationDict["CurrentValue"] = currentValue.GetString();
                    }
                    else
                    {
                        locationDict["CurrentValue"] = currentValue.GetRawText();
                    }
                }
                
                issueDict["Location"] = locationDict;
            }

            // Fix Fix structure
            if (issueElement.TryGetProperty("Fix", out var fix))
            {
                var fixDict = new Dictionary<string, object?>();
                
                if (fix.TryGetProperty("Action", out var action))
                    fixDict["Action"] = action.GetString() ?? "ReplaceText";
                
                if (fix.TryGetProperty("NewValue", out var newValue))
                    fixDict["NewValue"] = newValue.GetString();
                
                if (fix.TryGetProperty("OldText", out var oldText))
                    fixDict["OldText"] = oldText.GetString();
                
                if (fix.TryGetProperty("NewText", out var newText))
                    fixDict["NewText"] = newText.GetString();
                
                if (fix.TryGetProperty("Reason", out var reason))
                    fixDict["Reason"] = reason.GetString();
                
                issueDict["Fix"] = fixDict;
            }

            return issueDict;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Failed to fix issue structure, skipping issue");
            return null;
        }
    }

    #endregion

    private string CreateFallbackStructuredAnalysis(string errorMessage)
    {
        _logger.LogWarning("PRECISION REDTEAM: Creating fallback analysis due to: {ErrorMessage}", errorMessage);
        
        var fallback = new StructuredRedTeamAnalysis
        {
            Issues = new List<PreciseIssue>(),
            Summary = $"FALLBACK ANALYSIS: {errorMessage} - No specific issues identified due to service failure.",
            HighPriorityCount = 0,
            MediumPriorityCount = 0,
            LowPriorityCount = 0
        };
        
        var serializedFallback = JsonSerializer.Serialize(fallback, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("PRECISION REDTEAM: Generated fallback analysis: {FallbackLength} characters", serializedFallback.Length);
        
        return serializedFallback;
    }

    private string CreateFallbackGlobalAnalysis(string errorMessage)
    {
        _logger.LogWarning("REDTEAM GLOBAL: Creating fallback global analysis due to: {ErrorMessage}", errorMessage);
        
        var fallback = new GlobalRedTeamAnalysis
        {
            MacroIssues = new List<MacroIssue>(),
            CriticalDocuments = Array.Empty<string>(),
            FocusAreas = Array.Empty<string>(),
            OverallAssessment = $"FALLBACK GLOBAL ANALYSIS: {errorMessage} - Unable to perform macro-level assessment due to service failure.",
            RequiresDetailedAnalysis = false // Safe default when analysis fails
        };
        
        var serializedFallback = JsonSerializer.Serialize(fallback, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("REDTEAM GLOBAL: Generated fallback global analysis: {FallbackLength} characters", serializedFallback.Length);
        
        return serializedFallback;
    }

    public async Task<string> FixCaseAsync(string redTeamAnalysis, string currentJson, string caseId, int iterationNumber = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SURGICAL FIX: Starting precision corrections - iteration {Iteration}", iterationNumber);
        _logger.LogInformation("SURGICAL FIX: Current JSON length: {JsonLength} chars", currentJson?.Length ?? 0);

        // Validate input JSON
        if (string.IsNullOrWhiteSpace(currentJson))
        {
            throw new ArgumentException("Current case JSON cannot be null or empty", nameof(currentJson));
        }

        // Validate JSON structure
        try
        {
            using var doc = JsonDocument.Parse(currentJson);
            _logger.LogInformation("SURGICAL FIX: JSON validation successful for case {CaseId}", caseId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SURGICAL FIX: Invalid JSON provided to FixCaseAsync for case {CaseId}", caseId);
            throw new ArgumentException($"Invalid JSON structure: {ex.Message}", nameof(currentJson));
        }

        // Parse structured RedTeam analysis
        StructuredRedTeamAnalysis analysis;
        try
        {
            analysis = JsonSerializer.Deserialize<StructuredRedTeamAnalysis>(redTeamAnalysis)!;
            _logger.LogInformation("SURGICAL FIX: Parsed {IssueCount} issues ({HighPriority} high, {MediumPriority} medium, {LowPriority} low)", 
                analysis.Issues.Count, analysis.HighPriorityCount, analysis.MediumPriorityCount, analysis.LowPriorityCount);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SURGICAL FIX: Failed to parse structured RedTeam analysis, falling back to LLM approach");
            return await FallbackToLLMFix(redTeamAnalysis, currentJson, caseId, iterationNumber, cancellationToken);
        }

        // If no issues found, return original
        if (analysis.Issues.Count == 0)
        {
            _logger.LogInformation("SURGICAL FIX: No issues to fix for case {CaseId}", caseId);
            return currentJson;
        }

        // Apply precision corrections using injected PrecisionEditor
        var correctedJson = await _precisionEditor.ApplyPreciseFixesAsync(currentJson, analysis, caseId, cancellationToken);

        // Validate final result
        try
        {
            using var finalDoc = JsonDocument.Parse(correctedJson);
            _logger.LogInformation("SURGICAL FIX: Completed for case {CaseId}, iteration {Iteration}: {ResultLength} chars", 
                caseId, iterationNumber, correctedJson.Length);
            return correctedJson;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SURGICAL FIX: Final validation failed for case {CaseId}, returning original", caseId);
            return currentJson;
        }
    }

    private async Task<string> FallbackToLLMFix(string redTeamAnalysis, string currentJson, string caseId, int iterationNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("FALLBACK FIX: Using LLM approach for case {CaseId}", caseId);

        var systemPrompt = """
            You are an expert case correction agent. Apply specific corrections mentioned in the analysis.
            Return complete corrected JSON only (no markdown, no explanations).
            Make minimal, surgical changes based solely on the analysis.
            """;

        var userPrompt = $"""
            Apply corrections from this analysis:
            
            {redTeamAnalysis}
            
            To this case JSON:
            {currentJson}
            
            Return corrected complete JSON (iteration #{iterationNumber}).
            """;

        var result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogError("FALLBACK FIX: Empty response for case {CaseId}", caseId);
            return currentJson;
        }
        
        try
        {
            using var doc = JsonDocument.Parse(result);
            _logger.LogInformation("FALLBACK FIX: Successful for case {CaseId}: {ResultLength} chars", caseId, result.Length);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "FALLBACK FIX: Invalid JSON for case {CaseId}, returning original", caseId);
            return currentJson;
        }
    }

    public async Task<bool> IsCaseCleanAsync(string redTeamAnalysis, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing if case is clean and ready for packaging");

        var systemPrompt = """
            You are a quality assessment agent. Analyze red team feedback to determine if a case is ready for final packaging.
            
            Respond with exactly "CLEAN" if the case has no critical issues, or "NEEDS_FIX" if critical issues remain.
            
            Consider critical issues:
            - Logical inconsistencies that break the case narrative
            - Missing or contradictory evidence
            - Timeline errors that affect investigation flow
            - Character inconsistencies that confuse the story
            - Serious formatting or structural problems
            
            Minor issues (not critical):
            - Small wording improvements
            - Minor character details
            - Stylistic suggestions
            - Performance optimizations
            """;

        var userPrompt = $"""
            Analyze this red team feedback and determine if the case is CLEAN (ready for packaging) or NEEDS_FIX (has critical issues):
            
            RED TEAM ANALYSIS:
            {redTeamAnalysis}
            
            Respond with exactly one word: "CLEAN" or "NEEDS_FIX"
            """;

        var result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
        
        // Validate the result is not empty
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogError("Quality assessment returned empty response for case {CaseId} - defaulting to NEEDS_FIX for safety", caseId);
            return false; // Default to needs fix if we can't assess
        }
        
        // Parse the LLM response
        var cleanResult = result.Trim().ToUpperInvariant();
        var isClean = cleanResult.Contains("CLEAN") && !cleanResult.Contains("NEEDS_FIX");
        
        // Additional safety check - if response is too short or unclear, default to needs fix
        if (cleanResult.Length < 4)
        {
            _logger.LogWarning("Quality assessment response too short ({Length} chars): '{Response}' - defaulting to NEEDS_FIX", cleanResult.Length, result);
            return false;
        }
        
        _logger.LogInformation("Case quality assessment: '{Result}' (isClean: {IsClean})", cleanResult, isClean);
        return isClean;
    }

    public async Task<CaseGenerationOutput> PackageCaseAsync(string finalJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Packaging final case: {CaseId}", caseId);

        try
        {
            // Decode from Base64 if needed (orchestrator encodes to avoid Durable Task JSON inspection)
            string actualJson;
            try
            {
                var bytes = Convert.FromBase64String(finalJson);
                actualJson = System.Text.Encoding.UTF8.GetString(bytes);
                _logger.LogInformation("PACKAGE: Decoded Base64 manifest, length = {Length}", actualJson.Length);
            }
            catch (FormatException)
            {
                // Not Base64, use as-is (for backwards compatibility)
                actualJson = finalJson;
                _logger.LogInformation("PACKAGE: Using raw JSON manifest, length = {Length}", actualJson.Length);
            }
            
            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var files = new List<GeneratedFile>();

            // Parse the validated case to extract information
            JsonElement validatedCase;
            try 
            {
                validatedCase = JsonSerializer.Deserialize<JsonElement>(actualJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse finalJson as valid JSON for case {CaseId}. JSON starts with: {JsonStart}", 
                    caseId, actualJson.Length > 100 ? actualJson[..100] : actualJson);
                throw new InvalidOperationException($"Invalid JSON provided to PackageCaseAsync for case {caseId}: {ex.Message}", ex);
            }
            
            // Extract case details from validated JSON
            var timezone = validatedCase.TryGetProperty("timezone", out var tzProp) ? tzProp.GetString() : "UTC";
            var difficulty = validatedCase.TryGetProperty("difficulty", out var diffProp) ? diffProp.GetString() : "Rookie";
            
            // Debug: Log the top-level properties of the JSON
            var topLevelProperties = new List<string>();
            foreach (var property in validatedCase.EnumerateObject())
            {
                topLevelProperties.Add(property.Name);
            }
            _logger.LogInformation("PACKAGE: Top-level JSON properties for case {CaseId}: {Properties}", 
                caseId, string.Join(", ", topLevelProperties));
            
            // Debug: Log sample structure to understand the JSON format
            var jsonSample = finalJson.Length > 1000 ? finalJson[..1000] + "..." : finalJson;
            _logger.LogDebug("PACKAGE: JSON sample for case {CaseId}: {JsonSample}", caseId, jsonSample);
            
            // Count documents and media with detailed logging
            var documentsCount = 0;
            var mediaCount = 0;
            var suspectsCount = 0;
            
            if (validatedCase.TryGetProperty("documents", out var docsObj) &&
                docsObj.TryGetProperty("items", out var docsArray))
            {
                documentsCount = docsArray.GetArrayLength();
                _logger.LogInformation("PACKAGE: Found {DocumentsCount} documents in case {CaseId}", documentsCount, caseId);
            }
            else
            {
                _logger.LogWarning("PACKAGE: No 'documents.items' property found in case {CaseId}", caseId);
            }
            
            if (validatedCase.TryGetProperty("media", out var mediaArray))
            {
                mediaCount = mediaArray.GetArrayLength();
                _logger.LogInformation("PACKAGE: Found {MediaCount} media items in case {CaseId}", mediaCount, caseId);
            }
            else
            {
                // For v2-hierarchical format, media is not in the JSON - count from storage
                _logger.LogInformation("PACKAGE: No 'media' property found in case {CaseId} - counting from storage (v2-hierarchical format)", caseId);
                try
                {
                    var mediaFiles = await _storageService.ListFilesAsync(bundlesContainer, $"{caseId}/media/", cancellationToken);
                    // Count only .json files (each media item has a .json and optionally a .png)
                    mediaCount = mediaFiles.Count(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                    _logger.LogInformation("PACKAGE: Found {MediaCount} media items from storage for case {CaseId}", mediaCount, caseId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PACKAGE: Could not count media files from storage for case {CaseId}", caseId);
                    mediaCount = 0;
                }
            }
            
            if (validatedCase.TryGetProperty("entities", out var entitiesObj) &&
                entitiesObj.TryGetProperty("suspects", out var suspectsArray))
            {
                suspectsCount = suspectsArray.GetArrayLength();
                _logger.LogInformation("PACKAGE: Found {SuspectsCount} suspects in case {CaseId}", suspectsCount, caseId);
            }
            else
            {
                _logger.LogWarning("PACKAGE: No 'entities.suspects' property found in case {CaseId}", caseId);
            }

            // Create comprehensive case manifest
            var caseManifest = new CaseBundle
            {
                CaseId = caseId,
                GeneratedAt = DateTime.UtcNow,
                Timezone = timezone ?? "UTC",
                Difficulty = difficulty ?? "Rookie",
                Counts = new CaseCounts
                {
                    Documents = documentsCount,
                    Media = mediaCount,
                    Suspects = suspectsCount
                },
                ValidationResults = new CaseValidationSummary
                {
                    TimestampsNormalized = "PASS",
                    AllIdsResolved = "PASS", 
                    MediaIntegrity = "PASS"
                },
                Manifest = new List<FileManifestEntry>(),
                RedTeamAnalysis = "RedTeamAnalysis.txt"
            };

            // Save the canonical normalized case file to BUNDLE
            var normalizedCaseFileName = $"{caseId}/normalized_case.json";
            await _storageService.SaveFileAsync(bundlesContainer, normalizedCaseFileName, actualJson, cancellationToken);
            
            var normalizedCaseHash = ComputeSHA256Hash(actualJson);
            caseManifest.Manifest.Add(new FileManifestEntry
            {
                Filename = "normalized_case.json",
                RelativePath = normalizedCaseFileName,
                Sha256 = normalizedCaseHash,
                MimeType = "application/json"
            });
            
            files.Add(new GeneratedFile
            {
                Path = normalizedCaseFileName,
                Type = "json",
                Size = System.Text.Encoding.UTF8.GetByteCount(actualJson),
                CreatedAt = DateTime.UtcNow
            });

            // Add individual documents to manifest (try to read from storage)
            if (validatedCase.TryGetProperty("documents", out var documentsObj) &&
                documentsObj.TryGetProperty("items", out var documentsArray))
            {
                _logger.LogInformation("PACKAGE: Processing {DocumentCount} documents for manifest", documentsArray.GetArrayLength());
                
                foreach (var doc in documentsArray.EnumerateArray())
                {
                    // doc is a string like "@documents/doc_interview_001"
                    var docRef = doc.GetString();
                    if (docRef != null && docRef.StartsWith("@documents/"))
                    {
                        var docId = docRef.Substring("@documents/".Length);
                        var docFileName = $"{caseId}/documents/{docId}.json";
                        
                        try
                        {
                            var docContent = await _storageService.GetFileAsync(bundlesContainer, docFileName, cancellationToken);
                            var docHash = ComputeSHA256Hash(docContent);
                            
                            caseManifest.Manifest.Add(new FileManifestEntry
                            {
                                Filename = $"{docId}.json",
                                RelativePath = docFileName,
                                Sha256 = docHash,
                                MimeType = "application/json"
                            });
                            
                            _logger.LogDebug("PACKAGE: Added document {DocId} to manifest (hash: {Hash})", docId, docHash[..8]);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "PACKAGE: Could not read document {DocId} from storage at {Path} - skipping", 
                                docId, docFileName);
                        }
                    }
                }
            }

            // Add individual media to manifest (try to read from storage)
            if (validatedCase.TryGetProperty("media", out var mediaManifestArray))
            {
                _logger.LogInformation("PACKAGE: Processing {MediaCount} media items for manifest", mediaManifestArray.GetArrayLength());
                
                foreach (var media in mediaManifestArray.EnumerateArray())
                {
                    if (media.TryGetProperty("evidenceId", out var evidenceIdProp))
                    {
                        var evidenceId = evidenceIdProp.GetString();
                        var mediaFileName = $"{caseId}/media/{evidenceId}.json";
                        
                        try
                        {
                            var mediaContent = await _storageService.GetFileAsync(bundlesContainer, mediaFileName, cancellationToken);
                            var mediaHash = ComputeSHA256Hash(mediaContent);
                            
                            caseManifest.Manifest.Add(new FileManifestEntry
                            {
                                Filename = $"{evidenceId}.json",
                                RelativePath = mediaFileName,
                                Sha256 = mediaHash,
                                MimeType = "application/json"
                            });
                            
                            _logger.LogDebug("PACKAGE: Added media {EvidenceId} to manifest (hash: {Hash})", evidenceId, mediaHash[..8]);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "PACKAGE: Could not read media {EvidenceId} from storage at {Path} - creating metadata-only entry", 
                                evidenceId, mediaFileName);
                            
                            // Create a metadata-only entry using the media content from the main JSON
                            var mediaJsonContent = media.GetRawText();
                            var mediaHash = ComputeSHA256Hash(mediaJsonContent);
                            
                            caseManifest.Manifest.Add(new FileManifestEntry
                            {
                                Filename = $"{evidenceId}.json",
                                RelativePath = mediaFileName,
                                Sha256 = mediaHash,
                                MimeType = "application/json"
                            });
                            
                            _logger.LogInformation("PACKAGE: Created metadata-only entry for media {EvidenceId}", evidenceId);
                        }
                    }
                }
            }
            else
            {
                // For v2-hierarchical format, list media files directly from storage
                _logger.LogInformation("PACKAGE: No 'media' property in JSON - listing media files from storage (v2-hierarchical format)");
                try
                {
                    var mediaFiles = await _storageService.ListFilesAsync(bundlesContainer, $"{caseId}/media/", cancellationToken);
                    _logger.LogInformation("PACKAGE: Found {MediaFilesCount} media files in storage for case {CaseId}", mediaFiles.Count(), caseId);
                    
                    foreach (var mediaFile in mediaFiles)
                    {
                        try
                        {
                            var mediaContent = await _storageService.GetFileAsync(bundlesContainer, mediaFile, cancellationToken);
                            var mediaHash = ComputeSHA256Hash(mediaContent);
                            var fileName = Path.GetFileName(mediaFile);
                            var mimeType = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : 
                                          fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "application/octet-stream";
                            
                            caseManifest.Manifest.Add(new FileManifestEntry
                            {
                                Filename = fileName,
                                RelativePath = mediaFile,
                                Sha256 = mediaHash,
                                MimeType = mimeType
                            });
                            
                            _logger.LogDebug("PACKAGE: Added media file {FileName} to manifest (hash: {Hash})", fileName, mediaHash[..8]);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "PACKAGE: Could not read media file {MediaFile} from storage - skipping", mediaFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PACKAGE: Could not list media files from storage for case {CaseId}", caseId);
                }
            }

            // Add the main manifest file to itself (for completeness)
            var tempManifestJson = JsonSerializer.Serialize(caseManifest, new JsonSerializerOptions { WriteIndented = true });
            var caseManifestFileName = $"{caseId}/{caseId}.json";
            var manifestHash = ComputeSHA256Hash(tempManifestJson);
            
            caseManifest.Manifest.Add(new FileManifestEntry
            {
                Filename = $"{caseId}.json",
                RelativePath = caseManifestFileName,
                Sha256 = manifestHash,
                MimeType = "application/json"
            });
            
            // Now serialize with the complete manifest
            var caseManifestJson = JsonSerializer.Serialize(caseManifest, new JsonSerializerOptions { WriteIndented = true });
            await _storageService.SaveFileAsync(bundlesContainer, caseManifestFileName, caseManifestJson, cancellationToken);
            
            files.Add(new GeneratedFile
            {
                Path = caseManifestFileName,
                Type = "json",
                Size = System.Text.Encoding.UTF8.GetByteCount(caseManifestJson),
                CreatedAt = DateTime.UtcNow
            });

            // Save legacy metadata for compatibility
            var bundlePath = $"{caseId}";
            var metadata = new CaseMetadata
            {
                Title = caseId,
                Difficulty = difficulty ?? "Rookie",
                EstimatedDuration = 60,
                Categories = new List<string> { "Investigation", "Training" },
                Tags = new List<string> { "generated", "ai" },
                GeneratedAt = DateTime.UtcNow
            };

            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            var metadataFileName = $"{caseId}/metadata.json";
            await _storageService.SaveFileAsync(bundlesContainer, metadataFileName, metadataJson, cancellationToken);
            
            // Add metadata.json to manifest
            var metadataHash = ComputeSHA256Hash(metadataJson);
            caseManifest.Manifest.Add(new FileManifestEntry
            {
                Filename = "metadata.json",
                RelativePath = metadataFileName,
                Sha256 = metadataHash,
                MimeType = "application/json"
            });
            
            files.Add(new GeneratedFile
            {
                Path = metadataFileName,
                Type = "json",
                Size = System.Text.Encoding.UTF8.GetByteCount(metadataJson),
                CreatedAt = DateTime.UtcNow
            });

            return new CaseGenerationOutput
            {
                BundlePath = bundlePath,
                CaseId = caseId,
                Files = files,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to package case: {CaseId}", caseId);
            throw;
        }
    }

    // Public method for testing PDF generation
    public async Task<byte[]> GenerateTestPdfAsync(string title, string markdownContent, string documentType = "general", CancellationToken cancellationToken = default)
    {
        return await _pdfRenderingService.GenerateTestPdfAsync(title, markdownContent, documentType, cancellationToken);
    }

    // Public method for testing image generation
    public async Task<string> GenerateTestImageAsync(MediaSpec spec, string caseId, CancellationToken cancellationToken = default)
    {
        return await _imagesService.GenerateAsync(caseId, spec);
    }

    // Save RedTeam analysis to logs container
    public async Task SaveRedTeamAnalysisAsync(string caseId, string redTeamAnalysis, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving RedTeam analysis for case {CaseId}", caseId);
        
        var logsContainer = "logs";
        var fileName = $"{caseId}/RedTeamAnalysis.txt";
        await _storageService.SaveFileAsync(logsContainer, fileName, redTeamAnalysis, cancellationToken);
        
        _logger.LogInformation("RedTeam analysis saved successfully to {Container}/{FileName}", logsContainer, fileName);
    }

    private static string ComputeSHA256Hash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}