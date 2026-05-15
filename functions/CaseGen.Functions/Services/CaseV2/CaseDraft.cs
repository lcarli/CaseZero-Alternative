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

    /// <summary>Builds a context summary for downstream prompts. Rich enough for coherence,
    /// trimmed enough to keep token cost predictable.</summary>
    public string ToSummaryJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        caseId = CaseId,
        metadata = new
        {
            title = Metadata.Title,
            description = Metadata.Description,
            location = Metadata.Location,
            incidentDate = Metadata.IncidentDate,
            openedAt = Metadata.OpenedAt,
            difficulty = Metadata.Difficulty,
            requiredRank = Metadata.RequiredRank,
            category = Metadata.Category,
            victim = Metadata.Victim,
            tags = Metadata.Tags
        },
        culpritId = CulpritId,
        // Suspects: use full profile if available, otherwise fall back to the stub
        suspects = (SuspectFull.Count > 0 ? SuspectFull.Select(s => new
        {
            s.Id, s.Name, s.Age, s.Occupation, s.Relationship,
            s.Motive, s.Alibi, s.AlibiVerified, s.Background,
            isCulprit = s.Id == CulpritId
        }).Cast<object>().ToList() : SuspectStubs.Select(s => new
        {
            s.Id, s.Name, role = s.Role, isCulprit = s.IsCulprit
        }).Cast<object>().ToList()),
        // Assets: full short description if available, body NOT included to keep tokens manageable
        assets = (AssetFull.Count > 0 ? AssetFull.Select(a => new
        {
            a.Id, a.Type, a.Title, a.Description, a.Visibility, a.Category
        }).Cast<object>().ToList() : AssetStubs.Select(a => new
        {
            a.Id, a.Type, a.Title, role = a.Role, a.Visibility
        }).Cast<object>().ToList()),
        // Result PDFs (forensics output)
        resultAssets = ResultAssets.Select(a => new { a.Id, a.Type, a.Title, a.Description }),
        timeline = Timeline.Select(t => new { t.Time, t.Event, t.Source, t.Verified, t.Importance }),
        temporalEvents = TemporalEvents.Select(t => new { t.Id, t.TriggerAtMinutes, t.Type, message = t.Payload?.Message ?? t.Payload?.EmailId ?? t.Payload?.AssetId }),
        analysisTypes = AnalysisTypes.Select(t => new { t.Type, t.DurationMinutes, t.AvailableFor }),
        // Forensics: include conclusionText so question/explanation tasks see the lab findings
        forensics = (ForensicFull.Count > 0 ? ForensicFull.Select(f => new
        {
            f.InputAssetId, f.AnalysisType, f.Findings, f.MatchedSuspectId, f.ConclusionText
        }).Cast<object>().ToList() : ForensicStubs.Select(f => new
        {
            f.InputAssetId, f.AnalysisType, f.Findings, f.MatchedSuspectId, role = f.Role
        }).Cast<object>().ToList()),
        // Briefing + follow-up emails: subjects only (bodies stay where they're written)
        briefing = new { Briefing.From, Briefing.Subject },
        followUpEmails = FollowUpEmails.Select(e => new { e.Id, e.From, e.Subject, e.SentAt, e.Visibility }),
        resultEmails = ResultEmails.Select(e => new { e.Id, e.From, e.Subject, e.Visibility })
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
