using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CaseGen.Functions.Services;

public interface IPrecisionEditor
{
    Task<string> ApplyPreciseFixesAsync(string originalJson, StructuredRedTeamAnalysis analysis, string caseId, CancellationToken cancellationToken = default);
}

public class PrecisionEditor : IPrecisionEditor
{
    private readonly ILogger<PrecisionEditor> _logger;
    private readonly ILLMService _llmService;

    public PrecisionEditor(ILogger<PrecisionEditor> logger, ILLMService llmService)
    {
        _logger = logger;
        _llmService = llmService;
    }

    public async Task<string> ApplyPreciseFixesAsync(string originalJson, StructuredRedTeamAnalysis analysis, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PRECISION EDITOR: Starting surgical fixes for case {CaseId} - {IssueCount} issues", 
            caseId, analysis.Issues.Count);

        if (analysis.Issues.Count == 0)
        {
            _logger.LogInformation("PRECISION EDITOR: No issues to fix for case {CaseId}", caseId);
            return originalJson;
        }

        // Filter out skeleton-related issues that shouldn't be processed
        var validIssues = analysis.Issues
            .Where(issue => !string.IsNullOrEmpty(issue.Location.DocId) && 
                           !issue.Location.DocId.Equals("skeleton", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (validIssues.Count != analysis.Issues.Count)
        {
            _logger.LogInformation("PRECISION EDITOR: Filtered out {SkippedCount} skeleton-related issues", 
                analysis.Issues.Count - validIssues.Count);
        }

        if (validIssues.Count == 0)
        {
            _logger.LogInformation("PRECISION EDITOR: No valid document issues to fix for case {CaseId}", caseId);
            return originalJson;
        }

        try
        {
            // Parse the original JSON into a mutable structure
            var jsonDoc = JsonDocument.Parse(originalJson);
            var mutableJson = JsonSerializer.Deserialize<Dictionary<string, object>>(originalJson)!;

            // Apply fixes in priority order (High -> Medium -> Low)
            var orderedIssues = validIssues
                .OrderBy(i => i.Priority == "High" ? 1 : i.Priority == "Medium" ? 2 : 3)
                .ToList();

            int appliedFixes = 0;
            var failedFixes = new List<string>();

            foreach (var issue in orderedIssues)
            {
                bool fixApplied = false;

                try
                {
                    switch (issue.Fix.Action)
                    {
                        case "UpdateTimestamp":
                            fixApplied = UpdateTimestamp(mutableJson, issue);
                            break;
                        case "ReplaceText":
                            fixApplied = ReplaceText(mutableJson, issue);
                            break;
                        case "MoveToAddendum":
                            fixApplied = await MoveToAddendum(mutableJson, issue, caseId, cancellationToken);
                            break;
                        case "RemoveReference":
                            fixApplied = RemoveReference(mutableJson, issue);
                            break;
                        default:
                            _logger.LogWarning("PRECISION EDITOR: Unknown fix action '{Action}' for issue in {DocId}", 
                                issue.Fix.Action, issue.Location.DocId);
                            break;
                    }

                    if (fixApplied)
                    {
                        appliedFixes++;
                        _logger.LogInformation("PRECISION EDITOR: Applied {Action} fix for {DocId} - {Problem}", 
                            issue.Fix.Action, issue.Location.DocId, issue.Problem);
                    }
                    else
                    {
                        failedFixes.Add($"{issue.Location.DocId}: {issue.Fix.Action}");
                        _logger.LogWarning("PRECISION EDITOR: Failed to apply {Action} fix for {DocId} - Field: {Field}, Section: {Section}, Pattern: {Pattern}", 
                            issue.Fix.Action, issue.Location.DocId, issue.Location.Field, issue.Location.Section, issue.Location.LinePattern);
                    }
                }
                catch (Exception ex)
                {
                    failedFixes.Add($"{issue.Location.DocId}: {issue.Fix.Action} - {ex.Message}");
                    _logger.LogError(ex, "PRECISION EDITOR: Exception applying {Action} fix for {DocId}", 
                        issue.Fix.Action, issue.Location.DocId);
                }
            }

            // Serialize the corrected JSON
            var correctedJson = JsonSerializer.Serialize(mutableJson, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // Validate the result
            using var validationDoc = JsonDocument.Parse(correctedJson);
            
            _logger.LogInformation("PRECISION EDITOR: Completed for case {CaseId} - {Applied}/{Total} fixes applied, {Failed} failed", 
                caseId, appliedFixes, validIssues.Count, failedFixes.Count);

            if (failedFixes.Any())
            {
                _logger.LogWarning("PRECISION EDITOR: Failed fixes: {FailedFixes}", string.Join(", ", failedFixes));
            }

            jsonDoc.Dispose();
            return correctedJson;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "PRECISION EDITOR: JSON parsing/serialization error for case {CaseId}", caseId);
            return originalJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PRECISION EDITOR: Unexpected error for case {CaseId}", caseId);
            return originalJson;
        }
    }

    private bool UpdateTimestamp(Dictionary<string, object> json, PreciseIssue issue)
    {
        try
        {
            var targetDoc = FindDocumentById(json, issue.Location.DocId);
            if (targetDoc == null) return false;

            if (!string.IsNullOrEmpty(issue.Location.Field))
            {
                // Update specific field
                return UpdateFieldValue(targetDoc, issue.Location.Field, issue.Fix.NewValue);
            }
            else if (!string.IsNullOrEmpty(issue.Location.LinePattern))
            {
                // Update timestamp in content using pattern matching
                return UpdateTimestampInContent(targetDoc, issue.Location.LinePattern, issue.Fix);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating timestamp for {DocId}", issue.Location.DocId);
            return false;
        }
    }

    private bool ReplaceText(Dictionary<string, object> json, PreciseIssue issue)
    {
        try
        {
            var targetDoc = FindDocumentById(json, issue.Location.DocId);
            if (targetDoc == null) return false;

            var oldText = issue.Fix.OldText ?? issue.Location.CurrentValue ?? issue.Location.LinePattern;
            var newText = issue.Fix.NewText ?? issue.Fix.NewValue;

            if (string.IsNullOrEmpty(oldText) || string.IsNullOrEmpty(newText))
                return false;

            // If field is specified, replace text in that specific field
            if (!string.IsNullOrEmpty(issue.Location.Field))
            {
                return ReplaceTextInField(targetDoc, issue.Location.Field, oldText, newText);
            }
            
            // Otherwise, replace in content field (legacy behavior)
            if (targetDoc.TryGetValue("content", out var contentObj) && contentObj is JsonElement contentElement)
            {
                var content = contentElement.GetString() ?? "";
                var updatedContent = content.Replace(oldText, newText);
                if (updatedContent != content)
                {
                    targetDoc["content"] = updatedContent;
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing text for {DocId}", issue.Location.DocId);
            return false;
        }
    }

    private Task<bool> MoveToAddendum(Dictionary<string, object> json, PreciseIssue issue, string caseId, CancellationToken cancellationToken)
    {
        try
        {
            var targetDoc = FindDocumentById(json, issue.Location.DocId);
            if (targetDoc == null) return Task.FromResult(false);

            // For complex moves like this, we might need LLM assistance
            // For now, implement a simple text removal and note addition
            if (targetDoc.TryGetValue("content", out var contentObj) && contentObj is JsonElement contentElement)
            {
                var content = contentElement.GetString() ?? "";
                var textToMove = issue.Location.LinePattern ?? issue.Location.CurrentValue;

                if (!string.IsNullOrEmpty(textToMove) && content.Contains(textToMove))
                {
                    // Remove the problematic reference from current location
                    var updatedContent = content.Replace(textToMove, "[Moved to addendum - see post-incident analysis]");
                    targetDoc["content"] = updatedContent;

                    // TODO: Add actual addendum creation logic
                    _logger.LogInformation("PRECISION EDITOR: Moved content to addendum placeholder for {DocId}", issue.Location.DocId);
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving content to addendum for {DocId}", issue.Location.DocId);
            return Task.FromResult(false);
        }
    }

    private bool RemoveReference(Dictionary<string, object> json, PreciseIssue issue)
    {
        try
        {
            var targetDoc = FindDocumentById(json, issue.Location.DocId);
            if (targetDoc == null) return false;

            if (targetDoc.TryGetValue("content", out var contentObj) && contentObj is JsonElement contentElement)
            {
                var content = contentElement.GetString() ?? "";
                var referenceToRemove = issue.Location.LinePattern ?? issue.Location.CurrentValue;

                if (!string.IsNullOrEmpty(referenceToRemove))
                {
                    // Simple removal - replace with empty string or appropriate placeholder
                    var updatedContent = content.Replace(referenceToRemove, "");
                    
                    // Clean up any double spaces or formatting issues
                    updatedContent = Regex.Replace(updatedContent, @"\s+", " ").Trim();
                    
                    targetDoc["content"] = updatedContent;
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing reference for {DocId}", issue.Location.DocId);
            return false;
        }
    }

    private Dictionary<string, object>? FindDocumentById(Dictionary<string, object> json, string docId)
    {
        if (!json.TryGetValue("documents", out var documentsObj) || documentsObj is not JsonElement documentsElement)
        {
            _logger.LogWarning("PRECISION EDITOR: No 'documents' array found in JSON structure");
            return null;
        }

        foreach (var docElement in documentsElement.EnumerateArray())
        {
            if (docElement.TryGetProperty("docId", out var docIdProp) && 
                docIdProp.GetString() == docId)
            {
                _logger.LogDebug("PRECISION EDITOR: Found document {DocId}", docId);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(docElement.GetRawText());
            }
        }

        _logger.LogWarning("PRECISION EDITOR: Document {DocId} not found in documents array", docId);
        return null;
    }

    private bool UpdateFieldValue(Dictionary<string, object> document, string fieldPath, string? newValue)
    {
        if (string.IsNullOrEmpty(newValue)) return false;
        return SetFieldValue(document, fieldPath, newValue);
    }

    private bool UpdateTimestampInContent(Dictionary<string, object> document, string pattern, IssueFix fix)
    {
        if (!document.TryGetValue("content", out var contentObj) || contentObj is not JsonElement contentElement)
            return false;

        var content = contentElement.GetString() ?? "";
        var oldText = fix.OldText ?? pattern;
        var newText = fix.NewText ?? fix.NewValue;

        if (!string.IsNullOrEmpty(oldText) && !string.IsNullOrEmpty(newText))
        {
            var updatedContent = content.Replace(oldText, newText);
            if (updatedContent != content)
            {
                document["content"] = updatedContent;
                return true;
            }
        }

        return false;
    }

    private bool ReplaceTextInField(Dictionary<string, object> document, string fieldPath, string oldText, string newText)
    {
        try
        {
            var fieldValue = GetFieldValue(document, fieldPath);
            if (fieldValue == null) return false;

            var currentText = fieldValue.ToString() ?? "";
            var updatedText = currentText.Replace(oldText, newText);
            
            if (updatedText != currentText)
            {
                return SetFieldValue(document, fieldPath, updatedText);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing text in field {Field}", fieldPath);
            return false;
        }
    }

    private object? GetFieldValue(Dictionary<string, object> document, string fieldPath)
    {
        var pathParts = ParseFieldPath(fieldPath);
        var current = (object)document;

        foreach (var part in pathParts)
        {
            if (part.IsArrayIndex)
            {
                if (current is JsonElement arrayElement && arrayElement.ValueKind == JsonValueKind.Array)
                {
                    var array = arrayElement.EnumerateArray().ToArray();
                    if (part.ArrayIndex >= 0 && part.ArrayIndex < array.Length)
                    {
                        current = array[part.ArrayIndex];
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (current is List<object> list)
                {
                    if (part.ArrayIndex >= 0 && part.ArrayIndex < list.Count)
                    {
                        current = list[part.ArrayIndex];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (current is JsonElement element)
                {
                    if (element.TryGetProperty(part.Property, out var prop))
                    {
                        current = prop;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (current is Dictionary<string, object> dict)
                {
                    if (dict.TryGetValue(part.Property, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        return current;
    }

    private bool SetFieldValue(Dictionary<string, object> document, string fieldPath, object newValue)
    {
        var pathParts = ParseFieldPath(fieldPath);
        if (pathParts.Count == 0) return false;

        var current = (object)document;
        var parents = new List<(object obj, FieldPathPart part)>();

        // Navigate to the parent of the target field
        for (int i = 0; i < pathParts.Count - 1; i++)
        {
            var part = pathParts[i];
            parents.Add((current, part));

            if (part.IsArrayIndex)
            {
                if (current is JsonElement arrayElement && arrayElement.ValueKind == JsonValueKind.Array)
                {
                    var array = arrayElement.EnumerateArray().ToArray();
                    if (part.ArrayIndex >= 0 && part.ArrayIndex < array.Length)
                    {
                        current = array[part.ArrayIndex];
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (current is List<object> list)
                {
                    if (part.ArrayIndex >= 0 && part.ArrayIndex < list.Count)
                    {
                        current = list[part.ArrayIndex];
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (current is JsonElement element)
                {
                    if (element.TryGetProperty(part.Property, out var prop))
                    {
                        current = prop;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (current is Dictionary<string, object> dict)
                {
                    if (dict.TryGetValue(part.Property, out var value))
                    {
                        current = value;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        // Set the final field
        var finalPart = pathParts[^1];
        if (finalPart.IsArrayIndex)
        {
            // Set array element
            if (current is JsonElement arrayElement && arrayElement.ValueKind == JsonValueKind.Array)
            {
                var arrayList = arrayElement.EnumerateArray().Select(el => (object)el).ToList();
                if (finalPart.ArrayIndex >= 0 && finalPart.ArrayIndex < arrayList.Count)
                {
                    arrayList[finalPart.ArrayIndex] = newValue;
                    
                    // Update the parent container with the modified array
                    if (parents.Count > 0)
                    {
                        var (parentObj, parentPart) = parents[^1];
                        if (parentObj is Dictionary<string, object> parentDict && !parentPart.IsArrayIndex)
                        {
                            parentDict[parentPart.Property] = arrayList;
                            return true;
                        }
                    }
                }
                return false;
            }
            else if (current is List<object> list)
            {
                if (finalPart.ArrayIndex >= 0 && finalPart.ArrayIndex < list.Count)
                {
                    list[finalPart.ArrayIndex] = newValue;
                    return true;
                }
                return false;
            }
            else
            {
                _logger.LogWarning("Cannot set array element on non-array object for {Path}", fieldPath);
                return false;
            }
        }
        else
        {
            if (current is Dictionary<string, object> dict)
            {
                dict[finalPart.Property] = newValue;
                return true;
            }
            else if (current is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                // Convert JsonElement to Dictionary and update the parent
                var convertedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText())!;
                convertedDict[finalPart.Property] = newValue;
                
                // Update the parent container with the modified dictionary
                if (parents.Count > 0)
                {
                    var (parentObj, parentPart) = parents[^1];
                    if (parentObj is Dictionary<string, object> parentDict && !parentPart.IsArrayIndex)
                    {
                        parentDict[parentPart.Property] = convertedDict;
                        return true;
                    }
                }
                return false;
            }
            else
            {
                _logger.LogWarning("Cannot set property {Property} on non-dictionary object of type {Type} for path {Path}", 
                    finalPart.Property, current?.GetType().Name ?? "null", fieldPath);
                return false;
            }
        }
    }

    private List<FieldPathPart> ParseFieldPath(string fieldPath)
    {
        var parts = new List<FieldPathPart>();
        var segments = fieldPath.Split('.');

        foreach (var segment in segments)
        {
            var bracketIndex = segment.IndexOf('[');
            if (bracketIndex >= 0)
            {
                var property = segment[..bracketIndex];
                var arrayPart = segment[(bracketIndex + 1)..];
                var closeBracketIndex = arrayPart.IndexOf(']');
                
                if (closeBracketIndex >= 0)
                {
                    var indexStr = arrayPart[..closeBracketIndex];
                    if (int.TryParse(indexStr, out var index))
                    {
                        if (!string.IsNullOrEmpty(property))
                        {
                            parts.Add(new FieldPathPart { Property = property });
                        }
                        parts.Add(new FieldPathPart { IsArrayIndex = true, ArrayIndex = index });
                    }
                }
            }
            else
            {
                parts.Add(new FieldPathPart { Property = segment });
            }
        }

        return parts;
    }

    private class FieldPathPart
    {
        public string Property { get; set; } = "";
        public bool IsArrayIndex { get; set; }
        public int ArrayIndex { get; set; }
    }
}