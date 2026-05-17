using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models.CaseV2;

/// <summary>
/// Inputs for the case.json v2 generation pipeline.
/// </summary>
public class GenerateCaseV2Request
{
    [JsonPropertyName("caseId")] public string? CaseId { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("theme")] public string? Theme { get; set; }
    [JsonPropertyName("location")] public string? Location { get; set; }
    [JsonPropertyName("difficulty")] public string? Difficulty { get; set; }
    [JsonPropertyName("requiredRank")] public string? RequiredRank { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; } = "en-US";
    [JsonPropertyName("seed")] public int? Seed { get; set; }
    [JsonPropertyName("writeToDisk")] public bool WriteToDisk { get; set; } = true;
}

public class GenerateCaseV2Response
{
    public string CaseId { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string CaseJson { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
    public Dictionary<string, double> StageLatencyMs { get; set; } = new();
    public int AssetsRenderedPdfs { get; set; }
    public int AssetsRenderedImages { get; set; }
    public int AssetsSkipped { get; set; }
    public List<string> AssetRenderingErrors { get; set; } = new();
    public int BlobsPublished { get; set; }
    public List<string> AutoFixesApplied { get; set; } = new();
    public bool RefineAttempted { get; set; }
    public int RefineErrorsBefore { get; set; }
    public int RefineErrorsAfter { get; set; }
    public int RefineIterations { get; set; }
    public bool RedTeamRerun { get; set; }
    public Services.CaseV2.ConsistencyReport? Consistency { get; set; }
    public Services.CaseV2.Tasks.RedTeamTask.Report? RedTeam { get; set; }
    public Services.CaseV2.Tasks.RedTeamTask.Report? RedTeamInitial { get; set; }
    public List<string> RedTeamVerdictTrajectory { get; set; } = new();
    public Services.CaseV2.Tasks.SolverTask.SolverResult? Solver { get; set; }
}

// ----- Stage 1: Plot -----
public class PlotResult
{
    [JsonPropertyName("metadata")] public PlotMetadata Metadata { get; set; } = new();
    [JsonPropertyName("suspects")] public List<PlotSuspect> Suspects { get; set; } = new();
    [JsonPropertyName("culpritId")] public string CulpritId { get; set; } = string.Empty;
    [JsonPropertyName("briefingEmail")] public PlotBriefingEmail BriefingEmail { get; set; } = new();
}

public class PlotMetadata
{
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("location")] public string Location { get; set; } = string.Empty;
    [JsonPropertyName("incidentDate")] public string IncidentDate { get; set; } = string.Empty;
    [JsonPropertyName("openedAt")] public string OpenedAt { get; set; } = string.Empty;
    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "Detective";
    [JsonPropertyName("requiredRank")] public string RequiredRank { get; set; } = "Detective";
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("briefing")] public string Briefing { get; set; } = string.Empty;
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("victim")] public PlotVictim? Victim { get; set; }
}

public class PlotVictim
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("age")] public int Age { get; set; }
    [JsonPropertyName("occupation")] public string Occupation { get; set; } = string.Empty;
}

public class PlotSuspect
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("age")] public int? Age { get; set; }
    [JsonPropertyName("occupation")] public string? Occupation { get; set; }
    [JsonPropertyName("relationship")] public string? Relationship { get; set; }
    [JsonPropertyName("motive")] public string? Motive { get; set; }
    [JsonPropertyName("alibi")] public string? Alibi { get; set; }
    [JsonPropertyName("alibiVerified")] public bool AlibiVerified { get; set; }
    [JsonPropertyName("background")] public string? Background { get; set; }
}

public class PlotBriefingEmail
{
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
}

// ----- Stage 2: Evidence -----
public class EvidenceResult
{
    [JsonPropertyName("assets")] public List<EvidenceAsset> Assets { get; set; } = new();
    [JsonPropertyName("timeline")] public List<EvidenceTimelineEntry> Timeline { get; set; } = new();
    [JsonPropertyName("temporalEvents")] public List<EvidenceTemporalEvent> TemporalEvents { get; set; } = new();
    [JsonPropertyName("emails")] public List<EvidenceEmail> Emails { get; set; } = new();
}

public class EvidenceAsset
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = "pdf";
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "initial";
    [JsonPropertyName("category")] public string? Category { get; set; }

    /// <summary>Long-form body used by <see cref="Services.CaseV2.AssetRenderingService"/>:
    /// markdown for pdf/document assets (rendered with QuestPDF) or an image prompt for photo/image assets.</summary>
    [JsonPropertyName("body")] public string? Body { get; set; }

    /// <summary>Structured document model — preferred over <see cref="Body"/> for
    /// pdf/document/digital assets so the renderer can produce real tables,
    /// transcripts, key/value blocks, etc.</summary>
    [JsonPropertyName("bodyDoc")] public EvidenceDocument? BodyDoc { get; set; }
}

public class EvidenceTimelineEntry
{
    [JsonPropertyName("time")] public string Time { get; set; } = string.Empty;
    [JsonPropertyName("event")] public string Event { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("verified")] public bool Verified { get; set; }
    [JsonPropertyName("importance")] public string? Importance { get; set; }
}

public class EvidenceTemporalEvent
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("triggerAtMinutes")] public int TriggerAtMinutes { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "memo";
    [JsonPropertyName("payload")] public EvidenceTemporalPayload? Payload { get; set; }
}

public class EvidenceTemporalPayload
{
    [JsonPropertyName("emailId")] public string? EmailId { get; set; }
    [JsonPropertyName("assetId")] public string? AssetId { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class EvidenceEmail
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
    [JsonPropertyName("sentAt")] public string SentAt { get; set; } = string.Empty;
    [JsonPropertyName("priority")] public string? Priority { get; set; }
    [JsonPropertyName("attachments")] public List<string> Attachments { get; set; } = new();
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "hidden";
}

// ----- Stage 3: Forensics -----
public class ForensicsResult
{
    [JsonPropertyName("analysisTypes")] public List<ForensicsAnalysisType> AnalysisTypes { get; set; } = new();
    [JsonPropertyName("forensicOutcomes")] public List<ForensicsOutcome> ForensicOutcomes { get; set; } = new();
    [JsonPropertyName("resultAssets")] public List<EvidenceAsset> ResultAssets { get; set; } = new();
    [JsonPropertyName("resultEmails")] public List<EvidenceEmail> ResultEmails { get; set; } = new();
}

public class ForensicsAnalysisType
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("durationMinutes")] public int DurationMinutes { get; set; }
    [JsonPropertyName("availableFor")] public List<string> AvailableFor { get; set; } = new();
}

public class ForensicsOutcome
{
    [JsonPropertyName("inputAssetId")] public string InputAssetId { get; set; } = string.Empty;
    [JsonPropertyName("analysisType")] public string AnalysisType { get; set; } = string.Empty;
    [JsonPropertyName("findings")] public bool Findings { get; set; }
    [JsonPropertyName("matchedSuspectId")] public string? MatchedSuspectId { get; set; }
    [JsonPropertyName("matchedAssetId")] public string? MatchedAssetId { get; set; }
    [JsonPropertyName("conclusionText")] public string? ConclusionText { get; set; }
    [JsonPropertyName("resultAssetId")] public string? ResultAssetId { get; set; }
    [JsonPropertyName("resultEmailId")] public string? ResultEmailId { get; set; }
}

// ----- Stage 4: Rules -----
public class RulesResult
{
    [JsonPropertyName("rules")] public List<RulesRule> Rules { get; set; } = new();
}

public class RulesRule
{
    [JsonPropertyName("ruleId")] public string RuleId { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("trigger")] public RulesTrigger Trigger { get; set; } = new();
    [JsonPropertyName("actions")] public List<RulesAction> Actions { get; set; } = new();
}

public class RulesTrigger
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("inputAssetId")] public string? InputAssetId { get; set; }
    [JsonPropertyName("analysisType")] public string? AnalysisType { get; set; }
    [JsonPropertyName("emailId")] public string? EmailId { get; set; }
    [JsonPropertyName("assetId")] public string? AssetId { get; set; }
    [JsonPropertyName("suspectId")] public string? SuspectId { get; set; }
    [JsonPropertyName("atMinutes")] public int? AtMinutes { get; set; }
    [JsonPropertyName("operator")] public string? Operator { get; set; }
    [JsonPropertyName("conditions")] public List<RulesTrigger>? Conditions { get; set; }
}

public class RulesAction
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("emailId")] public string? EmailId { get; set; }
    [JsonPropertyName("assetId")] public string? AssetId { get; set; }
    [JsonPropertyName("suspectId")] public string? SuspectId { get; set; }
    [JsonPropertyName("level")] public string? Level { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("verified")] public bool? Verified { get; set; }
}

// ----- Stage 5: Solution -----
public class SolutionResult
{
    [JsonPropertyName("culpritId")] public string CulpritId { get; set; } = string.Empty;
    [JsonPropertyName("requiredEvidenceIds")] public List<string> RequiredEvidenceIds { get; set; } = new();
    [JsonPropertyName("requiredAnalysisIds")] public List<string> RequiredAnalysisIds { get; set; } = new();
    [JsonPropertyName("questions")] public List<SolutionQuestion> Questions { get; set; } = new();
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = string.Empty;
    [JsonPropertyName("minimumScore")] public double MinimumScore { get; set; } = 0.7;
    [JsonPropertyName("maxAttempts")] public int MaxAttempts { get; set; } = 3;
    [JsonPropertyName("partialCreditRules")] public SolutionPartialCredit PartialCreditRules { get; set; } = new();
}

public class SolutionQuestion
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
    [JsonPropertyName("options")] public List<SolutionOption> Options { get; set; } = new();
    [JsonPropertyName("correctOptionId")] public string CorrectOptionId { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public double Weight { get; set; } = 1.0;
}

public class SolutionOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
}

public class SolutionPartialCredit
{
    [JsonPropertyName("culpritWeight")] public double CulpritWeight { get; set; } = 0.4;
    [JsonPropertyName("evidenceWeight")] public double EvidenceWeight { get; set; } = 0.2;
    [JsonPropertyName("analysisWeight")] public double AnalysisWeight { get; set; } = 0.2;
    [JsonPropertyName("questionsWeight")] public double QuestionsWeight { get; set; } = 0.2;
}
