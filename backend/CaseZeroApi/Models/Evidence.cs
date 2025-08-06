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
        
        // GDD Enhancements
        public bool IsUnlocked { get; set; } = true; // Whether evidence is available for analysis
        public bool RequiresAnalysis { get; set; } = false; // Whether evidence needs forensic analysis
        public EvidenceStatus AnalysisStatus { get; set; } = EvidenceStatus.Available;
        public string? AnalysisResult { get; set; } // Result of forensic analysis
        public DateTime? AnalysisRequestedAt { get; set; }
        public DateTime? AnalysisCompletedAt { get; set; }
        public string? DependsOnEvidenceIds { get; set; } // JSON array of evidence IDs this depends on
        public EvidenceCategory Category { get; set; } = EvidenceCategory.Physical;
        public EvidencePriority Priority { get; set; } = EvidencePriority.Medium;
        public string? Metadata { get; set; } // JSON metadata for specific evidence types
        
        // Navigation properties
        public virtual Case Case { get; set; } = null!;
        public virtual User? CollectedByUser { get; set; }
        public virtual ICollection<ForensicAnalysis> ForensicAnalyses { get; set; } = new List<ForensicAnalysis>();
    }

    public enum EvidenceStatus
    {
        Available,      // Ready for analysis
        Submitted,      // Sent to forensic lab
        InAnalysis,     // Being analyzed
        Completed,      // Analysis complete
        Inconclusive    // Analysis was inconclusive
    }

    public enum EvidenceCategory
    {
        Physical,       // Physical objects, weapons, etc.
        Digital,        // Videos, photos, audio files
        Document,       // PDFs, reports, emails
        Biological,     // DNA, blood, fingerprints
        Communication,  // Phone records, messages, emails
        Location,       // GPS data, cell tower data
        Witness,        // Witness statements, testimonies
        Technical       // Ballistics, toxicology, etc.
    }

    public enum EvidencePriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}