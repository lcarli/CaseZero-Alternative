using System.Text.Json.Serialization;

namespace CaseZeroApi.Models;

/// <summary>
/// Represents a document in the normalized case bundle
/// </summary>
public class CaseDocument
{
    [JsonPropertyName("docId")]
    public string DocId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("words")]
    public int Words { get; set; }

    [JsonPropertyName("sections")]
    public List<DocumentSection> Sections { get; set; } = new();
}

/// <summary>
/// Represents a section within a document
/// </summary>
public class DocumentSection
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// DTO for file viewer item
/// </summary>
public class FileViewerItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public DateTime Modified { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? EvidenceId { get; set; }
    public bool IsUnlocked { get; set; } = true;
}

/// <summary>
/// Response containing case files for the file viewer
/// </summary>
public class CaseFilesResponse
{
    public string CaseId { get; set; } = string.Empty;
    public List<FileViewerItem> Files { get; set; } = new();
    public int TotalFiles { get; set; }
    public Dictionary<string, int> FilesByCategory { get; set; } = new();
}
