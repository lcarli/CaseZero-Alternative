using System.Text.Json;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.Activities.QA;

/// <summary>
/// Phase 7: QA Deep Dive Activity - Focused analysis of specific problematic area
/// Loads ONLY the specific entity + direct dependencies for surgical analysis
/// </summary>
public class QA_DeepDiveActivity
{
    private readonly ILogger<QA_DeepDiveActivity> _logger;
    private readonly IContextManager _contextManager;
    private readonly ILLMService _llmService;

    public QA_DeepDiveActivity(
        ILogger<QA_DeepDiveActivity> logger,
        IContextManager contextManager,
        ILLMService llmService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _llmService = llmService;
    }

    [Function("QA_DeepDiveActivity")]
    public async Task<string> Run([ActivityTrigger] QA_DeepDiveActivityModel input)
    {
        var caseId = input.CaseId;
        var issueArea = input.IssueArea;
        
        _logger.LogInformation("QA Deep Dive: Analyzing issue area '{IssueArea}' for case {CaseId}", issueArea, caseId);

        // Parse issue area to determine entity type and ID
        var (entityType, entityId, issueType) = ParseIssueArea(issueArea);
        _logger.LogInformation("QA Deep Dive: Parsed as type={Type}, id={Id}, issue={Issue}", entityType, entityId, issueType);

        // Load focused context: only the specific entity and its dependencies
        var focusedContext = await LoadFocusedContextAsync(caseId, entityType, entityId);
        _logger.LogInformation("QA Deep Dive: Loaded {Count} context items ({TotalChars} total chars)", 
            focusedContext.Count, focusedContext.Values.Sum(v => v?.Length ?? 0));

        // Build prompt for deep analysis
        var prompt = BuildDeepDivePrompt(issueArea, entityType, entityId, issueType, focusedContext);

        // Call LLM for detailed analysis
        _logger.LogInformation("QA Deep Dive: Calling LLM for detailed analysis");
        var systemPrompt = "You are an expert detective case analyst. Perform deep analysis of specific case issues and provide actionable recommendations.";
        var llmResponse = await _llmService.GenerateAsync(caseId, systemPrompt, prompt);

        // Parse and structure the response
        var analysis = ParseAnalysisResponse(llmResponse, issueArea);
        
        var resultJson = JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("QA Deep Dive: Analysis complete for {IssueArea}", issueArea);
        
        return resultJson;
    }

    private (string entityType, string entityId, string issueType) ParseIssueArea(string issueArea)
    {
        // Expected formats:
        // "suspect_S001_alibi" -> type: suspect, id: S001, issue: alibi
        // "evidence_E003_chain" -> type: evidence, id: E003, issue: chain
        // "timeline_gaps" -> type: timeline, id: "", issue: gaps
        // "document_DOC001_references" -> type: document, id: DOC001, issue: references

        var parts = issueArea.Split('_');
        
        if (parts.Length >= 2)
        {
            var entityType = parts[0]; // suspect, evidence, witness, document, timeline
            var entityId = parts.Length >= 3 ? parts[1] : "";
            var issueType = parts.Length >= 3 ? parts[2] : parts[1];
            
            return (entityType, entityId, issueType);
        }

        return ("unknown", "", issueArea);
    }

    private async Task<Dictionary<string, string?>> LoadFocusedContextAsync(string caseId, string entityType, string entityId)
    {
        var context = new Dictionary<string, string?>();

        try
        {
            // Load the specific entity
            if (!string.IsNullOrEmpty(entityId))
            {
                var entityPath = entityType switch
                {
                    "suspect" => $"entities/suspects/{entityId}.json",
                    "evidence" => $"entities/evidence/{entityId}.json",
                    "witness" => $"entities/witnesses/{entityId}.json",
                    "document" => $"documents/{entityId}.json",
                    _ => null
                };

                if (entityPath != null)
                {
                    try
                    {
                        var entityData = await _contextManager.LoadContextAsync<string>(caseId, entityPath);
                        context[$"target_entity"] = entityData;
                        _logger.LogInformation("QA Deep Dive: Loaded target entity from {Path} ({Length} chars)", 
                            entityPath, entityData?.Length ?? 0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "QA Deep Dive: Could not load entity from {Path}", entityPath);
                    }
                }
            }

            // Load timeline (always useful for context)
            try
            {
                var timeline = await _contextManager.LoadContextAsync<string>(caseId, "expand/timeline.json");
                context["timeline"] = timeline;
                _logger.LogInformation("QA Deep Dive: Loaded timeline ({Length} chars)", timeline?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QA Deep Dive: Could not load timeline");
            }

            // Load related documents if analyzing an entity
            if (!string.IsNullOrEmpty(entityId) && entityType != "document")
            {
                try
                {
                    var documentsQuery = await _contextManager.QueryContextAsync<object>(caseId, "documents/*");
                    var relatedDocs = new List<string>();

                    foreach (var docResult in documentsQuery)
                    {
                        var docJson = JsonSerializer.Serialize(docResult.Data);
                        
                        // Check if document mentions the entity ID
                        if (docJson.Contains(entityId, StringComparison.OrdinalIgnoreCase))
                        {
                            relatedDocs.Add(docJson);
                            
                            if (relatedDocs.Count >= 5) // Limit to 5 most relevant documents
                                break;
                        }
                    }

                    if (relatedDocs.Any())
                    {
                        context["related_documents"] = JsonSerializer.Serialize(relatedDocs);
                        _logger.LogInformation("QA Deep Dive: Loaded {Count} related documents", relatedDocs.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "QA Deep Dive: Could not load related documents");
                }
            }

            // Load plan core for case difficulty and requirements
            try
            {
                var planCore = await _contextManager.LoadContextAsync<string>(caseId, "plan/core.json");
                context["plan_core"] = planCore;
                _logger.LogInformation("QA Deep Dive: Loaded plan core ({Length} chars)", planCore?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QA Deep Dive: Could not load plan core");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QA Deep Dive: Error loading focused context");
        }

        return context;
    }

    private string BuildDeepDivePrompt(string issueArea, string entityType, string entityId, string issueType, Dictionary<string, string?> context)
    {
        var prompt = $@"You are analyzing a specific issue in a detective case generation.

# ISSUE TO ANALYZE
**Area**: {issueArea}
**Type**: {entityType}
**Entity ID**: {entityId}
**Issue Type**: {issueType}

# FOCUSED CONTEXT
";

        foreach (var (key, value) in context)
        {
            if (!string.IsNullOrEmpty(value))
            {
                prompt += $@"
## {key.ToUpper().Replace('_', ' ')}
```json
{value}
```
";
            }
        }

        prompt += $@"

# ANALYSIS TASK
Perform a DETAILED analysis of this specific issue:

1. **Problem Identification**
   - What exactly is the problem?
   - Why is it problematic for case quality?
   - What are the specific inconsistencies or gaps?

2. **Root Cause**
   - What likely caused this issue?
   - Are there related issues in the data?

3. **Impact Assessment**
   - How severe is this issue?
   - What aspects of the case does it affect?
   - Which other entities or documents are impacted?

4. **Suggested Fix**
   - What specific changes are needed?
   - Provide concrete examples of what should be changed
   - Are there multiple entities that need updates?

5. **Verification**
   - How can we verify the fix worked?
   - What should we check after applying the fix?

# OUTPUT FORMAT
Return ONLY valid JSON with this structure:
```json
{{
  ""issueArea"": ""{issueArea}"",
  ""problemDetails"": ""Detailed explanation of the problem (2-3 sentences)"",
  ""rootCause"": ""Why this happened (1-2 sentences)"",
  ""impact"": ""What this affects (1-2 sentences)"",
  ""severity"": ""high"" | ""medium"" | ""low"",
  ""suggestedFix"": ""Concrete steps to fix (be specific with examples)"",
  ""affectedEntities"": [
    ""entity_id_1"",
    ""entity_id_2""
  ],
  ""verificationSteps"": [
    ""step 1"",
    ""step 2""
  ]
}}
```

Be SPECIFIC and ACTIONABLE. Provide concrete examples in suggestedFix.";

        return prompt;
    }

    private object ParseAnalysisResponse(string llmResponse, string issueArea)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonContent);
                
                // Return as anonymous object for serialization
                return new
                {
                    issueArea = doc.RootElement.TryGetProperty("issueArea", out var area) ? area.GetString() : issueArea,
                    problemDetails = doc.RootElement.TryGetProperty("problemDetails", out var details) ? details.GetString() : "",
                    rootCause = doc.RootElement.TryGetProperty("rootCause", out var cause) ? cause.GetString() : "",
                    impact = doc.RootElement.TryGetProperty("impact", out var imp) ? imp.GetString() : "",
                    severity = doc.RootElement.TryGetProperty("severity", out var sev) ? sev.GetString() : "medium",
                    suggestedFix = doc.RootElement.TryGetProperty("suggestedFix", out var fix) ? fix.GetString() : "",
                    affectedEntities = doc.RootElement.TryGetProperty("affectedEntities", out var entities) 
                        ? entities.EnumerateArray().Select(e => e.GetString()).Where(s => s != null).ToList()
                        : new List<string?>(),
                    verificationSteps = doc.RootElement.TryGetProperty("verificationSteps", out var steps)
                        ? steps.EnumerateArray().Select(s => s.GetString()).Where(s => s != null).ToList()
                        : new List<string?>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QA Deep Dive: Error parsing analysis response");
        }

        // Fallback if parsing fails
        return new
        {
            issueArea,
            problemDetails = "Analysis parsing failed",
            rootCause = "Unknown",
            impact = "Unknown",
            severity = "medium",
            suggestedFix = "Manual review required",
            affectedEntities = new List<string>(),
            verificationSteps = new List<string>()
        };
    }
}
