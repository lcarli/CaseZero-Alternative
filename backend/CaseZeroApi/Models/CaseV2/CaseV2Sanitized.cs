namespace CaseZeroApi.Models.CaseV2;

public class CaseV2Sanitized
{
    public string Version { get; set; } = "2.0";
    public string CaseId { get; set; } = string.Empty;
    public CaseV2Metadata Metadata { get; set; } = new();
    public List<CaseV2Asset> Assets { get; set; } = new();
    public List<CaseV2Email> Emails { get; set; } = new();
    public List<CaseV2Suspect> Suspects { get; set; } = new();
    public List<CaseV2TimelineEvent>? Timeline { get; set; }
    public CaseV2ForensicsDefaultsClient? ForensicsDefaults { get; set; }
    public SanitizedSolution? Solution { get; set; }
    public CaseV2GameMetadataClient? GameMetadata { get; set; }
}

public class CaseV2ForensicsDefaultsClient
{
    public List<CaseV2AnalysisType> AnalysisTypes { get; set; } = new();
}

public class CaseV2GameMetadataClient
{
    public string SchemaVersion { get; set; } = "2.0";
    public string CreatedAt { get; set; } = string.Empty;
    public List<string>? Tags { get; set; }
    public List<string>? ContentWarnings { get; set; }
    public List<string>? Localizations { get; set; }
}

public class SanitizedSolution
{
    public List<SanitizedQuestion> Questions { get; set; } = new();
    public double MinimumScore { get; set; }
    public int MaxAttempts { get; set; }
}

public class SanitizedQuestion
{
    public string Id { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public List<CaseV2QuestionOption> Options { get; set; } = new();
    public double? Weight { get; set; }
}
