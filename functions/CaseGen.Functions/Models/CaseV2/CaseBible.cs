using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models.CaseV2;

public sealed class CaseBible
{
    [JsonPropertyName("crimeFactId")] public string CrimeFactId { get; set; } = "";
    [JsonPropertyName("entities")] public List<CaseEntity> Entities { get; set; } = new();
    [JsonPropertyName("facts")] public List<CaseFact> Facts { get; set; } = new();
    [JsonPropertyName("claims")] public List<CaseClaim> Claims { get; set; } = new();
    [JsonPropertyName("sources")] public List<CaseSourcePlan> Sources { get; set; } = new();
    [JsonPropertyName("chronology")] public List<CaseChronologyEntry> Chronology { get; set; } = new();
    [JsonPropertyName("analysisTypes")] public List<ForensicsAnalysisType> AnalysisTypes { get; set; } = new();
    [JsonPropertyName("outcomes")] public List<CaseForensicPlan> Outcomes { get; set; } = new();
    [JsonPropertyName("alibis")] public List<CaseAlibiPlan> Alibis { get; set; } = new();
    [JsonPropertyName("proofSets")] public List<CaseProofSet> ProofSets { get; set; } = new();
    [JsonPropertyName("primaryProofSetId")] public string PrimaryProofSetId { get; set; } = "";
    [JsonPropertyName("questions")] public List<CaseQuestionPlan> Questions { get; set; } = new();
    [JsonPropertyName("explanationFactIds")] public List<string> ExplanationFactIds { get; set; } = new();
}

public sealed class CaseEntity
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("knownFactIds")] public List<string> KnownFactIds { get; set; } = new();
}

public sealed class CaseTimeWindow
{
    [JsonPropertyName("precision")] public string Precision { get; set; } = "";
    [JsonPropertyName("earliest")] public string Earliest { get; set; } = "";
    [JsonPropertyName("latest")] public string Latest { get; set; } = "";
}

public sealed class CaseFact
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("statement")] public string Statement { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("entityIds")] public List<string> EntityIds { get; set; } = new();
    [JsonPropertyName("time")] public CaseTimeWindow? Time { get; set; }
    [JsonPropertyName("isPrivate")] public bool IsPrivate { get; set; }
    [JsonPropertyName("decisive")] public bool Decisive { get; set; }
}

public sealed class CaseClaim
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("statement")] public string Statement { get; set; } = "";
    [JsonPropertyName("speakerEntityId")] public string SpeakerEntityId { get; set; } = "";
    [JsonPropertyName("truthStatus")] public string TruthStatus { get; set; } = "";
    [JsonPropertyName("relatedFactIds")] public List<string> RelatedFactIds { get; set; } = new();
    [JsonPropertyName("time")] public CaseTimeWindow? Time { get; set; }
}

public sealed class CaseSourcePlan
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("authorEntityId")] public string AuthorEntityId { get; set; } = "";
    [JsonPropertyName("from")] public string? From { get; set; }
    [JsonPropertyName("recordedAt")] public string RecordedAt { get; set; } = "";
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "initial";
    [JsonPropertyName("factIds")] public List<string> FactIds { get; set; } = new();
    [JsonPropertyName("claimIds")] public List<string> ClaimIds { get; set; } = new();
    [JsonPropertyName("attachments")] public List<string> Attachments { get; set; } = new();
    [JsonPropertyName("discovery")] public RulesTrigger? Discovery { get; set; }
}

public sealed class CaseChronologyEntry
{
    [JsonPropertyName("referenceId")] public string ReferenceId { get; set; } = "";
    [JsonPropertyName("sourceId")] public string SourceId { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("importance")] public string Importance { get; set; } = "medium";
}

public sealed class CaseForensicPlan
{
    [JsonPropertyName("inputAssetId")] public string InputAssetId { get; set; } = "";
    [JsonPropertyName("analysisType")] public string AnalysisType { get; set; } = "";
    [JsonPropertyName("findings")] public bool Findings { get; set; }
    [JsonPropertyName("matchedSuspectId")] public string? MatchedSuspectId { get; set; }
    [JsonPropertyName("matchBasis")] public string MatchBasis { get; set; } = "none";
    [JsonPropertyName("factIds")] public List<string> FactIds { get; set; } = new();
    [JsonPropertyName("resultAssetId")] public string? ResultAssetId { get; set; }
    [JsonPropertyName("resultEmailId")] public string? ResultEmailId { get; set; }
    [JsonPropertyName("limitations")] public string Limitations { get; set; } = "";
}

public sealed class CaseAlibiPlan
{
    [JsonPropertyName("suspectId")] public string SuspectId { get; set; } = "";
    [JsonPropertyName("verifiedFactIds")] public List<string> VerifiedFactIds { get; set; } = new();
    [JsonPropertyName("sourceIds")] public List<string> SourceIds { get; set; } = new();
}

public sealed class CaseProofSet
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("conclusionFactId")] public string ConclusionFactId { get; set; } = "";
    [JsonPropertyName("factIds")] public List<string> FactIds { get; set; } = new();
    [JsonPropertyName("sourceIds")] public List<string> SourceIds { get; set; } = new();
    [JsonPropertyName("analysisIds")] public List<string> AnalysisIds { get; set; } = new();
}

public sealed class CaseQuestionPlan
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("topic")] public string Topic { get; set; } = "";
    [JsonPropertyName("weight")] public double Weight { get; set; } = 1;
    [JsonPropertyName("answer")] public string Answer { get; set; } = "";
    [JsonPropertyName("factIds")] public List<string> FactIds { get; set; } = new();
    [JsonPropertyName("sourceIds")] public List<string> SourceIds { get; set; } = new();
}

public class CaseAuthoredContent
{
    [JsonPropertyName("support")] public List<CaseContentSupport> Support { get; set; } = new();
}

public sealed class CaseContentSupport
{
    [JsonPropertyName("referenceId")] public string ReferenceId { get; set; } = "";
    [JsonPropertyName("excerpt")] public string Excerpt { get; set; } = "";
}
