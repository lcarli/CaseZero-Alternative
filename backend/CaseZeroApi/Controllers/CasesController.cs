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
        private readonly ICaseAccessService _caseAccessService;
        private readonly ICaseFormatService _caseFormatService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<CasesController> _logger;

        public CasesController(
            ApplicationDbContext context, 
            ICaseObjectService caseObjectService,
            ICaseAccessService caseAccessService,
            ICaseFormatService caseFormatService,
            IBlobStorageService blobStorageService,
            ILogger<CasesController> logger)
        {
            _context = context;
            _caseObjectService = caseObjectService;
            _caseAccessService = caseAccessService;
            _caseFormatService = caseFormatService;
            _blobStorageService = blobStorageService;
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

            // Get cases filtered by user rank using new service
            var availableCaseIds = await _caseAccessService.GetAvailableCasesForUserAsync(userId);
            var cases = new List<CaseDto>();

            foreach (var caseId in availableCaseIds)
            {
                try
                {
                    var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                    if (caseObject?.Metadata != null)
                    {
                        // Get user case data if exists
                        var userCase = await _context.UserCases
                            .Where(uc => uc.UserId == userId && uc.CaseId == caseId)
                            .Include(uc => uc.Case)
                            .ThenInclude(c => c.CaseProgresses.Where(cp => cp.UserId == userId))
                            .FirstOrDefaultAsync();

                        var caseDto = new CaseDto
                        {
                            Id = caseId,
                            Title = caseObject.Metadata.Title,
                            Description = caseObject.Metadata.Description,
                            Status = userCase?.Case?.Status ?? CaseStatus.Open,
                            Priority = GetPriorityFromDifficulty(caseObject.Metadata.Difficulty),
                            CreatedAt = caseObject.Metadata.StartDateTime,
                            Location = caseObject.Metadata.Location,
                            IncidentDate = caseObject.Metadata.IncidentDateTime,
                            BriefingText = caseObject.Metadata.Briefing,
                            EstimatedDifficultyLevel = caseObject.Metadata.Difficulty,
                            MinimumRankRequired = Enum.TryParse<DetectiveRank>(caseObject.Metadata.MinRankRequired, true, out var rank) ? rank : DetectiveRank.Rook,
                            UserProgress = userCase?.Case?.CaseProgresses.FirstOrDefault() != null ? new CaseProgressDto
                            {
                                UserId = userCase.Case.CaseProgresses.First().UserId,
                                CaseId = userCase.Case.CaseProgresses.First().CaseId,
                                EvidencesCollected = userCase.Case.CaseProgresses.First().EvidencesCollected,
                                InterviewsCompleted = userCase.Case.CaseProgresses.First().InterviewsCompleted,
                                ReportsSubmitted = userCase.Case.CaseProgresses.First().ReportsSubmitted,
                                LastActivity = userCase.Case.CaseProgresses.First().LastActivity,
                                CompletionPercentage = userCase.Case.CaseProgresses.First().CompletionPercentage
                            } : null,
                            AssignedUsers = new List<UserDto>(),
                            Evidences = new List<EvidenceDto>(),
                            Suspects = new List<SuspectDto>()
                        };
                        
                        cases.Add(caseDto);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load case {CaseId} for user {UserId}", caseId, userId);
                    // Continue with other cases
                }
            }

            _logger.LogInformation("Returned {Count} cases for user {UserId}", cases.Count, userId);
            return Ok(cases);
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
                    PersonalEmail = uc.User.PersonalEmail,
                    Department = uc.User.Department,
                    Position = uc.User.Position,
                    BadgeNumber = uc.User.BadgeNumber,
                    EmailVerified = uc.User.EmailVerified
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

            // Check if it's a generated case (from blob storage)
            if (id.StartsWith("CASE-", StringComparison.OrdinalIgnoreCase))
            {
                // For generated cases, verify user has access via CaseAccessService
                var availableCases = await _caseAccessService.GetAvailableCasesForUserAsync(userId);
                if (!availableCases.Contains(id))
                {
                    return NotFound("Case not found or user does not have access");
                }

                // Load case bundle from blob storage
                try
                {
                    var bundle = await _blobStorageService.GetCaseBundleAsync(id);
                    if (bundle == null)
                    {
                        return NotFound($"Case data not found for case {id}");
                    }

                    // Convert NormalizedCaseBundle to CaseObject format for frontend
                    var caseObject = await _caseFormatService.ConvertToGameFormatAsync(bundle);
                    if (caseObject == null)
                    {
                        _logger.LogError("Failed to convert case bundle {CaseId} to CaseObject", id);
                        return StatusCode(500, "Failed to convert case data");
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
                            unlockConditions = s.UnlockConditions
                        }).ToList(),
                        forensicAnalyses = caseObject.ForensicAnalyses?.Select(f => new
                        {
                            id = f.Id,
                            evidenceId = f.EvidenceId,
                            analysisType = f.AnalysisType,
                            description = f.Description,
                            resultFile = f.ResultFile
                        }).ToList(),
                        unlockLogic = caseObject.UnlockLogic,
                        gameMetadata = caseObject.GameMetadata
                    };

                    return Ok(sanitizedCaseData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load generated case {CaseId}", id);
                    return StatusCode(500, "Failed to load case data");
                }
            }

            // Verify user has access to this case (database cases)
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

            // Get available cases from filesystem filtered by user rank
            var availableCaseIds = await _caseAccessService.GetAvailableCasesForUserAsync(userId);
            var cases = new List<CaseDto>();

            foreach (var caseId in availableCaseIds)
            {
                try
                {
                    // Check if it's a generated case (from blob storage)
                    if (caseId.StartsWith("CASE-", StringComparison.OrdinalIgnoreCase))
                    {
                        // Load from blob storage
                        try
                        {
                            // Get manifest for difficulty and counts (it's more reliable than normalized_case.json)
                            var manifest = await _blobStorageService.GetCaseManifestAsync(caseId);
                            
                            if (manifest != null)
                            {
                                // Get case progress if it exists
                                var caseProgress = await _context.CaseProgresses
                                    .FirstOrDefaultAsync(cp => cp.UserId == userId && cp.CaseId == caseId);

                                // Map difficulty string to numeric level
                                int difficultyLevel = GetDifficultyLevelFromString(manifest.Difficulty ?? "Rookie");

                                // Build case info from manifest
                                var caseDto = new CaseDto
                                {
                                    Id = manifest.CaseId,
                                    Title = $"Generated Case {manifest.CaseId.Replace("CASE-", "")}",
                                    Description = $"AI-generated case with {manifest.Counts?.Documents ?? 0} documents, {manifest.Counts?.Media ?? 0} media files, {manifest.Counts?.Suspects ?? 0} suspects",
                                    Status = caseProgress?.CompletionPercentage >= 100 ? CaseStatus.Resolved : CaseStatus.Open,
                                    Priority = GetPriorityFromDifficulty(difficultyLevel),
                                    CreatedAt = manifest.GeneratedAt,
                                    Location = "Unknown", // Generated cases don't have location in metadata yet
                                    IncidentDate = manifest.GeneratedAt,
                                    BriefingText = $"Investigate this case with {manifest.Counts?.Documents ?? 0} documents",
                                    EstimatedDifficultyLevel = difficultyLevel,
                                    UserProgress = caseProgress != null ? new CaseProgressDto
                                    {
                                        UserId = caseProgress.UserId,
                                        CaseId = caseProgress.CaseId,
                                        EvidencesCollected = caseProgress.EvidencesCollected,
                                        InterviewsCompleted = caseProgress.InterviewsCompleted,
                                        ReportsSubmitted = caseProgress.ReportsSubmitted,
                                        LastActivity = caseProgress.LastActivity,
                                        CompletionPercentage = caseProgress.CompletionPercentage
                                    } : null,
                                    AssignedUsers = new List<UserDto>(),
                                    Evidences = new List<EvidenceDto>(),
                                    Suspects = new List<SuspectDto>()
                                };
                                
                                cases.Add(caseDto);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to load case {CaseId} from blob storage for dashboard", caseId);
                            // Continue with other cases
                        }
                    }
                    else
                    {
                        // Load from filesystem (existing logic)
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
                                AssignedUsers = new List<UserDto>(),
                                Evidences = new List<EvidenceDto>(),
                                Suspects = new List<SuspectDto>()
                            };
                            
                            cases.Add(caseDto);
                        }
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

        /// <summary>
        /// Convert a normalized case bundle (from CaseGen) to game format
        /// </summary>
        /// <param name="id">Case ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Case object in game-consumable format</returns>
        [HttpGet("{id}/formats/game")]
        public async Task<IActionResult> GetGameFormat(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Check if user has access to this case
                var availableCases = await _caseAccessService.GetAvailableCasesForUserAsync(userId);
                if (!availableCases.Contains(id))
                {
                    return Forbid("Access denied to this case");
                }

                // For now, we'll need to implement a way to retrieve the normalized case bundle
                // This could be from a storage service, database, or external API
                // TODO: Implement normalized bundle retrieval
                
                // Example implementation (you'll need to replace this with actual data retrieval):
                var normalizedBundleJson = await GetNormalizedCaseBundleAsync(id, cancellationToken);
                
                if (string.IsNullOrEmpty(normalizedBundleJson))
                {
                    return NotFound($"Normalized case bundle not found for case {id}");
                }

                // Validate the bundle format
                var validationResult = await _caseFormatService.ValidateForGameFormatAsync(normalizedBundleJson, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Case bundle validation failed for {CaseId}: {Issues}", 
                        id, string.Join(", ", validationResult.Issues));
                    
                    return BadRequest(new 
                    { 
                        error = "Case bundle format is invalid", 
                        issues = validationResult.Issues,
                        warnings = validationResult.Warnings
                    });
                }

                // Convert to game format
                var gameFormatCase = await _caseFormatService.ConvertToGameFormatAsync(normalizedBundleJson, cancellationToken);
                
                _logger.LogInformation("Successfully converted case {CaseId} to game format", id);
                return Ok(gameFormatCase);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid case format for case {CaseId}", id);
                return BadRequest(new { error = "Invalid case format", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting case {CaseId} to game format", id);
                return StatusCode(500, new { error = "Internal server error during format conversion" });
            }
        }

        /// <summary>
        /// Validate a normalized case bundle for game format compatibility
        /// </summary>
        /// <param name="id">Case ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        [HttpGet("{id}/formats/game/validate")]
        public async Task<IActionResult> ValidateGameFormat(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Check if user has access to this case
                var availableCases = await _caseAccessService.GetAvailableCasesForUserAsync(userId);
                if (!availableCases.Contains(id))
                {
                    return Forbid("Access denied to this case");
                }

                var normalizedBundleJson = await GetNormalizedCaseBundleAsync(id, cancellationToken);
                
                if (string.IsNullOrEmpty(normalizedBundleJson))
                {
                    return NotFound($"Normalized case bundle not found for case {id}");
                }

                var validationResult = await _caseFormatService.ValidateForGameFormatAsync(normalizedBundleJson, cancellationToken);
                
                return Ok(validationResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating case {CaseId} for game format", id);
                return StatusCode(500, new { error = "Internal server error during validation" });
            }
        }

        /// <summary>
        /// Placeholder method to retrieve normalized case bundle
        /// TODO: Implement actual data retrieval from storage/database/external service
        /// </summary>
        private async Task<string> GetNormalizedCaseBundleAsync(string caseId, CancellationToken cancellationToken)
        {
            // TODO: Implement actual retrieval logic
            // This could involve:
            // 1. Querying a database for stored normalized bundles
            // 2. Calling CaseGen.Functions API to get the case
            // 3. Reading from blob storage
            // 4. Calling an external service
            
            _logger.LogWarning("GetNormalizedCaseBundleAsync not implemented - returning empty string for case {CaseId}", caseId);
            return await Task.FromResult(string.Empty);
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

        private static int GetDifficultyLevelFromString(string difficulty)
        {
            return difficulty?.ToLower() switch
            {
                "rookie" => 1,
                "detective" => 3,
                "sergeant" => 5,
                "lieutenant" => 7,
                "captain" => 9,
                "commander" => 10,
                _ => 1
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