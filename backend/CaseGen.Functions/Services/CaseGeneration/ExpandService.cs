using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services.CaseGeneration;

/// <summary>
/// Service responsible for expanding planned case elements into detailed content (Phase 3).
/// Handles suspect expansion, evidence expansion, timeline expansion, relationship synthesis, and legacy monolithic expansion.
/// </summary>
public class ExpandService
{
    private readonly ILLMService _llmService;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly IContextManager _contextManager;
    private readonly ILogger<ExpandService> _logger;

    public ExpandService(
        ILLMService llmService,
        IJsonSchemaProvider schemaProvider,
        IContextManager contextManager,
        ILogger<ExpandService> logger)
    {
        _llmService = llmService;
        _schemaProvider = schemaProvider;
        _contextManager = contextManager;
        _logger = logger;
    }

    /// <summary>
    /// Expands a single suspect with detailed profile information (Phase 3.1).
    /// </summary>
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

    /// <summary>
    /// Expands a single evidence item with detailed information (Phase 3.2).
    /// </summary>
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

    /// <summary>
    /// Expands the timeline with detailed event information (Phase 3.3).
    /// </summary>
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

    /// <summary>
    /// Synthesizes relationships across all case elements (Phase 3.4).
    /// </summary>
    public async Task<string> SynthesizeRelationsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SYNTHESIZE-RELATIONS: Synthesizing relationships for case {CaseId}", caseId);
        
        // Load all Plan contexts (core, suspects, timeline, evidence) and expanded timeline
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

    /// <summary>
    /// Helper method to load context from hierarchical storage.
    /// </summary>
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

    /// <summary>
    /// LEGACY: Original monolithic expansion method (Phase 1).
    /// Expands complete case in one LLM call. Use hierarchical methods (ExpandSuspect -> ExpandEvidence -> etc.) for new cases.
    /// </summary>
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

        return await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }
}
