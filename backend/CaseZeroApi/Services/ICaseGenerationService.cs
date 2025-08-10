using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CaseZeroApi.Services
{
    /// <summary>
    /// Service for generating detective cases using Azure Foundry AI
    /// </summary>
    public interface ICaseGenerationService
    {
        Task<CasePackage> GenerateCaseAsync(CaseSeed seed, GenerationOptions? options = null, CancellationToken ct = default);
        Task<string> GenerateCaseJsonAsync(CaseSeed seed, CancellationToken ct = default);
        Task<List<GeneratedDoc>> GenerateInterrogatoriosAsync(CaseContext ctx, CancellationToken ct = default);
        Task<List<GeneratedDoc>> GenerateRelatoriosAsync(CaseContext ctx, CancellationToken ct = default);
        Task<List<GeneratedDoc>> GenerateLaudosAsync(CaseContext ctx, CancellationToken ct = default);
        Task<GeneratedDoc> GenerateEvidenceManifestAsync(CaseContext ctx, CancellationToken ct = default);
        Task<List<ImagePrompt>> GenerateImagePromptsAsync(CaseContext ctx, CancellationToken ct = default);
    }
}