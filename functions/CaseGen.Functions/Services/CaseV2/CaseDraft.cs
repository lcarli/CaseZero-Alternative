using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Slim, evolving case state passed between micro-tasks. Each task gets a small
/// summarised view of upstream output (IDs + 1-line role) plus its specific input.
/// Full bodies stay in <see cref="FullPlot"/> / <see cref="FullEvidence"/> / etc.
/// </summary>
public class CaseDraft
{
    public string CaseId { get; set; } = string.Empty;
    public GenerateCaseV2Request Request { get; set; } = new();

    public PlotMetadata Metadata { get; set; } = new();
    public string CulpritId { get; set; } = string.Empty;

    public List<SuspectStub> SuspectStubs { get; set; } = new();
    public List<PlotSuspect> SuspectFull { get; set; } = new();

    public List<AssetStub> AssetStubs { get; set; } = new();
    public List<EvidenceAsset> AssetFull { get; set; } = new();
    public List<EvidenceAsset> ResultAssets { get; set; } = new();

    public List<EvidenceTimelineEntry> Timeline { get; set; } = new();
    public List<EvidenceTemporalEvent> TemporalEvents { get; set; } = new();

    public List<ForensicsAnalysisType> AnalysisTypes { get; set; } = new();
    public List<ForensicOutcomeStub> ForensicStubs { get; set; } = new();
    public List<ForensicsOutcome> ForensicFull { get; set; } = new();
    public List<EvidenceEmail> ResultEmails { get; set; } = new();

    public PlotBriefingEmail Briefing { get; set; } = new();
    public List<EvidenceEmail> FollowUpEmails { get; set; } = new();

    public List<RulesRule> Rules { get; set; } = new();

    public List<string> RequiredEvidenceIds { get; set; } = new();
    public List<string> RequiredAnalysisIds { get; set; } = new();
    public List<QuestionTopic> QuestionTopics { get; set; } = new();
    public List<SolutionQuestion> Questions { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Builds a compact JSON summary for downstream prompts (avoid token bloat).</summary>
    public string ToSummaryJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        caseId = CaseId,
        metadata = new
        {
            title = Metadata.Title,
            location = Metadata.Location,
            difficulty = Metadata.Difficulty,
            requiredRank = Metadata.RequiredRank,
            category = Metadata.Category,
            victim = Metadata.Victim
        },
        culpritId = CulpritId,
        suspects = SuspectStubs.Select(s => new { s.Id, s.Name, s.Role }),
        assets = AssetStubs.Select(a => new { a.Id, a.Type, a.Title, a.Role, a.Visibility }),
        forensics = ForensicStubs.Select(f => new { f.InputAssetId, f.AnalysisType, f.Findings, f.MatchedSuspectId, f.Role }),
        analysisTypes = AnalysisTypes.Select(t => new { t.Type, t.AvailableFor })
    }, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });
}

public class SuspectStub
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("isCulprit")] public bool IsCulprit { get; set; }
}

public class AssetStub
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = "pdf";
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "initial";
}

public class ForensicOutcomeStub
{
    [JsonPropertyName("inputAssetId")] public string InputAssetId { get; set; } = string.Empty;
    [JsonPropertyName("analysisType")] public string AnalysisType { get; set; } = string.Empty;
    [JsonPropertyName("findings")] public bool Findings { get; set; }
    [JsonPropertyName("matchedSuspectId")] public string? MatchedSuspectId { get; set; }
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
}

public class QuestionTopic
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("topic")] public string Topic { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public double Weight { get; set; } = 1.0;
}
