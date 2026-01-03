using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.ContextSlicing;

/// <summary>
/// EPIC 5.1: Context slice builder for Expand phase activities.
/// Provides: Plan + specific entity being expanded.
/// </summary>
public class ExpandContextSliceBuilder : IContextSliceBuilder
{
    private readonly IContextManager _contextManager;
    private readonly ILogger<ExpandContextSliceBuilder> _logger;

    public string ActivityType => "Expand";

    public ExpandContextSliceBuilder(
        IContextManager contextManager,
        ILogger<ExpandContextSliceBuilder> logger)
    {
        _contextManager = contextManager;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> BuildContextSliceAsync(
        string caseId,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EPIC 5.1: Building Expand context slice for case {CaseId}", caseId);

        var context = new Dictionary<string, string>();

        try
        {
            // Load plan (minimal, essential context)
            var planCore = await _contextManager.LoadContextAsync<string>(caseId, "plan/core.json");
            if (!string.IsNullOrEmpty(planCore))
                context["plan_core"] = planCore;

            var planSuspects = await _contextManager.LoadContextAsync<string>(caseId, "plan/suspects.json");
            if (!string.IsNullOrEmpty(planSuspects))
                context["plan_suspects"] = planSuspects;

            // If expanding a specific entity, load only related context
            if (parameters != null && parameters.TryGetValue("entityId", out var entityId) && entityId is string id)
            {
                if (id.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                {
                    // Expanding suspect - no additional context needed beyond plan
                    _logger.LogInformation("EPIC 5.1: Expanding suspect {EntityId}", id);
                }
                else if (id.StartsWith("EV", StringComparison.OrdinalIgnoreCase))
                {
                    // Expanding evidence - include timeline
                    var timeline = await _contextManager.LoadContextAsync<string>(caseId, "expand/timeline.json");
                    if (!string.IsNullOrEmpty(timeline))
                        context["timeline"] = timeline;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EPIC 5.1: Error loading expand context for case {CaseId}", caseId);
        }

        _logger.LogInformation("EPIC 5.1: Expand context slice built with {Count} items", context.Count);
        return context;
    }
}
