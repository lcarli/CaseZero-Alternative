using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.ContextSlicing;

/// <summary>
/// EPIC 5.1: Context slice builder for Design phase activities.
/// Provides: Plan + Expanded content (but not documents/media yet).
/// </summary>
public class DesignContextSliceBuilder : IContextSliceBuilder
{
    private readonly IContextManager _contextManager;
    private readonly ILogger<DesignContextSliceBuilder> _logger;

    public string ActivityType => "Design";

    public DesignContextSliceBuilder(
        IContextManager contextManager,
        ILogger<DesignContextSliceBuilder> logger)
    {
        _contextManager = contextManager;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> BuildContextSliceAsync(
        string caseId,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EPIC 5.1: Building Design context slice for case {CaseId}", caseId);

        var context = new Dictionary<string, string>();

        try
        {
            // Load plan
            var planCore = await _contextManager.LoadContextAsync<string>(caseId, "plan/core.json");
            if (!string.IsNullOrEmpty(planCore))
                context["plan_core"] = planCore;

            // Load expanded content (suspects, evidence, timeline, relations)
            var expandedPaths = new[] 
            { 
                "expand/suspects", 
                "expand/evidence", 
                "expand/timeline.json", 
                "expand/relations.json" 
            };

            foreach (var path in expandedPaths)
            {
                try
                {
                    if (path.EndsWith(".json"))
                    {
                        var content = await _contextManager.LoadContextAsync<string>(caseId, path);
                        if (!string.IsNullOrEmpty(content))
                            context[path.Replace("/", "_").Replace(".json", "")] = content;
                    }
                    else
                    {
                        // Load all items in folder
                        var items = await _contextManager.QueryContextAsync<object>(caseId, $"{path}/*");
                        if (items.Any())
                        {
                            var aggregated = items.Select(i => JsonSerializer.Serialize(i.Data)).ToList();
                            context[path.Replace("/", "_")] = JsonSerializer.Serialize(aggregated);
                        }
                    }
                }
                catch
                {
                    // Skip missing context items
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EPIC 5.1: Error loading design context for case {CaseId}", caseId);
        }

        _logger.LogInformation("EPIC 5.1: Design context slice built with {Count} items", context.Count);
        return context;
    }
}
