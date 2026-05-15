namespace CaseZeroApi.Models
{
    /// <summary>
    /// Tracks which assets are visible to a user in a specific case session
    /// </summary>
    public class CaseSessionVisibleAsset
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual CaseSession? CaseSession { get; set; }
    }
}
