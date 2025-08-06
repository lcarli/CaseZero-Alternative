using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/case-submissions")]
    [Authorize]
    public class CaseSubmissionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CaseSubmissionController> _logger;

        public CaseSubmissionController(ApplicationDbContext context, ILogger<CaseSubmissionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitCase([FromBody] CreateCaseSubmissionDto request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify user has access to the case
            var userCase = await _context.UserCases
                .Include(uc => uc.Case)
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == request.CaseId);

            if (userCase == null)
                return Forbid("You don't have access to this case");

            // Check if case is still open for submissions
            if (userCase.Case.Status == CaseStatus.Closed || userCase.Case.Status == CaseStatus.Resolved)
                return BadRequest("Case is already closed");

            // Check if user has already submitted for this case
            var existingSubmission = await _context.CaseSubmissions
                .FirstOrDefaultAsync(cs => cs.CaseId == request.CaseId && cs.SubmittedByUserId == userId);

            if (existingSubmission != null)
                return BadRequest("You have already submitted a solution for this case");

            // Create submission
            var submission = new CaseSubmission
            {
                CaseId = request.CaseId,
                SubmittedByUserId = userId,
                SuspectName = request.SuspectName,
                KeyEvidenceDescription = request.KeyEvidenceDescription,
                SupportingEvidenceIds = JsonSerializer.Serialize(request.SupportingEvidenceIds),
                Reasoning = request.Reasoning
            };

            _context.CaseSubmissions.Add(submission);

            // Update case status
            userCase.Case.Status = CaseStatus.UnderReview;

            await _context.SaveChangesAsync();

            // Evaluate submission immediately (in reality this might be done by a supervisor)
            await EvaluateSubmission(submission.Id);

            return Ok(new { message = "Case submitted successfully", submissionId = submission.Id });
        }

        [HttpGet("case/{caseId}")]
        public async Task<IActionResult> GetCaseSubmissions(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify user has access to the case
            var userCase = await _context.UserCases
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == caseId);

            if (userCase == null)
                return Forbid("You don't have access to this case");

            var submissions = await _context.CaseSubmissions
                .Where(cs => cs.CaseId == caseId && cs.SubmittedByUserId == userId)
                .ToListAsync();

            var submissionDtos = submissions.Select(cs => new CaseSubmissionDto
            {
                Id = cs.Id,
                CaseId = cs.CaseId,
                SuspectName = cs.SuspectName,
                KeyEvidenceDescription = cs.KeyEvidenceDescription,
                SupportingEvidenceIds = string.IsNullOrEmpty(cs.SupportingEvidenceIds) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(cs.SupportingEvidenceIds)!,
                Reasoning = cs.Reasoning,
                SubmittedAt = cs.SubmittedAt,
                Status = cs.Status,
                IsCorrectSuspect = cs.IsCorrectSuspect,
                IsValidEvidence = cs.IsValidEvidence,
                Score = cs.Score,
                Feedback = cs.Feedback
            }).ToList();

            return Ok(submissionDtos);
        }

        [HttpGet("{submissionId}")]
        public async Task<IActionResult> GetSubmission(int submissionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var submission = await _context.CaseSubmissions
                .Include(cs => cs.Case)
                .ThenInclude(c => c.UserCases)
                .FirstOrDefaultAsync(cs => cs.Id == submissionId);

            if (submission == null)
                return NotFound();

            if (!submission.Case.UserCases.Any(uc => uc.UserId == userId))
                return Forbid();

            var dto = new CaseSubmissionDto
            {
                Id = submission.Id,
                CaseId = submission.CaseId,
                SuspectName = submission.SuspectName,
                KeyEvidenceDescription = submission.KeyEvidenceDescription,
                SupportingEvidenceIds = string.IsNullOrEmpty(submission.SupportingEvidenceIds) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(submission.SupportingEvidenceIds)!,
                Reasoning = submission.Reasoning,
                SubmittedAt = submission.SubmittedAt,
                Status = submission.Status,
                IsCorrectSuspect = submission.IsCorrectSuspect,
                IsValidEvidence = submission.IsValidEvidence,
                Score = submission.Score,
                Feedback = submission.Feedback
            };

            return Ok(dto);
        }

        private async Task EvaluateSubmission(int submissionId)
        {
            var submission = await _context.CaseSubmissions
                .Include(cs => cs.Case)
                .FirstOrDefaultAsync(cs => cs.Id == submissionId);

            if (submission == null) return;

            var case_ = submission.Case;

            // Evaluate suspect correctness
            var isCorrectSuspect = string.Equals(submission.SuspectName.Trim(), 
                case_.CorrectSuspectName?.Trim(), StringComparison.OrdinalIgnoreCase);

            // Evaluate evidence validity (simplified logic)
            var isValidEvidence = true; // For now, assume evidence is valid if submitted
            
            if (!string.IsNullOrEmpty(case_.CorrectEvidenceIds))
            {
                var correctEvidenceIds = JsonSerializer.Deserialize<List<string>>(case_.CorrectEvidenceIds) ?? new List<string>();
                var submittedEvidenceIds = string.IsNullOrEmpty(submission.SupportingEvidenceIds) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(submission.SupportingEvidenceIds) ?? new List<string>();

                // Check if at least one correct evidence is included
                isValidEvidence = correctEvidenceIds.Any(correctId => submittedEvidenceIds.Contains(correctId));
            }

            // Calculate score
            double score = 0;
            var feedback = new List<string>();

            if (isCorrectSuspect)
            {
                score += 60; // 60 points for correct suspect
                feedback.Add("✓ Suspect identificado corretamente");
            }
            else
            {
                feedback.Add("✗ Suspect incorreto");
            }

            if (isValidEvidence)
            {
                score += 30; // 30 points for valid evidence
                feedback.Add("✓ Evidência válida apresentada");
            }
            else
            {
                feedback.Add("✗ Evidência insuficiente ou incorreta");
            }

            // Points for reasoning quality (simplified)
            if (submission.Reasoning.Length > 100)
            {
                score += 10; // 10 points for detailed reasoning
                feedback.Add("✓ Raciocínio bem fundamentado");
            }
            else
            {
                feedback.Add("✗ Raciocínio precisa ser mais detalhado");
            }

            // Determine status
            var status = SubmissionStatus.Rejected;
            if (isCorrectSuspect && isValidEvidence)
            {
                status = SubmissionStatus.Approved;
                case_.Status = CaseStatus.Resolved;
                case_.ClosedAt = DateTime.UtcNow;
            }
            else if (isCorrectSuspect || isValidEvidence)
            {
                status = SubmissionStatus.NeedsRevision;
            }

            // Update submission
            submission.Status = status;
            submission.IsCorrectSuspect = isCorrectSuspect;
            submission.IsValidEvidence = isValidEvidence;
            submission.Score = score;
            submission.Feedback = string.Join("\n", feedback);
            submission.EvaluatedAt = DateTime.UtcNow;

            // Update user stats if case is resolved
            if (status == SubmissionStatus.Approved)
            {
                var user = await _context.Users.FindAsync(submission.SubmittedByUserId);
                if (user != null)
                {
                    user.CasesResolved++;
                    user.ExperiencePoints += (int)score;
                    
                    // Calculate new success rate
                    var totalCases = user.CasesResolved + user.CasesFailed;
                    user.SuccessRate = totalCases > 0 ? (double)user.CasesResolved / totalCases * 100 : 0;
                    
                    // Check for promotion
                    await CheckForPromotion(user);
                }
            }
            else if (status == SubmissionStatus.Rejected)
            {
                var user = await _context.Users.FindAsync(submission.SubmittedByUserId);
                if (user != null)
                {
                    user.CasesFailed++;
                    var totalCases = user.CasesResolved + user.CasesFailed;
                    user.SuccessRate = totalCases > 0 ? (double)user.CasesResolved / totalCases * 100 : 0;
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task CheckForPromotion(User user)
        {
            var currentRank = user.Rank;
            var newRank = currentRank;

            // Simple promotion logic based on experience points and success rate
            if (user.ExperiencePoints >= 1000 && user.SuccessRate >= 80 && currentRank < DetectiveRank.Captain)
            {
                newRank = (DetectiveRank)((int)currentRank + 1);
            }
            else if (user.ExperiencePoints >= 500 && user.SuccessRate >= 70 && currentRank < DetectiveRank.Sergeant)
            {
                newRank = DetectiveRank.Sergeant;
            }
            else if (user.ExperiencePoints >= 250 && user.SuccessRate >= 60 && currentRank < DetectiveRank.Detective2)
            {
                newRank = DetectiveRank.Detective2;
            }

            if (newRank != currentRank)
            {
                user.Rank = newRank;
                user.LastPromotionDate = DateTime.UtcNow;
                
                // Grant access to higher priority cases
                if (newRank >= DetectiveRank.Sergeant)
                {
                    user.CanAccessHighPriorityCases = true;
                }

                // Create promotion notification email
                var email = new Email
                {
                    ToUserId = user.Id,
                    FromUserId = user.Id, // Simplified - in reality this would be from system admin
                    Subject = $"Promoção para {GetRankName(newRank)}",
                    Content = $"Parabéns! Você foi promovido para {GetRankName(newRank)} com base em seu excelente desempenho. " +
                             $"Pontos de experiência: {user.ExperiencePoints}, Taxa de sucesso: {user.SuccessRate:F1}%",
                    Type = EmailType.PromotionNotice,
                    Priority = EmailPriority.High,
                    IsSystemGenerated = true
                };

                _context.Emails.Add(email);
                await _context.SaveChangesAsync();
            }
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