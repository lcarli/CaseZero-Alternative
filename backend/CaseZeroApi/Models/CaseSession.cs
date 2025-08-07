namespace CaseZeroApi.Models
{
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
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}