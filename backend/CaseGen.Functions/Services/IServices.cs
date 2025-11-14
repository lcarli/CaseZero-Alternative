using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public interface ICaseGenerationService
{
    Task<string> PlanCaseAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken = default);
    
    // Phase 2: Hierarchical Plan methods
    Task<string> PlanCoreAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken = default);
    Task<string> PlanSuspectsAsync(string caseId, CancellationToken cancellationToken = default);
    Task<string> PlanTimelineAsync(string caseId, CancellationToken cancellationToken = default);
    Task<string> PlanEvidenceAsync(string caseId, CancellationToken cancellationToken = default);
    
    Task<string> ExpandCaseAsync(string planJson, string caseId, CancellationToken cancellationToken = default);
    
    // Phase 3: Hierarchical Expand methods
    Task<string> ExpandSuspectAsync(string caseId, string suspectId, CancellationToken cancellationToken = default);
    Task<string> ExpandEvidenceAsync(string caseId, string evidenceId, CancellationToken cancellationToken = default);
    Task<string> ExpandTimelineAsync(string caseId, CancellationToken cancellationToken = default);
    Task<string> SynthesizeRelationsAsync(string caseId, CancellationToken cancellationToken = default);
    
    // Helper methods
    Task<string> LoadContextAsync(string caseId, string path, CancellationToken cancellationToken = default);
    
    // Phase 4: Design by document/media type
    Task<string> DesignDocumentTypeAsync(string caseId, string docType, CancellationToken cancellationToken = default);
    Task<string> DesignMediaTypeAsync(string caseId, string mediaType, CancellationToken cancellationToken = default);
    
    // Phase 5.5: Email generation pipeline
    Task<string?> GenerateEmailDesignsAsync(string caseId, CancellationToken cancellationToken = default);
    Task<string?> ExpandEmailsAsync(string caseId, CancellationToken cancellationToken = default);
    Task<string> NormalizeEmailsAsync(string caseId, CancellationToken cancellationToken = default);
    
    // Visual consistency for image generation
    Task<string> DesignVisualConsistencyRegistryAsync(string caseId, CancellationToken cancellationToken = default);
    Task<int> GenerateMasterReferencesAsync(string caseId, CancellationToken cancellationToken = default);
    
    Task<string> DesignCaseAsync(string planJson, string expandedJson, string caseId, string? difficulty = null, CancellationToken cancellationToken = default);
    Task<string> GenerateDocumentFromSpecAsync(DocumentSpec spec, string designJson, string caseId, string? planJson = null, string? expandJson = null, string? difficultyOverride = null, CancellationToken ct = default);
    Task<string> GenerateMediaFromSpecAsync(MediaSpec spec, string designJson, string caseId, string? planJson = null, string? expandJson = null, string? difficultyOverride = null, CancellationToken ct = default);
    Task<string> RenderDocumentFromJsonAsync(string docId, string documentJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> RenderMediaFromJsonAsync(MediaSpec spec, string caseId, CancellationToken cancellationToken = default);
    Task<NormalizationResult> NormalizeCaseDeterministicAsync(NormalizationInput input, CancellationToken cancellationToken = default);
    Task<string> ValidateRulesAsync(string normalizedJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> RedTeamCaseAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> RedTeamGlobalAnalysisAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> RedTeamFocusedAnalysisAsync(string validatedJson, string caseId, string globalAnalysis, string[] focusAreas, CancellationToken cancellationToken = default);
    Task<string> FixCaseAsync(string redTeamAnalysis, string currentJson, string caseId, int iterationNumber = 1, CancellationToken cancellationToken = default);
    Task<bool> IsCaseCleanAsync(string redTeamAnalysis, string caseId, CancellationToken cancellationToken = default);
    Task SaveRedTeamAnalysisAsync(string caseId, string redTeamAnalysis, CancellationToken cancellationToken = default);
    Task<CaseGenerationOutput> PackageCaseAsync(string finalJson, string caseId, CancellationToken cancellationToken = default);
    
    // Test method for PDF generation
    Task<byte[]> GenerateTestPdfAsync(string title, string markdownContent, string documentType = "general", CancellationToken cancellationToken = default);
    
    // Test method for image generation  
    Task<string> GenerateTestImageAsync(MediaSpec spec, string caseId, CancellationToken cancellationToken = default);
}

public interface IStorageService
{
    Task<string> SaveFileAsync(string containerName, string fileName, string content, CancellationToken cancellationToken = default);
    Task<string> SaveFileAsync(string containerName, string fileName, byte[] content, CancellationToken cancellationToken = default);
    Task SaveFileBytesAsync(string containerName, string fileName, byte[] content, CancellationToken cancellationToken = default);
    Task<string> GetFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task<byte[]?> GetFileBytesAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix = "", CancellationToken cancellationToken = default);
}

public interface ILLMService
{
    Task<string> GenerateAsync(string caseId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> GenerateStructuredAsync(string caseId, string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageAsync(string caseId, string prompt, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageWithReferenceAsync(string caseId, string prompt, byte[] referenceImage, byte[]? maskImage = null, CancellationToken cancellationToken = default);
}

public interface ILLMProvider
{
    Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null, CancellationToken cancellationToken = default);
}

public interface INormalizerService
{
    Task<NormalizationResult> NormalizeCaseAsync(NormalizationInput input, CancellationToken cancellationToken = default);
}

public interface IPdfRenderingService
{
    Task<string> RenderDocumentFromJsonAsync(string docId, string documentJson, string caseId, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateTestPdfAsync(string title, string markdownContent, string documentType = "general", CancellationToken cancellationToken = default);
}

public interface IRedTeamCacheService
{
    Task<string?> GetCachedAnalysisAsync(string contentHash, string analysisType, string[]? focusAreas = null, CancellationToken cancellationToken = default);
    Task CacheAnalysisAsync(string contentHash, string analysis, string analysisType, string[]? focusAreas = null, CancellationToken cancellationToken = default);
    string ComputeContentHash(string content);
    Task ClearExpiredEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}

public interface IImagesService
{
    Task<string> GenerateAsync(string caseId, MediaSpec spec, CancellationToken cancellationToken = default);
}