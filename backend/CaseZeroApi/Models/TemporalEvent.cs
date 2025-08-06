namespace CaseZeroApi.Models
{
    public class TemporalEvent
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty; // Unique identifier within the case
        public required string Title { get; set; }
        public string? Content { get; set; }
        public int TriggerTimeMinutes { get; set; } // Minutes from case start
        public TemporalEventType Type { get; set; } = TemporalEventType.Memo;
        public string? FileName { get; set; } // File path relative to case folder
        public bool IsTriggered { get; set; } = false;
        public DateTime? TriggeredAt { get; set; }
        public string? TriggerCondition { get; set; } // JSON string for complex conditions
        
        // Navigation properties
        public virtual Case Case { get; set; } = null!;
    }

    public enum TemporalEventType
    {
        Memo,           // Memo from police chief
        Witness,        // New witness statement
        Evidence,       // New evidence discovered
        LabUpdate,      // Laboratory update
        News,           // News/media update
        Phone,          // Phone call
        Email,          // Email received
        Document        // New document arrived
    }
}