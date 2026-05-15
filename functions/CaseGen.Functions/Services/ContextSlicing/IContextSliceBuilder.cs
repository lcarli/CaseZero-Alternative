using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CaseGen.Functions.Services.ContextSlicing;

/// <summary>
/// EPIC 5.1: Interface for building minimal, predictable context slices for activities.
/// Reduces LLM invention by providing only relevant, structured context.
/// </summary>
public interface IContextSliceBuilder
{
    /// <summary>
    /// Gets the activity type this builder supports.
    /// </summary>
    string ActivityType { get; }

    /// <summary>
    /// Builds a minimal context slice for the activity.
    /// Returns a dictionary of context keys to JSON strings.
    /// </summary>
    Task<Dictionary<string, string>> BuildContextSliceAsync(
        string caseId,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
}
