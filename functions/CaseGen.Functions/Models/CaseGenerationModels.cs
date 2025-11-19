using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models;

public record PlanActivityModel
{
    public required CaseGenerationRequest Request { get; init; }
    public required string CaseId { get; init; }
}

// Phase 2: Hierarchical Plan sub-activities
public record PlanCoreActivityModel
{
    public required CaseGenerationRequest Request { get; init; }
    public required string CaseId { get; init; }
}

public record PlanSuspectsActivityModel
{
    public required string CorePlanRef { get; init; } // "@plan/core"
    public required string CaseId { get; init; }
}

public record PlanTimelineActivityModel
{
    public required string CorePlanRef { get; init; } // "@plan/core"
    public required string SuspectsRef { get; init; } // "@plan/suspects"
    public required string CaseId { get; init; }
}

public record PlanEvidenceActivityModel
{
    public required string CorePlanRef { get; init; } // "@plan/core"
    public required string SuspectsRef { get; init; } // "@plan/suspects"
    public required string TimelineRef { get; init; } // "@plan/timeline"
    public required string CaseId { get; init; }
}

// Helper model to load context from orchestrator
public record LoadContextActivityModel
{
    public required string CaseId { get; init; }
    public required string Path { get; init; } // e.g., "plan/suspects"
}

// Phase 4: Design by document type
public record DesignDocumentTypeActivityModel
{
    public required string CaseId { get; init; }
    public required string DocType { get; init; } // e.g., "police_report", "interview", "forensics_report"
}

// Phase 4: Design by media type
public record DesignMediaTypeActivityModel
{
    public required string CaseId { get; init; }
    public required string MediaType { get; init; } // e.g., "crime_scene_photo", "mugshot", "evidence_photo"
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

// Phase 3: Hierarchical Expand sub-activities
public record ExpandSuspectActivityModel
{
    public required string CaseId { get; init; }
    public required string SuspectId { get; init; } // "S001", "S002", etc.
}

public record ExpandEvidenceActivityModel
{
    public required string CaseId { get; init; }
    public required string EvidenceId { get; init; }
}

public record ExpandTimelineActivityModel
{
    public required string CaseId { get; init; }
}

public record SynthesizeRelationsActivityModel
{
    public required string CaseId { get; init; }
}

public record ValidateActivityModel
{
    public required string NormalizedJson { get; init; }
    public required string CaseId { get; init; }
}




public record FixActivityModel
{
    public required string RedTeamAnalysis { get; init; }
    public required string CurrentJson { get; init; }
    public required string CaseId { get; init; }
    public int IterationNumber { get; init; } = 1;
}

public record CheckCaseCleanActivityModel
{
    public required string RedTeamAnalysis { get; init; }
    public required string CaseId { get; init; }
}

// Hierarchical RedTeam Analysis Models
public record RedTeamGlobalActivityModel
{
    public required string ValidatedJson { get; init; }
    public required string CaseId { get; init; }
}

public record RedTeamFocusedActivityModel
{
    public required string ValidatedJson { get; init; }
    public required string CaseId { get; init; }
    public required string GlobalAnalysis { get; init; }
    public required string[] FocusAreas { get; init; } // Specific documents/sections to analyze
}

public record GlobalRedTeamAnalysis
{
    public required List<MacroIssue> MacroIssues { get; init; }
    public required string[] CriticalDocuments { get; init; } // Documents that need focused analysis
    public required string[] FocusAreas { get; init; } // Specific areas to examine in detail
    public required string OverallAssessment { get; init; }
    public required bool RequiresDetailedAnalysis { get; init; }
}

public record MacroIssue
{
    public required string Type { get; init; } // "CrossDocumentInconsistency", "ChronologicalGap", "NarrativeContradiction"
    public required string Severity { get; init; } // "Critical", "Major", "Minor"
    public required string[] AffectedDocuments { get; init; }
    public required string Description { get; init; }
    public required string[] RequiredFocusAreas { get; init; } // What needs detailed analysis
}

public record PreciseIssue
{
    public required string Priority { get; init; } // "High", "Medium", "Low"
    public required string Type { get; init; } // "TimestampConflict", "PostCreationReference", etc.
    public required IssueLocation Location { get; init; }
    public required string Problem { get; init; }
    public required IssueFix Fix { get; init; }
}

public record IssueLocation
{
    public required string DocId { get; init; }
    public string? Field { get; init; } // e.g., "Metadata.CompilationTime", "Content"
    public string? Section { get; init; } // e.g., "Immediate Actions Taken", "Log Metadata"
    public string? LinePattern { get; init; } // Text pattern to find exact location
    public string? CurrentValue { get; init; } // Current problematic value
}

public record IssueFix
{
    public required string Action { get; init; } // "UpdateTimestamp", "ReplaceText", "MoveToAddendum", "RemoveReference"
    public string? NewValue { get; init; } // New value to set
    public string? OldText { get; init; } // Text to be replaced (for ReplaceText)
    public string? NewText { get; init; } // Replacement text
    public string? NewSection { get; init; } // For moves/additions
    public string? Reason { get; init; } // Why this fix is needed
}

public record StructuredRedTeamAnalysis
{
    public required List<PreciseIssue> Issues { get; init; }
    public required string Summary { get; init; }
    public required int HighPriorityCount { get; init; }
    public required int MediumPriorityCount { get; init; }
    public required int LowPriorityCount { get; init; }
}

public record RedTeamChunkSpec
{
    public required string[] DocIds { get; init; }
    public required string[] EvidenceIds { get; init; }
    public int EstimatedBytes { get; init; }
}

public record NormalizeActivityModel
{
    public required string[] Documents { get; init; }
    public required string[] Media { get; init; }
    public required string CaseId { get; init; }
    public string? Difficulty { get; init; }
    public string? Timezone { get; init; }
    public string? PlanJson { get; init; }
    public string? ExpandedJson { get; init; }
    public string? DesignJson { get; init; }
    public RenderedDocument[] RenderedDocs { get; init; } = Array.Empty<RenderedDocument>();
    public RenderedMedia[] RenderedMedia { get; init; } = Array.Empty<RenderedMedia>();
}

public record GenerateDocumentItemInput
{
    public required string CaseId { get; init; }
    public required DocumentSpec Spec { get; init; }
    public required string? DifficultyOverride { get; init; } = "";
    
    // Phase 5: Removed PlanJson, ExpandedJson, DesignJson
    // Context will be loaded via ContextManager based on Spec.Type
}

public record GenerateMediaItemInput
{
    public required string CaseId { get; init; }
    public required MediaSpec Spec { get; init; }
    public required string? DifficultyOverride { get; init; } = "";
    
    // Phase 5: Removed PlanJson, ExpandedJson, DesignJson
    // Context will be loaded via ContextManager based on media type
}

public record RenderDocumentItemInput
{
    public required string CaseId { get; init; }
    public required string DocId { get; init; }
    public required string DocumentJson { get; init; }
}

public record RenderMediaItemInput
{
    public required string CaseId { get; init; }
    public required MediaSpec Spec { get; init; }
}

// Phase 6: Normalize activities models
public record NormalizeEntitiesActivityModel
{
    public required string CaseId { get; init; }
}

public record NormalizeDocumentsActivityModel
{
    public required string CaseId { get; init; }
    public required string[] DocIds { get; init; }
}

public record NormalizeManifestActivityModel
{
    public required string CaseId { get; init; }
}

// Phase 7: QA activities models
public record QA_ScanIssuesActivityModel
{
    public required string CaseId { get; init; }
}

public record QaScanIssue
{
    public required string Area { get; init; }
    public required string Severity { get; init; }  // "high", "medium", "low"
    public required string Description { get; init; }
}

public record QA_DeepDiveActivityModel
{
    public required string CaseId { get; init; }
    public required string IssueArea { get; init; }  // e.g., "suspect_S001_alibi", "evidence_E003_chain"
}

public record FixEntityActivityModel
{
    public required string CaseId { get; init; }
    public required string EntityId { get; init; }  // e.g., "S001", "E003", "W002"
    public required string IssueDescription { get; init; }  // Description from deep dive analysis
}

public record CheckCaseCleanActivityV2Model
{
    public required string CaseId { get; init; }
    public required string[] IssueAreas { get; init; }  // List of issue areas that were addressed
}

public record RenderedDocument
{
    public required string DocId { get; init; }
    public required string FilePath { get; init; }
    public long FileSize { get; init; }
    public string? Sha256Hash { get; init; }
    public string ContentType { get; init; } = "application/pdf";
}

public record RenderedMedia
{
    public required string EvidenceId { get; init; }
    public required string FilePath { get; init; }
    public long FileSize { get; init; }
    public string? Sha256Hash { get; init; }
    public string ContentType { get; init; } = "image/png";
    public string Kind { get; init; } = "photo";
}

public record SaveRedTeamAnalysisActivityModel
{
    public required string CaseId { get; init; }
    public required string RedTeamAnalysis { get; init; }
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

    [JsonPropertyName("renderFiles")]
    public bool RenderFiles { get; init; } = false;

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

    [JsonPropertyName("stepDurationsSeconds")]
    public Dictionary<string, double> StepDurationsSeconds { get; init; } = new();
}

public record CaseGenerationOutput
{
    [JsonPropertyName("bundlePath")]
    public required string BundlePath { get; init; }
    
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }
    
    [JsonPropertyName("files")]
    public required List<GeneratedFile> Files { get; init; }
    
    [JsonPropertyName("metadata")]
    public CaseMetadata? Metadata { get; init; }
}

public record GeneratedFile
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }
    
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("size")]
    public long Size { get; init; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}

public record CaseMetadata
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("difficulty")]
    public required string Difficulty { get; init; }
    
    [JsonPropertyName("estimatedDuration")]
    public int EstimatedDuration { get; init; }
    
    [JsonPropertyName("categories")]
    public required List<string> Categories { get; init; }
    
    [JsonPropertyName("tags")]
    public required List<string> Tags { get; init; }
    
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
    public const string RenderDocs = "RenderDocs";
    public const string RenderImages = "RenderImages";
    public const string Normalize = "Normalize";
    public const string RuleValidate = "RuleValidate";
    public const string RedTeam = "RedTeam";
    public const string Fix = "Fix";
    public const string Package = "Package";
    
    public static readonly string[] AllSteps = {
        Plan, Expand, Design, GenDocs, GenMedia, RenderDocs, RenderImages,
        Normalize, RuleValidate, RedTeam, Fix, Package
    };
}

// Document and Media Specifications for structured case design
public record LengthTarget
{
    [JsonPropertyName("min")]
    public required int Min { get; init; }
    
    [JsonPropertyName("max")]
    public required int Max { get; init; }
}

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
    public required LengthTarget LengthTarget { get; init; }
    
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
    
    [JsonPropertyName("visualReferenceIds")]
    public string[]? VisualReferenceIds { get; init; }
    
    public bool Deferred { get; init; } = false;
}

public record VisualReference
{
    [JsonPropertyName("referenceId")]
    public required string ReferenceId { get; init; }
    
    [JsonPropertyName("category")]
    public required string Category { get; init; } // "evidence", "suspect", "location"
    
    [JsonPropertyName("detailedDescription")]
    public required string DetailedDescription { get; init; }
    
    [JsonPropertyName("colorPalette")]
    public string[]? ColorPalette { get; init; }
    
    [JsonPropertyName("distinctiveFeatures")]
    public string[]? DistinctiveFeatures { get; init; }
    
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }
}

public record VisualConsistencyRegistry
{
    [JsonPropertyName("caseId")]
    public required string CaseId { get; init; }
    
    [JsonPropertyName("references")]
    public required Dictionary<string, VisualReference> References { get; init; }
    
    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
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
    public string? DocId { get; init; }
    public string? Notes { get; init; }
}

// Test models for PDF generation
public record DocumentTestRequest
{
    public required string DocId { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public int Words { get; init; }
    public required DocumentSection[] Sections { get; init; }
}

public record DocumentSection
{
    public required string Title { get; init; }
    public required string Content { get; init; }
}

public record DocumentTestResponse
{
    public required string MarkdownContent { get; init; }
    public required string PdfBlobUrl { get; init; }
    public required string DocumentType { get; init; }
    public required string FileName { get; init; }
}

// Normalization Models

public record NormalizationInput
{
    public required string CaseId { get; init; }
    public string? Difficulty { get; init; }
    public string? Timezone { get; init; }
    public string? PlanJson { get; init; }
    public string? ExpandedJson { get; init; }
    public string? DesignJson { get; init; }
    public required string[] Documents { get; init; }
    public required string[] Media { get; init; }
    public RenderedDocument[] RenderedDocs { get; init; } = Array.Empty<RenderedDocument>();
    public RenderedMedia[] RenderedMedia { get; init; } = Array.Empty<RenderedMedia>();
}

public record NormalizationResult
{
    public required string NormalizedJson { get; init; }
    public required NormalizedCaseBundle Bundle { get; init; }
    public required CaseManifest Manifest { get; init; }
    public required NormalizationLog Log { get; init; }
}

public record NormalizedCaseBundle
{
    public required string CaseId { get; init; }
    public required string Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public required string Timezone { get; init; }
    public string? Difficulty { get; init; }
    public required NormalizedDocument[] Documents { get; init; }
    public required NormalizedMedia[] Media { get; init; }
    public required GatingGraph GatingGraph { get; init; }
    public required NormalizedCaseMetadata Metadata { get; init; }
}

public record NormalizedDocument
{
    public required string DocId { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required string[] Sections { get; init; }
    public required int[] LengthTarget { get; init; }
    public required bool Gated { get; init; }
    public GatingRule? GatingRule { get; init; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public required string Content { get; init; }
    public required Dictionary<string, object> Metadata { get; init; }
}

public record NormalizedMedia
{
    public required string EvidenceId { get; init; }
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public required string Prompt { get; init; }
    public Dictionary<string, object>? Constraints { get; init; }
    public bool Deferred { get; init; } = false;
    public DateTime? CreatedAt { get; set; }
    public required Dictionary<string, object> Metadata { get; init; }
}

public record GatingGraph
{
    public required GatingNode[] Nodes { get; init; }
    public required GatingEdge[] Edges { get; init; }
    public bool HasCycles { get; init; }
    public string[] CycleDescription { get; init; } = Array.Empty<string>();
}

public record GatingNode
{
    public required string Id { get; init; }
    public required string Type { get; init; } // "document" or "evidence"
    public required bool Gated { get; init; }
    public string? UnlockAction { get; init; }
    public string[] RequiredIds { get; init; } = Array.Empty<string>();
}

public record GatingEdge
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Relationship { get; init; } // "unlocks", "requires"
}

public record CaseManifest
{
    public required string CaseId { get; init; }
    public required string Version { get; init; }
    public DateTime GeneratedAt { get; init; }
    public required string[] BundlePaths { get; init; }
    public required ManifestEntry[] Documents { get; init; }
    public required ManifestEntry[] Media { get; init; }
    public required Dictionary<string, string> FileHashes { get; init; }
    public required CaseVisibility Visibility { get; init; }
}

public record ManifestEntry
{
    public required string Id { get; init; }
    public required string RelativePath { get; init; }
    public required string Type { get; init; }
    public required bool Gated { get; init; }
    public required string Hash { get; init; }
    public long SizeBytes { get; init; }
}

public record CaseVisibility
{
    public required string[] AlwaysVisible { get; init; }
    public required string[] GatedVisible { get; init; }
    public required string[] HiddenUntilUnlocked { get; init; }
}

public record NormalizedCaseMetadata
{
    public required string GeneratedBy { get; init; }
    public required string Pipeline { get; init; }
    public DateTime GeneratedAt { get; init; }
    public required Dictionary<string, object> ValidationResults { get; init; }
    public required string[] AppliedRules { get; init; }
}

public record NormalizationLog
{
    public required string CaseId { get; init; }
    public required string Step { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;
    public required string Status { get; init; }
    public required LogEntry[] Entries { get; init; }
    public required ValidationResult[] ValidationResults { get; init; }
    public string? ErrorMessage { get; init; }
}

public record LogEntry
{
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; } // "INFO", "WARN", "ERROR"
    public required string Message { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}

public record ValidationResult
{
    public required string Rule { get; init; }
    public required string Status { get; init; } // "PASS", "FAIL", "WARN"
    public required string Description { get; init; }
    public string? Details { get; init; }
}

public record CaseBundle
{
    public required string CaseId { get; init; }
    public DateTime GeneratedAt { get; init; }
    public required string Timezone { get; init; }
    public required string Difficulty { get; init; }
    public required CaseCounts Counts { get; init; }
    public required CaseValidationSummary ValidationResults { get; init; }
    public required List<FileManifestEntry> Manifest { get; init; }
    public required string RedTeamAnalysis { get; init; }
}

public record CaseCounts
{
    public int Documents { get; init; }
    public int Media { get; init; }
    public int Suspects { get; init; }
}

public record CaseValidationSummary
{
    public required string TimestampsNormalized { get; init; }
    public required string AllIdsResolved { get; init; }
    public required string MediaIntegrity { get; init; }
}

public record FileManifestEntry
{
    public required string Filename { get; init; }
    public required string RelativePath { get; init; }
    public required string Sha256 { get; init; }
    public required string MimeType { get; init; }
}

public record LLMUsage
{
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
}

public record LLMResponse
{
    public required string Content { get; init; }
    public LLMUsage? Usage { get; init; }
}

public record RedTeamCacheEntry
{
    public required string ContentHash { get; init; }
    public required string Analysis { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string AnalysisType { get; init; } // "Global" or "Focused"
    public string[]? FocusAreas { get; init; } // For focused analysis
}