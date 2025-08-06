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
    public class EmailController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailController> _logger;

        public EmailController(ApplicationDbContext context, ILogger<EmailController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetEmails([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var emails = await _context.Emails
                .Include(e => e.FromUser)
                .Where(e => e.ToUserId == userId)
                .OrderByDescending(e => e.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var emailDtos = emails.Select(e => new EmailDto
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
                Attachments = string.IsNullOrEmpty(e.Attachments) 
                    ? new List<EmailAttachmentDto>() 
                    : JsonSerializer.Deserialize<List<EmailAttachmentDto>>(e.Attachments)!
            }).ToList();

            return Ok(emailDtos);
        }

        [HttpGet("{emailId}")]
        public async Task<IActionResult> GetEmail(int emailId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var email = await _context.Emails
                .Include(e => e.FromUser)
                .FirstOrDefaultAsync(e => e.Id == emailId && e.ToUserId == userId);

            if (email == null)
                return NotFound();

            // Mark as read
            if (!email.IsRead)
            {
                email.IsRead = true;
                email.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            var dto = new EmailDto
            {
                Id = email.Id,
                CaseId = email.CaseId,
                Subject = email.Subject,
                Content = email.Content,
                Preview = email.Preview,
                SentAt = email.SentAt,
                IsRead = email.IsRead,
                Priority = email.Priority,
                Type = email.Type,
                SenderName = email.FromUser.FirstName + " " + email.FromUser.LastName,
                Attachments = string.IsNullOrEmpty(email.Attachments) 
                    ? new List<EmailAttachmentDto>() 
                    : JsonSerializer.Deserialize<List<EmailAttachmentDto>>(email.Attachments)!
            };

            return Ok(dto);
        }

        [HttpGet("unread/count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var count = await _context.Emails
                .CountAsync(e => e.ToUserId == userId && !e.IsRead);

            return Ok(new { count });
        }

        [HttpPost("{emailId}/mark-read")]
        public async Task<IActionResult> MarkAsRead(int emailId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var email = await _context.Emails
                .FirstOrDefaultAsync(e => e.Id == emailId && e.ToUserId == userId);

            if (email == null)
                return NotFound();

            email.IsRead = true;
            email.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Email marked as read" });
        }

        [HttpPost("send-case-briefing")]
        public async Task<IActionResult> SendCaseBriefing([FromBody] SendCaseBriefingDto request)
        {
            // This would typically be restricted to administrators or system
            // For demo purposes, allowing authenticated users to send briefings
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var case_ = await _context.Cases.FindAsync(request.CaseId);
            if (case_ == null)
                return NotFound("Case not found");

            var targetUser = await _context.Users.FindAsync(request.ToUserId);
            if (targetUser == null)
                return NotFound("User not found");

            // Create briefing email
            var briefingContent = $@"BRIEFING DO CASO: {case_.Title}

DETALHES DO INCIDENTE:
- Localização: {case_.Location ?? "A ser determinado"}
- Data do Incidente: {case_.IncidentDate?.ToString("dd/MM/yyyy HH:mm") ?? "A ser determinado"}
- Prioridade: {case_.Priority}
- Nível de Dificuldade: {case_.EstimatedDifficultyLevel}/10

DESCRIÇÃO:
{case_.BriefingText ?? case_.Description}

INSTRUÇÕES:
1. Revise todos os arquivos de evidência disponíveis
2. Solicite análises forenses conforme necessário
3. Identifique suspeitos e verifique álibis
4. Colete evidências suficientes para fundamentar uma acusação
5. Submeta seu relatório final com suspeito e evidências

Este caso requer rank mínimo: {GetRankName(case_.MinimumRankRequired)}

Bom trabalho, detetive.

Chefe Johnson
Departamento de Polícia Metropolitana";

            var attachments = new List<EmailAttachmentDto>();

            // Add initial case files as attachments
            var initialEvidences = await _context.Evidences
                .Where(e => e.CaseId == request.CaseId && e.IsUnlocked)
                .Take(3)
                .ToListAsync();

            foreach (var evidence in initialEvidences)
            {
                attachments.Add(new EmailAttachmentDto
                {
                    Name = evidence.Name,
                    Size = "varies",
                    Type = evidence.Type
                });
            }

            var email = new Email
            {
                ToUserId = request.ToUserId,
                FromUserId = userId,
                CaseId = request.CaseId,
                Subject = $"URGENTE: Designação de Caso #{case_.Id}",
                Content = briefingContent,
                Preview = $"Nova designação de caso: {case_.Title}. Prioridade {case_.Priority}...",
                Type = EmailType.CaseBriefing,
                Priority = case_.Priority switch
                {
                    CasePriority.Critical or CasePriority.Emergency => EmailPriority.Urgent,
                    CasePriority.High => EmailPriority.High,
                    _ => EmailPriority.Normal
                },
                Attachments = JsonSerializer.Serialize(attachments),
                IsSystemGenerated = false
            };

            _context.Emails.Add(email);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Case briefing sent successfully", emailId = email.Id });
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

    public class SendCaseBriefingDto
    {
        public required string CaseId { get; set; }
        public required string ToUserId { get; set; }
    }
}