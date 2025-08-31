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
    public async Task<string> PlanActivity([ActivityTrigger] PlanActivityModel model)
    {
        _logger.LogInformation("Planning case {CaseTitle}", model.CaseId);
        return await _caseGenerationService.PlanCaseAsync(model.Request);
    }

    [Function("ExpandActivity")]
    public async Task<string> ExpandActivity([ActivityTrigger] string planJson)
    {
        _logger.LogInformation("Expanding case plan");
        return await _caseGenerationService.ExpandCaseAsync(planJson);
    }

    [Function("DesignActivity")]
    public async Task<string> DesignActivity([ActivityTrigger] DesignActivityModel model)
    {
        _logger.LogInformation("Designing case structure");
        return await _caseGenerationService.DesignCaseAsync(model.PlanJson, model.ExpandedJson, model.Difficulty);
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

    [Function("GenerateDocumentItemActivity")]
    public async Task<string> GenerateDocumentItemActivity([ActivityTrigger] GenerateDocumentItemInput input)
    {
        _logger.LogInformation("Activity: GenerateDocumentItem [{DocId}]", input.Spec.DocId);
        return await _caseGenerationService.GenerateDocumentFromSpecAsync(input.Spec, input.DesignJson);
    }

    [Function("GenerateMediaItemActivity")]
    public async Task<string> GenerateMediaItemActivity([ActivityTrigger] GenerateMediaItemInput input)
    {
        _logger.LogInformation("Activity: GenerateMediaItem [{EvidenceId}]", input.Spec.EvidenceId);
        return await _caseGenerationService.GenerateMediaFromSpecAsync(input.Spec, input.DesignJson);
    }


    [Function("NormalizeActivity")]
    public async Task<string> NormalizeActivity([ActivityTrigger] NormalizeActivityModel model)
    {
        _logger.LogInformation("Normalizing case content");
        return await _caseGenerationService.NormalizeCaseAsync(model.Documents, model.Media);
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
    public async Task<CaseGenerationOutput> PackageActivity([ActivityTrigger] PackageActivityModel model)
    {
        _logger.LogInformation("Packaging case {CaseId}", model.CaseId);
        return await _caseGenerationService.PackageCaseAsync(model.FinalJson, model.CaseId);
    }
}