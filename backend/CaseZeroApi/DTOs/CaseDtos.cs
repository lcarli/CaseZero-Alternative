using CaseZeroApi.Models;

namespace CaseZeroApi.DTOs
{
    public class CaseDto
    {
        public required string Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public CaseStatus Status { get; set; }
        public CasePriority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public List<UserDto> AssignedUsers { get; set; } = new();
        public CaseProgressDto? UserProgress { get; set; }
        
        // GDD Enhancements
        public CaseType Type { get; set; }
        public DetectiveRank MinimumRankRequired { get; set; }
        public string? Location { get; set; }
        public DateTime? IncidentDate { get; set; }
        public string? BriefingText { get; set; }
        public string? VictimInfo { get; set; }
        public bool HasMultipleSuspects { get; set; }
        public int EstimatedDifficultyLevel { get; set; }
        public double MaxScore { get; set; }
        public List<EvidenceDto> Evidences { get; set; } = new();
        public List<SuspectDto> Suspects { get; set; } = new();
    }

    public class CaseProgressDto
    {
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public int EvidencesCollected { get; set; }
        public int InterviewsCompleted { get; set; }
        public int ReportsSubmitted { get; set; }
        public DateTime LastActivity { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class EvidenceDto
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public required string Name { get; set; }
        public required string Type { get; set; }
        public string? Description { get; set; }
        public string? FilePath { get; set; }
        public DateTime CollectedAt { get; set; }
        public bool IsUnlocked { get; set; }
        public bool RequiresAnalysis { get; set; }
        public EvidenceStatus AnalysisStatus { get; set; }
        public string? AnalysisResult { get; set; }
        public EvidenceCategory Category { get; set; }
        public EvidencePriority Priority { get; set; }
        public List<string> DependsOnEvidenceIds { get; set; } = new();
        public List<ForensicAnalysisDto> ForensicAnalyses { get; set; } = new();
    }

    public class ForensicAnalysisDto
    {
        public int Id { get; set; }
        public int EvidenceId { get; set; }
        public required string AnalysisType { get; set; }
        public ForensicAnalysisStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Results { get; set; }
        public double? ConfidenceLevel { get; set; }
        public bool IsMatch { get; set; }
    }

    public class SuspectDto
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public required string Name { get; set; }
        public string? Alias { get; set; }
        public int? Age { get; set; }
        public string? Description { get; set; }
        public string? Motive { get; set; }
        public string? Alibi { get; set; }
        public bool HasAlibiVerified { get; set; }
        public SuspectStatus Status { get; set; }
        public string? PhotoPath { get; set; }
    }

    public class CaseSubmissionDto
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public required string SuspectName { get; set; }
        public required string KeyEvidenceDescription { get; set; }
        public List<string> SupportingEvidenceIds { get; set; } = new();
        public required string Reasoning { get; set; }
        public DateTime SubmittedAt { get; set; }
        public SubmissionStatus Status { get; set; }
        public bool IsCorrectSuspect { get; set; }
        public bool IsValidEvidence { get; set; }
        public double Score { get; set; }
        public string? Feedback { get; set; }
    }

    public class CreateCaseSubmissionDto
    {
        public required string CaseId { get; set; }
        public required string SuspectName { get; set; }
        public required string KeyEvidenceDescription { get; set; }
        public List<string> SupportingEvidenceIds { get; set; } = new();
        public required string Reasoning { get; set; }
    }

    public class EmailDto
    {
        public int Id { get; set; }
        public string? CaseId { get; set; }
        public required string Subject { get; set; }
        public required string Content { get; set; }
        public string? Preview { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public EmailPriority Priority { get; set; }
        public EmailType Type { get; set; }
        public List<EmailAttachmentDto> Attachments { get; set; } = new();
        public string SenderName { get; set; } = string.Empty;
    }

    public class EmailAttachmentDto
    {
        public required string Name { get; set; }
        public required string Size { get; set; }
        public required string Type { get; set; }
    }

    public class DashboardDto
    {
        public UserStatsDto Stats { get; set; } = new();
        public List<CaseDto> Cases { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public List<EmailDto> RecentEmails { get; set; } = new();
    }

    public class UserStatsDto
    {
        public int CasesResolved { get; set; }
        public int CasesActive { get; set; }
        public double SuccessRate { get; set; }
        public double AverageRating { get; set; }
        public DetectiveRank Rank { get; set; }
        public int ExperiencePoints { get; set; }
        public string RankName { get; set; } = string.Empty;
    }

    public class RecentActivityDto
    {
        public required string Description { get; set; }
        public DateTime Date { get; set; }
        public string? CaseId { get; set; }
    }
}