using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CasesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CasesController> _logger;

        public CasesController(ApplicationDbContext context, ILogger<CasesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetCases()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var userCases = await _context.UserCases
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.Case)
                .ThenInclude(c => c.UserCases)
                .ThenInclude(uc => uc.User)
                .Include(uc => uc.Case)
                .ThenInclude(c => c.CaseProgresses.Where(cp => cp.UserId == userId))
                .Select(uc => new CaseDto
                {
                    Id = uc.Case.Id,
                    Title = uc.Case.Title,
                    Description = uc.Case.Description,
                    Status = uc.Case.Status,
                    Priority = uc.Case.Priority,
                    CreatedAt = uc.Case.CreatedAt,
                    ClosedAt = uc.Case.ClosedAt,
                    AssignedUsers = uc.Case.UserCases.Select(uc2 => new UserDto
                    {
                        Id = uc2.User.Id,
                        FirstName = uc2.User.FirstName,
                        LastName = uc2.User.LastName,
                        Email = uc2.User.Email!,
                        Department = uc2.User.Department,
                        Position = uc2.User.Position,
                        BadgeNumber = uc2.User.BadgeNumber,
                        IsApproved = uc2.User.IsApproved
                    }).ToList(),
                    UserProgress = uc.Case.CaseProgresses.FirstOrDefault() != null ? new CaseProgressDto
                    {
                        UserId = uc.Case.CaseProgresses.First().UserId,
                        CaseId = uc.Case.CaseProgresses.First().CaseId,
                        EvidencesCollected = uc.Case.CaseProgresses.First().EvidencesCollected,
                        InterviewsCompleted = uc.Case.CaseProgresses.First().InterviewsCompleted,
                        ReportsSubmitted = uc.Case.CaseProgresses.First().ReportsSubmitted,
                        LastActivity = uc.Case.CaseProgresses.First().LastActivity,
                        CompletionPercentage = uc.Case.CaseProgresses.First().CompletionPercentage
                    } : null
                })
                .ToListAsync();

            return Ok(userCases);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCase(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var userCase = await _context.UserCases
                .Where(uc => uc.UserId == userId && uc.CaseId == id)
                .Include(uc => uc.Case)
                .ThenInclude(c => c.UserCases)
                .ThenInclude(uc => uc.User)
                .Include(uc => uc.Case)
                .ThenInclude(c => c.CaseProgresses.Where(cp => cp.UserId == userId))
                .Include(uc => uc.Case)
                .ThenInclude(c => c.Evidences)
                .ThenInclude(e => e.ForensicAnalyses)
                .Include(uc => uc.Case)
                .ThenInclude(c => c.Suspects)
                .FirstOrDefaultAsync();

            if (userCase == null)
            {
                return NotFound();
            }

            var caseDto = new CaseDto
            {
                Id = userCase.Case.Id,
                Title = userCase.Case.Title,
                Description = userCase.Case.Description,
                Status = userCase.Case.Status,
                Priority = userCase.Case.Priority,
                CreatedAt = userCase.Case.CreatedAt,
                ClosedAt = userCase.Case.ClosedAt,
                Type = userCase.Case.Type,
                MinimumRankRequired = userCase.Case.MinimumRankRequired,
                Location = userCase.Case.Location,
                IncidentDate = userCase.Case.IncidentDate,
                BriefingText = userCase.Case.BriefingText,
                VictimInfo = userCase.Case.VictimInfo,
                HasMultipleSuspects = userCase.Case.HasMultipleSuspects,
                EstimatedDifficultyLevel = userCase.Case.EstimatedDifficultyLevel,
                MaxScore = userCase.Case.MaxScore,
                AssignedUsers = userCase.Case.UserCases.Select(uc => new UserDto
                {
                    Id = uc.User.Id,
                    FirstName = uc.User.FirstName,
                    LastName = uc.User.LastName,
                    Email = uc.User.Email!,
                    Department = uc.User.Department,
                    Position = uc.User.Position,
                    BadgeNumber = uc.User.BadgeNumber,
                    IsApproved = uc.User.IsApproved
                }).ToList(),
                Evidences = userCase.Case.Evidences
                    .Where(e => e.IsUnlocked) // Only show unlocked evidence
                    .Select(e => new EvidenceDto
                    {
                        Id = e.Id,
                        CaseId = e.CaseId,
                        Name = e.Name,
                        Type = e.Type,
                        Description = e.Description,
                        FilePath = e.FilePath,
                        CollectedAt = e.CollectedAt,
                        IsUnlocked = e.IsUnlocked,
                        RequiresAnalysis = e.RequiresAnalysis,
                        AnalysisStatus = e.AnalysisStatus,
                        AnalysisResult = e.AnalysisResult,
                        Category = e.Category,
                        Priority = e.Priority,
                        DependsOnEvidenceIds = string.IsNullOrEmpty(e.DependsOnEvidenceIds) 
                            ? new List<string>() 
                            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(e.DependsOnEvidenceIds) ?? new List<string>(),
                        ForensicAnalyses = e.ForensicAnalyses.Select(fa => new ForensicAnalysisDto
                        {
                            Id = fa.Id,
                            EvidenceId = fa.EvidenceId,
                            AnalysisType = fa.AnalysisType,
                            Status = fa.Status,
                            RequestedAt = fa.RequestedAt,
                            CompletedAt = fa.CompletedAt,
                            Results = fa.Results,
                            ConfidenceLevel = fa.ConfidenceLevel,
                            IsMatch = fa.IsMatch
                        }).ToList()
                    }).ToList(),
                Suspects = userCase.Case.Suspects.Select(s => new SuspectDto
                {
                    Id = s.Id,
                    CaseId = s.CaseId,
                    Name = s.Name,
                    Alias = s.Alias,
                    Age = s.Age,
                    Description = s.Description,
                    Motive = s.Motive,
                    Alibi = s.Alibi,
                    HasAlibiVerified = s.HasAlibiVerified,
                    Status = s.Status,
                    PhotoPath = s.PhotoPath
                }).ToList(),
                UserProgress = userCase.Case.CaseProgresses.FirstOrDefault() != null ? new CaseProgressDto
                {
                    UserId = userCase.Case.CaseProgresses.First().UserId,
                    CaseId = userCase.Case.CaseProgresses.First().CaseId,
                    EvidencesCollected = userCase.Case.CaseProgresses.First().EvidencesCollected,
                    InterviewsCompleted = userCase.Case.CaseProgresses.First().InterviewsCompleted,
                    ReportsSubmitted = userCase.Case.CaseProgresses.First().ReportsSubmitted,
                    LastActivity = userCase.Case.CaseProgresses.First().LastActivity,
                    CompletionPercentage = userCase.Case.CaseProgresses.First().CompletionPercentage
                } : null
            };

            return Ok(caseDto);
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Get user information with rank
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Unauthorized();

            // Get user cases
            var userCases = await _context.UserCases
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.Case)
                .ThenInclude(c => c.CaseProgresses.Where(cp => cp.UserId == userId))
                .ToListAsync();

            // Calculate stats
            var resolvedCases = userCases.Count(uc => uc.Case.Status == CaseStatus.Resolved);
            var activeCases = userCases.Count(uc => uc.Case.Status == CaseStatus.Open || uc.Case.Status == CaseStatus.InProgress);
            var totalCases = userCases.Count;
            var successRate = totalCases > 0 ? (double)resolvedCases / totalCases * 100 : 0;

            var stats = new UserStatsDto
            {
                CasesResolved = resolvedCases,
                CasesActive = activeCases,
                SuccessRate = Math.Round(successRate, 1),
                AverageRating = 4.8, // Mock data
                Rank = user.Rank,
                ExperiencePoints = user.ExperiencePoints,
                RankName = GetRankName(user.Rank)
            };

            // Get recent cases with enhanced information
            var cases = userCases
                .Where(uc => uc.Case.MinimumRankRequired <= user.Rank || user.CanAccessHighPriorityCases)
                .Select(uc => new CaseDto
                {
                    Id = uc.Case.Id,
                    Title = uc.Case.Title,
                    Description = uc.Case.Description,
                    Status = uc.Case.Status,
                    Priority = uc.Case.Priority,
                    CreatedAt = uc.Case.CreatedAt,
                    ClosedAt = uc.Case.ClosedAt,
                    Type = uc.Case.Type,
                    MinimumRankRequired = uc.Case.MinimumRankRequired,
                    Location = uc.Case.Location,
                    IncidentDate = uc.Case.IncidentDate,
                    BriefingText = uc.Case.BriefingText,
                    VictimInfo = uc.Case.VictimInfo,
                    HasMultipleSuspects = uc.Case.HasMultipleSuspects,
                    EstimatedDifficultyLevel = uc.Case.EstimatedDifficultyLevel,
                    MaxScore = uc.Case.MaxScore,
                    UserProgress = uc.Case.CaseProgresses.FirstOrDefault() != null ? new CaseProgressDto
                    {
                        UserId = uc.Case.CaseProgresses.First().UserId,
                        CaseId = uc.Case.CaseProgresses.First().CaseId,
                        EvidencesCollected = uc.Case.CaseProgresses.First().EvidencesCollected,
                        InterviewsCompleted = uc.Case.CaseProgresses.First().InterviewsCompleted,
                        ReportsSubmitted = uc.Case.CaseProgresses.First().ReportsSubmitted,
                        LastActivity = uc.Case.CaseProgresses.First().LastActivity,
                        CompletionPercentage = uc.Case.CaseProgresses.First().CompletionPercentage
                    } : null
                }).OrderByDescending(c => c.CreatedAt).ToList();

            // Get recent activities
            var recentActivities = new List<RecentActivityDto>();

            // Add case submission activities
            var recentSubmissions = await _context.CaseSubmissions
                .Where(cs => cs.SubmittedByUserId == userId)
                .Include(cs => cs.Case)
                .OrderByDescending(cs => cs.SubmittedAt)
                .Take(3)
                .ToListAsync();

            foreach (var submission in recentSubmissions)
            {
                recentActivities.Add(new RecentActivityDto
                {
                    Description = submission.Status == SubmissionStatus.Approved 
                        ? $"Caso {submission.Case.Title} resolvido com sucesso"
                        : $"Relatório submetido para {submission.Case.Title}",
                    Date = submission.SubmittedAt,
                    CaseId = submission.CaseId
                });
            }

            // Add forensic analysis activities
            var recentAnalyses = await _context.ForensicAnalyses
                .Where(fa => fa.RequestedByUserId == userId && fa.Status == ForensicAnalysisStatus.Completed)
                .Include(fa => fa.Evidence)
                .ThenInclude(e => e.Case)
                .OrderByDescending(fa => fa.CompletedAt)
                .Take(2)
                .ToListAsync();

            foreach (var analysis in recentAnalyses)
            {
                recentActivities.Add(new RecentActivityDto
                {
                    Description = $"Análise {analysis.AnalysisType} concluída - {analysis.Evidence.Name}",
                    Date = analysis.CompletedAt ?? analysis.RequestedAt,
                    CaseId = analysis.Evidence.CaseId
                });
            }

            recentActivities = recentActivities.OrderByDescending(ra => ra.Date).Take(5).ToList();

            // Get recent emails
            var recentEmails = await _context.Emails
                .Include(e => e.FromUser)
                .Where(e => e.ToUserId == userId)
                .OrderByDescending(e => e.SentAt)
                .Take(5)
                .Select(e => new EmailDto
                {
                    Id = e.Id,
                    CaseId = e.CaseId,
                    Subject = e.Subject,
                    Content = e.Content,
                    Preview = e.Preview,
                    SentAt = e.SentAt,
                    IsRead = e.IsRead,
                    Priority = e.Priority,
                    Type = e.Type,
                    SenderName = e.FromUser.FirstName + " " + e.FromUser.LastName,
                    Attachments = new List<EmailAttachmentDto>() // Simplified for dashboard
                })
                .ToListAsync();

            var dashboard = new DashboardDto
            {
                Stats = stats,
                Cases = cases,
                RecentActivities = recentActivities,
                RecentEmails = recentEmails
            };

            return Ok(dashboard);
        }

        private static string GetRankName(DetectiveRank rank)
        {
            return rank switch
            {
                DetectiveRank.Detective => "Detetive",
                DetectiveRank.Detective2 => "Detetive Sênior",
                DetectiveRank.Sergeant => "Sargento",
                DetectiveRank.Lieutenant => "Tenente",
                DetectiveRank.Captain => "Capitão",
                DetectiveRank.Commander => "Comandante",
                _ => "Detetive"
            };
        }
    }
}