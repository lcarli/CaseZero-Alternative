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
    public CaseBible? CaseBible { get; set; }

    public PlotMetadata Metadata { get; set; } = new();
    public string CulpritId { get; set; } = string.Empty;
    public InvestigationBlueprint Blueprint { get; set; } = new();
    public CaseGraph CaseGraph { get; set; } = new();
    public EvidenceGraph EvidenceGraph { get; set; } = new();

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
    public SolverDiagnosticTrace? SolverTrace { get; set; }

    /// <summary>Builds a context summary for downstream prompts. Rich enough for coherence,
    /// trimmed enough to keep token cost predictable.</summary>
    public string ToSummaryJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        caseId = CaseId,
        caseBible = CaseBible,
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
        caseGraph = CaseGraph,
        investigationBlueprint = new
        {
            Blueprint.CrimeMechanism,
            Blueprint.CulpritObjective,
            Blueprint.CaseHook,
            locale = Blueprint.Locale,
            incidentSequence = Blueprint.IncidentSequence.Select(b => new
            {
                b.Id, b.Time, b.Event, b.PublicDescription, b.Visibility,
                b.Source, b.Verified, b.Importance, b.CausalRole
            }),
            clueLadder = Blueprint.ClueLadder.Select(c => new
            {
                c.Id, c.Discovery, c.Inference, c.SupportsSuspectId, c.Strength, c.SourceType
                , c.Role
            }),
            redHerrings = Blueprint.RedHerrings.Select(r => new
            {
                r.SuspectId, r.Suspicion, r.Verification, r.Resolution
            })
        },
        evidenceGraph = new
        {
            claims = EvidenceGraph.Claims.Select(claim => new
            {
                claim.ClueId,
                claim.Discovery,
                claim.Inference,
                claim.SupportsSuspectId,
                claim.Strength,
                claim.SourceType,
                claim.Role,
                claim.AssetIds
            }),
            redHerrings = EvidenceGraph.RedHerrings.Select(redHerring => new
            {
                redHerring.SuspectId,
                redHerring.Suspicion,
                redHerring.Verification,
                redHerring.Resolution,
                redHerring.AssetIds
            })
        },
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
            a.Id, a.Type, a.Title, a.Description, a.Visibility, a.Category,
            a.EvidenceRole, a.SubjectSuspectId, a.ImagePurpose,
            forensicInputClueIds = AssetStubs.FirstOrDefault(stub => stub.Id == a.Id)?.ForensicInputClueIds
        }).Cast<object>().ToList() : AssetStubs.Select(a => new
        {
            a.Id, a.Type, a.Title, role = a.Role, a.Visibility, a.EvidenceRole,
            a.SubjectSuspectId, a.ImagePurpose, a.ForensicInputClueIds
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
            f.InputAssetId, f.InputObjectId, f.AnalysisType, f.Findings, f.MatchedSuspectId,
            f.ProducedProperties, f.LimitationKeys, role = f.Role
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
    [JsonPropertyName("archetypeId")] public string ArchetypeId { get; set; } = string.Empty;
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = "pdf";
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("evidenceRole")] public string? EvidenceRole { get; set; }
    [JsonPropertyName("subjectSuspectId")] public string? SubjectSuspectId { get; set; }
    [JsonPropertyName("imagePurpose")] public string? ImagePurpose { get; set; }
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "initial";
    [JsonPropertyName("layoutHint")] public string LayoutHint { get; set; } = string.Empty;
    [JsonPropertyName("supportsClueIds")] public List<string> SupportsClueIds { get; set; } = new();
    [JsonPropertyName("forensicInputClueIds")] public List<string> ForensicInputClueIds { get; set; } = new();
    [JsonPropertyName("resolvesRedHerringSuspectIds")] public List<string> ResolvesRedHerringSuspectIds { get; set; } = new();
    [JsonPropertyName("introducesRedHerringSuspectIds")] public List<string> IntroducesRedHerringSuspectIds { get; set; } = new();
    [JsonPropertyName("containedObjectIds")] public List<string> ContainedObjectIds { get; set; } = new();
}

public class InvestigationBlueprint
{
    [JsonPropertyName("crimeMechanism")] public string CrimeMechanism { get; set; } = string.Empty;
    [JsonPropertyName("culpritObjective")] public string CulpritObjective { get; set; } = string.Empty;
    [JsonPropertyName("caseHook")] public string CaseHook { get; set; } = string.Empty;
    [JsonPropertyName("locale")] public CaseLocaleContext Locale { get; set; } = new();
    [JsonPropertyName("incidentSequence")] public List<CanonicalIncidentBeat> IncidentSequence { get; set; } = new();
    /// <summary>
    /// Transitional generator input. CaseGraphProjection is the only supported bridge
    /// into the canonical graph and cannot overwrite a hand-authored CaseGraph.
    /// </summary>
    [JsonPropertyName("clueLadder")] public List<CanonicalClue> ClueLadder { get; set; } = new();
    [JsonPropertyName("redHerrings")] public List<CanonicalRedHerring> RedHerrings { get; set; } = new();
}

public class CanonicalIncidentBeat
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("time")] public string Time { get; set; } = string.Empty;
    [JsonPropertyName("event")] public string Event { get; set; } = string.Empty;
    [JsonPropertyName("publicDescription")] public string? PublicDescription { get; set; }
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "private";
    [JsonPropertyName("source")] public string Source { get; set; } = "investigation";
    [JsonPropertyName("verified")] public bool Verified { get; set; }
    [JsonPropertyName("importance")] public string Importance { get; set; } = "medium";
    [JsonPropertyName("causalRole")] public string CausalRole { get; set; } = string.Empty;
}

public class CaseLocaleContext
{
    [JsonPropertyName("timeZoneId")] public string TimeZoneId { get; set; } = "UTC";
    [JsonPropertyName("utcOffset")] public string UtcOffset { get; set; } = "+00:00";
    [JsonPropertyName("policeAgency")] public string PoliceAgency { get; set; } = string.Empty;
    [JsonPropertyName("investigatorName")] public string InvestigatorName { get; set; } = string.Empty;
    [JsonPropertyName("investigatorEmail")] public string InvestigatorEmail { get; set; } = string.Empty;
}

public class CanonicalClue
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("discovery")] public string Discovery { get; set; } = string.Empty;
    [JsonPropertyName("inference")] public string Inference { get; set; } = string.Empty;
    [JsonPropertyName("supportsSuspectId")] public string? SupportsSuspectId { get; set; }
    [JsonPropertyName("strength")] public string Strength { get; set; } = "supporting";
    [JsonPropertyName("sourceType")] public string SourceType { get; set; } = "document";
    [JsonPropertyName("role")] public string Role { get; set; } = "context";
    [JsonPropertyName("observationIds")] public List<string> ObservationIds { get; set; } = new();
}

public class CanonicalRedHerring
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("suspectId")] public string SuspectId { get; set; } = string.Empty;
    [JsonPropertyName("suspicion")] public string Suspicion { get; set; } = string.Empty;
    [JsonPropertyName("verification")] public string Verification { get; set; } = string.Empty;
    [JsonPropertyName("resolution")] public string Resolution { get; set; } = string.Empty;
    [JsonPropertyName("suspicionObservationIds")] public List<string> SuspicionObservationIds { get; set; } = new();
    [JsonPropertyName("verificationObservationIds")] public List<string> VerificationObservationIds { get; set; } = new();
}

public class ForensicOutcomeStub
{
    [JsonPropertyName("inputAssetId")] public string InputAssetId { get; set; } = string.Empty;
    [JsonPropertyName("inputObjectId")] public string InputObjectId { get; set; } = string.Empty;
    [JsonPropertyName("analysisType")] public string AnalysisType { get; set; } = string.Empty;
    [JsonPropertyName("findings")] public bool Findings { get; set; }
    [JsonPropertyName("matchedSuspectId")] public string? MatchedSuspectId { get; set; }
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("supportsClueIds")] public List<string> SupportsClueIds { get; set; } = new();
    [JsonPropertyName("producedProperties")] public List<ForensicObservationProperty> ProducedProperties { get; set; } = new();
    [JsonPropertyName("limitationKeys")] public List<string> LimitationKeys { get; set; } = new();
    [JsonPropertyName("referenceSampleObjectId")] public string? ReferenceSampleObjectId { get; set; }
    [JsonPropertyName("chainOfCustodyObservationId")] public string? ChainOfCustodyObservationId { get; set; }
    [JsonPropertyName("resultLayoutId")] public string ResultLayoutId { get; set; } = "ForensicReport";
}

public class QuestionTopic
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("topic")] public string Topic { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public double Weight { get; set; } = 1.0;
    [JsonPropertyName("supportingEvidenceIds")] public List<string> SupportingEvidenceIds { get; set; } = new();
}
