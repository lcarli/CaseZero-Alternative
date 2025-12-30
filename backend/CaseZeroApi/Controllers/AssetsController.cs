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
    [Route("api/cases/{caseId}/assets")]
    [Authorize]
    public class AssetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AssetsController> _logger;

        public AssetsController(ApplicationDbContext context, ILogger<AssetsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/cases/{caseId}/assets
        /// Retorna assets visíveis na sessão ativa do usuário
        /// CRÍTICO: Filtra por CaseSessionVisibleAssets
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCaseAssets(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // 1. Verificar acesso ao caso
                var userCase = await _context.UserCases
                    .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == caseId);

                if (userCase == null)
                {
                    _logger.LogWarning("User {UserId} attempted to access case {CaseId} without permission", userId, caseId);
                    return Forbid("You don't have access to this case");
                }

                // 2. Buscar sessão ativa do usuário
                var activeSession = await _context.CaseSessions
                    .Where(cs => cs.UserId == userId && cs.CaseId == caseId && cs.Status == SessionStatus.Active)
                    .OrderByDescending(cs => cs.SessionStart)
                    .FirstOrDefaultAsync();

                if (activeSession == null)
                {
                    _logger.LogWarning("User {UserId} has no active session for case {CaseId}", userId, caseId);
                    return BadRequest(new { message = "No active session found. Please start a session first." });
                }

                // 3. 🔒 FILTRO CRÍTICO: Buscar assets visíveis na sessão
                var visibleAssetIds = await _context.CaseSessionVisibleAssets
                    .Where(va => va.UserId == userId && va.CaseId == caseId)
                    .Select(va => va.AssetId)
                    .ToListAsync();

                if (!visibleAssetIds.Any())
                {
                    _logger.LogInformation("User {UserId} has no visible assets for case {CaseId}", userId, caseId);
                    return Ok(new List<AssetDto>());
                }

                // 4. Retornar metadata dos assets (formato mantido compatível com EvidenceDto)
                var assets = visibleAssetIds.Select(assetId => new AssetDto
                {
                    Id = assetId,
                    CaseId = caseId,
                    Name = assetId, // Nome será enriquecido depois com case.json
                    Type = "file", // Tipo genérico, será enriquecido depois
                    IsVisible = true
                }).ToList();

                _logger.LogInformation("Returned {Count} visible assets for user {UserId} in case {CaseId}", 
                    assets.Count, userId, caseId);

                return Ok(assets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assets for user {UserId} in case {CaseId}", userId, caseId);
                return StatusCode(500, new { message = "An error occurred while retrieving assets" });
            }
        }
    }
}
