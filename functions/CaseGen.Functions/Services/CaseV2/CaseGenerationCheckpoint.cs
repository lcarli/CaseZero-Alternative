using System.Text.Json;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public enum CaseGenerationStage
{
    Planning = 1,
    Authorship = 2,
    Forensics = 3,
    Solution = 4,
    Evaluation = 5,
    Rendering = 6,
    Publication = 7
}

public sealed record AssetPreparationState(string Id, bool PreparedForReview, bool CanonicalContent);

public sealed class CaseGenerationCheckpoint
{
    public string Version { get; set; } = CaseGenerationRuntime.Version;
    public string? JobId { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public CaseGenerationStage? CompletedStage { get; set; }
    public CaseDraft Draft { get; set; } = new();
    public CaseHarnessReport? Harness { get; set; }
    public GenerateCaseV2Response? Response { get; set; }
    public Dictionary<string, double> StageLatencyMs { get; set; } = new(StringComparer.Ordinal);
    public List<AssetPreparationState> AssetPreparation { get; set; } = [];
    public Dictionary<string, string> ReadableTexts { get; set; } = new(StringComparer.Ordinal);
    public CaseStagedBundle? StagedBundle { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? StagedCaseDirectory { get; set; }

    public static CaseGenerationCheckpoint Create(GenerateCaseV2Request request)
    {
        GenerationRequestValidator.Validate(request);
        return new()
        {
            RequestHash = Fingerprint(request),
            Draft = new() { CaseId = GenerationRequestValidator.NormalizeCaseId(request.CaseId), Request = request }
        };
    }

    public static string Fingerprint(GenerateCaseV2Request request) =>
        CaseGenerationSkillRegistry.Hash(JsonSerializer.Serialize(request, CaseDraft.CanonicalJsonOptions));

    public static void ValidateInput(CaseStageInput input)
    {
        GenerationRequestValidator.Validate(input.Request);
        ValidateJobId(input.JobId);
        if (!Enum.IsDefined(input.Stage))
            throw new TaskContractException("Invalid generation stage.");
        if (input.Stage == CaseGenerationStage.Planning)
        {
            if (input.Previous is not null)
                throw new TaskContractException("Planning cannot consume a previous checkpoint.");
        }
        else if (input.Previous is not { } previous || previous.JobId != input.JobId
            || (int)previous.Stage + 1 != (int)input.Stage || previous.RequestHash != Fingerprint(input.Request))
            throw new TaskContractException("Stage must consume the same job/request's immediate predecessor.");
    }

    public static void ValidateJobId(string jobId)
    {
        if (!Regex.IsMatch(jobId, @"\Acasev2-[a-zA-Z0-9_-]{1,128}\z"))
            throw new TaskContractException("Invalid private generation job ID.");
    }

    public void Bind(CaseStageInput input, bool completed = false)
    {
        ValidateInput(input);
        if (Version != CaseGenerationRuntime.Version || (JobId is not null && JobId != input.JobId)
            || RequestHash != Fingerprint(input.Request) || RequestHash != Fingerprint(Draft.Request)
            || (completed ? CompletedStage != input.Stage : (int)(CompletedStage ?? 0) + 1 != (int)input.Stage))
            throw new TaskContractException("Checkpoint does not belong to this job, request, stage or pipeline version.");
        JobId = input.JobId;
    }

    public void CapturePreparation() => AssetPreparation = Draft.AssetFull.Concat(Draft.ResultAssets)
        .Select(asset => new AssetPreparationState(asset.Id, asset.PreparedForReview, asset.CanonicalContent)).ToList();

    public void RestorePreparation()
    {
        var flags = AssetPreparation.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        foreach (var asset in Draft.AssetFull.Concat(Draft.ResultAssets))
        {
            if (!flags.TryGetValue(asset.Id, out var state))
                throw new TaskContractException($"Checkpoint has no preparation state for {asset.Id}.");
            asset.PreparedForReview = state.PreparedForReview;
            asset.CanonicalContent = state.CanonicalContent;
        }
    }

    public void CleanupStaging()
    {
        if (StagedCaseDirectory is null) return;
        var workspace = Directory.GetParent(Path.GetFullPath(StagedCaseDirectory));
        if (workspace is null || !Guid.TryParseExact(workspace.Name, "N", out _)
            || workspace.Parent?.Name is not (".casegen-staging" or "casegen-checkpoint"))
            throw new TaskContractException("Cannot clean an unexpected checkpoint staging path.");
        if (workspace.Exists) workspace.Delete(recursive: true);
        StagedCaseDirectory = null;
    }
}

public sealed record CaseCheckpointReference(string JobId, CaseGenerationStage Stage, string Path, string Sha256,
    string DefinitionHash, string RequestHash);
public sealed record CaseStagedBundle(CaseBundleManifest Manifest, string ManifestSha256, string? PreviousETag);
public sealed record CaseStageInput(string JobId, GenerateCaseV2Request Request, CaseGenerationStage Stage,
    CaseCheckpointReference? Previous = null);
public sealed record CaseStageResult(CaseCheckpointReference Reference, bool Approved, Functions.CaseV2.CaseV2JobResult? Result = null);

public interface ICaseGenerationCheckpointStore
{
    Task<CaseCheckpointReference?> FindAsync(string jobId, CaseGenerationStage stage, CancellationToken ct);
    Task<CaseGenerationCheckpoint> ReadAsync(CaseCheckpointReference reference, CancellationToken ct, bool restoreAssets = true);
    Task<CaseCheckpointReference> SaveAsync(string jobId, CaseGenerationCheckpoint checkpoint, CancellationToken ct);
    Task<bool> TryBeginStageAsync(CaseStageInput input, CaseGenerationCheckpoint checkpoint, CancellationToken ct);
    Task RecordFailureAsync(CaseStageInput input, CaseGenerationCheckpoint checkpoint, string failure, CancellationToken ct);
}
