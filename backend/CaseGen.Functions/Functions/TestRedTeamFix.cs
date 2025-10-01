using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;

namespace CaseGen.Functions.Functions
{
    public class TestRedTeamFix
    {
        private readonly ILogger<TestRedTeamFix> _logger;
        private readonly ICaseGenerationService _caseGenerationService;

        public TestRedTeamFix(ILogger<TestRedTeamFix> logger, ICaseGenerationService caseGenerationService)
        {
            _logger = logger;
            _caseGenerationService = caseGenerationService;
        }

        [Function("TestRedTeamFix")]
        public async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                _logger.LogInformation("TestRedTeamFix function triggered");

                // Read request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                if (string.IsNullOrEmpty(requestBody))
                {
                    return new BadRequestObjectResult("Request body is required");
                }

                // Parse the request
                var testRequest = JsonSerializer.Deserialize<TestRedTeamFixRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (testRequest == null)
                {
                    return new BadRequestObjectResult("Invalid request format");
                }

                // Validate required fields
                if (testRequest.NormalizedCase == null)
                {
                    return new BadRequestObjectResult("NormalizedCase is required");
                }

                // Serialize the normalized case to JSON
                var normalizedCaseJson = JsonSerializer.Serialize(testRequest.NormalizedCase, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Execute RedTeam analysis
                _logger.LogInformation("Executing RedTeam analysis...");
                var redTeamResult = await _caseGenerationService.RedTeamCaseAsync(normalizedCaseJson, testRequest.NormalizedCase.CaseId);

                if (string.IsNullOrEmpty(redTeamResult))
                {
                    return new ObjectResult(new { Error = "RedTeam analysis returned empty result" })
                    {
                        StatusCode = 500
                    };
                }

                _logger.LogInformation("RedTeam analysis completed. Result length: {Length}", redTeamResult.Length);

                // Execute Fix based on RedTeam analysis
                _logger.LogInformation("Executing Fix based on RedTeam analysis...");
                var fixResultJson = await _caseGenerationService.FixCaseAsync(redTeamResult, normalizedCaseJson, testRequest.NormalizedCase.CaseId);

                if (string.IsNullOrEmpty(fixResultJson))
                {
                    return new ObjectResult(new { Error = "Fix operation returned empty result" })
                    {
                        StatusCode = 500
                    };
                }

                _logger.LogInformation("Fix operation completed. Fixed case length: {Length}", fixResultJson.Length);

                // Parse the fixed result back to object
                var fixResult = JsonSerializer.Deserialize<NormalizedCaseBundle>(fixResultJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (fixResult == null)
                {
                    return new ObjectResult(new { Error = "Failed to parse fixed case JSON" })
                    {
                        StatusCode = 500
                    };
                }

                // Return both results
                var response = new TestRedTeamFixResponse
                {
                    RedTeamAnalysis = redTeamResult,
                    FixedCase = fixResult,
                    ExecutedAt = DateTime.UtcNow,
                    OriginalCaseId = testRequest.NormalizedCase.CaseId
                };

                return new OkObjectResult(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing JSON request");
                return new BadRequestObjectResult($"Invalid JSON format: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TestRedTeamFix function");
                return new ObjectResult(new { Error = ex.Message, Details = ex.ToString() })
                {
                    StatusCode = 500
                };
            }
        }
    }

    public class TestRedTeamFixRequest
    {
        public NormalizedCaseBundle NormalizedCase { get; set; } = null!;
        public string? ExpectedRedTeamAnalysis { get; set; }
    }

    public class TestRedTeamFixResponse
    {
        public string RedTeamAnalysis { get; set; } = string.Empty;
        public NormalizedCaseBundle FixedCase { get; set; } = null!;
        public DateTime ExecutedAt { get; set; }
        public string OriginalCaseId { get; set; } = string.Empty;
    }
}