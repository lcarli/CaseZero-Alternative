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
        /// Generate a simplified case using AI with minimal input
        /// </summary>
        [HttpPost("generate-simple")]
        public async Task<ActionResult<CasePackage>> GenerateSimpleCase([FromBody] SimpleCaseRequest request, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Generating simple case with difficulty: {Difficulty}", request.Difficulty);

                // Create a seed with reasonable defaults
                var seed = new CaseSeed(
                    Title: GenerateRandomTitle(),
                    Location: GenerateRandomLocation(), 
                    IncidentDateTime: GenerateRandomDateTime(),
                    Pitch: GenerateRandomPitch(request.Difficulty),
                    Twist: GenerateRandomTwist(),
                    Difficulty: request.Difficulty,
                    TargetDurationMinutes: GetDurationByDifficulty(request.Difficulty),
                    Constraints: "Ambiente urbano, investigação policial padrão",
                    Timezone: "America/Toronto"
                );

                var options = new GenerationOptions
                {
                    GenerateImages = false // Disable images for faster generation
                };

                var package = await _caseGenerationService.GenerateCaseAsync(seed, options, ct);

                return Ok(package);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating simple case with difficulty: {Difficulty}", request.Difficulty);
                return StatusCode(500, new { error = "Failed to generate case", details = ex.Message });
            }
        }

        /// <summary>
        /// Generate only the case JSON structure with minimal input
        /// </summary>
        [HttpPost("generate-simple-json")]
        public async Task<ActionResult<string>> GenerateSimpleCaseJson([FromBody] SimpleCaseRequest request, CancellationToken ct = default)
        {
            try
            {
                var seed = new CaseSeed(
                    Title: GenerateRandomTitle(),
                    Location: GenerateRandomLocation(),
                    IncidentDateTime: GenerateRandomDateTime(),
                    Pitch: GenerateRandomPitch(request.Difficulty),
                    Twist: GenerateRandomTwist(),
                    Difficulty: request.Difficulty,
                    TargetDurationMinutes: GetDurationByDifficulty(request.Difficulty),
                    Constraints: "Ambiente urbano, investigação policial padrão",
                    Timezone: "America/Toronto"
                );

                var caseJson = await _caseGenerationService.GenerateCaseJsonAsync(seed, ct);
                return Ok(new { caseJson });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating simple case JSON with difficulty: {Difficulty}", request.Difficulty);
                return StatusCode(500, new { error = "Failed to generate case JSON", details = ex.Message });
            }
        }

        #region Progressive Generation Endpoints
        
        /// <summary>
        /// Generate interrogations for a case context
        /// </summary>
        [HttpPost("generate-interrogations")]
        public async Task<ActionResult<List<GeneratedDoc>>> GenerateInterrogations([FromBody] CaseContext ctx, CancellationToken ct = default)
        {
            try
            {
                var interrogations = await _caseGenerationService.GenerateInterrogatoriosAsync(ctx, ct);
                return Ok(interrogations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating interrogations for case {CaseId}", ctx.CaseId);
                return StatusCode(500, new { error = "Failed to generate interrogations", details = ex.Message });
            }
        }

        /// <summary>
        /// Generate reports for a case context
        /// </summary>
        [HttpPost("generate-reports")]
        public async Task<ActionResult<List<GeneratedDoc>>> GenerateReports([FromBody] CaseContext ctx, CancellationToken ct = default)
        {
            try
            {
                var reports = await _caseGenerationService.GenerateRelatoriosAsync(ctx, ct);
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reports for case {CaseId}", ctx.CaseId);
                return StatusCode(500, new { error = "Failed to generate reports", details = ex.Message });
            }
        }

        /// <summary>
        /// Generate forensic analysis for a case context
        /// </summary>
        [HttpPost("generate-analysis")]
        public async Task<ActionResult<List<GeneratedDoc>>> GenerateAnalysis([FromBody] CaseContext ctx, CancellationToken ct = default)
        {
            try
            {
                var analysis = await _caseGenerationService.GenerateLaudosAsync(ctx, ct);
                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating analysis for case {CaseId}", ctx.CaseId);
                return StatusCode(500, new { error = "Failed to generate analysis", details = ex.Message });
            }
        }

        /// <summary>
        /// Generate evidence manifest for a case context
        /// </summary>
        [HttpPost("generate-evidence")]
        public async Task<ActionResult<GeneratedDoc>> GenerateEvidence([FromBody] CaseContext ctx, CancellationToken ct = default)
        {
            try
            {
                var evidence = await _caseGenerationService.GenerateEvidenceManifestAsync(ctx, ct);
                return Ok(evidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating evidence for case {CaseId}", ctx.CaseId);
                return StatusCode(500, new { error = "Failed to generate evidence", details = ex.Message });
            }
        }
        
        #endregion

        #region Helper Methods
        private static string GenerateRandomTitle()
        {
            var titles = new[]
            {
                "Mistério na Madrugada",
                "Desaparecimento Inexplicável", 
                "Crime no Escritório",
                "Roubo na Universidade",
                "Assassinato no Prédio",
                "Furto Misterioso",
                "Caso do Cofre Vazio",
                "Morte Suspeita"
            };
            return titles[Random.Shared.Next(titles.Length)];
        }

        private static string GenerateRandomLocation()
        {
            var locations = new[]
            {
                "Centro Empresarial Downtown",
                "Universidade Federal do Estado", 
                "Complexo Residencial Jardins",
                "Shopping Center Plaza",
                "Distrito Financeiro",
                "Campus Universitário Norte",
                "Bairro Comercial Central",
                "Zona Industrial Sul"
            };
            return locations[Random.Shared.Next(locations.Length)];
        }

        private static DateTimeOffset GenerateRandomDateTime()
        {
            var baseDate = DateTimeOffset.Now.AddDays(-Random.Shared.Next(1, 30));
            var hour = Random.Shared.Next(18, 24); // Evening hours for more drama
            return baseDate.Date.AddHours(hour).AddMinutes(Random.Shared.Next(0, 60));
        }

        private static string GenerateRandomPitch(string difficulty)
        {
            var basePitches = new Dictionary<string, string[]>
            {
                ["Easy"] = new[]
                {
                    "Furto simples em escritório durante o expediente. Poucas pessoas presentes, evidências claras.",
                    "Desaparecimento de objeto valioso em festa corporativa. Muitas testemunhas disponíveis."
                },
                ["Medium"] = new[]
                {
                    "Roubo em universidade durante a madrugada. Sistema de segurança comprometido, múltiplos suspeitos.",
                    "Assassinato em prédio comercial. Várias evidências contraditórias, motivos complexos."
                },
                ["Hard"] = new[]
                {
                    "Crime complexo com múltiplas vítimas. Evidências escassas, suspeitos com álibis sólidos.",
                    "Desaparecimento em circunstâncias misteriosas. Poucos vestígios, cenário elaborado."
                }
            };

            var pitches = basePitches.GetValueOrDefault(difficulty, basePitches["Medium"]);
            return pitches[Random.Shared.Next(pitches.Length)];
        }

        private static string GenerateRandomTwist()
        {
            var twists = new[]
            {
                "O principal suspeito tinha um álibi que foi posteriormente desmentido por evidências digitais.",
                "A vítima estava envolvida em atividades suspeitas que só vieram à tona durante a investigação.",
                "Testemunha-chave mente sobre seu paradeiro na hora do crime por motivos pessoais.",
                "Sistema de segurança foi sabotado por alguém com acesso interno privilegiado.",
                "Evidência crucial foi plantada para desviar a atenção do verdadeiro culpado."
            };
            return twists[Random.Shared.Next(twists.Length)];
        }

        private static int GetDurationByDifficulty(string difficulty)
        {
            return difficulty switch
            {
                "Easy" => 45,
                "Medium" => 75,
                "Hard" => 120,
                _ => 60
            };
        }
        #endregion
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

    public class SimpleCaseRequest
    {
        public required string Difficulty { get; set; } = "Medium";
    }
}