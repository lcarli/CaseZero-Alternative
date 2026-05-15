using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Services;
using System.Net;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Test endpoint to validate CaseFormatConverter with existing test cases
/// GET /api/test-converter?caseId=CASE-20260102-e6dcc658
/// </summary>
public class TestConverterFunction
{
    private readonly ILogger<TestConverterFunction> _logger;
    private readonly ICaseFormatConverterService _converter;

    public TestConverterFunction(
        ILogger<TestConverterFunction> logger,
        ICaseFormatConverterService converter)
    {
        _logger = logger;
        _converter = converter;
    }

    [Function("TestConverter")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("🧪 TestConverter endpoint called");

        try
        {
            // Get caseId from query string or body
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var caseId = query["caseId"];

            if (string.IsNullOrEmpty(caseId))
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new
                {
                    error = "Missing caseId parameter",
                    usage = "GET /api/test-converter?caseId=CASE-20260102-e6dcc658",
                    availableCases = new[]
                    {
                        "CASE-20260102-e6dcc658",
                        "CASE-20251027-b442dc10"
                    }
                });
                return response;
            }

            _logger.LogInformation("Testing converter with case: {CaseId}", caseId);

            // Call converter
            var caseV1Json = await _converter.ConvertCaseToV1Async(caseId, executionContext.CancellationToken);

            // Parse to validate JSON and get stats
            var caseV1 = JsonDocument.Parse(caseV1Json);
            var root = caseV1.RootElement;

            var stats = new
            {
                success = true,
                caseId,
                version = root.GetProperty("version").GetString(),
                convertedCaseId = root.GetProperty("caseId").GetString(),
                assetsCount = root.GetProperty("assets").GetArrayLength(),
                suspectsCount = root.GetProperty("suspects").GetArrayLength(),
                emailsCount = root.GetProperty("emails").GetArrayLength(),
                rulesCount = root.GetProperty("rules").GetArrayLength(),
                sizeKB = caseV1Json.Length / 1024.0,
                message = "✅ Conversion successful!"
            };

            _logger.LogInformation("✅ Converter test successful: {Stats}", JsonSerializer.Serialize(stats));

            // Return both stats and full JSON
            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(new
            {
                stats,
                preview = new
                {
                    metadata = root.TryGetProperty("metadata", out var meta) ? meta : default,
                    suspects = root.TryGetProperty("suspects", out var suspects) ? suspects : default,
                    assetsPreview = root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0
                        ? assets.EnumerateArray().Take(3).ToArray()
                        : Array.Empty<JsonElement>()
                },
                fullJson = JsonSerializer.Deserialize<object>(caseV1Json)
            });

            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ TestConverter failed");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace,
                innerError = ex.InnerException?.Message
            });

            return errorResponse;
        }
    }
}
