using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ForensicController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICaseAccessService _caseAccessService;
        private readonly ILogger<ForensicController> _logger;
        private readonly IForensicQueueService _forensicQueueService;

        public ForensicController(
            ApplicationDbContext context, 
            ICaseAccessService caseAccessService,
            ILogger<ForensicController> logger,
            IForensicQueueService forensicQueueService)
        {
            _context = context;
            _caseAccessService = caseAccessService;
            _logger = logger;
            _forensicQueueService = forensicQueueService;
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
                return StatusCode(403, new { message = "You don't have access to this case" });

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

        /// <summary>
        /// Get all visible evidences for forensic analysis for a specific case
        /// </summary>
        [HttpGet("case/{caseId}/visible-evidences")]
        public async Task<IActionResult> GetVisibleEvidencesForForensics(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // Check if user can access this case
                if (!await _caseAccessService.CanUserAccessCaseAsync(userId, caseId))
                {
                    return StatusCode(403, new { message = "User does not have access to this case" });
                }

                // Get visible evidences for the user
                var visibleEvidences = await _caseAccessService.GetVisibleEvidencesForUserAsync(userId, caseId);

                var forensicEvidences = visibleEvidences.Select(e => new {
                    id = e.Id,
                    name = e.Name,
                    type = e.Type,
                    fileName = e.FileName,
                    category = e.Category,
                    priority = e.Priority,
                    description = e.Description,
                    requiresAnalysis = e.RequiresAnalysis,
                    analysisRequired = e.AnalysisRequired,
                    canAnalyze = e.RequiresAnalysis || e.AnalysisRequired.Any(),
                    supportedAnalyses = GetSupportedAnalyses(e.Type, e.Category)
                }).ToList();

                return Ok(new {
                    caseId = caseId,
                    evidences = forensicEvidences,
                    totalCount = forensicEvidences.Count,
                    analysableCount = forensicEvidences.Count(e => e.canAnalyze)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting visible evidences for forensics: User {UserId}, Case {CaseId}", userId, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Request forensic analysis for evidence with time-based result delivery
        /// </summary>
        [HttpPost("case/{caseId}/evidence/{evidenceId}/analyze")]
        public async Task<IActionResult> RequestForensicAnalysis(string caseId, string evidenceId, [FromBody] ForensicAnalysisRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // Check if user can access this case
                if (!await _caseAccessService.CanUserAccessCaseAsync(userId, caseId))
                {
                    return StatusCode(403, new { message = "User does not have access to this case" });
                }

                // Get the case and evidence
                var userCase = await _caseAccessService.GetUserCaseInstanceAsync(userId, caseId);
                if (userCase == null)
                {
                    return NotFound("Case not found");
                }

                var evidence = userCase.Evidences.FirstOrDefault(e => e.Id == evidenceId);
                if (evidence == null)
                {
                    return NotFound("Evidence not found");
                }

                // Check if this evidence can be analyzed with the requested type
                var supportedAnalyses = GetSupportedAnalyses(evidence.Type, evidence.Category);
                if (!supportedAnalyses.Contains(request.AnalysisType))
                {
                    return BadRequest(new {
                        success = false,
                        message = "Este tipo de análise não é compatível com esta evidência.",
                        evidenceType = evidence.Type,
                        supportedAnalyses = supportedAnalyses
                    });
                }

                // Check if there's a forensic analysis defined for this evidence
                var forensicAnalysis = userCase.ForensicAnalyses.FirstOrDefault(fa => 
                    fa.EvidenceId == evidenceId && fa.AnalysisType.Equals(request.AnalysisType, StringComparison.OrdinalIgnoreCase));

                if (forensicAnalysis == null)
                {
                    // No analysis defined for this evidence/type combination
                    // Send "nothing can be done" email
                    return Ok(new {
                        success = true,
                        hasResult = false,
                        message = "Análise solicitada, mas não foram encontrados resultados relevantes para esta evidência.",
                        estimatedTime = "24 horas",
                        willReceiveEmail = true
                    });
                }

                // Schedule the analysis result to be delivered after the specified time
                var responseTime = forensicAnalysis.ResponseTime; // in minutes
                var deliveryTime = DateTime.UtcNow.AddMinutes(responseTime);

                // TODO: In a real implementation, this would:
                // 1. Schedule a background job to deliver results after responseTime
                // 2. Update evidence visibility status when time elapses
                // 3. Send email notification when results are ready

                _logger.LogInformation("Forensic analysis requested: User {UserId}, Case {CaseId}, Evidence {EvidenceId}, Type {AnalysisType}, Delivery in {ResponseTime} minutes", 
                    userId, caseId, evidenceId, request.AnalysisType, responseTime);

                return Ok(new {
                    success = true,
                    hasResult = true,
                    message = "Análise solicitada com sucesso. Resultado será entregue em breve.",
                    analysisType = request.AnalysisType,
                    estimatedDelivery = deliveryTime,
                    responseTimeMinutes = responseTime,
                    resultFile = forensicAnalysis.ResultFile
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting forensic analysis: User {UserId}, Case {CaseId}, Evidence {EvidenceId}", userId, caseId, evidenceId);
                return StatusCode(500, "Internal server error");
            }
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

        private static List<string> GetSupportedAnalyses(string evidenceType, string evidenceCategory)
        {
            return evidenceType.ToLower() switch
            {
                "physical" => new List<string> { "DNA", "Fingerprint", "Trace" },
                "document" => new List<string> { "HandwritingAnalysis", "DocumentAuthentication" },
                "digital" => new List<string> { "DigitalForensics", "MetadataAnalysis" },
                "audio" => new List<string> { "VoiceAnalysis", "AudioEnhancement" },
                "video" => new List<string> { "VideoAnalysis", "FacialRecognition" },
                "image" => new List<string> { "ImageAnalysis", "PhotoAuthentication" },
                _ => evidenceCategory.ToLower() switch
                {
                    "biological" => new List<string> { "DNA", "Toxicology" },
                    "ballistics" => new List<string> { "Ballistics", "GunpowderResidue" },
                    "technical" => new List<string> { "TechnicalAnalysis", "DigitalForensics" },
                    _ => new List<string> { "GeneralAnalysis" }
                }
            };
        }

        /// <summary>
        /// POST /api/forensics/request
        /// Endpoint moderno para submissão de análises forenses usando ForensicRequest
        /// Tarefas 32-34: Renomeado para inputAssetId, validação de visibilidade, enfileiramento
        /// </summary>
        [HttpPost("/api/forensics/request")]
        public async Task<IActionResult> SubmitForensicRequest([FromBody] SubmitForensicRequestDto request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // 1. Verificar acesso ao caso
                var userCase = await _context.UserCases
                    .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == request.CaseId);

                if (userCase == null)
                {
                    _logger.LogWarning("User {UserId} attempted to submit forensic request for case {CaseId} without permission",
                        userId, request.CaseId);
                    return Forbid("You don't have access to this case");
                }

                // 2. 🔒 VALIDAÇÃO CRÍTICA (tarefa 33): Verificar se inputAssetId está visível
                var isAssetVisible = await _context.CaseSessionVisibleAssets
                    .AnyAsync(va => va.UserId == userId && va.CaseId == request.CaseId && va.AssetId == request.InputAssetId);

                if (!isAssetVisible)
                {
                    _logger.LogWarning("User {UserId} attempted to analyze invisible asset {AssetId} in case {CaseId}",
                        userId, request.InputAssetId, request.CaseId);
                    return Forbid("Asset not visible in current session");
                }

                // 3. Criar registro ForensicRequest (tarefa 34)
                var forensicRequest = new ForensicRequest
                {
                    UserId = userId,
                    CaseId = request.CaseId,
                    InputAssetId = request.InputAssetId,
                    InputAssetName = request.InputAssetName ?? request.InputAssetId,
                    AnalysisType = request.AnalysisType,
                    Status = "pending",
                    RequestedAt = DateTime.UtcNow,
                    ResultEmailId = null // NULL inicialmente, será preenchido quando completar
                };

                _context.ForensicRequests.Add(forensicRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Forensic request created: Id {Id}, User {UserId}, Case {CaseId}, Asset {AssetId}, Type {AnalysisType}",
                    forensicRequest.Id, userId, request.CaseId, request.InputAssetId, request.AnalysisType);

                // 4. ✅ Tarefa 35: Enfileirar em Azure Storage Queue para processamento assíncrono
                await _forensicQueueService.EnqueueForensicRequestAsync(
                    forensicRequest.Id,
                    request.CaseId,
                    userId,
                    request.InputAssetId,
                    request.AnalysisType
                );

                return Ok(new
                {
                    requestId = forensicRequest.Id,
                    status = forensicRequest.Status,
                    message = "Forensic analysis request submitted successfully",
                    estimatedCompletionMinutes = 5 // TODO: calcular baseado em GameTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting forensic request: User {UserId}, Case {CaseId}, Asset {AssetId}",
                    userId, request.CaseId, request.InputAssetId);
                return StatusCode(500, new { message = "An error occurred while submitting the forensic request" });
            }
        }
    }

    public class SubmitForensicRequestDto
    {
        public required string CaseId { get; set; }
        public required string InputAssetId { get; set; } // Renomeado de evidenceId
        public string? InputAssetName { get; set; }
        public required string AnalysisType { get; set; }
    }

    public class RequestAnalysisDto
    {
        public int EvidenceId { get; set; }
        public required string AnalysisType { get; set; }
    }

    public class ForensicAnalysisRequest
    {
        public required string AnalysisType { get; set; }
        public string? Notes { get; set; }
    }
}