using System.Text.Json;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.Activities.QA;

/// <summary>
/// Phase 7: Fix Entity Activity - Applies surgical fix to specific entity
/// Loads only the target entity + direct dependencies, applies correction, saves back
/// </summary>
public class FixEntityActivity
{
    private readonly ILogger<FixEntityActivity> _logger;
    private readonly IContextManager _contextManager;
    private readonly ILLMService _llmService;

    public FixEntityActivity(
        ILogger<FixEntityActivity> logger,
        IContextManager contextManager,
        ILLMService llmService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _llmService = llmService;
    }

    [Function("FixEntityActivity")]
    public async Task<string> Run([ActivityTrigger] FixEntityActivityModel input)
    {
        var caseId = input.CaseId;
        var entityId = input.EntityId;
        var issueDescription = input.IssueDescription;

        _logger.LogInformation("Fix Entity: Applying surgical fix to entity {EntityId} in case {CaseId}", entityId, caseId);

        // Determine entity type from ID pattern
        var entityType = DetermineEntityType(entityId);
        _logger.LogInformation("Fix Entity: Detected type={Type} for entity {EntityId}", entityType, entityId);

        // Load focused context: target entity + dependencies
        var context = await LoadFocusedContextAsync(caseId, entityType, entityId);
        _logger.LogInformation("Fix Entity: Loaded {Count} context items", context.Count);

        // Build prompt for surgical fix
        var prompt = BuildFixPrompt(entityId, entityType, issueDescription, context);

        // Call LLM to apply fix
        _logger.LogInformation("Fix Entity: Calling LLM to apply surgical fix");
        var systemPrompt = "You are an expert case editor. Apply precise, surgical fixes to case entities while maintaining consistency with the rest of the case.";
        var llmResponse = await _llmService.GenerateAsync(caseId, systemPrompt, prompt);

        // Extract fixed entity JSON
        var fixedEntity = ExtractFixedEntity(llmResponse);
        
        if (string.IsNullOrEmpty(fixedEntity))
        {
            _logger.LogError("Fix Entity: Failed to extract fixed entity from LLM response");
            return JsonSerializer.Serialize(new
            {
                success = false,
                entityId,
                message = "Failed to extract fixed entity from LLM response"
            });
        }

        // Determine save path
        var savePath = GetEntityPath(entityType, entityId);
        
        if (string.IsNullOrEmpty(savePath))
        {
            _logger.LogError("Fix Entity: Could not determine save path for type={Type}, id={EntityId}", entityType, entityId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                entityId,
                message = $"Unknown entity type: {entityType}"
            });
        }

        // Save fixed entity back to storage
        await _contextManager.SaveContextAsync(caseId, savePath, fixedEntity);
        _logger.LogInformation("Fix Entity: Saved fixed entity to {Path} ({Length} chars)", savePath, fixedEntity.Length);

        // Return success result
        var result = new
        {
            success = true,
            entityId,
            entityType,
            savePath,
            changesSummary = $"Applied surgical fix to {entityType} {entityId} based on issue: {issueDescription.Substring(0, Math.Min(100, issueDescription.Length))}..."
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private string DetermineEntityType(string entityId)
    {
        // Entity ID patterns:
        // S001, S002 -> suspect
        // E001, E002 -> evidence
        // W001, W002 -> witness
        // DOC001, etc -> document
        
        if (Regex.IsMatch(entityId, @"^S\d+$", RegexOptions.IgnoreCase))
            return "suspect";
        
        if (Regex.IsMatch(entityId, @"^E\d+$", RegexOptions.IgnoreCase))
            return "evidence";
        
        if (Regex.IsMatch(entityId, @"^W\d+$", RegexOptions.IgnoreCase))
            return "witness";
        
        if (entityId.StartsWith("DOC", StringComparison.OrdinalIgnoreCase))
            return "document";

        return "unknown";
    }

    private string? GetEntityPath(string entityType, string entityId)
    {
        return entityType switch
        {
            "suspect" => $"entities/suspects/{entityId}.json",
            "evidence" => $"entities/evidence/{entityId}.json",
            "witness" => $"entities/witnesses/{entityId}.json",
            "document" => $"documents/{entityId}.json",
            _ => null
        };
    }

    private async Task<Dictionary<string, string?>> LoadFocusedContextAsync(string caseId, string entityType, string entityId)
    {
        var context = new Dictionary<string, string?>();

        try
        {
            // Load the target entity
            var entityPath = GetEntityPath(entityType, entityId);
            if (entityPath != null)
            {
                try
                {
                    var entityData = await _contextManager.LoadContextAsync<string>(caseId, entityPath);
                    context["current_entity"] = entityData;
                    _logger.LogInformation("Fix Entity: Loaded current entity from {Path} ({Length} chars)", 
                        entityPath, entityData?.Length ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fix Entity: Could not load entity from {Path}", entityPath);
                }
            }

            // Load timeline for temporal context
            try
            {
                var timeline = await _contextManager.LoadContextAsync<string>(caseId, "expand/timeline.json");
                context["timeline"] = timeline;
                _logger.LogInformation("Fix Entity: Loaded timeline ({Length} chars)", timeline?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fix Entity: Could not load timeline");
            }

            // Load related entities (suspects for this case)
            if (entityType == "suspect" || entityType == "evidence" || entityType == "witness")
            {
                try
                {
                    var relatedPath = entityType == "suspect" ? "entities/suspects/*" : 
                                     entityType == "evidence" ? "entities/evidence/*" : 
                                     "entities/witnesses/*";
                    
                    var relatedQuery = await _contextManager.QueryContextAsync<object>(caseId, relatedPath);
                    var relatedEntities = relatedQuery
                        .Where(r => !r.Path.Contains($"{entityId}.json", StringComparison.OrdinalIgnoreCase))
                        .Take(3) // Limit to 3 related entities for context
                        .Select(r => JsonSerializer.Serialize(r.Data))
                        .ToList();

                    if (relatedEntities.Any())
                    {
                        context["related_entities"] = JsonSerializer.Serialize(relatedEntities);
                        _logger.LogInformation("Fix Entity: Loaded {Count} related entities", relatedEntities.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fix Entity: Could not load related entities");
                }
            }

            // Load plan core for case requirements
            try
            {
                var planCore = await _contextManager.LoadContextAsync<string>(caseId, "plan/core.json");
                context["plan_core"] = planCore;
                _logger.LogInformation("Fix Entity: Loaded plan core ({Length} chars)", planCore?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fix Entity: Could not load plan core");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fix Entity: Error loading focused context");
        }

        return context;
    }

    private string BuildFixPrompt(string entityId, string entityType, string issueDescription, Dictionary<string, string?> context)
    {
        // EPIC 4.2: Detect playability issues and provide specific guidance
        var isPlayabilityIssue = issueDescription.Contains("[PLAYABILITY]");
        var playabilityType = ExtractPlayabilityType(issueDescription);

        var prompt = $@"You are applying a SURGICAL FIX to a specific entity in a detective case.

# TARGET ENTITY
**Entity ID**: {entityId}
**Entity Type**: {entityType}
**Issue to Fix**: {issueDescription}

{(isPlayabilityIssue ? GetPlayabilityFixGuidance(playabilityType) : "")}

# CURRENT ENTITY DATA
```json
{context.GetValueOrDefault("current_entity", "{}")}
```
";

        if (context.ContainsKey("timeline") && !string.IsNullOrEmpty(context["timeline"]))
        {
            prompt += $@"

# TIMELINE CONTEXT (for consistency)
```json
{context["timeline"]}
```
";
        }

        if (context.ContainsKey("related_entities") && !string.IsNullOrEmpty(context["related_entities"]))
        {
            prompt += $@"

# RELATED ENTITIES (for consistency)
```json
{context["related_entities"]}
```
";
        }

        if (context.ContainsKey("plan_core") && !string.IsNullOrEmpty(context["plan_core"]))
        {
            prompt += $@"

# CASE REQUIREMENTS
```json
{context["plan_core"]}
```
";
        }

        prompt += $@"

# FIX INSTRUCTIONS

1. **Apply the specific fix** described in the issue
2. **Maintain consistency** with timeline and related entities
3. **Preserve all other fields** that are not related to the issue
4. **Keep realistic details** - don't make things too perfect
5. **Maintain the same JSON structure** as the current entity

# OUTPUT FORMAT
Return ONLY the COMPLETE fixed entity as valid JSON. Do not add explanations or markdown.
Include ALL fields from the original entity, with only the necessary corrections applied.

Example structure (preserve all original fields):
```json
{{
  ""id"": ""{entityId}"",
  // ... all other fields with corrections applied ...
}}
```

CRITICAL: Return ONLY valid JSON, nothing else. The output will be saved directly to storage.";

        return prompt;
    }

    private string ExtractFixedEntity(string llmResponse)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                // Validate it's valid JSON
                using var doc = JsonDocument.Parse(jsonContent);
                
                // Pretty print for storage
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fix Entity: Error extracting fixed entity JSON");
        }

        return string.Empty;
    }

    // EPIC 4.2: Helper methods for playability issue fixes

    private string ExtractPlayabilityType(string issueDescription)
    {
        if (issueDescription.Contains("DEAD_END")) return "dead_end";
        if (issueDescription.Contains("MISSING_LINK")) return "missing_link";
        if (issueDescription.Contains("EXCESSIVE_AMBIGUITY")) return "excessive_ambiguity";
        if (issueDescription.Contains("UNSUPPORTED_CONCLUSION")) return "unsupported_conclusion";
        if (issueDescription.Contains("CIRCULAR_REASONING")) return "circular_reasoning";
        return "general";
    }

    private string GetPlayabilityFixGuidance(string playabilityType)
    {
        return playabilityType switch
        {
            "dead_end" => @"
# PLAYABILITY FIX: DEAD END
This investigation path leads nowhere. Your fix should:
- Add connections to other evidence or documents
- Provide clues that point to next investigative steps
- Include references to other entity IDs that players can follow
- Ensure at least one logical path forward exists
",
            "missing_link" => @"
# PLAYABILITY FIX: MISSING LINK
There's a logical gap in the investigation chain. Your fix should:
- Create explicit connections between related evidence/suspects
- Add supporting details that bridge the logical gap
- Reference other documents/evidence that complete the chain
- Make relationships clearer without being too obvious
",
            "excessive_ambiguity" => @"
# PLAYABILITY FIX: EXCESSIVE AMBIGUITY
The information is too ambiguous to reach conclusions. Your fix should:
- Add clarifying details that narrow interpretations
- Provide corroborating information from other sources
- Include timestamps, locations, or other concrete facts
- Reduce contradictions or explain discrepancies
- Keep some ambiguity if appropriate to difficulty level
",
            "unsupported_conclusion" => @"
# PLAYABILITY FIX: UNSUPPORTED CONCLUSION
A logical conclusion requires evidence that doesn't exist. Your fix should:
- Add supporting evidence that justifies the conclusion
- Include details that make the inference possible
- Reference other documents/evidence that support the reasoning
- Ensure the logic chain is complete and followable
",
            "circular_reasoning" => @"
# PLAYABILITY FIX: CIRCULAR REASONING
The reasoning path loops without progress. Your fix should:
- Break the circular dependency
- Add external evidence that provides resolution
- Create a clear entry/exit point in the reasoning chain
- Ensure players can make progress toward the solution
",
            _ => @"
# PLAYABILITY FIX: GENERAL
This entity has playability issues. Your fix should:
- Improve clarity and investigative value
- Add connections to other case elements
- Ensure players can make logical progress
- Maintain consistency with the rest of the case
"
        };
    }
}
