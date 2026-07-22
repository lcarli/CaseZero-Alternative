using CaseGen.Functions.Models.CaseV2;
using System.Text.Json.Nodes;

namespace CaseGen.Functions.Services.CaseV2;

public sealed record BatchGenerationRequest(
    string Difficulty,
    int Seed,
    string Theme,
    string Language = "en-US");

public sealed class BatchGenerationSample
{
    public BatchGenerationRequest Request { get; init; } = new("Detective", 0, "default");
    public FinalValidationReport Validation { get; init; } = new();
    public bool FirstPassSuccess { get; init; }
    public int RepairOperations { get; init; }
    public bool RepairPlateaued { get; init; }
    public bool SolverSucceeded { get; init; }
    public IReadOnlyList<SpecialistFinding> SpecialistFindings { get; init; } = Array.Empty<SpecialistFinding>();
    public double LatencyMs { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public IReadOnlyList<string> EvidenceLayouts { get; init; } = Array.Empty<string>();
}

public sealed class BatchGenerationReport
{
    public int TotalCases { get; init; }
    public double GraphValidationPassRate { get; init; }
    public double FirstPassSuccessRate { get; init; }
    public double AverageRepairOperations { get; init; }
    public double RepairPlateauRate { get; init; }
    public double SolverSuccessRate { get; init; }
    public IReadOnlyDictionary<string, int> SpecialistFindingsByCategory { get; init; } =
        new Dictionary<string, int>();
    public double AverageLatencyMs { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public int EvidenceLayoutDiversity { get; init; }
    public IReadOnlyList<BatchGenerationSample> Samples { get; init; } = Array.Empty<BatchGenerationSample>();
}

public sealed class CaseV2BatchHarness
{
    public Task<BatchGenerationReport> RunAsync(
        IEnumerable<BatchGenerationRequest> requests,
        ICaseV2GeneratorService generator,
        CancellationToken ct = default) =>
        RunAsync(
            requests,
            async (request, token) =>
            {
                var response = await generator.GenerateAsync(new GenerateCaseV2Request
                {
                    CaseId = $"case_batch_{request.Difficulty.ToLowerInvariant()}_{request.Seed}",
                    Difficulty = request.Difficulty,
                    RequiredRank = request.Difficulty,
                    Theme = request.Theme,
                    Seed = request.Seed,
                    Language = request.Language,
                    WriteToDisk = false
                }, token);
                return FromResponse(request, response);
            },
            ct);

    public static BatchGenerationSample FromResponse(
        BatchGenerationRequest request,
        GenerateCaseV2Response response)
    {
        var layouts = new List<string>();
        try
        {
            var root = JsonNode.Parse(response.CaseJson);
            layouts.AddRange((root?["assets"] as JsonArray ?? [])
                .Select(asset => asset?["metadata"]?["layout"]?.GetValue<string>()
                                 ?? asset?["type"]?.GetValue<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!));
        }
        catch
        {
            // Schema/final validation records the malformed JSON; the batch still reports the sample.
        }
        var specialist = response.RedTeam?.Findings.Select(finding => new SpecialistFinding(
                Enum.TryParse<SpecialistReviewKind>(finding.Area, out var reviewer)
                    ? reviewer
                    : SpecialistReviewKind.PlayerExperience,
                finding.ConstraintId ?? finding.Area,
                finding.NodeId ?? finding.FactRefs.FirstOrDefault() ?? "review.root",
                Enum.TryParse<ReviewSeverity>(finding.Severity, true, out var severity)
                    ? severity
                    : ReviewSeverity.Medium,
                finding.Issue,
                finding.Suggestion,
                false))
            .ToArray() ?? Array.Empty<SpecialistFinding>();
        return new BatchGenerationSample
        {
            Request = request,
            Validation = response.FinalValidation ?? new FinalValidationReport
            {
                CaseId = response.CaseId,
                Difficulty = request.Difficulty,
                Issues = response.ValidationErrors.Select(error => new FinalValidationIssue(
                    FinalValidationGate.Graph,
                    "generation.validation",
                    "generation",
                    error,
                    true)).ToList()
            },
            FirstPassSuccess = !response.RefineAttempted && response.ValidationErrors.Count == 0,
            RepairOperations = response.RepairOperationCount,
            RepairPlateaued = response.RepairPlateauCount > 0,
            SolverSucceeded = response.Solver?.Correct == true,
            SpecialistFindings = specialist,
            LatencyMs = response.StageLatencyMs.Values.Sum(),
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            EvidenceLayouts = response.EvidenceLayouts.Count > 0 ? response.EvidenceLayouts : layouts
        };
    }

    public async Task<BatchGenerationReport> RunAsync(
        IEnumerable<BatchGenerationRequest> requests,
        Func<BatchGenerationRequest, CancellationToken, Task<BatchGenerationSample>> generate,
        CancellationToken ct = default)
    {
        var ordered = requests
            .OrderBy(request => request.Difficulty, StringComparer.Ordinal)
            .ThenBy(request => request.Theme, StringComparer.Ordinal)
            .ThenBy(request => request.Seed)
            .ToArray();
        var samples = new List<BatchGenerationSample>();
        foreach (var request in ordered)
            samples.Add(await generate(request, ct));
        return Summarize(samples);
    }

    public static BatchGenerationReport Summarize(IReadOnlyList<BatchGenerationSample> samples)
    {
        if (samples.Count == 0)
            return new BatchGenerationReport();
        return new BatchGenerationReport
        {
            TotalCases = samples.Count,
            GraphValidationPassRate = Rate(samples.Count(sample =>
                sample.Validation.Issues.All(issue =>
                    issue.Gate != FinalValidationGate.Graph || !issue.Blocking)), samples.Count),
            FirstPassSuccessRate = Rate(samples.Count(sample => sample.FirstPassSuccess), samples.Count),
            AverageRepairOperations = samples.Average(sample => sample.RepairOperations),
            RepairPlateauRate = Rate(samples.Count(sample => sample.RepairPlateaued), samples.Count),
            SolverSuccessRate = Rate(samples.Count(sample => sample.SolverSucceeded), samples.Count),
            SpecialistFindingsByCategory = samples
                .SelectMany(sample => sample.SpecialistFindings)
                .GroupBy(finding => finding.Reviewer.ToString())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            AverageLatencyMs = samples.Average(sample => sample.LatencyMs),
            TotalInputTokens = samples.Sum(sample => sample.InputTokens),
            TotalOutputTokens = samples.Sum(sample => sample.OutputTokens),
            EvidenceLayoutDiversity = samples.SelectMany(sample => sample.EvidenceLayouts)
                .Distinct(StringComparer.Ordinal).Count(),
            Samples = samples.ToArray()
        };
    }

    private static double Rate(int numerator, int denominator) =>
        denominator == 0 ? 0 : Math.Round((double)numerator / denominator, 4);
}

public enum DifficultyApprovalStatus
{
    Blocked,
    PendingValidation,
    Approved
}

public sealed class DifficultyApprovalReport
{
    public string Difficulty { get; init; } = string.Empty;
    public DifficultyApprovalStatus Status { get; init; }
    public List<string> BlockingReasons { get; init; } = new();
    public BatchGenerationReport? Batch { get; init; }
    public bool HasPersistedGolden { get; init; }
    public bool HasRealGeneratedGolden { get; init; }
}

public static class DifficultyApprovalGate
{
    public static DifficultyApprovalReport Evaluate(
        string difficulty,
        BatchGenerationReport? batch,
        bool hasPersistedGolden,
        bool hasRealGeneratedGolden)
    {
        var reasons = new List<string>();
        if (!hasPersistedGolden)
            reasons.Add("a persisted golden artifact is required");
        if (!hasRealGeneratedGolden)
            reasons.Add("a real generated case must pass final validation");
        if (batch is null || batch.TotalCases == 0)
        {
            reasons.Add("a representative multi-seed batch is required");
        }
        else
        {
            if (batch.GraphValidationPassRate < 1)
                reasons.Add("graph validation pass rate must be 100%");
            if (batch.SolverSuccessRate < 1)
                reasons.Add("sequential solver success rate must be 100%");
            if (batch.RepairPlateauRate > 0)
                reasons.Add("repair plateau rate must be zero");
            if (batch.Samples.Any(sample => sample.Validation.Issues.Any(issue =>
                    issue.Blocking
                    && issue.Gate is FinalValidationGate.Schema
                        or FinalValidationGate.Proof
                        or FinalValidationGate.Reachability
                        or FinalValidationGate.Forensic
                        or FinalValidationGate.Evidence
                        or FinalValidationGate.Specialist)))
            {
                reasons.Add("batch contains blocking schema, proof, reachability, forensic, evidence, or specialist issues");
            }
        }

        var higherRank = difficulty is "Sergeant" or "Lieutenant" or "Captain" or "Commander";
        if (higherRank)
            reasons.Add("higher ranks remain blocked until the previous topology is approved");
        var status = reasons.Count == 0
            ? DifficultyApprovalStatus.Approved
            : batch is null || !hasRealGeneratedGolden
                ? DifficultyApprovalStatus.Blocked
                : DifficultyApprovalStatus.PendingValidation;
        return new DifficultyApprovalReport
        {
            Difficulty = difficulty,
            Status = status,
            BlockingReasons = reasons,
            Batch = batch,
            HasPersistedGolden = hasPersistedGolden,
            HasRealGeneratedGolden = hasRealGeneratedGolden
        };
    }
}
