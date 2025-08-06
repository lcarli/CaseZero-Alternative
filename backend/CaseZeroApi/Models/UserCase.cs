namespace CaseZeroApi.Models
{
    public class UserCase
    {
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public UserCaseRole Role { get; set; } = UserCaseRole.Detective;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Case Case { get; set; } = null!;
    }

    public enum UserCaseRole
    {
        Detective,
        Lead,
        Assistant,
        Observer
    }
}