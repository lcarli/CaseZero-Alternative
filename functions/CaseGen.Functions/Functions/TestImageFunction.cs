using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;

namespace CaseGen.Functions.Functions;

// DTO for test function that accepts constraints as string
public record TestMediaSpec
{
    [JsonPropertyName("evidenceId")]
    public required string EvidenceId { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("constraints")]
    public string? Constraints { get; init; }
}

public class TestImageFunction
{
    private readonly ILogger<TestImageFunction> _logger;
    private readonly ICaseGenerationService _caseGenerationService;

    public TestImageFunction(ILogger<TestImageFunction> logger, ICaseGenerationService caseGenerationService)
    {
        _logger = logger;
        _caseGenerationService = caseGenerationService;
    }

    [Function("TestImageGeneration")]
    public async Task<HttpResponseData> TestImageGeneration(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "test/image")] HttpRequestData req)
    {
        // This is a TEST FUNCTION ONLY - all actual image generation logic is in the main orchestrator
        // via CaseGenerationService.RenderMediaFromJsonAsync()
        _logger.LogInformation("TestImageGeneration function processed a request.");

        try
        {
            // Read the request body
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var testMediaSpec = JsonSerializer.Deserialize<TestMediaSpec>(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (testMediaSpec == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid TestMediaSpec format");
                return badResponse;
            }

            // Convert TestMediaSpec to MediaSpec
            var mediaSpec = ConvertToMediaSpec(testMediaSpec);

            _logger.LogInformation("Generating image for evidence: {EvidenceId}", mediaSpec.EvidenceId);

            // Use the service to generate the image
            var caseId = $"test-{DateTime.Now:yyyyMMdd-HHmmss}";
            var result = await TestImageGenerationInternal(mediaSpec, caseId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var jsonResponse = JsonSerializer.Serialize(new
            {
                EvidenceId = mediaSpec.EvidenceId,
                Kind = mediaSpec.Kind,
                Title = mediaSpec.Title,
                Result = result,
                CaseId = caseId
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await response.WriteStringAsync(jsonResponse);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestImageGeneration");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    private async Task<string> TestImageGenerationInternal(MediaSpec mediaSpec, string caseId)
    {
        try
        {
            // Use the dedicated test method
            return await _caseGenerationService.GenerateTestImageAsync(mediaSpec, caseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GenerateTestImageAsync");
            throw;
        }
    }

    private static MediaSpec ConvertToMediaSpec(TestMediaSpec testSpec)
    {
        // Convert constraints string to Dictionary if present
        Dictionary<string, object>? constraints = null;
        
        if (!string.IsNullOrEmpty(testSpec.Constraints))
        {
            // Parse the constraints string into a dictionary
            // Format: "- Key1: Value1\n- Key2: Value2"
            constraints = new Dictionary<string, object>();
            var lines = testSpec.Constraints.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("- ") && trimmedLine.Contains(": "))
                {
                    var colonIndex = trimmedLine.IndexOf(": ");
                    var key = trimmedLine[2..colonIndex].Trim(); // Remove "- " prefix
                    var value = trimmedLine[(colonIndex + 2)..].Trim();
                    constraints[key] = value;
                }
            }
        }

        return new MediaSpec
        {
            EvidenceId = testSpec.EvidenceId,
            Kind = testSpec.Kind,
            Title = testSpec.Title,
            Prompt = testSpec.Prompt,
            Constraints = constraints
        };
    }
}
