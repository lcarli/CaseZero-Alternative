using CaseGen.Functions.Services;
using CaseGen.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Test function to generate images for an existing case
/// </summary>
public class TestGenerateImagesFunction
{
    private readonly ILogger<TestGenerateImagesFunction> _logger;
    private readonly IContextManager _contextManager;
    private readonly ICaseGenerationService _caseGenerationService;

    public TestGenerateImagesFunction(
        ILogger<TestGenerateImagesFunction> logger,
        IContextManager contextManager,
        ICaseGenerationService caseGenerationService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _caseGenerationService = caseGenerationService;
    }

    [Function("TestGenerateImages")]
    public async Task<HttpResponseData> TestGenerateImages(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("TestGenerateImages function triggered");

        try
        {
            // Parse request body to get caseId
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Received request body: {Body}", requestBody);
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var requestData = JsonSerializer.Deserialize<TestGenerateImagesRequest>(requestBody, options);

            if (string.IsNullOrEmpty(requestData?.CaseId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync($"CaseId is required in request body. Received: {requestBody}");
                return badResponse;
            }

            var caseId = requestData.CaseId;
            _logger.LogInformation("Starting image generation test for case {CaseId}", caseId);

            // Query all media specs from design/media/*
            _logger.LogInformation("=== Querying design/media/* ===");
            var mediaSpecsQueryRaw = await _contextManager.QueryContextAsync<string>(
                caseId, 
                "design/media/*");
            var mediaSpecsList = mediaSpecsQueryRaw.ToList();

            if (!mediaSpecsList.Any())
            {
                _logger.LogWarning("No media specs found for case {CaseId}", caseId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"No media specs found for case {caseId}. Make sure the case has design/media/* files.");
                return notFoundResponse;
            }

            _logger.LogInformation("Found {Count} media specs", mediaSpecsList.Count);

            // Parse media specs and generate images
            var results = new List<ImageGenerationResultDto>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var mediaSpecData in mediaSpecsList)
            {
                try
                {
                    // Parse the design file - it contains a "specifications" array
                    var designDoc = JsonDocument.Parse(mediaSpecData.Data).RootElement;
                    
                    if (!designDoc.TryGetProperty("specifications", out var specificationsArray))
                    {
                        _logger.LogWarning("Design file {Path} does not have 'specifications' property", mediaSpecData.Path);
                        continue;
                    }

                    // Iterate through each specification in the array
                    foreach (var specElement in specificationsArray.EnumerateArray())
                    {
                        try
                        {
                            var evidenceId = specElement.GetProperty("evidenceId").GetString() ?? "unknown";
                            var title = specElement.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                            var kind = specElement.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() : null;

                            _logger.LogInformation("Generating image for {EvidenceId}: {Title}", evidenceId, title);

                            // Deserialize to MediaSpec object for service call
                            var mediaSpec = JsonSerializer.Deserialize<MediaSpec>(specElement.GetRawText(), options);
                            if (mediaSpec == null)
                            {
                                _logger.LogWarning("Failed to deserialize media spec for {EvidenceId}", evidenceId);
                                results.Add(new ImageGenerationResultDto
                                {
                                    EvidenceId = evidenceId,
                                    Title = title,
                                    Success = false,
                                    Error = "Failed to deserialize MediaSpec"
                                });
                                failureCount++;
                                continue;
                            }

                            // Generate the image
                            var imageUrl = await _caseGenerationService.RenderMediaFromJsonAsync(mediaSpec, caseId);

                            results.Add(new ImageGenerationResultDto
                            {
                                EvidenceId = evidenceId,
                                Title = title,
                                Kind = kind,
                                Success = true,
                                ImageUrl = imageUrl
                            });
                            successCount++;

                            _logger.LogInformation("Successfully generated image for {EvidenceId}", evidenceId);
                        }
                        catch (Exception ex)
                        {
                            var evidenceId = specElement.TryGetProperty("evidenceId", out var idProp) 
                                ? idProp.GetString() ?? "unknown" 
                                : "unknown";
                            _logger.LogError(ex, "Error generating image for {EvidenceId}", evidenceId);
                            results.Add(new ImageGenerationResultDto
                            {
                                EvidenceId = evidenceId,
                                Success = false,
                                Error = ex.Message
                            });
                            failureCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing design file {Path}", mediaSpecData.Path);
                    results.Add(new ImageGenerationResultDto
                    {
                        EvidenceId = mediaSpecData.Path,
                        Success = false,
                        Error = ex.Message
                    });
                    failureCount++;
                }
            }

            // Create success response
            _logger.LogInformation("=== Image Generation Summary ===");
            _logger.LogInformation("Total media specs: {Total}", mediaSpecsList.Count);
            _logger.LogInformation("Successful: {Success}", successCount);
            _logger.LogInformation("Failed: {Failed}", failureCount);

            var response = req.CreateResponse(HttpStatusCode.OK);
            
            var responseData = new
            {
                caseId = caseId,
                totalMediaSpecs = mediaSpecsList.Count,
                successCount = successCount,
                failureCount = failureCount,
                results = results
            };

            await response.WriteAsJsonAsync(responseData);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestGenerateImages");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}\n\nStack: {ex.StackTrace}");
            return errorResponse;
        }
    }
}

// Request DTO
public record TestGenerateImagesRequest
{
    public string? CaseId { get; init; }
}

// Response DTO
public record ImageGenerationResultDto
{
    public required string EvidenceId { get; init; }
    public string? Title { get; init; }
    public string? Kind { get; init; }
    public required bool Success { get; init; }
    public string? ImageUrl { get; init; }
    public string? Error { get; init; }
}
