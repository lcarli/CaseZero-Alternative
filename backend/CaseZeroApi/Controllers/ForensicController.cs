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
    [Route("api/[controller]")]
    [Authorize]
    public class ForensicController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ForensicController> _logger;

        public ForensicController(ApplicationDbContext context, ILogger<ForensicController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("request-analysis")]
        public async Task<IActionResult> RequestAnalysis([FromBody] RequestAnalysisDto request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify user has access to the evidence
            var evidence = await _context.Evidences
                .Include(e => e.Case)
                .ThenInclude(c => c.UserCases)
                .FirstOrDefaultAsync(e => e.Id == request.EvidenceId);

            if (evidence == null)
                return NotFound("Evidence not found");

            if (!evidence.Case.UserCases.Any(uc => uc.UserId == userId))
                return Forbid("You don't have access to this case");

            // Create forensic analysis request
            var analysis = new ForensicAnalysis
            {
                EvidenceId = request.EvidenceId,
                RequestedByUserId = userId,
                AnalysisType = request.AnalysisType,
                Status = ForensicAnalysisStatus.Requested
            };

            _context.ForensicAnalyses.Add(analysis);

            // Update evidence status
            evidence.AnalysisStatus = EvidenceStatus.Submitted;
            evidence.AnalysisRequestedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Simulate lab processing time (in reality this would be a background job)
            await SimulateForensicAnalysis(analysis.Id);

            return Ok(new { message = "Analysis requested successfully", analysisId = analysis.Id });
        }

        [HttpGet("analysis/{analysisId}")]
        public async Task<IActionResult> GetAnalysisResult(int analysisId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var analysis = await _context.ForensicAnalyses
                .Include(fa => fa.Evidence)
                .ThenInclude(e => e.Case)
                .ThenInclude(c => c.UserCases)
                .FirstOrDefaultAsync(fa => fa.Id == analysisId);

            if (analysis == null)
                return NotFound();

            if (!analysis.Evidence.Case.UserCases.Any(uc => uc.UserId == userId))
                return Forbid();

            var dto = new ForensicAnalysisDto
            {
                Id = analysis.Id,
                EvidenceId = analysis.EvidenceId,
                AnalysisType = analysis.AnalysisType,
                Status = analysis.Status,
                RequestedAt = analysis.RequestedAt,
                CompletedAt = analysis.CompletedAt,
                Results = analysis.Results,
                ConfidenceLevel = analysis.ConfidenceLevel,
                IsMatch = analysis.IsMatch
            };

            return Ok(dto);
        }

        [HttpGet("evidence/{evidenceId}/analyses")]
        public async Task<IActionResult> GetEvidenceAnalyses(int evidenceId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var evidence = await _context.Evidences
                .Include(e => e.Case)
                .ThenInclude(c => c.UserCases)
                .Include(e => e.ForensicAnalyses)
                .FirstOrDefaultAsync(e => e.Id == evidenceId);

            if (evidence == null)
                return NotFound();

            if (!evidence.Case.UserCases.Any(uc => uc.UserId == userId))
                return Forbid();

            var analyses = evidence.ForensicAnalyses.Select(fa => new ForensicAnalysisDto
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
            }).ToList();

            return Ok(analyses);
        }

        private async Task SimulateForensicAnalysis(int analysisId)
        {
            // In a real application, this would be a background job
            // For now, we'll simulate immediate results for demo purposes
            var analysis = await _context.ForensicAnalyses.FindAsync(analysisId);
            if (analysis == null) return;

            // Simulate different analysis results based on type
            var random = new Random();
            analysis.Status = ForensicAnalysisStatus.Completed;
            analysis.CompletedAt = DateTime.UtcNow;

            switch (analysis.AnalysisType.ToLower())
            {
                case "dna":
                    analysis.Results = JsonSerializer.Serialize(new
                    {
                        profileFound = random.Next(0, 100) > 20,
                        matches = random.Next(0, 3),
                        quality = random.Next(60, 100) + "%",
                        contaminationRisk = random.Next(0, 100) < 10 ? "High" : "Low"
                    });
                    analysis.ConfidenceLevel = random.NextDouble() * 0.4 + 0.6; // 60-100%
                    analysis.IsMatch = random.Next(0, 100) > 50;
                    break;

                case "fingerprint":
                    analysis.Results = JsonSerializer.Serialize(new
                    {
                        minutiaePoints = random.Next(8, 16),
                        patternType = new[] { "Loop", "Whorl", "Arch" }[random.Next(0, 3)],
                        clarity = random.Next(70, 100) + "%"
                    });
                    analysis.ConfidenceLevel = random.NextDouble() * 0.3 + 0.7; // 70-100%
                    analysis.IsMatch = random.Next(0, 100) > 40;
                    break;

                case "ballistics":
                    analysis.Results = JsonSerializer.Serialize(new
                    {
                        caliber = ".45 ACP",
                        striationPattern = "Unique pattern identified",
                        weaponType = "Pistol",
                        firingPin = "Distinctive marks found"
                    });
                    analysis.ConfidenceLevel = random.NextDouble() * 0.2 + 0.8; // 80-100%
                    analysis.IsMatch = random.Next(0, 100) > 30;
                    break;

                default:
                    analysis.Results = JsonSerializer.Serialize(new
                    {
                        analysisCompleted = true,
                        findings = "Analysis completed with standard procedures"
                    });
                    analysis.ConfidenceLevel = random.NextDouble() * 0.4 + 0.6;
                    analysis.IsMatch = random.Next(0, 100) > 50;
                    break;
            }

            // Update evidence status
            var evidence = await _context.Evidences.FindAsync(analysis.EvidenceId);
            if (evidence != null)
            {
                evidence.AnalysisStatus = EvidenceStatus.Completed;
                evidence.AnalysisCompletedAt = DateTime.UtcNow;
                evidence.AnalysisResult = analysis.Results;
            }

            await _context.SaveChangesAsync();
        }
    }

    public class RequestAnalysisDto
    {
        public int EvidenceId { get; set; }
        public required string AnalysisType { get; set; }
    }
}