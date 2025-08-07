using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CasesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICaseObjectService _caseObjectService;
        private readonly ILogger<CasesController> _logger;

        public CasesController(ApplicationDbContext context, ICaseObjectService caseObjectService, ILogger<CasesController> logger)
        {
            _context = context;
            _caseObjectService = caseObjectService;
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

        [HttpGet("{id}/data")]
        public async Task<IActionResult> GetCaseData(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Verify user has access to this case
            var userCase = await _context.UserCases
                .Where(uc => uc.UserId == userId && uc.CaseId == id)
                .FirstOrDefaultAsync();

            if (userCase == null)
            {
                return NotFound("Case not found or user does not have access");
            }

            try
            {
                // Load case data from filesystem using CaseObjectService
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(id);
                if (caseObject == null)
                {
                    return NotFound($"Case data not found for case {id}");
                }

                // Return sanitized case data without exposing solution details
                var sanitizedCaseData = new
                {
                    caseId = caseObject.CaseId,
                    metadata = caseObject.Metadata,
                    evidences = caseObject.Evidences?.Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        type = e.Type,
                        fileName = e.FileName,
                        category = e.Category,
                        priority = e.Priority,
                        description = e.Description,
                        location = e.Location,
                        isUnlocked = e.IsUnlocked,
                        requiresAnalysis = e.RequiresAnalysis,
                        dependsOn = e.DependsOn,
                        linkedSuspects = e.LinkedSuspects,
                        analysisRequired = e.AnalysisRequired,
                        unlockConditions = e.UnlockConditions
                    }).ToList(),
                    suspects = caseObject.Suspects?.Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        alias = s.Alias,
                        age = s.Age,
                        occupation = s.Occupation,
                        description = s.Description,
                        relationship = s.Relationship,
                        motive = s.Motive,
                        alibi = s.Alibi,
                        alibiVerified = s.AlibiVerified,
                        behavior = s.Behavior,
                        backgroundInfo = s.BackgroundInfo,
                        linkedEvidence = s.LinkedEvidence,
                        comments = s.Comments,
                        status = s.Status,
                        unlockConditions = s.UnlockConditions
                        // Note: isActualCulprit is intentionally excluded to prevent spoilers
                    }).ToList(),
                    forensicAnalyses = caseObject.ForensicAnalyses?.Select(fa => new
                    {
                        id = fa.Id,
                        evidenceId = fa.EvidenceId,
                        analysisType = fa.AnalysisType,
                        responseTime = fa.ResponseTime,
                        resultFile = fa.ResultFile,
                        description = fa.Description
                    }).ToList(),
                    temporalEvents = caseObject.TemporalEvents?.Select(te => new
                    {
                        id = te.Id,
                        triggerTime = te.TriggerTime,
                        type = te.Type,
                        title = te.Title,
                        content = te.Content,
                        fileName = te.FileName
                    }).ToList(),
                    timeline = caseObject.Timeline,
                    unlockLogic = caseObject.UnlockLogic,
                    gameMetadata = caseObject.GameMetadata
                    // Note: solution is intentionally excluded to prevent spoilers
                };

                return Ok(sanitizedCaseData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case data for case {CaseId}", id);
                return StatusCode(500, "Internal server error while loading case data");
            }
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

            // Get available cases from filesystem
            var availableCaseIds = await _caseObjectService.GetAvailableCasesAsync();
            var cases = new List<CaseDto>();

            foreach (var caseId in availableCaseIds)
            {
                try
                {
                    var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                    if (caseObject?.Metadata != null)
                    {
                        // Get last session for this case (handle if table doesn't exist)
                        CaseSession? lastSession = null;
                        try 
                        {
                            lastSession = await _context.CaseSessions
                                .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                                .OrderByDescending(cs => cs.SessionStart)
                                .FirstOrDefaultAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not load session data for case {CaseId}", caseId);
                        }

                        // Get case progress if it exists
                        var caseProgress = await _context.CaseProgresses
                            .FirstOrDefaultAsync(cp => cp.UserId == userId && cp.CaseId == caseId);

                        var caseDto = new CaseDto
                        {
                            Id = caseId,
                            Title = caseObject.Metadata.Title,
                            Description = caseObject.Metadata.Description,
                            Status = DetermineCaseStatus(lastSession, caseProgress),
                            Priority = GetPriorityFromDifficulty(caseObject.Metadata.Difficulty),
                            CreatedAt = caseObject.Metadata.StartDateTime,
                            Location = caseObject.Metadata.Location,
                            IncidentDate = caseObject.Metadata.IncidentDateTime,
                            BriefingText = caseObject.Metadata.Briefing,
                            EstimatedDifficultyLevel = caseObject.Metadata.Difficulty,
                            UserProgress = caseProgress != null ? new CaseProgressDto
                            {
                                UserId = caseProgress.UserId,
                                CaseId = caseProgress.CaseId,
                                EvidencesCollected = caseProgress.EvidencesCollected,
                                InterviewsCompleted = caseProgress.InterviewsCompleted,
                                ReportsSubmitted = caseProgress.ReportsSubmitted,
                                LastActivity = lastSession?.SessionEnd ?? lastSession?.SessionStart ?? caseProgress.LastActivity,
                                CompletionPercentage = caseProgress.CompletionPercentage
                            } : null,
                            AssignedUsers = new List<UserDto>(), // Empty for now
                            Evidences = new List<EvidenceDto>(),
                            Suspects = new List<SuspectDto>()
                        };
                        
                        cases.Add(caseDto);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load case {CaseId} for dashboard", caseId);
                    // Continue with other cases
                }
            }

            // Calculate stats based on loaded cases
            var resolvedCases = cases.Count(c => c.Status == CaseStatus.Resolved);
            var activeCases = cases.Count(c => c.Status == CaseStatus.Open || c.Status == CaseStatus.InProgress);
            var totalCases = cases.Count;
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

            // Get recent activities - simplify for now since we're using filesystem cases
            var recentActivities = new List<RecentActivityDto>();

            // Add recent case sessions as activities (handle if table doesn't exist)
            try 
            {
                var recentSessions = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && !cs.IsActive)
                    .OrderByDescending(cs => cs.SessionEnd)
                    .Take(5)
                    .ToListAsync();

                foreach (var session in recentSessions)
                {
                    var caseTitle = cases.FirstOrDefault(c => c.Id == session.CaseId)?.Title ?? session.CaseId;
                    recentActivities.Add(new RecentActivityDto
                    {
                        Description = $"Sessão de investigação concluída - {caseTitle}",
                        Date = session.SessionEnd ?? session.SessionStart,
                        CaseId = session.CaseId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load session activities");
            }

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
                Cases = cases.OrderByDescending(c => c.UserProgress?.LastActivity ?? c.CreatedAt).ToList(),
                RecentActivities = recentActivities,
                RecentEmails = recentEmails
            };

            return Ok(dashboard);
        }

        private static CaseStatus DetermineCaseStatus(CaseSession? lastSession, CaseProgress? caseProgress)
        {
            if (caseProgress != null && caseProgress.CompletionPercentage >= 100)
            {
                return CaseStatus.Resolved;
            }
            
            if (lastSession != null || caseProgress != null)
            {
                return CaseStatus.InProgress;
            }
            
            return CaseStatus.Open;
        }

        private static CasePriority GetPriorityFromDifficulty(int difficulty)
        {
            return difficulty switch
            {
                >= 8 => CasePriority.Critical,
                >= 6 => CasePriority.High,
                >= 4 => CasePriority.Medium,
                _ => CasePriority.Low
            };
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