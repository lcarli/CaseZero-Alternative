using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for manual processing
    public class CaseProcessingController : ControllerBase
    {
        private readonly ICaseProcessingService _caseProcessingService;
        private readonly ILogger<CaseProcessingController> _logger;

        public CaseProcessingController(
            ICaseProcessingService caseProcessingService,
            ILogger<CaseProcessingController> logger)
        {
            _caseProcessingService = caseProcessingService;
            _logger = logger;
        }

        /// <summary>
        /// Manually trigger processing of all new cases from the cases folder
        /// </summary>
        [HttpPost("process-all")]
        public async Task<IActionResult> ProcessAllCases()
        {
            try
            {
                _logger.LogInformation("Manual case processing triggered");
                await _caseProcessingService.ProcessNewCasesAsync();
                
                return Ok(new {
                    success = true,
                    message = "Case processing completed successfully",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual case processing");
                return StatusCode(500, new {
                    success = false,
                    message = "Error occurred during case processing",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Process a specific case by ID
        /// </summary>
        [HttpPost("process/{caseId}")]
        public async Task<IActionResult> ProcessSpecificCase(string caseId)
        {
            try
            {
                // Check if case already processed
                if (await _caseProcessingService.IsCaseAlreadyProcessedAsync(caseId))
                {
                    return BadRequest(new {
                        success = false,
                        message = $"Case {caseId} has already been processed",
                        caseId = caseId
                    });
                }

                _logger.LogInformation("Manual processing triggered for case {CaseId}", caseId);
                await _caseProcessingService.ProcessCaseAsync(caseId);
                
                return Ok(new {
                    success = true,
                    message = $"Case {caseId} processed successfully",
                    caseId = caseId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing case {CaseId}", caseId);
                return StatusCode(500, new {
                    success = false,
                    message = $"Error occurred while processing case {caseId}",
                    caseId = caseId,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Check if a case has already been processed
        /// </summary>
        [HttpGet("status/{caseId}")]
        public async Task<IActionResult> GetCaseProcessingStatus(string caseId)
        {
            try
            {
                var isProcessed = await _caseProcessingService.IsCaseAlreadyProcessedAsync(caseId);
                
                return Ok(new {
                    caseId = caseId,
                    isProcessed = isProcessed,
                    status = isProcessed ? "Processed" : "Not Processed",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking processing status for case {CaseId}", caseId);
                return StatusCode(500, new {
                    success = false,
                    message = $"Error checking status for case {caseId}",
                    error = ex.Message
                });
            }
        }
    }
}