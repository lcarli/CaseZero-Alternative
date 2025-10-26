using System.Text.Json;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.Activities.Normalize;

/// <summary>
/// Phase 6 Task 6.3: Creates a manifest index with references to all entities and documents
/// Does NOT copy content - only creates reference paths
/// </summary>
public class NormalizeManifestActivity
{
    private readonly ILogger<NormalizeManifestActivity> _logger;
    private readonly IContextManager _contextManager;

    public NormalizeManifestActivity(
        ILogger<NormalizeManifestActivity> logger,
        IContextManager contextManager)
    {
        _logger = logger;
        _contextManager = contextManager;
    }

    [Function("NormalizeManifestActivity")]
    public async Task<string> Run([ActivityTrigger] NormalizeManifestActivityModel model)
    {
        var caseId = model.CaseId;
        _logger.LogInformation("Creating manifest for case {CaseId}", caseId);

        try
        {
            // Query all entities from context
            var suspectsQuery = await _contextManager.QueryContextAsync<object>(caseId, "entities/suspects/*");
            var evidenceQuery = await _contextManager.QueryContextAsync<object>(caseId, "entities/evidence/*");
            var witnessesQuery = await _contextManager.QueryContextAsync<object>(caseId, "entities/witnesses/*");
            
            // Query all documents from context
            var documentsQuery = await _contextManager.QueryContextAsync<object>(caseId, "documents/*");

            // Build reference arrays (IMPORTANT: Only references, not content!)
            var suspectRefs = suspectsQuery
                .Select(s => $"@entities/suspects/{ExtractIdFromPath(s.Path)}")
                .OrderBy(r => r)
                .ToList();

            var evidenceRefs = evidenceQuery
                .Select(e => $"@entities/evidence/{ExtractIdFromPath(e.Path)}")
                .OrderBy(r => r)
                .ToList();

            var witnessRefs = witnessesQuery
                .Select(w => $"@entities/witnesses/{ExtractIdFromPath(w.Path)}")
                .OrderBy(r => r)
                .ToList();

            var documentRefs = documentsQuery
                .Select(d => $"@documents/{ExtractIdFromPath(d.Path)}")
                .OrderBy(r => r)
                .ToList();

            // Create manifest with references only
            var manifest = new
            {
                caseId = caseId,
                version = "v2-hierarchical",
                generatedAt = DateTime.UtcNow.ToString("o"),
                entities = new
                {
                    suspects = suspectRefs,
                    evidence = evidenceRefs,
                    witnesses = witnessRefs,
                    total = suspectRefs.Count + evidenceRefs.Count + witnessRefs.Count
                },
                documents = new
                {
                    items = documentRefs,
                    total = documentRefs.Count
                },
                context = new
                {
                    plan = new
                    {
                        core = "@plan/core",
                        suspects = "@plan/suspects",
                        timeline = "@plan/timeline",
                        evidence = "@plan/evidence"
                    },
                    expand = new
                    {
                        timeline = "@expand/timeline",
                        relations = "@expand/relations"
                    }
                }
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Save manifest to context root
            await _contextManager.SaveContextAsync(caseId, "manifest.json", manifestJson);

            _logger.LogInformation(
                "Created manifest for case {CaseId}: {Suspects} suspects, {Evidence} evidence, {Witnesses} witnesses, {Documents} documents",
                caseId, suspectRefs.Count, evidenceRefs.Count, witnessRefs.Count, documentRefs.Count);

            return manifestJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create manifest for case {CaseId}", caseId);
            throw;
        }
    }

    /// <summary>
    /// Extracts the ID from a context path
    /// e.g., "entities/suspects/S001.json" -> "S001"
    /// </summary>
    private string ExtractIdFromPath(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return fileName;
    }
}
