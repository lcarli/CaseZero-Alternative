namespace CaseZeroApi.Models
{
    /// <summary>
    /// Tracks which emails are visible to a user in a specific case session
    /// </summary>
    public class CaseSessionVisibleEmail
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual CaseSession? CaseSession { get; set; }
    }
}
