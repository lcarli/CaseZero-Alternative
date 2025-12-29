using System.Text.Json.Serialization;

namespace CaseZeroApi.Models.CaseV1;

/// <summary>
/// Case JSON v1.0 - Modelo completo do caso investigativo
/// </summary>
public class CaseV1
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("caseId")]
    public string CaseId { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public CaseMetadata Metadata { get; set; } = new();

    [JsonPropertyName("assets")]
    public List<Asset> Assets { get; set; } = new();

    [JsonPropertyName("emails")]
    public List<Email> Emails { get; set; } = new();

    [JsonPropertyName("suspects")]
    public List<Suspect> Suspects { get; set; } = new();

    [JsonPropertyName("rules")]
    public List<Rule>? Rules { get; set; } = new(); // Nullable - será removido na sanitização

    [JsonPropertyName("forensicsDefaults")]
    public ForensicsDefaults ForensicsDefaults { get; set; } = new();
}

/// <summary>
/// Metadados do caso
/// </summary>
public class CaseMetadata
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    [JsonPropertyName("estimatedTimeMinutes")]
    public int EstimatedTimeMinutes { get; set; }

    [JsonPropertyName("requiredRank")]
    public string RequiredRank { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("incidentDate")]
    public string IncidentDate { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("briefing")]
    public string Briefing { get; set; } = string.Empty;

    [JsonPropertyName("victim")]
    public Victim? Victim { get; set; }
}

public class Victim
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("occupation")]
    public string Occupation { get; set; } = string.Empty;

    [JsonPropertyName("lastSeen")]
    public string LastSeen { get; set; } = string.Empty;
}

/// <summary>
/// Asset (evidência, documento, mídia)
/// </summary>
public class Asset
{
    [JsonPropertyName("assetId")]
    public string AssetId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // document, image, video, audio, physical, digital

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = string.Empty; // initial, hidden

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Email do caso
/// </summary>
public class Email
{
    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("sentAt")]
    public string SentAt { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty; // normal, high, urgent

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = string.Empty; // initial, hidden

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("attachments")]
    public List<string>? Attachments { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Suspeito
/// </summary>
public class Suspect
{
    [JsonPropertyName("suspectId")]
    public string SuspectId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("occupation")]
    public string Occupation { get; set; } = string.Empty;

    [JsonPropertyName("relationship")]
    public string Relationship { get; set; } = string.Empty;

    [JsonPropertyName("motive")]
    public string Motive { get; set; } = string.Empty;

    [JsonPropertyName("alibi")]
    public string Alibi { get; set; } = string.Empty;

    [JsonPropertyName("alibiVerified")]
    public bool AlibiVerified { get; set; }

    [JsonPropertyName("background")]
    public string Background { get; set; } = string.Empty;

    [JsonPropertyName("linkedAssets")]
    public List<string>? LinkedAssets { get; set; }

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = string.Empty; // initial, hidden
}

/// <summary>
/// Regra de progressão (SERVER-SIDE APENAS)
/// </summary>
public class Rule
{
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [JsonPropertyName("trigger")]
    public Trigger Trigger { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<RuleAction> Actions { get; set; } = new();
}

public class Trigger
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // forensics_complete, attachment_download, etc.

    [JsonPropertyName("inputAssetId")]
    public string? InputAssetId { get; set; }

    [JsonPropertyName("analysisType")]
    public string? AnalysisType { get; set; }

    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }

    [JsonPropertyName("assetId")]
    public string? AssetId { get; set; }

    [JsonPropertyName("gameTimeMinutes")]
    public int? GameTimeMinutes { get; set; }
}

public class RuleAction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // reveal_email, reveal_asset, reveal_suspect, send_notification

    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }

    [JsonPropertyName("assetId")]
    public string? AssetId { get; set; }

    [JsonPropertyName("suspectId")]
    public string? SuspectId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Configurações padrão de análises forenses
/// </summary>
public class ForensicsDefaults
{
    [JsonPropertyName("analysisTypes")]
    public List<AnalysisType> AnalysisTypes { get; set; } = new();

    [JsonPropertyName("noFindingsEmail")]
    public NoFindingsEmailTemplate NoFindingsEmail { get; set; } = new();
}

public class AnalysisType
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("durationMinutes")]
    public int DurationMinutes { get; set; }

    [JsonPropertyName("availableFor")]
    public List<string> AvailableFor { get; set; } = new();
}

public class NoFindingsEmailTemplate
{
    [JsonPropertyName("template")]
    public string Template { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
}
