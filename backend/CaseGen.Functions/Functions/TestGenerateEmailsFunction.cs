using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

/// <summary>
/// Test function to generate emails for an existing case
/// </summary>
public class TestGenerateEmailsFunction
{
    private readonly ILogger<TestGenerateEmailsFunction> _logger;
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly IContextManager _contextManager;

    public TestGenerateEmailsFunction(
        ILogger<TestGenerateEmailsFunction> logger,
        ICaseGenerationService caseGenerationService,
        IContextManager contextManager)
    {
        _logger = logger;
        _caseGenerationService = caseGenerationService;
        _contextManager = contextManager;
    }

    [Function("TestGenerateEmails")]
    public async Task<HttpResponseData> TestGenerateEmails(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("TestGenerateEmails function triggered");

        try
        {
            // Parse request body to get caseId
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Received request body: {Body}", requestBody);
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var requestData = JsonSerializer.Deserialize<TestGenerateEmailsRequest>(requestBody, options);

            if (string.IsNullOrEmpty(requestData?.CaseId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync($"CaseId is required in request body. Received: {requestBody}");
                return badResponse;
            }

            var caseId = requestData.CaseId;
            _logger.LogInformation("🧪 Starting email generation test for case {CaseId}", caseId);

            var results = new TestGenerateEmailsResult
            {
                CaseId = caseId,
                Steps = new List<StepResult>()
            };

            // Step 1: Generate Email Designs
            _logger.LogInformation("=== STEP 1: Generate Email Designs ===");
            try
            {
                await _caseGenerationService.GenerateEmailDesignsAsync(caseId);
                
                // Query design/emails/*
                var designFiles = await _contextManager.QueryContextAsync<string>(caseId, "design/emails/*");
                var designCount = designFiles.Count();
                
                _logger.LogInformation("✅ Generated {Count} email designs", designCount);
                
                results.Steps.Add(new StepResult
                {
                    StepName = "GenerateEmailDesigns",
                    Success = true,
                    FilesCreated = designFiles.Select(f => f.Path).ToList(),
                    Message = $"Created {designCount} email design files"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate email designs");
                results.Steps.Add(new StepResult
                {
                    StepName = "GenerateEmailDesigns",
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(results);
                return errorResponse;
            }

            // Step 2: Expand Emails
            _logger.LogInformation("=== STEP 2: Expand Emails ===");
            try
            {
                await _caseGenerationService.ExpandEmailsAsync(caseId);
                
                // Query expand/emails/*
                var expandFiles = await _contextManager.QueryContextAsync<string>(caseId, "expand/emails/*");
                var expandCount = expandFiles.Count();
                
                _logger.LogInformation("✅ Expanded {Count} emails", expandCount);
                
                results.Steps.Add(new StepResult
                {
                    StepName = "ExpandEmails",
                    Success = true,
                    FilesCreated = expandFiles.Select(f => f.Path).ToList(),
                    Message = $"Created {expandCount} expanded email files"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to expand emails");
                results.Steps.Add(new StepResult
                {
                    StepName = "ExpandEmails",
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(results);
                return errorResponse;
            }

            // Step 3: Normalize Emails
            _logger.LogInformation("=== STEP 3: Normalize Emails ===");
            try
            {
                await _caseGenerationService.NormalizeEmailsAsync(caseId);
                
                // Query emails/*
                var normalizedFiles = await _contextManager.QueryContextAsync<string>(caseId, "emails/*");
                var normalizedCount = normalizedFiles.Count();
                
                _logger.LogInformation("✅ Normalized {Count} emails", normalizedCount);
                
                // Parse and validate each email
                var emailValidations = new List<EmailValidation>();
                foreach (var emailFile in normalizedFiles)
                {
                    try
                    {
                        var emailData = JsonSerializer.Deserialize<JsonDocument>(emailFile.Data);
                        var validation = new EmailValidation
                        {
                            FilePath = emailFile.Path,
                            IsValid = true,
                            Fields = new Dictionary<string, bool>()
                        };

                        // Validate required fields
                        var root = emailData!.RootElement;
                        validation.Fields["emailId"] = root.TryGetProperty("emailId", out _);
                        validation.Fields["from"] = root.TryGetProperty("from", out _);
                        validation.Fields["to"] = root.TryGetProperty("to", out _);
                        validation.Fields["subject"] = root.TryGetProperty("subject", out _);
                        validation.Fields["content"] = root.TryGetProperty("content", out _);
                        validation.Fields["sentAt"] = root.TryGetProperty("sentAt", out _);
                        validation.Fields["priority"] = root.TryGetProperty("priority", out _);
                        validation.Fields["attachments"] = root.TryGetProperty("attachments", out _);
                        validation.Fields["gated"] = root.TryGetProperty("gated", out _);

                        validation.IsValid = validation.Fields.Values.All(v => v);
                        
                        // Validate ISO-8601 timestamp
                        if (root.TryGetProperty("sentAt", out var sentAtProp))
                        {
                            var sentAt = sentAtProp.GetString();
                            validation.TimestampFormat = DateTime.TryParse(sentAt, out _) ? "Valid ISO-8601" : "Invalid";
                        }

                        emailValidations.Add(validation);
                    }
                    catch (Exception ex)
                    {
                        emailValidations.Add(new EmailValidation
                        {
                            FilePath = emailFile.Path,
                            IsValid = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }
                
                results.Steps.Add(new StepResult
                {
                    StepName = "NormalizeEmails",
                    Success = true,
                    FilesCreated = normalizedFiles.Select(f => f.Path).ToList(),
                    Message = $"Created {normalizedCount} normalized email files",
                    EmailValidations = emailValidations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to normalize emails");
                results.Steps.Add(new StepResult
                {
                    StepName = "NormalizeEmails",
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(results);
                return errorResponse;
            }

            // Step 4: Verify NormalizedCaseBundle
            _logger.LogInformation("=== STEP 4: Verify NormalizedCaseBundle ===");
            try
            {
                // Read the normalized bundle from blob storage
                var bundleContexts = await _contextManager.QueryContextAsync<string>(caseId, "normalizedCaseBundle.json");
                var bundleJson = bundleContexts.FirstOrDefault()?.Data;
                
                if (bundleJson != null)
                {
                    var bundle = JsonSerializer.Deserialize<dynamic>(bundleJson);
                    var emailsProperty = ((JsonElement)bundle).GetProperty("emails");
                    var emailCount = emailsProperty.GetArrayLength();
                    
                    _logger.LogInformation("✅ NormalizedCaseBundle contains {Count} emails", emailCount);
                    
                    results.Steps.Add(new StepResult
                    {
                        StepName = "VerifyBundle",
                        Success = true,
                        Message = $"Bundle contains {emailCount} emails",
                        BundleEmailCount = emailCount
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ NormalizedCaseBundle not found");
                    results.Steps.Add(new StepResult
                    {
                        StepName = "VerifyBundle",
                        Success = false,
                        Message = "NormalizedCaseBundle not found in blob storage"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to verify bundle");
                results.Steps.Add(new StepResult
                {
                    StepName = "VerifyBundle",
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
            }

            // Return success response
            results.Success = results.Steps.All(s => s.Success);
            results.Summary = $"Email generation complete. {results.Steps.Count(s => s.Success)}/{results.Steps.Count} steps successful.";
            
            _logger.LogInformation("🎉 Test completed: {Summary}", results.Summary);

            var response = req.CreateResponse(results.Success ? HttpStatusCode.OK : HttpStatusCode.PartialContent);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var jsonContent = JsonSerializer.Serialize(results, jsonOptions);
            await response.WriteStringAsync(jsonContent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in TestGenerateEmails");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}

// Request model
public class TestGenerateEmailsRequest
{
    public string CaseId { get; set; } = string.Empty;
}

// Response models
public class TestGenerateEmailsResult
{
    public string CaseId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<StepResult> Steps { get; set; } = new();
}

public class StepResult
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> FilesCreated { get; set; } = new();
    public List<EmailValidation>? EmailValidations { get; set; }
    public int? BundleEmailCount { get; set; }
}

public class EmailValidation
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public Dictionary<string, bool> Fields { get; set; } = new();
    public string? TimestampFormat { get; set; }
    public string? ErrorMessage { get; set; }
}
