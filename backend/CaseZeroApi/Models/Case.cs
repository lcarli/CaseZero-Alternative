namespace CaseZeroApi.Models
{
    public class Case
    {
        public string Id { get; set; } = string.Empty;
        public required string Title { get; set; }
        public string? Description { get; set; }
        public CaseStatus Status { get; set; } = CaseStatus.Open;
        public CasePriority Priority { get; set; } = CasePriority.Medium;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }
        public string? ClosedByUserId { get; set; }
        
        // GDD Enhancements
        public CaseType Type { get; set; } = CaseType.Investigation;
        public DetectiveRank MinimumRankRequired { get; set; } = DetectiveRank.Detective;
        public string? Location { get; set; }
        public DateTime? IncidentDate { get; set; }
        public string? BriefingText { get; set; } // Initial briefing from police chief
        public string? VictimInfo { get; set; }
        public bool HasMultipleSuspects { get; set; } = true;
        public int EstimatedDifficultyLevel { get; set; } = 1; // 1-10 scale
        public string? CorrectSuspectName { get; set; } // For validation purposes
        public string? CorrectEvidenceIds { get; set; } // JSON array of key evidence IDs
        public double MaxScore { get; set; } = 100.0;
        public string? CaseNotes { get; set; } // Internal case notes
        
        // Navigation properties
        public virtual ICollection<UserCase> UserCases { get; set; } = new List<UserCase>();
        public virtual ICollection<CaseProgress> CaseProgresses { get; set; } = new List<CaseProgress>();
        public virtual ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
        public virtual ICollection<Suspect> Suspects { get; set; } = new List<Suspect>();
        public virtual ICollection<CaseSubmission> CaseSubmissions { get; set; } = new List<CaseSubmission>();
    }

    public enum CaseStatus
    {
        Open,
        InProgress,
        Resolved,
        Closed,
        Archived,       // Case archived without resolution
        UnderReview     // Submission pending review
    }

    public enum CasePriority
    {
        Low,
        Medium,
        High,
        Critical,
        Emergency
    }

    public enum CaseType
    {
        Investigation,  // Standard investigation
        ColdCase,       // Cold case reopened
        EmergencyResponse, // Active emergency
        FollowUp,       // Follow-up investigation
        Review,         // Case review
        Training        // Training scenario
    }
}