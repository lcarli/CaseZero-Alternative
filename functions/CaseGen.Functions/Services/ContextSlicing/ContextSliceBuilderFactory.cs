using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.ContextSlicing;

/// <summary>
/// EPIC 5.1: Factory for creating context slice builders by activity type.
/// Centralizes the logic for determining which minimal context to provide.
/// </summary>
public class ContextSliceBuilderFactory
{
    private readonly IContextManager _contextManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, IContextSliceBuilder> _builders;

    public ContextSliceBuilderFactory(
        IContextManager contextManager,
        ILoggerFactory loggerFactory)
    {
        _contextManager = contextManager;
        _loggerFactory = loggerFactory;
        
        // Initialize builders
        _builders = new Dictionary<string, IContextSliceBuilder>(StringComparer.OrdinalIgnoreCase)
        {
            ["Plan"] = new PlanContextSliceBuilder(_contextManager, _loggerFactory.CreateLogger<PlanContextSliceBuilder>()),
            ["Expand"] = new ExpandContextSliceBuilder(_contextManager, _loggerFactory.CreateLogger<ExpandContextSliceBuilder>()),
            ["Design"] = new DesignContextSliceBuilder(_contextManager, _loggerFactory.CreateLogger<DesignContextSliceBuilder>()),
            ["Generate"] = new GenerateContextSliceBuilder(_contextManager, _loggerFactory.CreateLogger<GenerateContextSliceBuilder>())
        };
    }

    /// <summary>
    /// Gets a context slice builder for the specified activity type.
    /// </summary>
    public IContextSliceBuilder GetBuilder(string activityType)
    {
        if (_builders.TryGetValue(activityType, out var builder))
            return builder;

        throw new ArgumentException($"No context slice builder found for activity type: {activityType}", nameof(activityType));
    }

    /// <summary>
    /// Builds a minimal context slice for the specified activity.
    /// </summary>
    public async Task<Dictionary<string, string>> BuildContextSliceAsync(
        string activityType,
        string caseId,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var builder = GetBuilder(activityType);
        return await builder.BuildContextSliceAsync(caseId, parameters, cancellationToken);
    }

    /// <summary>
    /// Gets all supported activity types.
    /// </summary>
    public IEnumerable<string> GetSupportedActivityTypes()
    {
        return _builders.Keys;
    }
}
