namespace CaseZeroApi.Models
{
    public enum SessionStatus
    {
        Active,
        Paused,
        Completed
    }

    public class CaseSession
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; } = DateTime.UtcNow;
        public DateTime? SessionEnd { get; set; }
        public int SessionDurationMinutes { get; set; } = 0;
        public string? GameTimeAtStart { get; set; }
        public string? GameTimeAtEnd { get; set; }
        public bool IsActive { get; set; } = true;
        public SessionStatus Status { get; set; } = SessionStatus.Active;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<CaseSessionVisibleAsset> VisibleAssets { get; set; } = new List<CaseSessionVisibleAsset>();
        public virtual ICollection<CaseSessionVisibleEmail> VisibleEmails { get; set; } = new List<CaseSessionVisibleEmail>();
        public virtual ICollection<CaseSessionEmailState> EmailStates { get; set; } = new List<CaseSessionEmailState>();
    }
}