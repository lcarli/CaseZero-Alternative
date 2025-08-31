using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public interface ICaseGenerationService
{
    Task<string> PlanCaseAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken = default);
    Task<string> ExpandCaseAsync(string planJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> DesignCaseAsync(string expandedJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> DesignCaseAsync(string planJson, string expandedJson, string caseId, string? difficulty = null, CancellationToken cancellationToken = default);
    Task<string[]> GenerateDocumentsAsync(string designJson, string caseId, CancellationToken cancellationToken = default);
    Task<string[]> GenerateMediaAsync(string designJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> GenerateDocumentFromSpecAsync(DocumentSpec spec, string designJson, string caseId, CancellationToken ct = default);
    Task<string> GenerateMediaFromSpecAsync(MediaSpec spec, string designJson, string caseId, CancellationToken ct = default);
    Task<string> NormalizeCaseAsync(string[] documents, string[] media, string caseId, CancellationToken cancellationToken = default);
    Task<string> IndexCaseAsync(string normalizedJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> ValidateRulesAsync(string indexedJson, string caseId, CancellationToken cancellationToken = default);
    Task<string> RedTeamCaseAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default);
    Task<CaseGenerationOutput> PackageCaseAsync(string finalJson, string caseId, CancellationToken cancellationToken = default);
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
}

public interface ILLMProvider
{
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default);
}