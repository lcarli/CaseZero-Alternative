using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Compatibility adapter for source-owner repairs, not an unscoped JSON author.</summary>
public class RefineCaseTask(ILLMProvider llm, ILogger logger)
{
    public class Result
    {
        public bool Succeeded { get; set; }
        public CaseDraft? Candidate { get; set; }
        public string? ErrorCode { get; set; }
        public string? Error { get; set; }
        public IReadOnlyList<string> ChangedComponents { get; set; } = Array.Empty<string>();
    }

    public Task<Result> RunAsync(CaseDraft draft, IReadOnlyList<string> validationErrors,
        RedTeamTask.Report report, string schemaJson, CancellationToken ct,
        string? difficulty = null, CaseBible? caseBible = null) =>
        RunAsync(draft, validationErrors, report.Findings, schemaJson, ct, difficulty, caseBible);

    public async Task<Result> RunAsync(CaseDraft draft, IReadOnlyList<string> validationErrors,
        IReadOnlyList<RedTeamTask.Finding> findings, string schemaJson, CancellationToken ct,
        string? difficulty = null, CaseBible? caseBible = null)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            TaskContractException.Require(caseBible is null || ContentHasher.Hash(caseBible) == ContentHasher.Hash(draft.CaseBible),
                "A repair cannot replace the draft's immutable CaseBible.");
            TaskContractException.Require(difficulty is null || difficulty == draft.Metadata.Difficulty,
                "A repair cannot override the selected difficulty.");
            var candidate = draft.Copy();
            var coordinator = new RepairCoordinator(llm,
                new MechanicalRulesBuilder(NullLogger<MechanicalRulesBuilder>.Instance), logger);
            await coordinator.RepairAsync(candidate, findings,
                validationErrors.Select(message => new RepairIssue("consistency", message)).ToArray(), false, ct);
            ct.ThrowIfCancellationRequested();
            CaseDraftRepair.Validate(candidate, draft, schemaJson);
            var before = draft.GetComponents();
            var after = candidate.GetComponents();
            var changed = before.Where(component => !JsonNode.DeepEquals(component.Value, after[component.Key]))
                .Select(component => component.Key).Order(StringComparer.Ordinal).ToArray();
            TaskContractException.Require(changed.Length > 0, "Source-owner repair produced no component changes.");
            candidate.Revision = draft.Revision + 1;
            return new Result { Succeeded = true, Candidate = candidate, ChangedComponents = changed };
        }
        catch (Exception ex) when (ex is TaskContractException or System.Text.Json.JsonException
            or Azure.RequestFailedException or System.ClientModel.ClientResultException
            or HttpRequestException or TimeoutException)
        {
            logger.LogWarning(ex, "Source-owner refinement failed");
            return new Result { ErrorCode = "repair_invalid", Error = ex.Message };
        }
    }
}
