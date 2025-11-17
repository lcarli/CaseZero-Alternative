using System.Text.Json;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.Activities.QA;

/// <summary>
/// Phase 7: Check Case Clean Activity V2 - Verifies if all identified issues were resolved
/// Lightweight verification using only manifest to check if case is ready
/// </summary>
public class CheckCaseCleanActivityV2
{
    private readonly ILogger<CheckCaseCleanActivityV2> _logger;
    private readonly IContextManager _contextManager;
    private readonly ILLMService _llmService;

    public CheckCaseCleanActivityV2(
        ILogger<CheckCaseCleanActivityV2> logger,
        IContextManager contextManager,
        ILLMService llmService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _llmService = llmService;
    }

    [Function("CheckCaseCleanActivityV2")]
    public async Task<string> Run([ActivityTrigger] CheckCaseCleanActivityV2Model input)
    {
        var caseId = input.CaseId;
        var issueAreas = input.IssueAreas;

        _logger.LogInformation("Check Clean V2: Verifying if {Count} issues were resolved for case {CaseId}", 
            issueAreas.Length, caseId);

        // Load lightweight context: only manifest
        var manifest = await _contextManager.LoadContextAsync<string>(caseId, "manifest.json");
        _logger.LogInformation("Check Clean V2: Loaded manifest ({Length} chars)", manifest?.Length ?? 0);

        if (string.IsNullOrEmpty(manifest))
        {
            _logger.LogWarning("Check Clean V2: Manifest not found or empty for case {CaseId}", caseId);
            return JsonSerializer.Serialize(new
            {
                isClean = false,
                message = "Manifest not found",
                remainingIssues = issueAreas.ToList()
            });
        }

        // Build verification prompt
        var prompt = BuildVerificationPrompt(issueAreas, manifest);

        // Call LLM for verification
        _logger.LogInformation("Check Clean V2: Calling LLM to verify issue resolution");
        var systemPrompt = "You are a quality assurance verifier. Check if previously identified issues have been resolved based on the case manifest.";
        var llmResponse = await _llmService.GenerateAsync(caseId, systemPrompt, prompt);

        // Parse verification result
        var (isClean, remainingIssues) = ParseVerificationResult(llmResponse, issueAreas);
        
        _logger.LogInformation("Check Clean V2: Case is {Status}. Remaining issues: {Count}", 
            isClean ? "CLEAN" : "NOT CLEAN", remainingIssues.Count);

        var result = new
        {
            isClean,
            totalIssuesChecked = issueAreas.Length,
            resolvedCount = issueAreas.Length - remainingIssues.Count,
            remainingIssues = remainingIssues,
            message = isClean 
                ? "All issues resolved. Case is ready." 
                : $"{remainingIssues.Count} issue(s) still need attention."
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private string BuildVerificationPrompt(string[] issueAreas, string manifest)
    {
        var issueList = string.Join("\n", issueAreas.Select((area, i) => $"{i + 1}. {area}"));

        var prompt = $@"You are verifying if previously identified issues in a detective case have been resolved.

# ISSUES TO VERIFY
The following issues were identified and should have been fixed:
{issueList}

# CURRENT CASE MANIFEST
```json
{manifest}
```

# VERIFICATION TASK
For EACH issue in the list above, determine if it has been resolved based on the manifest data.

The manifest shows:
- All entities (suspects, evidence, witnesses) with their IDs
- All documents with their IDs
- Context references (plan, expand data)

Check if the entities/areas mentioned in the issues exist and appear to be properly structured.

# VERIFICATION CRITERIA
An issue is considered RESOLVED if:
- The entity/area mentioned exists in the manifest
- The manifest shows reasonable structure (has expected sections)
- No obvious structural problems are visible

An issue is NOT RESOLVED if:
- The entity/area is missing from the manifest
- The manifest shows structural problems in that area
- You cannot verify the fix from the manifest alone

# OUTPUT FORMAT
Return ONLY valid JSON with this structure:
```json
{{
  ""isClean"": true | false,
  ""remainingIssues"": [
    ""issue_area_1"",
    ""issue_area_2""
  ],
  ""verificationNotes"": ""Brief explanation of remaining issues (if any)""
}}
```

If ALL issues are resolved, set isClean=true and remainingIssues=[].
If ANY issues remain, set isClean=false and list them in remainingIssues.

IMPORTANT: Be CONSERVATIVE. If you cannot verify a fix from the manifest, mark it as remaining.";

        return prompt;
    }

    private (bool isClean, List<string> remainingIssues) ParseVerificationResult(string llmResponse, string[] originalIssues)
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
                var root = doc.RootElement;

                var isClean = root.TryGetProperty("isClean", out var cleanEl) && cleanEl.GetBoolean();
                
                var remainingIssues = new List<string>();
                if (root.TryGetProperty("remainingIssues", out var issuesEl) && issuesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var issue in issuesEl.EnumerateArray())
                    {
                        var issueStr = issue.GetString();
                        if (!string.IsNullOrEmpty(issueStr))
                        {
                            remainingIssues.Add(issueStr);
                        }
                    }
                }

                _logger.LogInformation("Check Clean V2: Parsed result - isClean={IsClean}, remaining={Count}", 
                    isClean, remainingIssues.Count);

                return (isClean, remainingIssues);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check Clean V2: Error parsing verification result");
        }

        // Fallback: assume not clean if parsing fails
        _logger.LogWarning("Check Clean V2: Could not parse verification result, assuming NOT CLEAN");
        return (false, originalIssues.ToList());
    }
}
