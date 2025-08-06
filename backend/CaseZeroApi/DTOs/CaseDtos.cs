using CaseZeroApi.Models;

namespace CaseZeroApi.DTOs
{
    public class CaseDto
    {
        public required string Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public CaseStatus Status { get; set; }
        public CasePriority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public List<UserDto> AssignedUsers { get; set; } = new();
        public CaseProgressDto? UserProgress { get; set; }
    }

    public class CaseProgressDto
    {
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public int EvidencesCollected { get; set; }
        public int InterviewsCompleted { get; set; }
        public int ReportsSubmitted { get; set; }
        public DateTime LastActivity { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class DashboardDto
    {
        public UserStatsDto Stats { get; set; } = new();
        public List<CaseDto> Cases { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
    }

    public class UserStatsDto
    {
        public int CasesResolved { get; set; }
        public int CasesActive { get; set; }
        public double SuccessRate { get; set; }
        public double AverageRating { get; set; }
    }

    public class RecentActivityDto
    {
        public required string Description { get; set; }
        public DateTime Date { get; set; }
        public string? CaseId { get; set; }
    }
}