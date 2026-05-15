using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.ContextSlicing;

/// <summary>
/// EPIC 5.1: Context slice builder for Generate phase activities.
/// Provides: Design spec + targeted context for the specific document/media being generated.
/// </summary>
public class GenerateContextSliceBuilder : IContextSliceBuilder
{
    private readonly IContextManager _contextManager;
    private readonly ILogger<GenerateContextSliceBuilder> _logger;

    public string ActivityType => "Generate";

    public GenerateContextSliceBuilder(
        IContextManager contextManager,
        ILogger<GenerateContextSliceBuilder> logger)
    {
        _contextManager = contextManager;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> BuildContextSliceAsync(
        string caseId,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EPIC 5.1: Building Generate context slice for case {CaseId}", caseId);

        var context = new Dictionary<string, string>();

        try
        {
            // Load design (essential for generation)
            var design = await _contextManager.LoadContextAsync<string>(caseId, "design/specs.json");
            if (!string.IsNullOrEmpty(design))
                context["design"] = design;

            // If generating specific item, load only relevant expanded context
            if (parameters != null)
            {
                if (parameters.TryGetValue("docType", out var docType) && docType is string type)
                {
                    // Document generation - load suspects and timeline
                    var suspects = await LoadExpandedFolder(caseId, "expand/suspects");
                    if (!string.IsNullOrEmpty(suspects))
                        context["expand_suspects"] = suspects;

                    var timeline = await _contextManager.LoadContextAsync<string>(caseId, "expand/timeline.json");
                    if (!string.IsNullOrEmpty(timeline))
                        context["expand_timeline"] = timeline;
                }
                else if (parameters.TryGetValue("mediaType", out var mediaType) && mediaType is string mType)
                {
                    // Media generation - load only the specific evidence being visualized
                    if (parameters.TryGetValue("evidenceId", out var evidenceId) && evidenceId is string evId)
                    {
                        var evidence = await _contextManager.LoadContextAsync<string>(caseId, $"expand/evidence/{evId}.json");
                        if (!string.IsNullOrEmpty(evidence))
                            context["target_evidence"] = evidence;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EPIC 5.1: Error loading generate context for case {CaseId}", caseId);
        }

        _logger.LogInformation("EPIC 5.1: Generate context slice built with {Count} items", context.Count);
        return context;
    }

    private async Task<string?> LoadExpandedFolder(string caseId, string folderPath)
    {
        try
        {
            var items = await _contextManager.QueryContextAsync<object>(caseId, $"{folderPath}/*");
            if (items.Any())
            {
                var aggregated = items.Select(i => JsonSerializer.Serialize(i.Data)).ToList();
                return JsonSerializer.Serialize(aggregated);
            }
        }
        catch
        {
            // Folder doesn't exist or error loading
        }
        return null;
    }
}
