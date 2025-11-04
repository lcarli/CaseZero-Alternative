using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Test function to run normalization on an existing case
/// </summary>
public class TestNormalizeFunction
{
    private readonly ILogger<TestNormalizeFunction> _logger;
    private readonly IContextManager _contextManager;

    public TestNormalizeFunction(
        ILogger<TestNormalizeFunction> logger,
        IContextManager contextManager)
    {
        _logger = logger;
        _contextManager = contextManager;
    }

    [Function("TestNormalizeEntities")]
    public async Task<HttpResponseData> TestNormalizeEntities(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("TestNormalizeEntities function triggered");

        try
        {
            // Parse request body to get caseId
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Received request body: {Body}", requestBody);
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var requestData = JsonSerializer.Deserialize<TestNormalizeRequest>(requestBody, options);

            if (string.IsNullOrEmpty(requestData?.CaseId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync($"CaseId is required in request body. Received: {requestBody}");
                return badResponse;
            }

            var caseId = requestData.CaseId;
            _logger.LogInformation("🧪 Starting normalization test for case {CaseId}", caseId);

            // Test 1: Query suspects from expand/suspects/* - query as string then parse
            _logger.LogInformation("=== TEST 1: Querying expand/suspects/* ===");
            var suspectsQueryRaw = await _contextManager.QueryContextAsync<string>(
                caseId, 
                "expand/suspects/*");
            var suspectsList = suspectsQueryRaw
                .Select(s => new 
                {
                    Path = s.Path,
                    SizeBytes = s.SizeBytes,
                    Data = JsonDocument.Parse(s.Data).RootElement
                })
                .ToList();
            _logger.LogInformation("✅ Found {Count} suspects", suspectsList.Count);
            
            foreach (var suspect in suspectsList)
            {
                _logger.LogInformation("  - Suspect: {Path} ({Size} bytes)", suspect.Path, suspect.SizeBytes);
            }

            // Test 2: Query evidence from expand/evidence/* - query as string then parse
            _logger.LogInformation("=== TEST 2: Querying expand/evidence/* ===");
            var evidenceQueryRaw = await _contextManager.QueryContextAsync<string>(
                caseId, 
                "expand/evidence/*");
            var evidenceList = evidenceQueryRaw
                .Select(e => new 
                {
                    Path = e.Path,
                    SizeBytes = e.SizeBytes,
                    Data = JsonDocument.Parse(e.Data).RootElement
                })
                .ToList();
            _logger.LogInformation("✅ Found {Count} evidence items", evidenceList.Count);
            
            foreach (var evidence in evidenceList)
            {
                _logger.LogInformation("  - Evidence: {Path} ({Size} bytes)", evidence.Path, evidence.SizeBytes);
            }

            // Test 3: Show what we would normalize
            _logger.LogInformation("=== TEST 3: Normalization Summary ===");
            _logger.LogInformation("Would normalize:");
            _logger.LogInformation("  - {SuspectCount} suspects -> entities/suspects/", suspectsList.Count);
            _logger.LogInformation("  - {EvidenceCount} evidence -> entities/evidence/", evidenceList.Count);

            // Create success response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                caseId = caseId,
                success = true,
                results = new
                {
                    suspects = new
                    {
                        count = suspectsList.Count,
                        items = suspectsList.Select(s => new 
                        { 
                            path = s.Path, 
                            size = s.SizeBytes,
                            id = s.Data.TryGetProperty("suspectId", out var suspectId) ? suspectId.GetString() : "unknown"
                        }).ToList()
                    },
                    evidence = new
                    {
                        count = evidenceList.Count,
                        items = evidenceList.Select(e => new 
                        { 
                            path = e.Path, 
                            size = e.SizeBytes,
                            id = e.Data.TryGetProperty("evidenceId", out var evidenceId) ? evidenceId.GetString() : "unknown"
                        }).ToList()
                    }
                },
                message = $"Successfully queried {suspectsList.Count} suspects and {evidenceList.Count} evidence items"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error testing normalization: {Message}", ex.Message);
            
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

    private class TestNormalizeRequest
    {
        public string? CaseId { get; set; }
    }
}
