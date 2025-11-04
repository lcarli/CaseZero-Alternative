using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

/// <summary>
/// Manages granular context storage and retrieval for case generation pipeline.
/// Implements hierarchical context architecture to reduce token usage and improve precision.
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Saves context data to a specific path in the case context hierarchy.
    /// </summary>
    /// <typeparam name="T">Type of data to save</typeparam>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="contextPath">Path within context hierarchy (e.g., "plan/core", "entities/suspects/S001")</param>
    /// <param name="data">Data to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full path where data was saved</returns>
    Task<string> SaveContextAsync<T>(
        string caseId, 
        string contextPath, 
        T data, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads context data from a specific path.
    /// </summary>
    /// <typeparam name="T">Type of data to load</typeparam>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="contextPath">Path within context hierarchy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized data or null if not found</returns>
    Task<T?> LoadContextAsync<T>(
        string caseId, 
        string contextPath, 
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Queries multiple context paths and returns all matching data.
    /// Supports wildcard patterns (e.g., "entities/suspects/*").
    /// </summary>
    /// <typeparam name="T">Type of data to query</typeparam>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="queryPattern">Pattern to match (supports * wildcard)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching data with their paths</returns>
    Task<IEnumerable<ContextQueryResult<T>>> QueryContextAsync<T>(
        string caseId, 
        string queryPattern, 
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Builds a context snapshot by loading multiple specific paths.
    /// Used to create minimal context for LLM activities.
    /// </summary>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="contextPaths">Array of paths to include in snapshot</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Context snapshot with all requested data</returns>
    Task<ContextSnapshot> BuildSnapshotAsync(
        string caseId, 
        string[] contextPaths, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes context data at a specific path.
    /// </summary>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="contextPath">Path to delete (supports * wildcard for bulk delete)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items deleted</returns>
    Task<int> DeleteContextAsync(
        string caseId, 
        string contextPath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if context exists at a specific path.
    /// </summary>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="contextPath">Path to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if context exists</returns>
    Task<bool> ExistsAsync(
        string caseId, 
        string contextPath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all context paths for a case.
    /// </summary>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="prefix">Optional prefix to filter paths</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all context paths</returns>
    Task<IEnumerable<string>> ListContextPathsAsync(
        string caseId, 
        string? prefix = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata about the context storage for a case.
    /// Includes size, number of entities, last modified, etc.
    /// </summary>
    /// <param name="caseId">Unique case identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Context metadata</returns>
    Task<ContextMetadata> GetMetadataAsync(
        string caseId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the in-memory cache for a specific case or all cases.
    /// </summary>
    /// <param name="caseId">Case ID to clear, or null to clear all</param>
    void ClearCache(string? caseId = null);
}
