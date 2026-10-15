using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CaseZeroApi.Data;
using CaseZeroApi.Models;

namespace CaseZeroApi.Controllers;

/// <summary>
/// Global, case-independent inbox for the current player. Surfaces system e-mails
/// stored in the Emails table that are not tied to a specific case (CaseId == null),
/// such as promotion notices. Distinct from <see cref="EmailsController"/>, which
/// serves per-case e-mails sourced from the case bundle.
/// </summary>
[ApiController]
[Route("api/inbox")]
[Authorize]
public class InboxController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<InboxController> _logger;

    public InboxController(ApplicationDbContext db, ILogger<InboxController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public record InboxEmailDto(
        int Id,
        string Subject,
        string Content,
        string? Preview,
        string Type,
        string Priority,
        DateTime SentAt,
        bool IsRead,
        string? MetadataJson);

    /// <summary>GET /api/inbox — global e-mails for the current user, newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetInbox(CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var emails = await _db.Emails.AsNoTracking()
            .Where(e => e.ToUserId == userId && e.CaseId == null)
            .OrderByDescending(e => e.SentAt)
            .Select(e => new InboxEmailDto(
                e.Id,
                e.Subject,
                e.Content,
                e.Preview,
                e.Type.ToString(),
                e.Priority.ToString(),
                e.SentAt,
                e.IsRead,
                e.MetadataJson))
            .ToListAsync(ct);

        return Ok(emails);
    }

    /// <summary>GET /api/inbox/unread-count — number of unread global e-mails.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var count = await _db.Emails.AsNoTracking()
            .CountAsync(e => e.ToUserId == userId && e.CaseId == null && !e.IsRead, ct);

        return Ok(new { unread = count });
    }

    /// <summary>POST /api/inbox/{id}/read — marks a global e-mail as read.</summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var email = await _db.Emails
            .FirstOrDefaultAsync(e => e.Id == id && e.ToUserId == userId && e.CaseId == null, ct);
        if (email is null) return NotFound();

        if (!email.IsRead)
        {
            email.IsRead = true;
            email.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }
}
