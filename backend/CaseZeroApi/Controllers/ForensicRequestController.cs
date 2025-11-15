using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using System.Security.Claims;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ForensicRequestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ForensicRequestController> _logger;

        public ForensicRequestController(ApplicationDbContext context, ILogger<ForensicRequestController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/forensicrequest/{caseId}
        [HttpGet("{caseId}")]
        public async Task<ActionResult<IEnumerable<ForensicRequest>>> GetForensicRequests(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var requests = await _context.ForensicRequests
                .Where(fr => fr.CaseId == caseId && fr.UserId == userId)
                .OrderByDescending(fr => fr.RequestedAt)
                .ToListAsync();

            return Ok(requests);
        }

        // GET: api/forensicrequest/{caseId}/pending
        [HttpGet("{caseId}/pending")]
        public async Task<ActionResult<IEnumerable<ForensicRequest>>> GetPendingForensicRequests(string caseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var requests = await _context.ForensicRequests
                .Where(fr => fr.CaseId == caseId && fr.UserId == userId && 
                            (fr.Status == "pending" || fr.Status == "in-progress"))
                .OrderBy(fr => fr.EstimatedCompletionTime)
                .ToListAsync();

            return Ok(requests);
        }

        // GET: api/forensicrequest/{caseId}/{id}
        [HttpGet("{caseId}/{id}")]
        public async Task<ActionResult<ForensicRequest>> GetForensicRequest(string caseId, int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var request = await _context.ForensicRequests
                .FirstOrDefaultAsync(fr => fr.Id == id && fr.CaseId == caseId && fr.UserId == userId);

            if (request == null)
            {
                return NotFound();
            }

            return Ok(request);
        }

        // POST: api/forensicrequest
        [HttpPost]
        public async Task<ActionResult<ForensicRequest>> CreateForensicRequest(ForensicRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            request.UserId = userId;
            request.Status = "pending";

            _context.ForensicRequests.Add(request);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetForensicRequest), 
                new { caseId = request.CaseId, id = request.Id }, request);
        }

        // PUT: api/forensicrequest/{caseId}/{id}
        [HttpPut("{caseId}/{id}")]
        public async Task<IActionResult> UpdateForensicRequest(string caseId, int id, ForensicRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (id != request.Id || caseId != request.CaseId)
            {
                return BadRequest();
            }

            var existingRequest = await _context.ForensicRequests
                .FirstOrDefaultAsync(fr => fr.Id == id && fr.CaseId == caseId && fr.UserId == userId);

            if (existingRequest == null)
            {
                return NotFound();
            }

            existingRequest.Status = request.Status;
            existingRequest.CompletedAt = request.CompletedAt;
            existingRequest.ResultDocumentId = request.ResultDocumentId;
            existingRequest.Notes = request.Notes;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ForensicRequestExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/forensicrequest/{caseId}/{id}
        [HttpDelete("{caseId}/{id}")]
        public async Task<IActionResult> DeleteForensicRequest(string caseId, int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var request = await _context.ForensicRequests
                .FirstOrDefaultAsync(fr => fr.Id == id && fr.CaseId == caseId && fr.UserId == userId);

            if (request == null)
            {
                return NotFound();
            }

            _context.ForensicRequests.Remove(request);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ForensicRequestExists(int id)
        {
            return _context.ForensicRequests.Any(e => e.Id == id);
        }
    }
}
