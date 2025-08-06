namespace CaseZeroApi.Models
{
    public class ForensicAnalysis
    {
        public int Id { get; set; }
        public int EvidenceId { get; set; }
        public string RequestedByUserId { get; set; } = string.Empty;
        public required string AnalysisType { get; set; } // DNA, Fingerprint, Ballistics, etc.
        public ForensicAnalysisStatus Status { get; set; } = ForensicAnalysisStatus.Requested;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string? Results { get; set; } // JSON containing analysis results
        public string? TechnicianNotes { get; set; }
        public double? ConfidenceLevel { get; set; } // 0.0 to 1.0
        public bool IsMatch { get; set; } = false; // For comparison analyses
        public string? ComparedAgainst { get; set; } // What this was compared against
        
        // Navigation properties
        public virtual Evidence Evidence { get; set; } = null!;
        public virtual User RequestedByUser { get; set; } = null!;
    }

    public enum ForensicAnalysisStatus
    {
        Requested,      // Analysis has been requested
        InProgress,     // Lab is working on it
        Completed,      // Analysis is complete
        Inconclusive,   // Results were inconclusive
        Failed,         // Analysis failed (sample contaminated, etc.)
        OnHold          // Analysis is on hold (more evidence needed)
    }

    public enum ForensicAnalysisType
    {
        DNA,
        Fingerprint,
        Ballistics,
        Toxicology,
        HandwritingAnalysis,
        VoiceAnalysis,
        DigitalForensics,
        Trace,          // Trace evidence (fibers, paint, etc.)
        Documentation,  // Document analysis
        Photography     // Photo enhancement/analysis
    }
}