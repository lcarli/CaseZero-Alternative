namespace CaseZeroApi.Models
{
    public class ForensicRequest
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string EvidenceId { get; set; } = string.Empty;
        public string EvidenceName { get; set; } = string.Empty;
        public string AnalysisType { get; set; } = string.Empty; // DNA, Fingerprint, DigitalForensics, Ballistics
        public DateTime RequestedAt { get; set; } // GameTime when requested
        public DateTime EstimatedCompletionTime { get; set; } // GameTime when it will be ready
        public DateTime? CompletedAt { get; set; } // GameTime when actually completed
        public string Status { get; set; } = "pending"; // pending, in-progress, completed, cancelled
        public string? ResultDocumentId { get; set; } // Reference to the document with results
        public string? Notes { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
