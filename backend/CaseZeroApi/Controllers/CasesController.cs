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
                AverageRating = 4.8 // Mock data
            };

            // Get recent cases
            var cases = userCases.Select(uc => new CaseDto
            {
                Id = uc.Case.Id,
                Title = uc.Case.Title,
                Description = uc.Case.Description,
                Status = uc.Case.Status,
                Priority = uc.Case.Priority,
                CreatedAt = uc.Case.CreatedAt,
                ClosedAt = uc.Case.ClosedAt,
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

            // Mock recent activities
            var recentActivities = new List<RecentActivityDto>
            {
                new() { Description = "Caso BANK-2024-001 resolvido", Date = DateTime.UtcNow.AddDays(-1), CaseId = "BANK-2024-001" },
                new() { Description = "Nova evidência coletada em TECH-2024-002", Date = DateTime.UtcNow.AddDays(-2), CaseId = "TECH-2024-002" },
                new() { Description = "Entrevista realizada com suspeito", Date = DateTime.UtcNow.AddDays(-3) },
                new() { Description = "Relatório forense recebido", Date = DateTime.UtcNow.AddDays(-5) }
            };

            var dashboard = new DashboardDto
            {
                Stats = stats,
                Cases = cases,
                RecentActivities = recentActivities
            };

            return Ok(dashboard);
        }
    }
}