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

    // ========== Phase 2: Hierarchical Plan Activities ==========

    [Function("PlanCoreActivity")]
    public async Task<string> PlanCoreActivity([ActivityTrigger] PlanCoreActivityModel model)
    {
        _logger.LogInformation("PLAN-CORE: Generating core structure for case {CaseId}", model.CaseId);
        var result = await _caseGenerationService.PlanCoreAsync(model.Request, model.CaseId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, "plan-core", result);
        return result;
    }

    [Function("PlanSuspectsActivity")]
    public async Task<string> PlanSuspectsActivity([ActivityTrigger] PlanSuspectsActivityModel model)
    {
        _logger.LogInformation("PLAN-SUSPECTS: Generating suspects for case {CaseId}", model.CaseId);
        var result = await _caseGenerationService.PlanSuspectsAsync(model.CaseId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, "plan-suspects", result);
        return result;
    }

    [Function("PlanTimelineActivity")]
    public async Task<string> PlanTimelineActivity([ActivityTrigger] PlanTimelineActivityModel model)
    {
        _logger.LogInformation("PLAN-TIMELINE: Generating timeline for case {CaseId}", model.CaseId);
        var result = await _caseGenerationService.PlanTimelineAsync(model.CaseId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, "plan-timeline", result);
        return result;
    }

    [Function("PlanEvidenceActivity")]
    public async Task<string> PlanEvidenceActivity([ActivityTrigger] PlanEvidenceActivityModel model)
    {
        _logger.LogInformation("PLAN-EVIDENCE: Generating evidence plan for case {CaseId}", model.CaseId);
        var result = await _caseGenerationService.PlanEvidenceAsync(model.CaseId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, "plan-evidence", result);
        return result;
    }

    // ========== Original Monolithic Plan Activity ==========

    [Function("PlanActivity")]
    public async Task<string> PlanActivity([ActivityTrigger] PlanActivityModel model)
    {
        _logger.LogInformation("Planning case {CaseTitle}", model.CaseId);
        var planResult = await _caseGenerationService.PlanCaseAsync(model.Request, model.CaseId);
        
        // Save the plan response as formatted JSON
        await _caseLogging.LogStepResponseAsync(model.CaseId, "plancase", planResult);
        
        return planResult;
    }

    // ========== Phase 3: Hierarchical Expand Activities ==========

    [Function("ExpandSuspectActivity")]
    public async Task<string> ExpandSuspectActivity([ActivityTrigger] ExpandSuspectActivityModel model)
    {
        _logger.LogInformation("EXPAND-SUSPECT: Expanding suspect {SuspectId} for case {CaseId}", model.SuspectId, model.CaseId);
        var result = await _caseGenerationService.ExpandSuspectAsync(model.CaseId, model.SuspectId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, $"expand-suspect-{model.SuspectId}", result);
        return result;
    }

    [Function("ExpandEvidenceActivity")]
    public async Task<string> ExpandEvidenceActivity([ActivityTrigger] ExpandEvidenceActivityModel model)
    {
        _logger.LogInformation("EXPAND-EVIDENCE: Expanding evidence {EvidenceId} for case {CaseId}", model.EvidenceId, model.CaseId);
        var result = await _caseGenerationService.ExpandEvidenceAsync(model.CaseId, model.EvidenceId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, $"expand-evidence-{model.EvidenceId}", result);
        return result;
    }

    [Function("ExpandTimelineActivity")]
    public async Task<string> ExpandTimelineActivity([ActivityTrigger] ExpandTimelineActivityModel model)
    {
        _logger.LogInformation("EXPAND-TIMELINE: Expanding timeline for case {CaseId}", model.CaseId);
        var result = await _caseGenerationService.ExpandTimelineAsync(model.CaseId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, "expand-timeline", result);
        return result;
    }

    [Function("SynthesizeRelationsActivity")]
    public async Task<string> SynthesizeRelationsActivity([ActivityTrigger] SynthesizeRelationsActivityModel model)
    {
        _logger.LogInformation("SYNTHESIZE-RELATIONS: Synthesizing relationships for case {CaseId}", model.CaseId);
        var result = await _caseGenerationService.SynthesizeRelationsAsync(model.CaseId);
        await _caseLogging.LogStepResponseAsync(model.CaseId, "synthesize-relations", result);
        return result;
    }

    // ========== Helper Activities ==========

    [Function("LoadContextActivity")]
    public async Task<string> LoadContextActivity([ActivityTrigger] LoadContextActivityModel model)
    {
        _logger.LogInformation("LOAD-CONTEXT: Loading {Path} for case {CaseId}", model.Path, model.CaseId);
        var result = await _caseGenerationService.LoadContextAsync(model.CaseId, model.Path);
        return result;
    }

    // ========== Phase 4: Design Activities by Type ==========

    [Function("DesignDocumentTypeActivity")]
    public async Task<string> DesignDocumentTypeActivity([ActivityTrigger] DesignDocumentTypeActivityModel model)
    {
        _logger.LogInformation("DESIGN-DOC-TYPE: type={DocType} caseId={CaseId}", model.DocType, model.CaseId);
        var result = await _caseGenerationService.DesignDocumentTypeAsync(model.CaseId, model.DocType);
        await _caseLogging.LogStepResponseAsync(model.CaseId, $"design-document-{model.DocType}", result);
        return result;
    }

    [Function("DesignMediaTypeActivity")]
    public async Task<string> DesignMediaTypeActivity([ActivityTrigger] DesignMediaTypeActivityModel model)
    {
        _logger.LogInformation("DESIGN-MEDIA-TYPE: type={MediaType} caseId={CaseId}", model.MediaType, model.CaseId);
        var result = await _caseGenerationService.DesignMediaTypeAsync(model.CaseId, model.MediaType);
        await _caseLogging.LogStepResponseAsync(model.CaseId, $"design-media-{model.MediaType}", result);
        return result;
    }

    // ========== Phase 5.5: Email Generation Activities ==========

    [Function("GenerateEmailDesignsActivity")]
    public async Task<string> GenerateEmailDesignsActivity([ActivityTrigger] GenerateEmailDesignsActivityModel input)
    {
        _logger.LogInformation("EMAIL-DESIGN: caseId={CaseId}", input.CaseId);
        var result = await _caseGenerationService.GenerateEmailDesignsAsync(input.CaseId, CancellationToken.None);
        await _caseLogging.LogStepResponseAsync(input.CaseId, "email-design", result);
        return result;
    }

    [Function("ExpandEmailsActivity")]
    public async Task<string> ExpandEmailsActivity([ActivityTrigger] ExpandEmailsActivityModel input)
    {
        _logger.LogInformation("EMAIL-EXPAND: caseId={CaseId}", input.CaseId);
        var result = await _caseGenerationService.ExpandEmailsAsync(input.CaseId, CancellationToken.None);
        await _caseLogging.LogStepResponseAsync(input.CaseId, "email-expand", result);
        return result;
    }

    [Function("NormalizeEmailsActivity")]
    public async Task<string> NormalizeEmailsActivity([ActivityTrigger] NormalizeEmailsActivityModel input)
    {
        _logger.LogInformation("EMAIL-NORMALIZE: caseId={CaseId}", input.CaseId);
        var result = await _caseGenerationService.NormalizeEmailsAsync(input.CaseId);
        await _caseLogging.LogStepResponseAsync(input.CaseId, "email-normalize", result);
        return result;
    }

    // ========== Original Monolithic Expand Activity ==========

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
        // Phase 5: Pass minimal context - service will load via ContextManager
        return await _caseGenerationService.GenerateDocumentFromSpecAsync(
            input.Spec, 
            designJson: string.Empty, // Will be loaded by service from design/documents/{type}
            input.CaseId, 
            planJson: null, 
            expandJson: null, 
            input.DifficultyOverride);
    }

    [Function("GenerateMediaItemActivity")]
    public async Task<string> GenerateMediaItemActivity([ActivityTrigger] GenerateMediaItemInput input)
    {
        _logger.LogInformation("Activity: GenerateMediaItem [{EvidenceId}]", input.Spec.EvidenceId);
        // Phase 5: Pass minimal context - service will load via ContextManager
        return await _caseGenerationService.GenerateMediaFromSpecAsync(
            input.Spec, 
            designJson: string.Empty, // Will be loaded by service from design/media/{type}
            input.CaseId, 
            planJson: null, 
            expandJson: null, 
            input.DifficultyOverride);
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

    [Function("ValidateRulesActivity")]
    public async Task<string> ValidateRulesActivity([ActivityTrigger] ValidateActivityModel model)
    {
        _logger.LogInformation("Validating case rules");
        return await _caseGenerationService.ValidateRulesAsync(model.NormalizedJson, model.CaseId);
    }

    [Function("RedTeamGlobalActivity")]
    public async Task<string> RedTeamGlobalActivity([ActivityTrigger] RedTeamGlobalActivityModel model)
    {
        _logger.LogInformation("Global red team analysis - examining complete case context");
        return await _caseGenerationService.RedTeamGlobalAnalysisAsync(model.ValidatedJson, model.CaseId);
    }

    [Function("RedTeamFocusedActivity")]
    public async Task<string> RedTeamFocusedActivity([ActivityTrigger] RedTeamFocusedActivityModel model)
    {
        _logger.LogInformation("Focused red team analysis - examining specific areas: {FocusAreas}", string.Join(", ", model.FocusAreas));
        return await _caseGenerationService.RedTeamFocusedAnalysisAsync(model.ValidatedJson, model.CaseId, model.GlobalAnalysis, model.FocusAreas);
    }



    [Function("FixActivity")]
    public async Task<string> FixActivity([ActivityTrigger] FixActivityModel model)
    {
        _logger.LogInformation("Fixing case issues - iteration {Iteration}", model.IterationNumber);
        return await _caseGenerationService.FixCaseAsync(model.RedTeamAnalysis, model.CurrentJson, model.CaseId, model.IterationNumber);
    }

    [Function("CheckCaseCleanActivity")]
    public async Task<bool> CheckCaseCleanActivity([ActivityTrigger] CheckCaseCleanActivityModel model)
    {
        _logger.LogInformation("Checking if case is clean and ready for packaging");
        return await _caseGenerationService.IsCaseCleanAsync(model.RedTeamAnalysis, model.CaseId);
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