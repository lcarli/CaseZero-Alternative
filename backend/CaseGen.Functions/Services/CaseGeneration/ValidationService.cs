using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services.CaseGeneration;

/// <summary>
/// Service responsible for case validation and RedTeam analysis (Phase 6).
/// Handles normalization, validation rules, and quality assurance.
/// </summary>
public class ValidationService
{
    private readonly ILLMService _llmService;
    private readonly INormalizerService _normalizerService;
    private readonly IRedTeamCacheService _redTeamCache;
    private readonly IPrecisionEditor _precisionEditor;
    private readonly ILogger<ValidationService> _logger;

    public ValidationService(
        ILLMService llmService,
        INormalizerService normalizerService,
        IRedTeamCacheService redTeamCache,
        IPrecisionEditor precisionEditor,
        ILogger<ValidationService> logger)
    {
        _llmService = llmService;
        _normalizerService = normalizerService;
        _redTeamCache = redTeamCache;
        _precisionEditor = precisionEditor;
        _logger = logger;
    }

    /// <summary>
    /// Normalizes case content deterministically.
    /// </summary>
    public async Task<NormalizationResult> NormalizeCaseDeterministicAsync(NormalizationInput input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Normalizing case content deterministically for case {CaseId}", input.CaseId);
        return await _normalizerService.NormalizeCaseAsync(input, cancellationToken);
    }

    /// <summary>
    /// Validates case against quality rules.
    /// </summary>
    public async Task<string> ValidateRulesAsync(string normalizedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating case rules");

        var systemPrompt = """
            You are a specialist in validating detective game cases. 
            Verify compliance with gameplay, narrative consistency, and quality standards.
            
            Pay special attention to TEMPORAL CONSISTENCY:
            - All timestamps must use consistent timezone offset
            - Document creation dates must logically follow incident timeline  
            - Evidence collection times must be realistic and chronologically sound
            - Interview timestamps must be properly sequenced
            - No overlapping or conflicting timestamps
            - Chain of custody timestamps must be chronologically ordered
            """;

        var userPrompt = $"""
            Validate this normalized case against quality rules:
            
            {normalizedJson}
            
            Check: 
            1. TEMPORAL CONSISTENCY: Verify timestamps, timezone consistency, chronological logic
            2. Narrative consistency and logical flow
            3. Gameplay balance and challenge level
            4. Completeness of clues and evidence
            5. Realism and authenticity
            6. Overall case quality and solvability
            
            Flag timestamp inconsistencies, timezone mismatches, or chronological errors as critical issues.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    /// <summary>
    /// Global RedTeam analysis for macro-level issues.
    /// </summary>
    public async Task<string> RedTeamGlobalAnalysisAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("REDTEAM GLOBAL: Starting macro analysis for case {CaseId}", caseId);
        
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            _logger.LogError("REDTEAM GLOBAL: Empty validatedJson for case {CaseId}", caseId);
            return CreateFallbackGlobalAnalysis("Global analysis failed - empty input.");
        }

        var contentHash = _redTeamCache.ComputeContentHash(validatedJson);
        var cachedAnalysis = await _redTeamCache.GetCachedAnalysisAsync(contentHash, "Global", null, cancellationToken);
        
        if (cachedAnalysis != null)
        {
            _logger.LogInformation("REDTEAM GLOBAL: Using cached analysis (hash: {Hash})", contentHash[..8]);
            return cachedAnalysis;
        }

        var systemPrompt = """
            You are a senior forensic case analyst conducting high-level strategic assessment.
            Identify MACRO-LEVEL issues affecting case integrity and coherence:
            1. Cross-document inconsistencies
            2. Chronological problems (timeline gaps, impossible sequences)
            3. Narrative coherence issues
            4. Reference integrity (missing/broken cross-references)
            5. Structural completeness

            Do NOT focus on minor formatting or individual document issues.
            Provide strategic assessment for areas needing detailed analysis.
            """;

        var jsonStructure = """
            {
                "MacroIssues": [
                    {
                        "Type": "CrossDocumentInconsistency|ChronologicalGap|NarrativeContradiction|ReferenceIntegrity|StructuralCompleteness",
                        "Severity": "Critical|Major|Minor",
                        "AffectedDocuments": ["doc_id_1", "doc_id_2"],
                        "Description": "Clear description",
                        "RequiredFocusAreas": ["specific_section_1"]
                    }
                ],
                "CriticalDocuments": ["doc_ids_needing_analysis"],
                "FocusAreas": ["areas_to_examine"],
                "OverallAssessment": "Strategic assessment",
                "RequiresDetailedAnalysis": true
            }
            """;

        var userPrompt = $"""
            Analyze this forensic case for macro-level issues. Return JSON:

            {validatedJson}

            Required JSON structure:
            {jsonStructure}
            """;

        try
        {
            var result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("REDTEAM GLOBAL: Empty response for case {CaseId}", caseId);
                return CreateFallbackGlobalAnalysis("Empty response from LLM.");
            }

            var cleanedResult = CleanRedTeamJsonResponse(result);
            
            try
            {
                var analysis = JsonSerializer.Deserialize<GlobalRedTeamAnalysis>(cleanedResult);
                if (analysis != null)
                {
                    _logger.LogInformation("REDTEAM GLOBAL: Completed - {Count} macro issues", 
                        analysis.MacroIssues.Count);
                    
                    await _redTeamCache.CacheAnalysisAsync(contentHash, cleanedResult, "Global", null, cancellationToken);
                    return cleanedResult;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "REDTEAM GLOBAL: Invalid JSON for case {CaseId}", caseId);
            }

            await _redTeamCache.CacheAnalysisAsync(contentHash, cleanedResult, "Global", null, cancellationToken);
            return cleanedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REDTEAM GLOBAL: Exception for case {CaseId}", caseId);
            return CreateFallbackGlobalAnalysis($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies surgical fixes to case based on RedTeam analysis.
    /// </summary>
    public async Task<string> FixCaseAsync(StructuredRedTeamAnalysis analysis, string currentJson, string caseId, int iterationNumber = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SURGICAL FIX: Starting precision corrections - iteration {Iteration}", iterationNumber);
        _logger.LogInformation("SURGICAL FIX: Current JSON length: {Length} chars", currentJson?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(currentJson))
            throw new ArgumentException("Current case JSON cannot be null or empty", nameof(currentJson));

        try
        {
            using var doc = JsonDocument.Parse(currentJson);
            _logger.LogInformation("SURGICAL FIX: JSON validation successful for case {CaseId}", caseId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SURGICAL FIX: Invalid JSON for case {CaseId}", caseId);
            throw new ArgumentException($"Invalid JSON structure: {ex.Message}", nameof(currentJson));
        }

        // Use PrecisionEditor for surgical fixes
        var fixedJson = await _precisionEditor.ApplyPreciseFixesAsync(currentJson, analysis, caseId, cancellationToken);
        
        _logger.LogInformation("SURGICAL FIX: Completed for case {CaseId}", caseId);
        return fixedJson;
    }

    // Helper methods
    private string CreateFallbackGlobalAnalysis(string errorMessage)
    {
        _logger.LogWarning("REDTEAM GLOBAL: Creating fallback: {Error}", errorMessage);
        
        var fallback = new GlobalRedTeamAnalysis
        {
            MacroIssues = new List<MacroIssue>(),
            CriticalDocuments = Array.Empty<string>(),
            FocusAreas = Array.Empty<string>(),
            OverallAssessment = $"FALLBACK: {errorMessage}",
            RequiresDetailedAnalysis = false
        };
        
        return JsonSerializer.Serialize(fallback, new JsonSerializerOptions { WriteIndented = true });
    }

    private string CleanRedTeamJsonResponse(string jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
            return "{}";

        // Remove markdown code blocks if present
        var cleaned = jsonResponse.Trim();
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned.Substring(7);
        else if (cleaned.StartsWith("```"))
            cleaned = cleaned.Substring(3);
        
        if (cleaned.EndsWith("```"))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);

        return cleaned.Trim();
    }
}
