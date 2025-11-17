using System.Text.Json;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.Activities.QA;

/// <summary>
/// Phase 7: QA Scan Activity - Lightweight scan of manifest to identify problematic areas
/// Loads only manifest.json + metadata.json (if exists) for fast initial assessment
/// </summary>
public class QA_ScanIssuesActivity
{
    private readonly ILogger<QA_ScanIssuesActivity> _logger;
    private readonly IContextManager _contextManager;
    private readonly ILLMService _llmService;

    public QA_ScanIssuesActivity(
        ILogger<QA_ScanIssuesActivity> logger,
        IContextManager contextManager,
        ILLMService llmService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _llmService = llmService;
    }

    [Function("QA_ScanIssuesActivity")]
    public async Task<string> Run([ActivityTrigger] QA_ScanIssuesActivityModel input)
    {
        var caseId = input.CaseId;
        _logger.LogInformation("QA Scan: Starting lightweight scan for case {CaseId}", caseId);

        // Load lightweight context: only manifest and metadata
        var manifest = await _contextManager.LoadContextAsync<string>(caseId, "manifest.json");
        _logger.LogInformation("QA Scan: Loaded manifest ({Length} chars)", manifest?.Length ?? 0);

        // Try to load metadata if exists (optional)
        string? metadata = null;
        try
        {
            metadata = await _contextManager.LoadContextAsync<string>(caseId, "metadata.json");
            _logger.LogInformation("QA Scan: Loaded metadata ({Length} chars)", metadata?.Length ?? 0);
        }
        catch
        {
            _logger.LogInformation("QA Scan: No metadata.json found, proceeding with manifest only");
        }

        // Build prompt for LLM to perform quick scan
        var prompt = BuildScanPrompt(manifest, metadata);

        // Call LLM for rapid assessment
        _logger.LogInformation("QA Scan: Calling LLM for rapid assessment");
        var systemPrompt = "You are a quality assurance expert for detective case generation. Analyze case manifests to identify structural issues.";
        var llmResponse = await _llmService.GenerateAsync(caseId, systemPrompt, prompt);

        // Parse LLM response to extract issues
        var issues = ParseIssuesFromResponse(llmResponse);
        _logger.LogInformation("QA Scan: Identified {Count} issues", issues.Count);

        // Return as JSON
        var result = new
        {
            issues = issues.Select(i => new
            {
                area = i.Area,
                severity = i.Severity,
                description = i.Description
            }).ToList()
        };

        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        return resultJson;
    }

    private string BuildScanPrompt(string? manifest, string? metadata)
    {
        var prompt = @"You are a quality assurance expert for detective case generation.

TASK: Perform a RAPID SCAN of the case manifest to identify potential issues.
Focus on high-level structural problems, NOT minor details.

# MANIFEST DATA
```json
" + manifest + @"
```
";

        if (!string.IsNullOrEmpty(metadata))
        {
            prompt += @"

# CASE METADATA
```json
" + metadata + @"
```
";
        }

        prompt += @"

# AREAS TO SCAN
Look for these types of issues (provide SPECIFIC examples when found):

1. **Suspect Issues** (area: ""suspect_{id}_{issue_type}"")
   - Weak or missing alibis
   - Inconsistent motives
   - Missing background information
   - Unrealistic character behavior

2. **Evidence Issues** (area: ""evidence_{id}_{issue_type}"")
   - Broken chain of custody references
   - Missing forensic context
   - Inconsistent timestamps or locations
   - Unrealistic evidence types

3. **Timeline Issues** (area: ""timeline_{issue_type}"")
   - Chronological inconsistencies
   - Gaps in critical periods
   - Conflicting event sequences
   - Missing key events

4. **Document Issues** (area: ""document_{id}_{issue_type}"")
   - Missing critical document types
   - Insufficient document coverage
   - Inconsistent document references

5. **Witness Issues** (area: ""witness_{id}_{issue_type}"")
   - Missing witness statements
   - Inconsistent testimonies
   - Unrealistic witness accounts

# OUTPUT FORMAT
Return ONLY a valid JSON array of issues. Each issue must have:
- ""area"": specific identifier (e.g., ""suspect_S001_alibi"", ""evidence_E003_chain"", ""timeline_gaps"")
- ""severity"": ""high"", ""medium"", or ""low""
- ""description"": brief explanation (1-2 sentences max)

Example:
```json
[
  {
    ""area"": ""suspect_S001_alibi"",
    ""severity"": ""high"",
    ""description"": ""Suspect S001's alibi has a 3-hour gap during the time of crime with no witnesses or documentation.""
  },
  {
    ""area"": ""evidence_E003_chain"",
    ""severity"": ""medium"",
    ""description"": ""Evidence E003 chain of custody missing intermediate handler between discovery and lab analysis.""
  }
]
```

If no issues found, return: []

IMPORTANT: 
- Be SPECIFIC with area identifiers (include entity IDs)
- Focus on STRUCTURAL issues, not minor details
- Provide ACTIONABLE descriptions
- Return ONLY valid JSON, no explanations";

        return prompt;
    }

    private List<QaScanIssue> ParseIssuesFromResponse(string llmResponse)
    {
        var issues = new List<QaScanIssue>();

        try
        {
            // Try to extract JSON from response (might have markdown code blocks)
            var jsonStart = llmResponse.IndexOf('[');
            var jsonEnd = llmResponse.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                foreach (var item in root.EnumerateArray())
                {
                    var area = item.TryGetProperty("area", out var areaEl) ? areaEl.GetString() : "unknown";
                    var severity = item.TryGetProperty("severity", out var sevEl) ? sevEl.GetString() : "medium";
                    var description = item.TryGetProperty("description", out var descEl) ? descEl.GetString() : "";

                    if (!string.IsNullOrEmpty(area) && !string.IsNullOrEmpty(description))
                    {
                        issues.Add(new QaScanIssue
                        {
                            Area = area,
                            Severity = severity ?? "medium",
                            Description = description
                        });
                    }
                }
            }
            else
            {
                _logger.LogWarning("QA Scan: Could not find JSON array in LLM response");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QA Scan: Error parsing issues from LLM response");
        }

        return issues;
    }
}
