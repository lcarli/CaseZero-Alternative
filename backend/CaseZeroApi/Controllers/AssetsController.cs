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
    [Route("api/cases/{caseId}/assets")]
    [Authorize]
    public class AssetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AssetsController> _logger;
        private readonly IBlobStorageService _blobStorageService;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;

        public AssetsController(
            ApplicationDbContext context, 
            ILogger<AssetsController> logger,
            IBlobStorageService blobStorageService,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _blobStorageService = blobStorageService;
            _configuration = configuration;
            
            // Initialize BlobServiceClient for direct blob access
            var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
                ?? configuration["AzureWebJobsStorage"]
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? "UseDevelopmentStorage=true";
            _blobServiceClient = new BlobServiceClient(connectionString);
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

        /// <summary>
        /// GET /api/cases/{caseId}/assets/{assetId}/download
        /// Download de asset individual com validação de visibilidade
        /// CRÍTICO: Valida se assetId está em CaseSessionVisibleAssets antes do download
        /// </summary>
        [HttpGet("{assetId}/download")]
        public async Task<IActionResult> DownloadAsset(string caseId, string assetId)
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
                    _logger.LogWarning("User {UserId} attempted to download asset from case {CaseId} without permission", 
                        userId, caseId);
                    return StatusCode(403, new { message = "You don't have access to this case" });
                }

                // 2. 🔒 VALIDAÇÃO CRÍTICA: Verificar se asset está visível
                var isVisible = await _context.CaseSessionVisibleAssets
                    .AnyAsync(va => va.UserId == userId && va.CaseId == caseId && va.AssetId == assetId);

                if (!isVisible)
                {
                    _logger.LogWarning("User {UserId} attempted to download invisible asset {AssetId} from case {CaseId}", 
                        userId, assetId, caseId);
                    return StatusCode(403, new { message = "Asset not visible in current session" });
                }

                // 3. Download do blob
                var containerName = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                
                // Asset path: {caseId}/assets/{assetId}
                var blobPath = $"{caseId}/assets/{assetId}";
                var blobClient = containerClient.GetBlobClient(blobPath);

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogWarning("Asset file not found in blob storage: {BlobPath}", blobPath);
                    return NotFound(new { message = "Asset file not found" });
                }

                // 4. Stream do arquivo
                var download = await blobClient.DownloadStreamingAsync();
                var properties = await blobClient.GetPropertiesAsync();
                
                var contentType = properties.Value.ContentType;
                var fileName = assetId.Contains('/') ? assetId.Split('/').Last() : assetId;

                _logger.LogInformation("User {UserId} downloaded asset {AssetId} from case {CaseId}", 
                    userId, assetId, caseId);

                return File(download.Value.Content, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading asset {AssetId} for user {UserId} in case {CaseId}", 
                    assetId, userId, caseId);
                return StatusCode(500, new { message = "An error occurred while downloading the asset" });
            }
        }
    }
}
