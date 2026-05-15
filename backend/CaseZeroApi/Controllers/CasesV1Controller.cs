using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Services;
using CaseZeroApi.Models.CaseV1;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;

namespace CaseZeroApi.Controllers;

/// <summary>
/// Controller for case.json v1.0 cases from Azure Blob Storage
/// </summary>
[ApiController]
[Route("api/cases/v1")]
[Authorize]
public class CasesV1Controller : ControllerBase
{
    private readonly ICaseV1StorageService _storageService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CasesV1Controller> _logger;

    public CasesV1Controller(
        ICaseV1StorageService storageService,
        ApplicationDbContext context,
        ILogger<CasesV1Controller> logger)
    {
        _storageService = storageService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/cases/v1/dashboard - Get dashboard data with available cases
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        try
        {
            var cases = await _storageService.ListCasesAsync(cancellationToken);
            
            // Return dashboard-compatible format (frontend expects "cases" not "availableCases")
            return Ok(new
            {
                stats = new
                {
                    casesResolved = 0,
                    casesActive = cases.Count,
                    successRate = 0.0,
                    averageRating = 0.0
                },
                cases = cases.Select(c => new
                {
                    id = c.CaseId,
                    title = c.Title,
                    description = c.Description,
                    difficulty = c.Difficulty,
                    category = c.Category,
                    estimatedTimeMinutes = c.EstimatedTimeMinutes
                }).ToList(),
                recentActivities = new object[] { }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard data");
            return StatusCode(500, new { error = "Failed to load dashboard" });
        }
    }

    /// <summary>
    /// GET /api/cases/v1 - List all available cases
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CaseV1Metadata>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CaseV1Metadata>>> ListCases(CancellationToken cancellationToken)
    {
        try
        {
            var cases = await _storageService.ListCasesAsync(cancellationToken);
            return Ok(cases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cases");
            return StatusCode(500, new { error = "Failed to list cases" });
        }
    }

    /// <summary>
    /// GET /api/cases/v1/{caseId} - Get specific case (sanitized)
    /// </summary>
    [HttpGet("{caseId}")]
    [ProducesResponseType(typeof(CaseV1), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseV1>> GetCase(string caseId, CancellationToken cancellationToken)
    {
        try
        {
            var caseData = await _storageService.GetCaseAsync(caseId, cancellationToken);
            
            if (caseData == null)
            {
                return NotFound(new { error = $"Case not found: {caseId}" });
            }

            return Ok(caseData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load case {CaseId}", caseId);
            return StatusCode(500, new { error = "Failed to load case" });
        }
    }

    /// <summary>
    /// GET /api/cases/v1/{caseId}/raw - Get case without sanitization (admin only)
    /// </summary>
    [HttpGet("{caseId}/raw")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CaseV1), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseV1>> GetCaseRaw(string caseId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogWarning("⚠️ Loading RAW case {CaseId} without sanitization - Admin access", caseId);
            
            var caseData = await _storageService.GetCaseRawAsync(caseId, cancellationToken);
            
            if (caseData == null)
            {
                return NotFound(new { error = $"Case not found: {caseId}" });
            }

            return Ok(caseData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load raw case {CaseId}", caseId);
            return StatusCode(500, new { error = "Failed to load case" });
        }
    }

    /// <summary>
    /// GET /api/cases/v1/{caseId}/assets/{assetId} - Download asset file
    /// </summary>
    [HttpGet("{caseId}/assets/{assetId}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsset(string caseId, string assetId, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await _storageService.GetAssetAsync(caseId, assetId, cancellationToken);
            
            if (stream == null)
            {
                return NotFound(new { error = $"Asset not found: {caseId}/{assetId}" });
            }

            var contentType = await _storageService.GetAssetContentTypeAsync(caseId, assetId, cancellationToken)
                ?? "application/octet-stream";

            // Extract filename from assetId (e.g., "asset.briefing_doc" -> "briefing_doc")
            var fileName = assetId.Contains('.') 
                ? assetId.Substring(assetId.LastIndexOf('.') + 1) 
                : assetId;

            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load asset {CaseId}/{AssetId}", caseId, assetId);
            return StatusCode(500, new { error = "Failed to load asset" });
        }
    }

    /// <summary>
    /// HEAD /api/cases/v1/{caseId} - Check if case exists
    /// </summary>
    [HttpHead("{caseId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CaseExists(string caseId, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _storageService.CaseExistsAsync(caseId, cancellationToken);
            return exists ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if case exists: {CaseId}", caseId);
            return StatusCode(500);
        }
    }

    // REMOVED: GetDashboard endpoint - used obsolete DTOs (DashboardDto, CaseDto, etc.)
    // Frontend uses separate endpoints for case list and user stats

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
