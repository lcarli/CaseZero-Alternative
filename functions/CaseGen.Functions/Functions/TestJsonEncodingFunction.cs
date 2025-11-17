using System.Net;
using System.Text.Json;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Test function to verify JSON encoding (no double-encoding)
/// </summary>
public class TestJsonEncodingFunction
{
    private readonly ILogger<TestJsonEncodingFunction> _logger;
    private readonly IContextManager _contextManager;

    public TestJsonEncodingFunction(
        ILogger<TestJsonEncodingFunction> logger,
        IContextManager contextManager)
    {
        _logger = logger;
        _contextManager = contextManager;
    }

    [Function("TestJsonEncoding")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("🧪 TestJsonEncoding function triggered");

            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Received request body: {Body}", requestBody);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var requestData = JsonSerializer.Deserialize<TestEncodingRequest>(requestBody, options);

            if (string.IsNullOrEmpty(requestData?.CaseId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync($"CaseId is required in request body. Received: {requestBody}");
                return badResponse;
            }

            var caseId = requestData.CaseId;
            _logger.LogInformation("🧪 Starting JSON encoding test for case {CaseId}", caseId);

            // === TEST 1: Save and load a complex object with JsonElement ===
            _logger.LogInformation("=== TEST 1: Save/Load with JsonElement ===");
            
            // Create test data similar to what comes from expand phase
            var testSuspect = new
            {
                suspectId = "TEST001",
                name = "Test Person",
                alibi = "Test alibi with \"quotes\" and special chars: éàç",
                behavior = new
                {
                    demeanor = "calm",
                    inconsistencies = new[] { "First said X", "Then said Y" }
                },
                evidenceLinks = new[]
                {
                    new { evidenceRef = "EV001", connection = "Found at scene", strength = "strong" }
                }
            };

            // Serialize to string, then parse to JsonElement (simulating what we do in NormalizeEntitiesActivity)
            var testJson = JsonSerializer.Serialize(testSuspect);
            var testElement = JsonDocument.Parse(testJson).RootElement;
            
            _logger.LogInformation("Original JSON length: {Length} bytes", testJson.Length);
            _logger.LogInformation("Original JSON preview: {Preview}...", testJson.Substring(0, Math.Min(100, testJson.Length)));

            // Save using JsonElement (this should NOT double-encode)
            await _contextManager.SaveContextAsync(caseId, "test/suspect_element", testElement);
            _logger.LogInformation("✅ Saved JsonElement to test/suspect_element");

            // Load back - if NOT double-encoded, we can load as object
            // If it IS double-encoded, loading as object will fail and we need to load as string
            string? loadedString = null;
            bool isDoubleEncoded = false;
            bool isValidJson = false;

            try
            {
                // Try to load as the actual type - if this works, it's NOT double-encoded
                var loadedObject = await _contextManager.LoadContextAsync<Dictionary<string, JsonElement>>(
                    caseId, "test/suspect_element");
                
                if (loadedObject != null)
                {
                    isValidJson = true;
                    isDoubleEncoded = false;
                    // Serialize back to string for preview
                    loadedString = JsonSerializer.Serialize(loadedObject);
                    _logger.LogInformation("✅ Successfully loaded as object - NOT double-encoded");
                }
            }
            catch (JsonException)
            {
                // If loading as object failed, try as string - means it's double-encoded
                try
                {
                    loadedString = await _contextManager.LoadContextAsync<string>(caseId, "test/suspect_element");
                    isDoubleEncoded = true;
                    isValidJson = false;
                    _logger.LogWarning("⚠️ Had to load as string - appears to be double-encoded");
                }
                catch (Exception ex)
                {
                    _logger.LogError("❌ Failed to load in any format: {Message}", ex.Message);
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync($"Failed to load saved data: {ex.Message}");
                    return errorResponse;
                }
            }

            if (loadedString == null)
            {
                _logger.LogError("❌ Failed to load saved data");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Failed to load saved data");
                return errorResponse;
            }

            _logger.LogInformation("Loaded string length: {Length} bytes", loadedString.Length);
            _logger.LogInformation("Loaded string preview: {Preview}...", loadedString.Substring(0, Math.Min(100, loadedString.Length)));

            _logger.LogInformation(isValidJson ? "✅ JSON is valid (not double-encoded)" : "❌ JSON is double-encoded!");

            // === TEST 2: Try to parse loaded JSON ===
            _logger.LogInformation("=== TEST 2: Parse loaded JSON ===");
            
            if (!isValidJson)
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(loadedString);
                    var suspectId = parsed?["suspectId"].GetString();
                    var suspectName = parsed?["name"].GetString();
                    
                    _logger.LogInformation("✅ Successfully parsed: suspectId={SuspectId}, name={Name}", suspectId, suspectName);
                    isValidJson = true; // It parsed, so it's actually valid
                }
                catch (JsonException ex)
                {
                    _logger.LogError("❌ Failed to parse JSON: {Message}", ex.Message);
                }
            }
            else
            {
                _logger.LogInformation("✅ Already confirmed valid during load");
            }

            // === TEST 3: Save manifest-style object ===
            _logger.LogInformation("=== TEST 3: Save/Load manifest object ===");
            
            var testManifest = new
            {
                caseId = caseId,
                version = "v2-test",
                entities = new
                {
                    suspects = new[] { "@entities/suspects/TEST001" },
                    total = 1
                }
            };

            await _contextManager.SaveContextAsync(caseId, "test/manifest", testManifest);
            _logger.LogInformation("✅ Saved manifest object to test/manifest");

            // Try to load manifest - same approach
            string? loadedManifest = null;
            bool manifestValid = false;

            try
            {
                // Try to load as object first
                var manifestObj = await _contextManager.LoadContextAsync<Dictionary<string, JsonElement>>(
                    caseId, "test/manifest");
                
                if (manifestObj != null)
                {
                    manifestValid = true;
                    loadedManifest = JsonSerializer.Serialize(manifestObj);
                    _logger.LogInformation("✅ Manifest loaded successfully as object - NOT double-encoded");
                }
            }
            catch (JsonException)
            {
                try
                {
                    loadedManifest = await _contextManager.LoadContextAsync<string>(caseId, "test/manifest");
                    manifestValid = false;
                    _logger.LogWarning("⚠️ Manifest had to be loaded as string - double-encoded");
                }
                catch (Exception ex)
                {
                    _logger.LogError("❌ Failed to load manifest: {Message}", ex.Message);
                }
            }
            
            _logger.LogInformation(manifestValid ? "✅ Manifest JSON is valid" : "❌ Manifest JSON is double-encoded");

            // Build response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = caseId,
                success = isValidJson && manifestValid,
                tests = new
                {
                    jsonElement = new
                    {
                        passed = isValidJson,
                        originalLength = testJson.Length,
                        loadedLength = loadedString?.Length ?? 0,
                        isDoubleEncoded = isDoubleEncoded,
                        preview = loadedString?.Substring(0, Math.Min(200, loadedString?.Length ?? 0))
                    },
                    manifest = new
                    {
                        passed = manifestValid,
                        preview = loadedManifest?.Substring(0, Math.Min(200, loadedManifest?.Length ?? 0))
                    }
                },
                message = (isValidJson && manifestValid) 
                    ? "✅ All encoding tests passed - no double-encoding detected"
                    : "❌ Encoding issues detected - check logs for details"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error testing JSON encoding: {Message}", ex.Message);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = ex.Message,
                type = ex.GetType().Name,
                stackTrace = ex.StackTrace
            });
            
            return errorResponse;
        }
    }

    private class TestEncodingRequest
    {
        public string? CaseId { get; set; }
    }
}
