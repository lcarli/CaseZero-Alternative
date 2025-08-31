using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models;

public record PlanActivityModel
{
    public required CaseGenerationRequest Request { get; init; }
    public required string CaseId { get; init; }
}

public record DesignActivityModel
{
    public required string PlanJson { get; init; }
    public required string ExpandedJson { get; init; }
    public required string CaseId { get; init; }
    public string? Difficulty { get; init; }
}

public record ExpandActivityModel
{
    public required string PlanJson { get; init; }
    public required string CaseId { get; init; }
}

public record IndexActivityModel
{
    public required string NormalizedJson { get; init; }
    public required string CaseId { get; init; }
}

public record ValidateActivityModel
{
    public required string IndexedJson { get; init; }
    public required string CaseId { get; init; }
}

public record RedTeamActivityModel
{
    public required string ValidatedJson { get; init; }
    public required string CaseId { get; init; }
}

// Deprecated bulk activity models (not used in current orchestrator)
// public record GenerateDocumentsActivityModel
// {
//     public required string DesignJson { get; init; }
//     public required string CaseId { get; init; }
// }

// public record GenerateMediaActivityModel
// {
//     public required string DesignJson { get; init; }
//     public required string CaseId { get; init; }
// }

public record NormalizeActivityModel
{
    public required string[] Documents { get; init; }
    public required string[] Media { get; init; }
    public required string CaseId { get; init; }
}

// Duplicate models - removing to avoid confusion
// public record IndexActivityModel
// {
//     public required string NormalizedJson { get; init; }
//     public required string CaseId { get; init; }
// }

// public record ValidateActivityModel
// {
//     public required string IndexedJson { get; init; }
//     public required string CaseId { get; init; }
// }

// public record RedTeamActivityModel
// {
//     public required string ValidatedJson { get; init; }
//     public required string CaseId { get; init; }
// }

public record GenerateDocumentItemInput
{
    public required string CaseId { get; init; }
    public required string DesignJson { get; init; }
    public required DocumentSpec Spec { get; init; }
}

public record GenerateMediaItemInput
{
    public required string CaseId { get; init; }
    public required string DesignJson { get; init; }
    public required MediaSpec Spec { get; init; }
}

public record PackageActivityModel
{
    public required string FinalJson { get; init; }
    public required string CaseId { get; init; }
}

public record CaseGenerationRequest
{
    [JsonPropertyName("difficulty")]
    public string? Difficulty { get; init; }

    [JsonPropertyName("generateImages")]
    public bool GenerateImages { get; init; } = true;

    [JsonPropertyName("timezone")]
    public string Timezone { get; init; } = "America/Toronto";

    // Legacy fields kept for backwards compatibility but made optional
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("targetDurationMinutes")]
    public int? TargetDurationMinutes { get; init; }

    [JsonPropertyName("constraints")]
    public string[] Constraints { get; init; } = Array.Empty<string>();
}

public record CaseGenerationStatus
{
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = "";
    
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    
    [JsonPropertyName("currentStep")]
    public string CurrentStep { get; init; } = "";
    
    [JsonPropertyName("completedSteps")]
    public string[] CompletedSteps { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; init; }
    
    [JsonPropertyName("progress")]
    public double Progress { get; init; }
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; init; }
    
    [JsonPropertyName("estimatedCompletion")]
    public DateTime? EstimatedCompletion { get; init; }
    
    [JsonPropertyName("error")]
    public string? Error { get; init; }
    
    [JsonPropertyName("output")]
    public CaseGenerationOutput? Output { get; init; }
}

public record CaseGenerationOutput
{
    [JsonPropertyName("bundlePath")]
    public string BundlePath { get; init; } = "";
    
    [JsonPropertyName("caseId")]
    public string CaseId { get; init; } = "";
    
    [JsonPropertyName("files")]
    public GeneratedFile[] Files { get; init; } = Array.Empty<GeneratedFile>();
    
    [JsonPropertyName("metadata")]
    public CaseMetadata? Metadata { get; init; }
}

public record GeneratedFile
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";
    
    [JsonPropertyName("size")]
    public long Size { get; init; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}

public record CaseMetadata
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";
    
    [JsonPropertyName("difficulty")]
    public string Difficulty { get; init; } = "";
    
    [JsonPropertyName("estimatedDuration")]
    public int EstimatedDuration { get; init; }
    
    [JsonPropertyName("categories")]
    public string[] Categories { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; init; }
}

// Pipeline step definitions
public static class CaseGenerationSteps
{
    public const string Plan = "Plan";
    public const string Expand = "Expand";
    public const string Design = "Design";
    public const string GenDocs = "GenDocs";
    public const string GenMedia = "GenMedia";
    public const string Normalize = "Normalize";
    public const string Index = "Index";
    public const string RuleValidate = "RuleValidate";
    public const string RedTeam = "RedTeam";
    public const string Package = "Package";
    
    public static readonly string[] AllSteps = {
        Plan, Expand, Design, GenDocs, GenMedia, 
        Normalize, Index, RuleValidate, RedTeam, Package
    };
}

// Document and Media Specifications for structured case design
public record DocumentSpec
{
    [JsonPropertyName("docId")]
    public required string DocId { get; init; }
    
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("sections")]
    public required string[] Sections { get; init; }
    
    [JsonPropertyName("lengthTarget")]
    public required int[] LengthTarget { get; init; }
    
    [JsonPropertyName("gated")]
    public required bool Gated { get; init; }
    
    [JsonPropertyName("gatingRule")]
    public GatingRule? GatingRule { get; init; }
}

public record MediaSpec
{
    [JsonPropertyName("evidenceId")]
    public required string EvidenceId { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("constraints")]
    public Dictionary<string, object>? Constraints { get; init; }
    public bool Deferred { get; init; } = false;
}

public record DocumentAndMediaSpecs
{
    [JsonPropertyName("documentSpecs")]
    public required DocumentSpec[] DocumentSpecs { get; init; }

    [JsonPropertyName("mediaSpecs")]
    public required MediaSpec[] MediaSpecs { get; init; }
    
    public string? CaseId { get; init; }
    public string? Version { get; init; }
}

// Document Types enumeration for validation
public static class DocumentTypes
{
    public const string PoliceReport = "police_report";
    public const string Interview = "interview";
    public const string MemoAdmin = "memo_admin";
    public const string ForensicsReport = "forensics_report";
    public const string EvidenceLog = "evidence_log";
    public const string WitnessStatement = "witness_statement";
    
    public static readonly string[] AllTypes = {
        PoliceReport, Interview, MemoAdmin, ForensicsReport, EvidenceLog, WitnessStatement
    };
}

// Media Types enumeration for validation
public static class MediaTypes
{
    public const string Photo = "photo";
    public const string Audio = "audio";
    public const string Video = "video";
    public const string DocumentScan = "document_scan";
    public const string Diagram = "diagram";

    public static readonly string[] AllTypes = {
        Photo, Audio, Video, DocumentScan, Diagram
    };
}

// Difficulty levels with complexity profiles
public static class DifficultyLevels
{
    public static readonly Dictionary<string, DifficultyProfile> Profiles = new()
    {
        ["Rookie"] = new DifficultyProfile
        {
            Description = "very low; straight line; minimal jargon",
            Suspects = (2, 3),
            Documents = (6, 8),
            Evidences = (3, 5),
            ComplexityFactors = new[] { "linear_investigation", "clear_evidence", "simple_motive" },
            EstimatedDurationMinutes = (30, 60),
            RedHerrings = 0,
            GatedDocuments = 0,
            ForensicsComplexity = "basic"
        },
        ["Detective"] = new DifficultyProfile
        {
            Description = "low; basic cross-checks; a couple red herrings",
            Suspects = (3, 4),
            Documents = (8, 12),
            Evidences = (4, 7),
            ComplexityFactors = new[] { "basic_cross_checks", "simple_red_herrings", "witness_verification" },
            EstimatedDurationMinutes = (60, 120),
            RedHerrings = 2,
            GatedDocuments = 1,
            ForensicsComplexity = "standard"
        },
        ["Detective2"] = new DifficultyProfile
        {
            Description = "medium; branching; some misdirection",
            Suspects = (4, 5),
            Documents = (10, 14),
            Evidences = (6, 9),
            ComplexityFactors = new[] { "branching_paths", "misdirection", "timeline_analysis", "evidence_correlation" },
            EstimatedDurationMinutes = (120, 180),
            RedHerrings = 3,
            GatedDocuments = 2,
            ForensicsComplexity = "intermediate"
        },
        ["Sergeant"] = new DifficultyProfile
        {
            Description = "medium-high; multi-source correlation",
            Suspects = (5, 6),
            Documents = (12, 16),
            Evidences = (8, 12),
            ComplexityFactors = new[] { "multi_source_correlation", "advanced_forensics", "witness_reliability", "chain_of_custody" },
            EstimatedDurationMinutes = (180, 240),
            RedHerrings = 4,
            GatedDocuments = 3,
            ForensicsComplexity = "advanced"
        },
        ["Lieutenant"] = new DifficultyProfile
        {
            Description = "high; layered timeline; multiple gates",
            Suspects = (6, 8),
            Documents = (14, 18),
            Evidences = (10, 15),
            ComplexityFactors = new[] { "layered_timeline", "multiple_gates", "evidence_dependencies", "expert_analysis", "technical_evidence" },
            EstimatedDurationMinutes = (240, 360),
            RedHerrings = 5,
            GatedDocuments = 4,
            ForensicsComplexity = "expert"
        },
        ["Captain"] = new DifficultyProfile
        {
            Description = "very high; deep inference; adversarial noise",
            Suspects = (7, 10),
            Documents = (16, 22),
            Evidences = (12, 18),
            ComplexityFactors = new[] { "deep_inference", "adversarial_noise", "counterintelligence", "expert_witnesses", "complex_motives" },
            EstimatedDurationMinutes = (360, 540),
            RedHerrings = 6,
            GatedDocuments = 5,
            ForensicsComplexity = "specialized"
        },
        ["Commander"] = new DifficultyProfile
        {
            Description = "extreme; serial/global arcs; chained cases",
            Suspects = (8, 12),
            Documents = (18, 25),
            Evidences = (15, 22),
            ComplexityFactors = new[] { "serial_connections", "global_implications", "chained_cases", "master_criminals", "international_scope" },
            EstimatedDurationMinutes = (540, 720),
            RedHerrings = 8,
            GatedDocuments = 6,
            ForensicsComplexity = "cutting_edge"
        }
    };

    public static readonly string[] AllLevels = Profiles.Keys.ToArray();
    
    public static DifficultyProfile GetProfile(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty) || !Profiles.ContainsKey(difficulty))
        {
            // Random difficulty if not specified or invalid
            var randomLevel = AllLevels[Random.Shared.Next(AllLevels.Length)];
            return Profiles[randomLevel];
        }
        return Profiles[difficulty];
    }
}

public record DifficultyProfile
{
    public required string Description { get; init; }
    public required (int Min, int Max) Suspects { get; init; }
    public required (int Min, int Max) Documents { get; init; }
    public required (int Min, int Max) Evidences { get; init; }
    public required string[] ComplexityFactors { get; init; }
    public required (int Min, int Max) EstimatedDurationMinutes { get; init; }
    public required int RedHerrings { get; init; }
    public required int GatedDocuments { get; init; }
    public required string ForensicsComplexity { get; init; }
}

public record GatingRule
{
    public required string Action { get; init; } // submit_evidence | role_required | manual_unlock
    public string? EvidenceId { get; init; }
    public string? Notes { get; init; }
}