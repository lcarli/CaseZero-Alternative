using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CaseGenerationController : ControllerBase
    {
        private readonly ICaseGenerationService _caseGenerationService;
        private readonly ILogger<CaseGenerationController> _logger;

        public CaseGenerationController(ICaseGenerationService caseGenerationService, ILogger<CaseGenerationController> logger)
        {
            _caseGenerationService = caseGenerationService;
            _logger = logger;
        }

        /// <summary>
        /// Generate a new case using AI
        /// </summary>
        [HttpPost("generate")]
        public async Task<ActionResult<CasePackage>> GenerateCase([FromBody] GenerateCaseRequest request, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Generating case: {Title}", request.Title);

                var seed = new CaseSeed(
                    Title: request.Title,
                    Location: request.Location,
                    IncidentDateTime: request.IncidentDateTime,
                    Pitch: request.Pitch,
                    Twist: request.Twist,
                    Difficulty: request.Difficulty ?? "Médio",
                    TargetDurationMinutes: request.TargetDurationMinutes ?? 60,
                    Constraints: request.Constraints,
                    Timezone: request.Timezone ?? "America/Toronto"
                );

                var options = new GenerationOptions
                {
                    GenerateImages = request.GenerateImages ?? true
                };

                var package = await _caseGenerationService.GenerateCaseAsync(seed, options, ct);

                return Ok(package);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating case: {Title}", request.Title);
                return StatusCode(500, new { error = "Failed to generate case", details = ex.Message });
            }
        }

        /// <summary>
        /// Generate only the case JSON structure
        /// </summary>
        [HttpPost("generate-json")]
        public async Task<ActionResult<string>> GenerateCaseJson([FromBody] GenerateCaseRequest request, CancellationToken ct = default)
        {
            try
            {
                var seed = new CaseSeed(
                    Title: request.Title,
                    Location: request.Location,
                    IncidentDateTime: request.IncidentDateTime,
                    Pitch: request.Pitch,
                    Twist: request.Twist,
                    Difficulty: request.Difficulty ?? "Médio",
                    TargetDurationMinutes: request.TargetDurationMinutes ?? 60,
                    Constraints: request.Constraints,
                    Timezone: request.Timezone ?? "America/Toronto"
                );

                var caseJson = await _caseGenerationService.GenerateCaseJsonAsync(seed, ct);
                return Ok(new { caseJson });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating case JSON: {Title}", request.Title);
                return StatusCode(500, new { error = "Failed to generate case JSON", details = ex.Message });
            }
        }
    }

    public class GenerateCaseRequest
    {
        public required string Title { get; set; }
        public required string Location { get; set; }
        public required DateTimeOffset IncidentDateTime { get; set; }
        public required string Pitch { get; set; }
        public required string Twist { get; set; }
        public string? Difficulty { get; set; }
        public int? TargetDurationMinutes { get; set; }
        public string? Constraints { get; set; }
        public string? Timezone { get; set; }
        public bool? GenerateImages { get; set; }
    }
}