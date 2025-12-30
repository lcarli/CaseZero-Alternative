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
    [Route("api/cases/{caseId}/emails")]
    [Authorize]
    public class EmailsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailsController> _logger;
        private readonly ICaseV1StorageService _caseStorageService;

        public EmailsController(
            ApplicationDbContext context,
            ILogger<EmailsController> logger,
            ICaseV1StorageService caseStorageService)
        {
            _context = context;
            _logger = logger;
            _caseStorageService = caseStorageService;
        }

        /// <summary>
        /// GET /api/cases/{caseId}/emails
        /// Retorna emails visíveis na sessão ativa do usuário
        /// CRÍTICO: Filtra por CaseSessionVisibleEmails
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCaseEmails(string caseId)
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
                    _logger.LogWarning("User {UserId} attempted to access emails from case {CaseId} without permission",
                        userId, caseId);
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

                // 3. 🔒 FILTRO CRÍTICO: Buscar emails visíveis na sessão
                var visibleEmailIds = await _context.CaseSessionVisibleEmails
                    .Where(ve => ve.UserId == userId && ve.CaseId == caseId)
                    .Select(ve => ve.EmailId)
                    .ToListAsync();

                if (!visibleEmailIds.Any())
                {
                    _logger.LogInformation("User {UserId} has no visible emails for case {CaseId}", userId, caseId);
                    return Ok(new List<CaseEmailDto>());
                }

                // 4. Carregar case.json para obter dados dos emails
                var caseData = await _caseStorageService.GetCaseAsync(caseId);
                if (caseData == null || caseData.Emails == null || !caseData.Emails.Any())
                {
                    _logger.LogWarning("Case data or emails not found for case {CaseId}", caseId);
                    return NotFound(new { message = "Case or emails not found" });
                }

                // 5. Filtrar emails pelo IDs visíveis
                var visibleEmails = caseData.Emails
                    .Where(e => visibleEmailIds.Contains(e.EmailId))
                    .ToList();

                // 6. Buscar estados dos emails (lidos, contagem de aberturas)
                var emailStates = await _context.CaseSessionEmailStates
                    .Where(es => es.UserId == userId && es.CaseId == caseId && visibleEmailIds.Contains(es.EmailId))
                    .ToDictionaryAsync(es => es.EmailId);

                // 7. Converter para DTOs
                var emailDtos = visibleEmails.Select(email => new CaseEmailDto
                {
                    EmailId = email.EmailId,
                    From = email.From,
                    To = email.To,
                    Subject = email.Subject,
                    SentAt = email.SentAt,
                    Priority = email.Priority,
                    HasAttachments = email.Attachments?.Any() ?? false,
                    AttachmentCount = email.Attachments?.Count ?? 0,
                    IsRead = emailStates.ContainsKey(email.EmailId) && emailStates[email.EmailId].ReadAt.HasValue,
                    ReadAt = emailStates.ContainsKey(email.EmailId) ? emailStates[email.EmailId].ReadAt : null,
                    OpenCount = emailStates.ContainsKey(email.EmailId) ? emailStates[email.EmailId].OpenCount : 0
                }).ToList();

                _logger.LogInformation("Returned {Count} visible emails for user {UserId} in case {CaseId}",
                    emailDtos.Count, userId, caseId);

                return Ok(emailDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting emails for user {UserId} in case {CaseId}", userId, caseId);
                return StatusCode(500, new { message = "An error occurred while retrieving emails" });
            }
        }
    }
}
