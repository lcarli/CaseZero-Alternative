using Microsoft.AspNetCore.Identity;

namespace CaseZeroApi.Models
{
    public class User : IdentityUser
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? BadgeNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsApproved { get; set; } = false;
        
        // Navigation properties
        public virtual ICollection<UserCase> UserCases { get; set; } = new List<UserCase>();
        public virtual ICollection<CaseProgress> CaseProgresses { get; set; } = new List<CaseProgress>();
    }
}