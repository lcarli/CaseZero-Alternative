namespace CaseZeroApi.Models
{
    public class Suspect
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public required string Name { get; set; }
        public string? Alias { get; set; }
        public int? Age { get; set; }
        public string? Description { get; set; }
        public string? BackgroundInfo { get; set; }
        public string? Motive { get; set; }
        public string? Alibi { get; set; }
        public bool HasAlibiVerified { get; set; } = false;
        public SuspectStatus Status { get; set; } = SuspectStatus.PersonOfInterest;
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public string? AddedByUserId { get; set; }
        
        // Guilt/innocence (for system validation)
        public bool IsActualCulprit { get; set; } = false; // Internal system field
        public string? PhotoPath { get; set; }
        public string? ContactInfo { get; set; }
        public string? LastKnownLocation { get; set; }
        
        // Navigation properties
        public virtual Case Case { get; set; } = null!;
        public virtual User? AddedByUser { get; set; }
    }

    public enum SuspectStatus
    {
        PersonOfInterest,   // Initial suspect
        Active,             // Under active investigation
        Cleared,            // Cleared of suspicion
        Charged,            // Formally charged
        Convicted,          // Found guilty
        Unknown             // Status unknown
    }
}