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
    public class EvidenceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EvidenceController> _logger;

        public EvidenceController(ApplicationDbContext context, ILogger<EvidenceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("case/{caseId}")]
        public async Task<IActionResult> GetCaseEvidence(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify user has access to the case
            var userCase = await _context.UserCases
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == caseId);

            if (userCase == null)
                return Forbid("You don't have access to this case");

            var evidences = await _context.Evidences
                .Include(e => e.ForensicAnalyses)
                .Where(e => e.CaseId == caseId)
                .ToListAsync();

            var evidenceDtos = evidences.Select(e => new EvidenceDto
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
                    : JsonSerializer.Deserialize<List<string>>(e.DependsOnEvidenceIds)!,
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
            }).ToList();

            // Check dependencies and unlock evidence if conditions are met
            await CheckAndUnlockEvidence(caseId, evidenceDtos);

            return Ok(evidenceDtos);
        }

        [HttpGet("{evidenceId}")]
        public async Task<IActionResult> GetEvidence(int evidenceId)
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

            var dto = new EvidenceDto
            {
                Id = evidence.Id,
                CaseId = evidence.CaseId,
                Name = evidence.Name,
                Type = evidence.Type,
                Description = evidence.Description,
                FilePath = evidence.FilePath,
                CollectedAt = evidence.CollectedAt,
                IsUnlocked = evidence.IsUnlocked,
                RequiresAnalysis = evidence.RequiresAnalysis,
                AnalysisStatus = evidence.AnalysisStatus,
                AnalysisResult = evidence.AnalysisResult,
                Category = evidence.Category,
                Priority = evidence.Priority,
                DependsOnEvidenceIds = string.IsNullOrEmpty(evidence.DependsOnEvidenceIds) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(evidence.DependsOnEvidenceIds)!,
                ForensicAnalyses = evidence.ForensicAnalyses.Select(fa => new ForensicAnalysisDto
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
            };

            return Ok(dto);
        }

        [HttpPost("{evidenceId}/unlock")]
        public async Task<IActionResult> UnlockEvidence(int evidenceId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var evidence = await _context.Evidences
                .Include(e => e.Case)
                .ThenInclude(c => c.UserCases)
                .FirstOrDefaultAsync(e => e.Id == evidenceId);

            if (evidence == null)
                return NotFound();

            if (!evidence.Case.UserCases.Any(uc => uc.UserId == userId))
                return Forbid();

            // Check if dependencies are met
            if (!string.IsNullOrEmpty(evidence.DependsOnEvidenceIds))
            {
                var dependencyIds = JsonSerializer.Deserialize<List<string>>(evidence.DependsOnEvidenceIds) ?? new List<string>();
                var dependencies = await _context.Evidences
                    .Where(e => dependencyIds.Contains(e.Id.ToString()) && e.CaseId == evidence.CaseId)
                    .ToListAsync();

                var unmetDependencies = dependencies
                    .Where(d => !d.IsUnlocked || (d.RequiresAnalysis && d.AnalysisStatus != EvidenceStatus.Completed))
                    .ToList();

                if (unmetDependencies.Any())
                {
                    return BadRequest(new { 
                        message = "Dependencies not met", 
                        unmetDependencies = unmetDependencies.Select(d => d.Name).ToList() 
                    });
                }
            }

            evidence.IsUnlocked = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Evidence unlocked successfully" });
        }

        private async Task CheckAndUnlockEvidence(string caseId, List<EvidenceDto> evidences)
        {
            var evidenceEntities = await _context.Evidences
                .Where(e => e.CaseId == caseId)
                .ToListAsync();

            bool hasUpdates = false;

            foreach (var evidence in evidenceEntities.Where(e => !e.IsUnlocked))
            {
                if (string.IsNullOrEmpty(evidence.DependsOnEvidenceIds))
                {
                    evidence.IsUnlocked = true;
                    hasUpdates = true;
                    continue;
                }

                var dependencyIds = JsonSerializer.Deserialize<List<string>>(evidence.DependsOnEvidenceIds) ?? new List<string>();
                var dependencies = evidenceEntities
                    .Where(e => dependencyIds.Contains(e.Id.ToString()))
                    .ToList();

                var dependenciesMet = dependencies.All(d => 
                    d.IsUnlocked && (!d.RequiresAnalysis || d.AnalysisStatus == EvidenceStatus.Completed));

                if (dependenciesMet)
                {
                    evidence.IsUnlocked = true;
                    hasUpdates = true;
                }
            }

            if (hasUpdates)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}