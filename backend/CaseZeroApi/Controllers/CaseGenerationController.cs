using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaseZeroApi.Controllers;

/// <summary>
/// Proxy for the case-generation Function App. Keeps the function key (and the function URL)
/// out of the browser, ensures every request carries a JWT, and gives us a single hook
/// for future audit/billing/quota.
///
/// Maps onto two function endpoints:
///   POST /api/casegeneration/generate    → POST {Function}/api/cases/v2/generate
///   GET  /api/casegeneration/jobs/{id}   → GET  {Function}/api/cases/v2/jobs/{id}
/// </summary>
[ApiController]
[Route("api/casegeneration")]
[Authorize]
public class CaseGenerationController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseGenerationController> _logger;

    public CaseGenerationController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CaseGenerationController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Kicks off a v2 case generation. Returns 202 with the jobId and a relative
    /// statusUri the client can poll via GET /jobs/{jobId}.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] object body, CancellationToken ct)
    {
        var (client, baseUrl) = BuildClient();
        if (client is null)
            return StatusCode(503, new { error = "Case generator not configured (CaseGenerator:FunctionBaseUrl missing)." });

        var json = body is null ? "{}" : System.Text.Json.JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await client.PostAsync($"{baseUrl}/api/cases/v2/generate", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = responseBody,
                ContentType = "application/json"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Case generator unreachable at {BaseUrl}", baseUrl);
            return StatusCode(502, new { error = "Case generator unreachable", detail = ex.Message });
        }
    }

    /// <summary>
    /// Polls a generation job's status. Returns whatever the function returns verbatim
    /// (queued | running:phase | done | failed) so the frontend can render progress.
    /// </summary>
    [HttpGet("jobs/{jobId}")]
    public async Task<IActionResult> GetJob(string jobId, CancellationToken ct)
    {
        var (client, baseUrl) = BuildClient();
        if (client is null)
            return StatusCode(503, new { error = "Case generator not configured." });

        try
        {
            using var response = await client.GetAsync($"{baseUrl}/api/cases/v2/jobs/{jobId}", ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = responseBody,
                ContentType = "application/json"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Case generator unreachable at {BaseUrl}", baseUrl);
            return StatusCode(502, new { error = "Case generator unreachable", detail = ex.Message });
        }
    }

    private (HttpClient? client, string? baseUrl) BuildClient()
    {
        var baseUrl = _configuration["CaseGenerator:FunctionBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return (null, null);
        baseUrl = baseUrl.TrimEnd('/');

        var client = _httpClientFactory.CreateClient("case-generator");
        // The function endpoints are registered as Anonymous, but if a deploy ever flips
        // them to Function level we can pass the key via x-functions-key header.
        var key = _configuration["CaseGenerator:FunctionKey"];
        if (!string.IsNullOrWhiteSpace(key) && !client.DefaultRequestHeaders.Contains("x-functions-key"))
        {
            client.DefaultRequestHeaders.Add("x-functions-key", key);
        }
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, baseUrl);
    }
}
