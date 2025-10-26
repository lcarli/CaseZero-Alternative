namespace CaseGen.Functions.Models;

/// <summary>
/// Result of a context query operation containing the data and its path.
/// </summary>
/// <typeparam name="T">Type of the context data</typeparam>
public class ContextQueryResult<T> where T : class
{
    /// <summary>
    /// The context path where this data was found.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// The deserialized data.
    /// </summary>
    public required T Data { get; set; }

    /// <summary>
    /// Size in bytes of the serialized data.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// When this context was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }
}

/// <summary>
/// A snapshot of context containing multiple loaded items.
/// Used to provide minimal, focused context to LLM activities.
/// </summary>
public class ContextSnapshot
{
    /// <summary>
    /// Case ID this snapshot belongs to.
    /// </summary>
    public required string CaseId { get; set; }

    /// <summary>
    /// When this snapshot was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Dictionary of context path -> deserialized data.
    /// </summary>
    public required Dictionary<string, object> Items { get; set; } = new();

    /// <summary>
    /// Total size in bytes of all items in this snapshot.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Estimated token count (approximate: bytes / 4).
    /// </summary>
    public int EstimatedTokens => (int)(TotalSizeBytes / 4);

    /// <summary>
    /// Metadata about what was included in this snapshot.
    /// </summary>
    public SnapshotMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets a typed item from the snapshot.
    /// </summary>
    public T? GetItem<T>(string path) where T : class
    {
        if (Items.TryGetValue(path, out var item))
        {
            return item as T;
        }
        return null;
    }

    /// <summary>
    /// Gets all items of a specific type.
    /// </summary>
    public IEnumerable<T> GetItemsOfType<T>() where T : class
    {
        return Items.Values.OfType<T>();
    }
}

/// <summary>
/// Metadata about what's included in a context snapshot.
/// </summary>
public class SnapshotMetadata
{
    /// <summary>
    /// Number of items in the snapshot.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Paths that were requested.
    /// </summary>
    public string[] RequestedPaths { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Paths that were successfully loaded.
    /// </summary>
    public string[] LoadedPaths { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Paths that failed to load.
    /// </summary>
    public string[] FailedPaths { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Time taken to build this snapshot.
    /// </summary>
    public TimeSpan BuildDuration { get; set; }
}

/// <summary>
/// Reference to context stored elsewhere.
/// Format: "@path/to/context" (e.g., "@suspects/S001", "@evidence/E003")
/// </summary>
public class ContextReference
{
    /// <summary>
    /// The reference string (starts with @).
    /// </summary>
    public required string Reference { get; set; }

    /// <summary>
    /// Type of entity being referenced.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// ID of the entity being referenced.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Parses a reference string into a ContextReference object.
    /// </summary>
    /// <param name="reference">Reference string (e.g., "@suspects/S001")</param>
    /// <returns>Parsed ContextReference</returns>
    public static ContextReference Parse(string reference)
    {
        if (!reference.StartsWith("@"))
        {
            throw new ArgumentException("Context reference must start with '@'", nameof(reference));
        }

        var path = reference.TrimStart('@');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return new ContextReference
        {
            Reference = reference,
            EntityType = parts.Length > 0 ? parts[0] : null,
            EntityId = parts.Length > 1 ? parts[^1] : null
        };
    }

    /// <summary>
    /// Converts this reference to a context path (without @).
    /// </summary>
    public string ToContextPath()
    {
        return Reference.TrimStart('@');
    }

    /// <summary>
    /// Creates a reference string from a context path.
    /// </summary>
    public static string FromPath(string contextPath)
    {
        return $"@{contextPath.TrimStart('/')}";
    }
}

/// <summary>
/// Typed reference to a specific entity.
/// </summary>
public class EntityReference
{
    /// <summary>
    /// Type of entity (suspects, evidence, witnesses, documents, etc.).
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// Unique ID of the entity.
    /// </summary>
    public required string EntityId { get; set; }

    /// <summary>
    /// Optional display name for the entity.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Converts to a context reference string.
    /// </summary>
    public string ToReference()
    {
        return $"@entities/{EntityType}/{EntityId}";
    }

    /// <summary>
    /// Converts to a context path (without @).
    /// </summary>
    public string ToPath()
    {
        return $"entities/{EntityType}/{EntityId}";
    }
}

/// <summary>
/// Metadata about the context storage for a case.
/// Provides overview without loading all content.
/// </summary>
public class ContextMetadata
{
    /// <summary>
    /// Case ID this metadata belongs to.
    /// </summary>
    public required string CaseId { get; set; }

    /// <summary>
    /// When the case context was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the case context was last modified.
    /// </summary>
    public DateTime LastModifiedAt { get; set; }

    /// <summary>
    /// Total size in bytes of all context data.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Number of context items stored.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Breakdown by context type.
    /// </summary>
    public Dictionary<string, int> ItemsByType { get; set; } = new();

    /// <summary>
    /// All entity references in this case.
    /// </summary>
    public EntityReferences Entities { get; set; } = new();

    /// <summary>
    /// Current phase of case generation.
    /// </summary>
    public string? CurrentPhase { get; set; }

    /// <summary>
    /// Completion percentage (0-100).
    /// </summary>
    public int CompletionPercentage { get; set; }

    /// <summary>
    /// Any warnings or issues detected.
    /// </summary>
    public string[] Warnings { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Collection of entity references organized by type.
/// </summary>
public class EntityReferences
{
    /// <summary>
    /// Suspect references.
    /// </summary>
    public List<EntityReference> Suspects { get; set; } = new();

    /// <summary>
    /// Evidence references.
    /// </summary>
    public List<EntityReference> Evidence { get; set; } = new();

    /// <summary>
    /// Witness references.
    /// </summary>
    public List<EntityReference> Witnesses { get; set; } = new();

    /// <summary>
    /// Document references.
    /// </summary>
    public List<EntityReference> Documents { get; set; } = new();

    /// <summary>
    /// Media/image references.
    /// </summary>
    public List<EntityReference> Media { get; set; } = new();

    /// <summary>
    /// Gets all references as a flat list.
    /// </summary>
    public IEnumerable<EntityReference> GetAll()
    {
        return Suspects
            .Concat(Evidence)
            .Concat(Witnesses)
            .Concat(Documents)
            .Concat(Media);
    }

    /// <summary>
    /// Gets count of all entities.
    /// </summary>
    public int TotalCount =>
        Suspects.Count + Evidence.Count + Witnesses.Count + Documents.Count + Media.Count;
}
