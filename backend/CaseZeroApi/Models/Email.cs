namespace CaseZeroApi.Models
{
    public class Email
    {
        public int Id { get; set; }
        public string? CaseId { get; set; } // Associated case, if any
        public string ToUserId { get; set; } = string.Empty;
        public string FromUserId { get; set; } = string.Empty;
        public required string Subject { get; set; }
        public required string Content { get; set; }
        public string? Preview { get; set; } // Short preview text
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public EmailPriority Priority { get; set; } = EmailPriority.Normal;
        public EmailType Type { get; set; } = EmailType.General;
        public string? Attachments { get; set; } // JSON array of attachment info
        public bool IsSystemGenerated { get; set; } = false;
        
        // Navigation properties
        public virtual User ToUser { get; set; } = null!;
        public virtual User FromUser { get; set; } = null!;
        public virtual Case? Case { get; set; }
    }

    public enum EmailPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    public enum EmailType
    {
        General,            // General communication
        CaseAssignment,     // New case assignment
        CaseBriefing,       // Case briefing from chief
        ForensicResults,    // Forensic analysis results
        EvidenceNotification, // New evidence available
        SystemNotification, // System-generated notification
        CaseUpdate,         // Case status update
        PromotionNotice     // Career advancement notice
    }
}