using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.ContextSlicing;

/// <summary>
/// EPIC 5.1: Context slice builder for Plan phase activities.
/// Provides minimal context: only the request parameters.
/// </summary>
public class PlanContextSliceBuilder : IContextSliceBuilder
{
    private readonly IContextManager _contextManager;
    private readonly ILogger<PlanContextSliceBuilder> _logger;

    public string ActivityType => "Plan";

    public PlanContextSliceBuilder(
        IContextManager contextManager,
        ILogger<PlanContextSliceBuilder> logger)
    {
        _contextManager = contextManager;
        _logger = logger;
    }

    public Task<Dictionary<string, string>> BuildContextSliceAsync(
        string caseId,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EPIC 5.1: Building Plan context slice for case {CaseId}", caseId);

        var context = new Dictionary<string, string>();

        // Plan phase needs minimal context - just the request
        if (parameters != null && parameters.TryGetValue("request", out var request))
        {
            context["request"] = JsonSerializer.Serialize(request);
        }

        _logger.LogInformation("EPIC 5.1: Plan context slice built with {Count} items", context.Count);
        return Task.FromResult(context);
    }
}
