using Microsoft.AspNetCore.Identity;

namespace CaseZeroApi.Models
{
    public class User : IdentityUser
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string PersonalEmail { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? BadgeNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool EmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationSentAt { get; set; }
        
        // GDD Career Progression Enhancements
        public DetectiveRank Rank { get; set; } = DetectiveRank.Rook;
        public int ExperiencePoints { get; set; } = 0;
        public int CasesResolved { get; set; } = 0;
        public int CasesFailed { get; set; } = 0;
        public double SuccessRate { get; set; } = 0.0;
        public double AverageScore { get; set; } = 0.0;
        public DateTime? LastPromotionDate { get; set; }
        public string? Specializations { get; set; } // JSON array of specialization areas
        public bool CanAccessHighPriorityCases { get; set; } = false;
        
        // Navigation properties
        public virtual ICollection<UserCase> UserCases { get; set; } = new List<UserCase>();
        public virtual ICollection<CaseProgress> CaseProgresses { get; set; } = new List<CaseProgress>();
        public virtual ICollection<ForensicAnalysis> ForensicAnalysesRequested { get; set; } = new List<ForensicAnalysis>();
        public virtual ICollection<CaseSubmission> CaseSubmissions { get; set; } = new List<CaseSubmission>();
        public virtual ICollection<CaseSubmission> CaseSubmissionsEvaluated { get; set; } = new List<CaseSubmission>();
    }

    public enum DetectiveRank
    {
        Rook = 0,           // Entry level (new requirement)
        Detective = 1,      // Entry level
        Detective2 = 2,     // Senior Detective
        Sergeant = 3,       // Sergeant
        Lieutenant = 4,     // Lieutenant
        Captain = 5,        // Captain
        Commander = 6       // Commander
    }
}