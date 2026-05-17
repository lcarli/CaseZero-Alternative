namespace CaseZeroApi.Models
{
    public class CaseSubmission
    {
        public int Id { get; set; }
        public string CaseId { get; set; } = string.Empty;
        public string SubmittedByUserId { get; set; } = string.Empty;
        public required string SuspectName { get; set; }
        public string? SuspectId { get; set; } // If suspect is in system
        public required string KeyEvidenceDescription { get; set; }
        public string? SupportingEvidenceIds { get; set; } // JSON array of evidence IDs
        public required string Reasoning { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        
        // Evaluation results
        public SubmissionStatus Status { get; set; } = SubmissionStatus.UnderReview;
        public bool IsCorrectSuspect { get; set; } = false;
        public bool IsValidEvidence { get; set; } = false;
        public double Score { get; set; } = 0.0; // 0-100 score
        public string? Feedback { get; set; }
        public DateTime? EvaluatedAt { get; set; }
        public string? EvaluatedByUserId { get; set; }

        // v2 submission fields
        public string? RequestPayloadJson { get; set; }
        public int AttemptNumber { get; set; } = 1;

        // True when this submission counts toward the player's promotion / ranking.
        // After MaxAttempts have been used in graded mode, further submissions are
        // recorded with Graded = false and do not contribute to user stats.
        public bool Graded { get; set; } = true;

        // Per-category score components (0..weight). Persisted so the profile page
        // can show how a player typically performs in each scoring category without
        // re-running the solution engine. All four columns are nullable so existing
        // pre-migration rows can be ignored by the aggregator.
        public double? CulpritScore { get; set; }
        public double? EvidenceScore { get; set; }
        public double? AnalysisScore { get; set; }
        public double? QuestionsScore { get; set; }

        // Navigation properties
        public virtual Case Case { get; set; } = null!;
        public virtual User SubmittedByUser { get; set; } = null!;
        public virtual User? EvaluatedByUser { get; set; }
    }

    public enum SubmissionStatus
    {
        UnderReview,    // Submitted but not yet evaluated
        Approved,       // Correct solution
        Rejected,       // Incorrect solution
        NeedsRevision,  // Close but needs more work
        Archived        // Case closed without resolution
    }
}