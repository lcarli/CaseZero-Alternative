using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;
using CaseZeroApi.Services;
using Azure.Storage.Blobs;

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
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly IAuditLogService _auditLogService; // P86

        public EmailsController(
            ApplicationDbContext context,
            ILogger<EmailsController> logger,
            ICaseV1StorageService caseStorageService,
            IConfiguration configuration,
            IAuditLogService auditLogService) // P86
        {
            _context = context;
            _logger = logger;
            _caseStorageService = caseStorageService;
            _configuration = configuration;
            _auditLogService = auditLogService; // P86
            
            // Initialize BlobServiceClient for attachment downloads
            var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
                ?? configuration["AzureWebJobsStorage"]
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? "UseDevelopmentStorage=true";
            _blobServiceClient = new BlobServiceClient(connectionString);
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
                    return StatusCode(403, new { message = "You don't have access to this case" });
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
                // 🔒 IMPORTANT: Use GetCaseRawAsync to access all emails (visibility already filtered)
                var caseData = await _caseStorageService.GetCaseRawAsync(caseId);
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

        /// <summary>
        /// POST /api/cases/{caseId}/emails/{emailId}/open
        /// Marca email como aberto e incrementa contagem de aberturas
        /// Insere/atualiza em CaseSessionEmailState
        /// </summary>
        [HttpPost("{emailId}/open")]
        public async Task<IActionResult> OpenEmail(string caseId, string emailId)
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
                    _logger.LogWarning("User {UserId} attempted to open email from case {CaseId} without permission",
                        userId, caseId);
                    return StatusCode(403, new { message = "You don't have access to this case" });
                }

                // 2. 🔒 VALIDAÇÃO CRÍTICA: Verificar se email está visível
                var isVisible = await _context.CaseSessionVisibleEmails
                    .AnyAsync(ve => ve.UserId == userId && ve.CaseId == caseId && ve.EmailId == emailId);

                if (!isVisible)
                {
                    _logger.LogWarning("User {UserId} attempted to open invisible email {EmailId} in case {CaseId}",
                        userId, emailId, caseId);
                    return StatusCode(403, new { message = "Email not visible in current session" });
                }

                // 3. Buscar ou criar estado do email
                var emailState = await _context.CaseSessionEmailStates
                    .FirstOrDefaultAsync(es => es.UserId == userId && es.CaseId == caseId && es.EmailId == emailId);

                if (emailState == null)
                {
                    // Primeira abertura - criar novo estado
                    emailState = new CaseSessionEmailState
                    {
                        UserId = userId,
                        CaseId = caseId,
                        EmailId = emailId,
                        ReadAt = DateTime.UtcNow,
                        OpenCount = 1
                    };
                    _context.CaseSessionEmailStates.Add(emailState);
                    _logger.LogInformation("User {UserId} opened email {EmailId} for the first time in case {CaseId}",
                        userId, emailId, caseId);
                }
                else
                {
                    // Email já foi aberto - incrementar contagem
                    if (!emailState.ReadAt.HasValue)
                    {
                        emailState.ReadAt = DateTime.UtcNow;
                    }
                    emailState.OpenCount++;
                    _logger.LogInformation("User {UserId} opened email {EmailId} (count: {Count}) in case {CaseId}",
                        userId, emailId, emailState.OpenCount, caseId);
                }

                await _context.SaveChangesAsync();

                // P86: Audit log - email aberto
                await _auditLogService.LogActionAsync(
                    userId,
                    "email_open",
                    $"{caseId}/email/{emailId}",
                    caseId,
                    "success",
                    $"{{{{\"openCount\":{emailState.OpenCount}}}}}");

                return Ok(new
                {
                    emailId,
                    readAt = emailState.ReadAt,
                    openCount = emailState.OpenCount,
                    message = "Email opened successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening email {EmailId} for user {UserId} in case {CaseId}",
                    emailId, userId, caseId);
                return StatusCode(500, new { message = "An error occurred while opening the email" });
            }
        }

        /// <summary>
        /// GET /api/cases/{caseId}/emails/{emailId}
        /// Retorna email completo incluindo conteúdo e attachments
        /// CRÍTICO: Valida visibilidade antes de retornar
        /// </summary>
        [HttpGet("{emailId}")]
        public async Task<IActionResult> GetEmailDetails(string caseId, string emailId)
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
                    _logger.LogWarning("User {UserId} attempted to get email details from case {CaseId} without permission",
                        userId, caseId);
                    return StatusCode(403, new { message = "You don't have access to this case" });
                }

                // 2. 🔒 VALIDAÇÃO CRÍTICA: Verificar se email está visível
                var isVisible = await _context.CaseSessionVisibleEmails
                    .AnyAsync(ve => ve.UserId == userId && ve.CaseId == caseId && ve.EmailId == emailId);

                if (!isVisible)
                {
                    _logger.LogWarning("User {UserId} attempted to access invisible email {EmailId} in case {CaseId}",
                        userId, emailId, caseId);
                    return StatusCode(403, new { message = "Email not visible in current session" });
                }

                // 3. Carregar case.json para obter email completo
                // 🔒 IMPORTANT: Use GetCaseRawAsync to access email content (visibility already checked)
                var caseData = await _caseStorageService.GetCaseRawAsync(caseId);
                if (caseData == null || caseData.Emails == null)
                {
                    _logger.LogWarning("Case data or emails not found for case {CaseId}", caseId);
                    return NotFound(new { message = "Case or emails not found" });
                }

                // 4. Buscar email específico
                var email = caseData.Emails.FirstOrDefault(e => e.EmailId == emailId);
                if (email == null)
                {
                    _logger.LogWarning("Email {EmailId} not found in case {CaseId}", emailId, caseId);
                    return NotFound(new { message = "Email not found" });
                }

                // 5. Buscar estado do email
                var emailState = await _context.CaseSessionEmailStates
                    .FirstOrDefaultAsync(es => es.UserId == userId && es.CaseId == caseId && es.EmailId == emailId);

                // 6. Retornar email completo
                var emailDetails = new
                {
                    emailId = email.EmailId,
                    from = email.From,
                    to = email.To,
                    subject = email.Subject,
                    sentAt = email.SentAt,
                    priority = email.Priority,
                    content = email.Content,
                    attachments = email.Attachments ?? new List<string>(),
                    metadata = email.Metadata,
                    isRead = emailState?.ReadAt.HasValue ?? false,
                    readAt = emailState?.ReadAt,
                    openCount = emailState?.OpenCount ?? 0
                };

                _logger.LogInformation("User {UserId} accessed email {EmailId} details in case {CaseId}",
                    userId, emailId, caseId);

                return Ok(emailDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting email {EmailId} details for user {UserId} in case {CaseId}",
                    emailId, userId, caseId);
                return StatusCode(500, new { message = "An error occurred while retrieving email details" });
            }
        }

        /// <summary>
        /// POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download
        /// Download de attachment de email com validações e hooks
        /// CRÍTICO: Valida visibilidade do email, registra download e aplica regras de reveal
        /// </summary>
        [HttpPost("{emailId}/attachments/{assetId}/download")]
        public async Task<IActionResult> DownloadEmailAttachment(string caseId, string emailId, string assetId)
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
                    _logger.LogWarning("User {UserId} attempted to download attachment from case {CaseId} without permission",
                        userId, caseId);
                    return StatusCode(403, new { message = "You don't have access to this case" });
                }

                // 2. 🔒 VALIDAÇÃO CRÍTICA: Verificar se email está visível (tarefa 31)
                var isEmailVisible = await _context.CaseSessionVisibleEmails
                    .AnyAsync(ve => ve.UserId == userId && ve.CaseId == caseId && ve.EmailId == emailId);

                if (!isEmailVisible)
                {
                    _logger.LogWarning("User {UserId} attempted to download attachment from invisible email {EmailId} in case {CaseId}",
                        userId, emailId, caseId);
                    return StatusCode(403, new { message = "Email not visible in current session" });
                }

                // 3. Carregar email para verificar se assetId está nos attachments
                // 🔒 IMPORTANT: Use GetCaseRawAsync to access hidden emails (visibility already checked above)
                var caseData = await _caseStorageService.GetCaseRawAsync(caseId);
                if (caseData == null || caseData.Emails == null)
                {
                    _logger.LogWarning("Case data not found for case {CaseId}", caseId);
                    return NotFound(new { message = "Case not found" });
                }

                var email = caseData.Emails.FirstOrDefault(e => e.EmailId == emailId);
                if (email == null)
                {
                    _logger.LogWarning("Email {EmailId} not found in case {CaseId}", emailId, caseId);
                    return NotFound(new { message = "Email not found" });
                }

                // 4. Verificar se assetId está nos attachments do email
                if (email.Attachments == null || !email.Attachments.Contains(assetId))
                {
                    _logger.LogWarning("Asset {AssetId} is not an attachment of email {EmailId} in case {CaseId}",
                        assetId, emailId, caseId);
                    return BadRequest(new { message = "Asset is not an attachment of this email" });
                }

                // 5. Download do blob (reutilizando lógica do AssetsController)
                var containerName = _configuration["CaseGeneratorStorage:CasesContainer"] ?? "cases";
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                
                // Asset path: {caseId}/assets/{assetId}
                var blobPath = $"{caseId}/assets/{assetId}";
                var blobClient = containerClient.GetBlobClient(blobPath);

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogWarning("Attachment file not found in blob storage: {BlobPath}", blobPath);
                    return NotFound(new { message = "Attachment file not found" });
                }

                // 6. 🎯 HOOK PÓS-DOWNLOAD (tarefa 30): Registrar em EmailAttachmentsDownloaded
                var downloadRecord = new EmailAttachmentDownloaded
                {
                    UserId = userId,
                    CaseId = caseId,
                    EmailId = emailId,
                    AssetId = assetId,
                    DownloadedAt = DateTime.UtcNow
                };
                _context.EmailAttachmentsDownloaded.Add(downloadRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} downloaded attachment {AssetId} from email {EmailId} in case {CaseId}",
                    userId, assetId, emailId, caseId);

                // 7. 🎯 HOOK: Aplicar regra reveal_asset (tarefa 30)
                // TODO: Implementar RulesEngine.ApplyRule("reveal_asset", assetId)
                // Por enquanto, apenas log
                _logger.LogInformation("Triggering reveal_asset rule for asset {AssetId} in case {CaseId}", assetId, caseId);

                // 8. Stream do arquivo
                var download = await blobClient.DownloadStreamingAsync();
                var properties = await blobClient.GetPropertiesAsync();
                
                var contentType = properties.Value.ContentType;
                var fileName = assetId.Contains('/') ? assetId.Split('/').Last() : assetId;

                return File(download.Value.Content, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading attachment {AssetId} from email {EmailId} for user {UserId} in case {CaseId}",
                    assetId, emailId, userId, caseId);
                return StatusCode(500, new { message = "An error occurred while downloading the attachment" });
            }
        }
    }
}
