namespace CaseZeroApi.Models
{
    public class Evidence
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public required string Name { get; set; }
        public required string Type { get; set; }
        public string? Description { get; set; }
        public string? FilePath { get; set; }
        public string? CollectedByUserId { get; set; }
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual Case Case { get; set; } = null!;
        public virtual User? CollectedByUser { get; set; }
    }
}