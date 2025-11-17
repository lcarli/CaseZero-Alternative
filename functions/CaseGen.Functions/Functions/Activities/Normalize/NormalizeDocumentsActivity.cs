using System.Text.Json;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.Activities.Normalize;

/// <summary>
/// Phase 6 Task 6.2: Normalizes generated documents, extracts entity references,
/// and saves to context/documents/
/// </summary>
public class NormalizeDocumentsActivity
{
    private readonly ILogger<NormalizeDocumentsActivity> _logger;
    private readonly IContextManager _contextManager;
    private readonly IStorageService _storageService;

    public NormalizeDocumentsActivity(
        ILogger<NormalizeDocumentsActivity> logger,
        IContextManager contextManager,
        IStorageService storageService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _storageService = storageService;
    }

    [Function("NormalizeDocumentsActivity")]
    public async Task<string> Run([ActivityTrigger] NormalizeDocumentsActivityModel model)
    {
        var caseId = model.CaseId;
        var docIds = model.DocIds;
        
        _logger.LogInformation("Normalizing {Count} documents for case {CaseId}", docIds.Length, caseId);

        var normalizedCount = 0;
        var bundlesContainer = "bundles";

        foreach (var docId in docIds)
        {
            try
            {
                // Load document from temporary bundle storage
                var docPath = $"{caseId}/documents/{docId}.json";
                var docJson = await _storageService.GetFileAsync(bundlesContainer, docPath);
                
                if (string.IsNullOrEmpty(docJson))
                {
                    _logger.LogWarning("Document {DocId} not found in bundles", docId);
                    continue;
                }

                // Parse document to extract entity references
                var docElement = JsonDocument.Parse(docJson).RootElement;
                var entityReferences = ExtractEntityReferences(docJson, docElement);

                // Add entity references to the document metadata
                var enrichedDoc = new
                {
                    docId = docElement.GetProperty("docId").GetString(),
                    type = docElement.GetProperty("type").GetString(),
                    title = docElement.GetProperty("title").GetString(),
                    words = docElement.GetProperty("words").GetInt32(),
                    sections = docElement.GetProperty("sections"),
                    entityReferences = new
                    {
                        suspects = entityReferences.Suspects,
                        evidence = entityReferences.Evidence,
                        witnesses = entityReferences.Witnesses
                    }
                };

                var normalizedJson = JsonSerializer.Serialize(enrichedDoc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Save to context/documents/{docId}.json
                await _contextManager.SaveContextAsync(caseId, $"documents/{docId}.json", normalizedJson);
                normalizedCount++;
                
                _logger.LogInformation(
                    "Normalized document {DocId}: {Suspects} suspects, {Evidence} evidence refs",
                    docId, entityReferences.Suspects.Count, entityReferences.Evidence.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to normalize document {DocId}", docId);
            }
        }

        _logger.LogInformation("Normalized {Count}/{Total} documents for case {CaseId}", 
            normalizedCount, docIds.Length, caseId);

        return JsonSerializer.Serialize(new { NormalizedCount = normalizedCount, TotalRequested = docIds.Length });
    }

    /// <summary>
    /// Extracts entity references from document content
    /// Looks for patterns like: S001, E001, W001, etc.
    /// </summary>
    private EntityReferences ExtractEntityReferences(string docJson, JsonElement docElement)
    {
        var suspects = new HashSet<string>();
        var evidence = new HashSet<string>();
        var witnesses = new HashSet<string>();

        // Patterns for entity IDs
        var suspectPattern = new Regex(@"\bS\d{3}\b", RegexOptions.IgnoreCase);
        var evidencePattern = new Regex(@"\bE\d{3}\b", RegexOptions.IgnoreCase);
        var witnessPattern = new Regex(@"\bW\d{3}\b", RegexOptions.IgnoreCase);

        // Extract from all sections content
        if (docElement.TryGetProperty("sections", out var sectionsElement) && 
            sectionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var section in sectionsElement.EnumerateArray())
            {
                if (section.TryGetProperty("content", out var contentElement) && 
                    contentElement.ValueKind == JsonValueKind.String)
                {
                    var content = contentElement.GetString() ?? string.Empty;
                    
                    // Extract suspect IDs
                    foreach (Match match in suspectPattern.Matches(content))
                    {
                        suspects.Add(match.Value.ToUpper());
                    }
                    
                    // Extract evidence IDs
                    foreach (Match match in evidencePattern.Matches(content))
                    {
                        evidence.Add(match.Value.ToUpper());
                    }
                    
                    // Extract witness IDs
                    foreach (Match match in witnessPattern.Matches(content))
                    {
                        witnesses.Add(match.Value.ToUpper());
                    }
                }
            }
        }

        return new EntityReferences
        {
            Suspects = suspects.ToList(),
            Evidence = evidence.ToList(),
            Witnesses = witnesses.ToList()
        };
    }

    private class EntityReferences
    {
        public List<string> Suspects { get; set; } = new();
        public List<string> Evidence { get; set; } = new();
        public List<string> Witnesses { get; set; } = new();
    }
}
