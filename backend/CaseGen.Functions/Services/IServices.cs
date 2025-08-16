using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public interface ICaseGenerationService
{
    Task<string> PlanCaseAsync(CaseGenerationRequest request, CancellationToken cancellationToken = default);
    Task<string> ExpandCaseAsync(string planJson, CancellationToken cancellationToken = default);
    Task<string> DesignCaseAsync(string expandedJson, CancellationToken cancellationToken = default);
    Task<string[]> GenerateDocumentsAsync(string designJson, CancellationToken cancellationToken = default);
    Task<string[]> GenerateMediaAsync(string designJson, CancellationToken cancellationToken = default);
    Task<string> NormalizeCaseAsync(string[] documents, string[] media, CancellationToken cancellationToken = default);
    Task<string> IndexCaseAsync(string normalizedJson, CancellationToken cancellationToken = default);
    Task<string> ValidateRulesAsync(string indexedJson, CancellationToken cancellationToken = default);
    Task<string> RedTeamCaseAsync(string validatedJson, CancellationToken cancellationToken = default);
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
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> GenerateStructuredAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default);
}