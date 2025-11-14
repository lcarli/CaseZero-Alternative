using System.Text.Json.Serialization;

namespace CaseZeroApi.Models
{
    /// <summary>
    /// Represents the complete case object structure as defined in case.json
    /// This is used for loading case definitions from JSON files
    /// </summary>
    public class CaseObject
    {
        [JsonPropertyName("caseId")]
        public string CaseId { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public CaseMetadata Metadata { get; set; } = new();

        [JsonPropertyName("evidences")]
        public List<CaseEvidence> Evidences { get; set; } = new();

        [JsonPropertyName("suspects")]
        public List<CaseSuspect> Suspects { get; set; } = new();

        [JsonPropertyName("emails")]
        public List<CaseEmail> Emails { get; set; } = new();

        [JsonPropertyName("forensicAnalyses")]
        public List<CaseForensicAnalysis> ForensicAnalyses { get; set; } = new();

        [JsonPropertyName("temporalEvents")]
        public List<CaseTemporalEvent> TemporalEvents { get; set; } = new();

        [JsonPropertyName("timeline")]
        public List<CaseTimelineEvent> Timeline { get; set; } = new();

        [JsonPropertyName("solution")]
        public CaseSolution Solution { get; set; } = new();

        [JsonPropertyName("unlockLogic")]
        public CaseUnlockLogic UnlockLogic { get; set; } = new();

        [JsonPropertyName("gameMetadata")]
        public CaseGameMetadata GameMetadata { get; set; } = new();
    }

    public class CaseMetadata
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("startDateTime")]
        public DateTime StartDateTime { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("incidentDateTime")]
        public DateTime IncidentDateTime { get; set; }

        [JsonPropertyName("victimInfo")]
        public CaseVictimInfo VictimInfo { get; set; } = new();

        [JsonPropertyName("briefing")]
        public string Briefing { get; set; } = string.Empty;

        [JsonPropertyName("difficulty")]
        public int Difficulty { get; set; }

        [JsonPropertyName("estimatedDuration")]
        public string EstimatedDuration { get; set; } = string.Empty;

        [JsonPropertyName("minRankRequired")]
        public string MinRankRequired { get; set; } = string.Empty;
    }

    public class CaseVictimInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("occupation")]
        public string Occupation { get; set; } = string.Empty;

        [JsonPropertyName("causeOfDeath")]
        public string CauseOfDeath { get; set; } = string.Empty;
    }

    public class CaseEvidence
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("isUnlocked")]
        public bool IsUnlocked { get; set; }

        [JsonPropertyName("requiresAnalysis")]
        public bool RequiresAnalysis { get; set; }

        [JsonPropertyName("dependsOn")]
        public List<string> DependsOn { get; set; } = new();

        [JsonPropertyName("linkedSuspects")]
        public List<string> LinkedSuspects { get; set; } = new();

        [JsonPropertyName("analysisRequired")]
        public List<string> AnalysisRequired { get; set; } = new();

        [JsonPropertyName("unlockConditions")]
        public CaseUnlockConditions UnlockConditions { get; set; } = new();
    }

    public class CaseUnlockConditions
    {
        [JsonPropertyName("immediate")]
        public bool Immediate { get; set; }

        [JsonPropertyName("timeDelay")]
        public int? TimeDelay { get; set; }

        [JsonPropertyName("triggerEvent")]
        public string? TriggerEvent { get; set; }

        [JsonPropertyName("requiredEvidence")]
        public List<string> RequiredEvidence { get; set; } = new();

        [JsonPropertyName("requiredAnalysis")]
        public List<string> RequiredAnalysis { get; set; } = new();
    }

    public class CaseSuspect
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("alias")]
        public string? Alias { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("occupation")]
        public string Occupation { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("relationship")]
        public string Relationship { get; set; } = string.Empty;

        [JsonPropertyName("motive")]
        public string Motive { get; set; } = string.Empty;

        [JsonPropertyName("alibi")]
        public string Alibi { get; set; } = string.Empty;

        [JsonPropertyName("alibiVerified")]
        public bool AlibiVerified { get; set; }

        [JsonPropertyName("behavior")]
        public string Behavior { get; set; } = string.Empty;

        [JsonPropertyName("backgroundInfo")]
        public string BackgroundInfo { get; set; } = string.Empty;

        [JsonPropertyName("linkedEvidence")]
        public List<string> LinkedEvidence { get; set; } = new();

        [JsonPropertyName("comments")]
        public string Comments { get; set; } = string.Empty;

        [JsonPropertyName("isActualCulprit")]
        public bool IsActualCulprit { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("unlockConditions")]
        public CaseUnlockConditions UnlockConditions { get; set; } = new();
    }

    public class CaseForensicAnalysis
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("evidenceId")]
        public string EvidenceId { get; set; } = string.Empty;

        [JsonPropertyName("analysisType")]
        public string AnalysisType { get; set; } = string.Empty;

        [JsonPropertyName("responseTime")]
        public int ResponseTime { get; set; }

        [JsonPropertyName("resultFile")]
        public string ResultFile { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class CaseTemporalEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("triggerTime")]
        public int TriggerTime { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;
    }

    public class CaseTimelineEvent
    {
        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    public class CaseSolution
    {
        [JsonPropertyName("culprit")]
        public string Culprit { get; set; } = string.Empty;

        [JsonPropertyName("keyEvidence")]
        public string KeyEvidence { get; set; } = string.Empty;

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;

        [JsonPropertyName("requiredEvidence")]
        public List<string> RequiredEvidence { get; set; } = new();

        [JsonPropertyName("minimumScore")]
        public int MinimumScore { get; set; }
    }

    public class CaseUnlockLogic
    {
        [JsonPropertyName("progressionRules")]
        public List<CaseProgressionRule> ProgressionRules { get; set; } = new();

        [JsonPropertyName("analysisRules")]
        public List<CaseAnalysisRule> AnalysisRules { get; set; } = new();
    }

    public class CaseProgressionRule
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("unlocks")]
        public List<string> Unlocks { get; set; } = new();

        [JsonPropertyName("delay")]
        public int Delay { get; set; }
    }

    public class CaseAnalysisRule
    {
        [JsonPropertyName("evidenceId")]
        public string EvidenceId { get; set; } = string.Empty;

        [JsonPropertyName("analysisType")]
        public string AnalysisType { get; set; } = string.Empty;

        [JsonPropertyName("unlocks")]
        public List<string> Unlocks { get; set; } = new();

        [JsonPropertyName("critical")]
        public bool Critical { get; set; }
    }

    public class CaseGameMetadata
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; set; } = string.Empty;

        [JsonPropertyName("estimatedPlayTime")]
        public string EstimatedPlayTime { get; set; } = string.Empty;
    }

    public class CaseEmail
    {
        [JsonPropertyName("emailId")]
        public string EmailId { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("sentAt")]
        public string SentAt { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = string.Empty;

        [JsonPropertyName("attachments")]
        public List<string> Attachments { get; set; } = new();

        [JsonPropertyName("gated")]
        public bool Gated { get; set; }

        [JsonPropertyName("gatingRule")]
        public CaseEmailGatingRule? GatingRule { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class CaseEmailGatingRule
    {
        [JsonPropertyName("requiredDocuments")]
        public List<string> RequiredDocuments { get; set; } = new();

        [JsonPropertyName("requiredMedia")]
        public List<string> RequiredMedia { get; set; } = new();

        [JsonPropertyName("requiredEvidence")]
        public List<string> RequiredEvidence { get; set; } = new();

        [JsonPropertyName("unlockMessage")]
        public string UnlockMessage { get; set; } = string.Empty;
    }
}
