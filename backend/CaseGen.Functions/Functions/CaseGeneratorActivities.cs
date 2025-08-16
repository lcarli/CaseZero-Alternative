using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;

namespace CaseGen.Functions.Functions;

public class CaseGeneratorActivities
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly ILogger<CaseGeneratorActivities> _logger;

    public CaseGeneratorActivities(ICaseGenerationService caseGenerationService, ILogger<CaseGeneratorActivities> logger)
    {
        _caseGenerationService = caseGenerationService;
        _logger = logger;
    }

    [Function("PlanActivity")]
    public async Task<string> PlanActivity([ActivityTrigger] (CaseGenerationRequest request, string caseId) input)
    {
        _logger.LogInformation("Planning case {CaseId}", input.caseId);
        return await _caseGenerationService.PlanCaseAsync(input.request);
    }

    [Function("ExpandActivity")]
    public async Task<string> ExpandActivity([ActivityTrigger] string planJson)
    {
        _logger.LogInformation("Expanding case plan");
        return await _caseGenerationService.ExpandCaseAsync(planJson);
    }

    [Function("DesignActivity")]
    public async Task<string> DesignActivity([ActivityTrigger] string expandedJson)
    {
        _logger.LogInformation("Designing case structure");
        return await _caseGenerationService.DesignCaseAsync(expandedJson);
    }

    [Function("GenerateDocumentsActivity")]
    public async Task<string[]> GenerateDocumentsActivity([ActivityTrigger] string designJson)
    {
        _logger.LogInformation("Generating case documents");
        return await _caseGenerationService.GenerateDocumentsAsync(designJson);
    }

    [Function("GenerateMediaActivity")]
    public async Task<string[]> GenerateMediaActivity([ActivityTrigger] string designJson)
    {
        _logger.LogInformation("Generating media prompts");
        return await _caseGenerationService.GenerateMediaAsync(designJson);
    }

    [Function("NormalizeActivity")]
    public async Task<string> NormalizeActivity([ActivityTrigger] (string[] documents, string[] media) input)
    {
        _logger.LogInformation("Normalizing case content");
        return await _caseGenerationService.NormalizeCaseAsync(input.documents, input.media);
    }

    [Function("IndexActivity")]
    public async Task<string> IndexActivity([ActivityTrigger] string normalizedJson)
    {
        _logger.LogInformation("Indexing case content");
        return await _caseGenerationService.IndexCaseAsync(normalizedJson);
    }

    [Function("ValidateRulesActivity")]
    public async Task<string> ValidateRulesActivity([ActivityTrigger] string indexedJson)
    {
        _logger.LogInformation("Validating case rules");
        return await _caseGenerationService.ValidateRulesAsync(indexedJson);
    }

    [Function("RedTeamActivity")]
    public async Task<string> RedTeamActivity([ActivityTrigger] string validatedJson)
    {
        _logger.LogInformation("Red teaming case");
        return await _caseGenerationService.RedTeamCaseAsync(validatedJson);
    }

    [Function("PackageActivity")]
    public async Task<CaseGenerationOutput> PackageActivity([ActivityTrigger] (string finalJson, string caseId) input)
    {
        _logger.LogInformation("Packaging case {CaseId}", input.caseId);
        return await _caseGenerationService.PackageCaseAsync(input.finalJson, input.caseId);
    }
}