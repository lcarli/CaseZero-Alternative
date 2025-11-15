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
    [Route("api/[controller]")]
    [Authorize]
    public class NotesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotesController> _logger;

        public NotesController(ApplicationDbContext context, ILogger<NotesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all notes for a specific case
        /// </summary>
        [HttpGet("case/{caseId}")]
        public async Task<ActionResult<IEnumerable<NoteDto>>> GetNotesByCaseId(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var notes = await _context.Notes
                    .Where(n => n.UserId == userId && n.CaseId == caseId)
                    .OrderByDescending(n => n.UpdatedAt)
                    .Select(n => new NoteDto
                    {
                        Id = n.Id,
                        UserId = n.UserId,
                        CaseId = n.CaseId,
                        Title = n.Title,
                        Content = n.Content,
                        CreatedAt = n.CreatedAt,
                        UpdatedAt = n.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notes for case {CaseId}", caseId);
                return StatusCode(500, "Error fetching notes");
            }
        }

        /// <summary>
        /// Get a specific note by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<NoteDto>> GetNoteById(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var note = await _context.Notes
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (note == null)
                {
                    return NotFound("Note not found");
                }

                var noteDto = new NoteDto
                {
                    Id = note.Id,
                    UserId = note.UserId,
                    CaseId = note.CaseId,
                    Title = note.Title,
                    Content = note.Content,
                    CreatedAt = note.CreatedAt,
                    UpdatedAt = note.UpdatedAt
                };

                return Ok(noteDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching note {NoteId}", id);
                return StatusCode(500, "Error fetching note");
            }
        }

        /// <summary>
        /// Create a new note
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<NoteDto>> CreateNote([FromBody] CreateNoteRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var note = new Note
                {
                    UserId = userId,
                    CaseId = request.CaseId,
                    Title = request.Title,
                    Content = request.Content,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Notes.Add(note);
                await _context.SaveChangesAsync();

                var noteDto = new NoteDto
                {
                    Id = note.Id,
                    UserId = note.UserId,
                    CaseId = note.CaseId,
                    Title = note.Title,
                    Content = note.Content,
                    CreatedAt = note.CreatedAt,
                    UpdatedAt = note.UpdatedAt
                };

                _logger.LogInformation("Note created: {NoteId} for case {CaseId}", note.Id, request.CaseId);
                return CreatedAtAction(nameof(GetNoteById), new { id = note.Id }, noteDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating note for case {CaseId}", request.CaseId);
                return StatusCode(500, "Error creating note");
            }
        }

        /// <summary>
        /// Update an existing note
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<NoteDto>> UpdateNote(int id, [FromBody] UpdateNoteRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var note = await _context.Notes
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (note == null)
                {
                    return NotFound("Note not found");
                }

                note.Title = request.Title;
                note.Content = request.Content;
                note.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var noteDto = new NoteDto
                {
                    Id = note.Id,
                    UserId = note.UserId,
                    CaseId = note.CaseId,
                    Title = note.Title,
                    Content = note.Content,
                    CreatedAt = note.CreatedAt,
                    UpdatedAt = note.UpdatedAt
                };

                _logger.LogInformation("Note updated: {NoteId}", id);
                return Ok(noteDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating note {NoteId}", id);
                return StatusCode(500, "Error updating note");
            }
        }

        /// <summary>
        /// Delete a note
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var note = await _context.Notes
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (note == null)
                {
                    return NotFound("Note not found");
                }

                _context.Notes.Remove(note);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Note deleted: {NoteId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting note {NoteId}", id);
                return StatusCode(500, "Error deleting note");
            }
        }
    }
}
