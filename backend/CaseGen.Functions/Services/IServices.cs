using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public interface ICaseGenerationService
{
    Task<string> PlanCaseAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken = default);
    Task<string> ExpandCaseAsync(string planJson, string caseId, CancellationToken cancellationToken = default);
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
    Task<string> GetFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix = "", CancellationToken cancellationToken = default);
}

public interface ILLMService
{
    Task<string> GenerateAsync(string caseId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> GenerateStructuredAsync(string caseId, string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageAsync(string caseId, string prompt, CancellationToken cancellationToken = default);
}

public interface ILLMProvider
{
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default);
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

public interface IImagesService
{
    Task<string> GenerateAsync(string caseId, MediaSpec spec, CancellationToken cancellationToken = default);
}