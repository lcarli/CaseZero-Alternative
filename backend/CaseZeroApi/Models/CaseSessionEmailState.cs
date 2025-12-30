namespace CaseZeroApi.Models
{
    /// <summary>
    /// Tracks email read state per user/case
    /// </summary>
    public class CaseSessionEmailState
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public DateTime? ReadAt { get; set; }
        public int OpenCount { get; set; } = 0;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual CaseSession? CaseSession { get; set; }
    }
}
