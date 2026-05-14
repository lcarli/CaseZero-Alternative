using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly ICaseV2StorageService _storage;
    private readonly ISolutionService _solutionService;
    private readonly ILogger<CasesController> _logger;

    public CasesController(
        ICaseV2StorageService storage,
        ISolutionService solutionService,
        ILogger<CasesController> logger)
    {
        _storage = storage;
        _solutionService = solutionService;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var cases = await _storage.ListCasesAsync(ct);
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
                caseId = c.CaseId,
                title = c.Title,
                description = c.Description,
                difficulty = c.Difficulty,
                category = c.Category,
                estimatedDurationMinutes = c.EstimatedDurationMinutes,
                briefing = c.Briefing,
                tags = c.Tags,
                requiredRank = c.RequiredRank
            }).ToList(),
            recentActivities = Array.Empty<object>()
        });
    }

    [HttpGet]
    public async Task<ActionResult<List<CaseV2Metadata2>>> ListCases(CancellationToken ct)
    {
        var cases = await _storage.ListCasesAsync(ct);
        return Ok(cases);
    }

    [HttpGet("{caseId}")]
    public async Task<ActionResult<CaseV2Sanitized>> GetCase(string caseId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var data = await _storage.GetForUserAsync(caseId, userId, ct);
        return data is null ? NotFound(new { error = $"Case not found: {caseId}" }) : Ok(data);
    }

    [HttpGet("{caseId}/raw")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CaseV2>> GetCaseRaw(string caseId, CancellationToken ct)
    {
        var data = await _storage.GetRawAsync(caseId, ct);
        return data is null ? NotFound(new { error = $"Case not found: {caseId}" }) : Ok(data);
    }

    [HttpGet("{caseId}/assets")]
    public async Task<IActionResult> ListCaseAssets(string caseId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var data = await _storage.GetForUserAsync(caseId, userId, ct);
        return data is null ? NotFound() : Ok(data.Assets);
    }

    [HttpGet("{caseId}/emails")]
    public async Task<IActionResult> ListCaseEmails(string caseId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var data = await _storage.GetForUserAsync(caseId, userId, ct);
        return data is null ? NotFound() : Ok(data.Emails);
    }

    [HttpHead("{caseId}")]
    public async Task<IActionResult> CaseExists(string caseId, CancellationToken ct)
    {
        return await _storage.CaseExistsAsync(caseId, ct) ? Ok() : NotFound();
    }

    [HttpPost("{caseId}/submit")]
    public async Task<IActionResult> Submit(string caseId, [FromBody] SubmitCaseRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Request body required" });
        try
        {
            var result = await _solutionService.SubmitAsync(caseId, userId, request, ct);
            return Ok(result);
        }
        catch (MaxAttemptsExceededException ex)
        {
            return Conflict(new { error = ex.Message, maxAttempts = ex.MaxAttempts });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
