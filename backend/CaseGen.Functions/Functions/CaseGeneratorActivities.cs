using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using System.Text.Json;

namespace CaseGen.Functions.Functions;

public class CaseGeneratorActivities
{
    private readonly ICaseGenerationService _caseGenerationService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<CaseGeneratorActivities> _logger;

    public CaseGeneratorActivities(
        ICaseGenerationService caseGenerationService, 
        ICaseLoggingService caseLogging,
        ILogger<CaseGeneratorActivities> logger)
    {
        _caseGenerationService = caseGenerationService;
        _caseLogging = caseLogging;
        _logger = logger;
    }

    [Function("PlanActivity")]
    public async Task<string> PlanActivity([ActivityTrigger] PlanActivityModel model)
    {
        _logger.LogInformation("Planning case {CaseTitle}", model.CaseId);
        var planResult = await _caseGenerationService.PlanCaseAsync(model.Request, model.CaseId);
        
        // Save the plan response as formatted JSON
        await _caseLogging.LogStepResponseAsync(model.CaseId, "plancase", planResult);
        
        return planResult;
    }

    [Function("ExpandActivity")]
    public async Task<string> ExpandActivity([ActivityTrigger] ExpandActivityModel model)
    {
        _logger.LogInformation("Expanding case plan");
        var expandResult = await _caseGenerationService.ExpandCaseAsync(model.PlanJson, model.CaseId);
        
        // Save the expand response as formatted JSON
        await _caseLogging.LogStepResponseAsync(model.CaseId, "expandcase", expandResult);
        
        return expandResult;
    }

    [Function("DesignActivity")]
    public async Task<string> DesignActivity([ActivityTrigger] DesignActivityModel model)
    {
        _logger.LogInformation("Designing case structure");
        var designResult = await _caseGenerationService.DesignCaseAsync(model.PlanJson, model.ExpandedJson, model.CaseId, model.Difficulty);
        
        // Save the design response as formatted JSON
        await _caseLogging.LogStepResponseAsync(model.CaseId, "designcase", designResult);
        
        return designResult;
    }

    [Function("GenerateDocumentItemActivity")]
    public async Task<string> GenerateDocumentItemActivity([ActivityTrigger] GenerateDocumentItemInput input)
    {
        _logger.LogInformation("Activity: GenerateDocumentItem [{DocId}]", input.Spec.DocId);
        return await _caseGenerationService.GenerateDocumentFromSpecAsync(input.Spec, input.DesignJson, input.CaseId, input.PlanJson, input.ExpandedJson, input.DifficultyOverride);
    }

    [Function("GenerateMediaItemActivity")]
    public async Task<string> GenerateMediaItemActivity([ActivityTrigger] GenerateMediaItemInput input)
    {
        _logger.LogInformation("Activity: GenerateMediaItem [{EvidenceId}]", input.Spec.EvidenceId);
        return await _caseGenerationService.GenerateMediaFromSpecAsync(input.Spec, input.DesignJson, input.CaseId, input.PlanJson, input.ExpandedJson, input.DifficultyOverride);
    }

    [Function("RenderDocumentItemActivity")]
    public async Task<string> RenderDocumentItemActivity([ActivityTrigger] RenderDocumentItemInput input)
    {
        _logger.LogInformation("Activity: RenderDocumentItem [{DocId}]", input.DocId);
        return await _caseGenerationService.RenderDocumentFromJsonAsync(input.DocId, input.DocumentJson, input.CaseId);
    }

    [Function("RenderMediaItemActivity")]
    public async Task<string> RenderMediaItemActivity([ActivityTrigger] RenderMediaItemInput input)
    {
        _logger.LogInformation("Activity: RenderMediaItem [{EvidenceId}]", input.Spec.EvidenceId);
        return await _caseGenerationService.RenderMediaFromJsonAsync(input.Spec, input.CaseId);
    }


    [Function("NormalizeActivity")]
    public async Task<string> NormalizeActivity([ActivityTrigger] NormalizeActivityModel model)
    {
        _logger.LogInformation("Normalizing case content deterministically");
        
        var input = new NormalizationInput
        {
            CaseId = model.CaseId,
            Difficulty = model.Difficulty,
            Timezone = model.Timezone,
            PlanJson = model.PlanJson,
            ExpandedJson = model.ExpandedJson,
            DesignJson = model.DesignJson,
            Documents = model.Documents,
            Media = model.Media,
            RenderedDocs = model.RenderedDocs,
            RenderedMedia = model.RenderedMedia
        };
        
        var result = await _caseGenerationService.NormalizeCaseDeterministicAsync(input);
        
        // Return the JSON string directly - NormalizedJson is already a serialized JSON string
        return result.NormalizedJson;
    }

    [Function("IndexActivity")]
    public async Task<string> IndexActivity([ActivityTrigger] IndexActivityModel model)
    {
        _logger.LogInformation("Indexing case content");
        return await _caseGenerationService.IndexCaseAsync(model.NormalizedJson, model.CaseId);
    }

    [Function("ValidateRulesActivity")]
    public async Task<string> ValidateRulesActivity([ActivityTrigger] ValidateActivityModel model)
    {
        _logger.LogInformation("Validating case rules");
        return await _caseGenerationService.ValidateRulesAsync(model.IndexedJson, model.CaseId);
    }

    [Function("RedTeamActivity")]
    public async Task<string> RedTeamActivity([ActivityTrigger] RedTeamActivityModel model)
    {
        _logger.LogInformation("Red teaming case");
        return await _caseGenerationService.RedTeamCaseAsync(model.ValidatedJson, model.CaseId);
    }

    [Function("SaveRedTeamAnalysisActivity")]
    public async Task SaveRedTeamAnalysisActivity([ActivityTrigger] SaveRedTeamAnalysisActivityModel model)
    {
        _logger.LogInformation("Saving RedTeam analysis to logs container for case {CaseId}", model.CaseId);
        await _caseGenerationService.SaveRedTeamAnalysisAsync(model.CaseId, model.RedTeamAnalysis);
    }

    [Function("PackageActivity")]
    public async Task<CaseGenerationOutput> PackageActivity([ActivityTrigger] PackageActivityModel model)
    {
        _logger.LogInformation("Packaging case {CaseId}", model.CaseId);
        return await _caseGenerationService.PackageCaseAsync(model.FinalJson, model.CaseId);
    }
}