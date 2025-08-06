namespace CaseZeroApi.Models
{
    public class CaseProgress
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public int EvidencesCollected { get; set; } = 0;
        public int InterviewsCompleted { get; set; } = 0;
        public int ReportsSubmitted { get; set; } = 0;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public double CompletionPercentage { get; set; } = 0.0;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Case Case { get; set; } = null!;
    }
}