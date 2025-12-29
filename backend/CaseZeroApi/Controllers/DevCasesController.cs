using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using CaseZeroApi.Models.CaseV1;

namespace CaseZeroApi.Controllers;

/// <summary>
/// Controller de desenvolvimento para testar casos v1.0 sem Azure Blob
/// ⚠️ APENAS PARA DESENVOLVIMENTO - Desabilitado em produção
/// </summary>
[ApiController]
[Route("api/dev/cases")]
public class DevCasesController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DevCasesController> _logger;

    public DevCasesController(
        IWebHostEnvironment environment,
        ILogger<DevCasesController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Lista casos disponíveis em cases/samples/
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetAvailableDevCases()
    {
        if (!_environment.IsDevelopment() && !_environment.EnvironmentName.Contains("Dev"))
        {
            return NotFound("Dev endpoints are only available in Development environment");
        }

        try
        {
            var samplesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "cases", "samples");
            
            if (!Directory.Exists(samplesPath))
            {
                return NotFound($"Samples directory not found: {samplesPath}");
            }

            var caseFiles = Directory.GetFiles(samplesPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();

            return Ok(new
            {
                environment = "Development",
                samplesPath = samplesPath,
                availableCases = caseFiles,
                note = "⚠️ Dev endpoint - cases served from local filesystem"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing dev cases");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Busca um caso específico (COM SANITIZAÇÃO)
    /// GET /api/dev/cases/case_001
    /// </summary>
    [HttpGet("{caseId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDevCase(string caseId)
    {
        if (!_environment.IsDevelopment() && !_environment.EnvironmentName.Contains("Dev"))
        {
            return NotFound("Dev endpoints are only available in Development environment");
        }

        try
        {
            // Buscar arquivo no sistema de arquivos
            var samplesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "cases", "samples");
            var caseFilePath = Path.Combine(samplesPath, $"{caseId}.json");

            if (!System.IO.File.Exists(caseFilePath))
            {
                // Tentar sem o prefixo case_
                caseFilePath = Path.Combine(samplesPath, $"case.{caseId}.json");
                
                if (!System.IO.File.Exists(caseFilePath))
                {
                    return NotFound(new
                    {
                        error = $"Case file not found: {caseId}",
                        searchPath = samplesPath,
                        availableFiles = Directory.Exists(samplesPath) 
                            ? Directory.GetFiles(samplesPath, "*.json").Select(Path.GetFileName).ToList()
                            : new List<string>()
                    });
                }
            }

            _logger.LogInformation("Loading dev case from: {FilePath}", caseFilePath);

            // Ler e deserializar
            var jsonContent = await System.IO.File.ReadAllTextAsync(caseFilePath);
            var caseData = JsonSerializer.Deserialize<CaseV1>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (caseData == null)
            {
                return BadRequest(new { error = "Failed to deserialize case JSON" });
            }

            // 🔒 APLICAR SANITIZAÇÃO INLINE (sem dependências)
            // Remover regras e filtrar por visibilidade
            var sanitized = new CaseV1
            {
                Version = caseData.Version,
                CaseId = caseData.CaseId,
                Metadata = caseData.Metadata,
                ForensicsDefaults = caseData.ForensicsDefaults,
                Rules = null, // 🔒 NUNCA expor ao cliente
                Assets = caseData.Assets.Where(a => a.Visibility == "initial").ToList(),
                Emails = caseData.Emails.Where(e => e.Visibility == "initial").ToList(),
                Suspects = caseData.Suspects.Where(s => s.Visibility == "initial").ToList()
            };

            _logger.LogInformation(
                "Dev case {CaseId} sanitized: {AssetsCount} assets, {EmailsCount} emails visible",
                caseId, sanitized.Assets.Count, sanitized.Emails.Count);

            return Ok(new
            {
                environment = "Development",
                caseId = sanitized.CaseId,
                version = sanitized.Version,
                source = caseFilePath,
                sanitized = true,
                data = sanitized,
                warning = "⚠️ Rules have been removed for client safety. Only 'initial' visibility items are shown."
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for case {CaseId}", caseId);
            return BadRequest(new
            {
                error = "Invalid JSON format",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dev case {CaseId}", caseId);
            return StatusCode(500, new
            {
                error = "Internal server error",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Busca caso SEM sanitização (apenas para debug/testes)
    /// ⚠️ NUNCA usar em produção - expõe regras e dados sensíveis
    /// </summary>
    [HttpGet("{caseId}/raw")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDevCaseRaw(string caseId)
    {
        if (!_environment.IsDevelopment() && !_environment.EnvironmentName.Contains("Dev"))
        {
            return NotFound("Dev endpoints are only available in Development environment");
        }

        _logger.LogWarning("⚠️ Loading RAW case {CaseId} without sanitization - DEBUG ONLY", caseId);

        try
        {
            var samplesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "cases", "samples");
            var caseFilePath = Path.Combine(samplesPath, $"{caseId}.json");

            if (!System.IO.File.Exists(caseFilePath))
            {
                caseFilePath = Path.Combine(samplesPath, $"case.{caseId}.json");
                if (!System.IO.File.Exists(caseFilePath))
                {
                    return NotFound($"Case file not found: {caseId}");
                }
            }

            var jsonContent = await System.IO.File.ReadAllTextAsync(caseFilePath);
            var caseData = JsonSerializer.Deserialize<CaseV1>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Ok(new
            {
                environment = "Development",
                warning = "⚠️⚠️⚠️ RAW DATA - INCLUDES RULES AND ALL HIDDEN CONTENT ⚠️⚠️⚠️",
                sanitized = false,
                data = caseData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading raw dev case {CaseId}", caseId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
