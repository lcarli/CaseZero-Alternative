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
        
        // Navigation properties
        public virtual ICollection<UserCase> UserCases { get; set; } = new List<UserCase>();
        public virtual ICollection<CaseProgress> CaseProgresses { get; set; } = new List<CaseProgress>();
        public virtual ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
    }

    public enum CaseStatus
    {
        Open,
        InProgress,
        Resolved,
        Closed
    }

    public enum CasePriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}