using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public sealed record RepairIssue(string Stage, string Message)
{
    public string? NodeId { get; init; }
    public string? ConstraintId { get; init; }
}

public sealed record RepairPlan(
    bool CaseBible,
    bool Blueprint,
    bool Suspects,
    bool AssetPlan,
    bool Assets,
    bool Timeline,
    bool Forensics,
    bool Emails,
    bool Rules,
    bool Solution);

public sealed class RepairCoordinator
{
    private readonly ILLMProvider _llm;
    private readonly MechanicalRulesBuilder _mechanicalRules;
    private readonly ILogger _logger;
    private readonly RepairBudgetTracker _repairBudgets = new();

    public GranularRepairReport LastGranularReport { get; private set; } = new();

    public RepairCoordinator(ILLMProvider llm, MechanicalRulesBuilder mechanicalRules, ILogger logger)
    {
        _llm = llm;
        _mechanicalRules = mechanicalRules;
        _logger = logger;
    }

    public static RepairPlan BuildPlan(
        CaseDraft draft,
        IReadOnlyList<RedTeamTask.Finding> findings,
        IReadOnlyList<RepairIssue> validationIssues,
        bool solverFailed)
    {
        var ownerStages = findings
            .Where(finding => !string.Equals(finding.Severity, "low", StringComparison.OrdinalIgnoreCase))
            .Select(finding => finding.OwnerStage?.Trim())
            .Where(stage => !string.IsNullOrWhiteSpace(stage))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validationStages = validationIssues
            .Select(issue => issue.Stage)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasUnassignedFinding = findings.Any(finding =>
            !string.Equals(finding.Severity, "low", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(finding.OwnerStage));

        var caseBible = validationStages.Contains("caseBible") || ownerStages.Contains("caseBible");
        var blueprint = caseBible
                        || ownerStages.Contains("plotOutline")
                        || validationStages.Contains("blueprint")
                        || validationStages.Contains("locale");
        var suspects = blueprint || ownerStages.Contains("suspectCards");
        var assetPlan = blueprint || ownerStages.Contains("assetPlan") || validationStages.Contains("evidencePlan");
        var assets = assetPlan
            || ownerStages.Contains("assetCards")
            || validationStages.Contains("evidenceContent")
            || validationStages.Contains("consistency")
            || validationStages.Contains("schema")
            || validationStages.Contains("difficulty")
            || hasUnassignedFinding
            || solverFailed;
        var timeline = blueprint || assets || ownerStages.Contains("timeline");
        var forensics = !DifficultyProfileCatalog.Get(draft).AllEvidenceInitial
            && (blueprint
                || assets
                || ownerStages.Contains("forensics")
                || validationStages.Contains("forensics")
                || validationStages.Contains("difficulty")
                || solverFailed);
        var emails = blueprint
            || suspects
            || assets
            || timeline
            || forensics
            || ownerStages.Contains("emails");
        var rules = assetPlan
            || assets
            || timeline
            || forensics
            || emails
            || ownerStages.Contains("rules");
        var solution = blueprint
            || suspects
            || assetPlan
            || assets
            || timeline
            || forensics
            || emails
            || rules
            || ownerStages.Contains("solution")
            || solverFailed;

        return new(caseBible, blueprint, suspects, assetPlan, assets, timeline, forensics, emails, rules, solution);
    }

    public static IReadOnlyList<RepairOperation> BuildOperations(
        CaseDraft draft,
        IReadOnlyList<RedTeamTask.Finding> findings,
        IReadOnlyList<RepairIssue> validationIssues) =>
        GranularRepairPlanner.Build(draft, findings, validationIssues);

    public async Task<IReadOnlyList<string>> RepairAsync(
        CaseDraft draft,
        IReadOnlyList<RedTeamTask.Finding> findings,
        IReadOnlyList<RepairIssue> validationIssues,
        bool solverFailed,
        CancellationToken ct)
    {
        var canonicalReport = CaseBibleValidator.Validate(draft.CaseBible, draft.Request.Difficulty,
            draft.Request.Language, required: true);
        TaskContractException.Require(canonicalReport.IsValid,
            "immutable_root_invalid: an authored CaseBible cannot be repaired or regenerated. " +
            string.Join(" | ", canonicalReport.Errors));
        var plan = BuildPlan(draft, findings, validationIssues, solverFailed);
        TaskContractException.Require(!plan.CaseBible,
            "immutable_root_review: resolve the reported canonical-root defect in a new generation, not by replacing frozen truth. " +
            string.Join(" | ", validationIssues.Where(i => i.Stage == "caseBible").Select(i => i.Message)));
        TaskContractException.Require(!findings.Any(f => string.Equals(f.OwnerStage, "assetPlan", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(f.Severity, "low", StringComparison.OrdinalIgnoreCase))
            && !validationIssues.Any(i => i.Stage == "evidencePlan"),
            "immutable_source_assignment_invalid: existing source/portfolio assignments cannot be arbitrarily replanned after authorship.");
        var baseline = draft.Copy();
        var index = NodeDependencyIndex.Build(draft);
        var operations = BuildOperations(draft, findings, validationIssues);
        var acceptedOperations = new List<RepairOperation>();
        LastGranularReport = new GranularRepairReport();
        LastGranularReport.Operations.AddRange(operations);
        var invalidated = operations.SelectMany(operation => operation.InvalidatedNodeIds)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var record in index.Records.Where(record => !invalidated.Contains(record.NodeId)))
        {
            _repairBudgets.RecordValid(record.NodeId, record.ContentHash);
            LastGranularReport.FrozenNodeIds.Add(record.NodeId);
        }
        foreach (var operation in operations)
        {
            var currentHash = index.Find(operation.TargetNodeId)?.ContentHash;
            if (_repairBudgets.TryBegin(
                    operation,
                    DifficultyProfileCatalog.Get(draft).MaxRepairIterations,
                    currentHash,
                    out var plateau))
            {
                acceptedOperations.Add(operation);
            }
            else if (plateau is not null)
            {
                LastGranularReport.Plateaus.Add(plateau);
            }
        }
        if (operations.Count > 0 && acceptedOperations.Count == 0)
        {
            return LastGranularReport.Plateaus
                .Select(plateau => $"plateau:{plateau.NodeId}:{plateau.ConstraintId}")
                .ToArray();
        }

        var guidance = BuildGuidance(findings, validationIssues, solverFailed);
        var repaired = new List<string>();
        var granularAssetIds = acceptedOperations
            .Where(operation => operation.Operation == RepairOperationKind.RewriteAsset)
            .Select(operation => operation.TargetNodeId)
            .Where(id => draft.AssetStubs.Any(stub => stub.Id == id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var useGranularAssetRepair = granularAssetIds.Length > 0
                                     && !plan.Blueprint
                                     && !plan.AssetPlan;

        if (plan.Blueprint)
        {
            await new PlotOutlineTask(_llm, _logger).RunAsync(draft, ct);
            repaired.Add("plotOutline");
        }

        if (plan.Suspects)
        {
            var task = new SuspectCardTask(_llm, _logger);
            draft.SuspectFull = (await Task.WhenAll(
                draft.SuspectStubs.Select(stub => task.RunAsync(draft, stub, ct, guidance)))).ToList();
            repaired.Add("suspectCards");
        }

        if (useGranularAssetRepair)
        {
            var task = new AssetCardTask(_llm, _logger);
            foreach (var assetId in granularAssetIds)
            {
                var stub = draft.AssetStubs.Single(item => item.Id == assetId);
                var replacement = await task.RunAsync(draft, stub, ct, guidance);
                var indexOfExisting = draft.AssetFull.FindIndex(asset => asset.Id == assetId);
                if (indexOfExisting >= 0)
                    draft.AssetFull[indexOfExisting] = replacement;
                else
                    draft.AssetFull.Add(replacement);
                repaired.Add($"assetCards:{assetId}");
            }
        }
        else if (plan.Assets)
        {
            var task = new AssetCardTask(_llm, _logger);
            draft.AssetFull = (await Task.WhenAll(
                draft.AssetStubs.Select(stub => task.RunAsync(draft, stub, ct, guidance)))).ToList();
            repaired.Add("assetCards");
        }

        if (plan.Timeline)
        {
            await new TimelineTask(_llm, _logger).RunAsync(draft, ct);
            await new BriefingEmailTask(_llm, _logger).RunAsync(draft, ct);
            repaired.Add("timeline");
            repaired.Add("briefing");
        }

        if (plan.Forensics)
        {
            draft.ForensicFull.Clear();
            draft.ResultAssets.Clear();
            draft.ResultEmails.Clear();
            var outcomeTask = new ForensicOutcomeTask(_llm, _logger);
            var details = await Task.WhenAll(
                draft.ForensicStubs.Select(stub => outcomeTask.RunAsync(draft, stub, ct)));
            foreach (var (full, asset, email) in details)
            {
                draft.ForensicFull.Add(full);
                if (asset is not null) draft.ResultAssets.Add(asset);
                if (email is not null) draft.ResultEmails.Add(email);
            }
            repaired.Add("forensics");
        }

        if (plan.Emails)
        {
            draft.FollowUpEmails.Clear();
            await new InitialEmailsTask(_llm, _logger).RunAsync(draft, ct);
            repaired.Add("initialEmails");
        }

        if (plan.Rules)
        {
            draft.Rules.Clear();
            _mechanicalRules.Build(draft);
            if (!DifficultyProfileCatalog.Get(draft).AllEvidenceInitial)
                await new RulesTask(_llm, _logger).RunAsync(draft, ct);
            repaired.Add("rules");
        }

        if (plan.Solution)
        {
            draft.Explanation = string.Empty;
            var questionTask = new QuestionTask(_llm, _logger);
            draft.Questions = (await Task.WhenAll(
                draft.QuestionTopics.Select(topic => questionTask.RunAsync(draft, topic, ct)))).ToList();
            await new ExplanationTask(_llm, _logger).RunAsync(draft, ct);
            repaired.Add("solution");
        }

        CaseGraphProjection.RefreshGeneratedGraph(draft);
        CaseDraftRepair.ValidateProtected(baseline, draft);
        _logger.LogInformation("Targeted repair reran stages: {Stages}", string.Join(", ", repaired));
        return repaired;
    }

    private static string BuildGuidance(
        IEnumerable<RedTeamTask.Finding> findings,
        IEnumerable<RepairIssue> validationIssues,
        bool solverFailed)
    {
        var lines = validationIssues
            .Select(issue => $"- [{issue.Stage}] {issue.Message}")
            .Concat(findings
                .Where(finding => !string.Equals(finding.Severity, "low", StringComparison.OrdinalIgnoreCase))
                .Select(finding =>
                    $"- [{finding.OwnerStage ?? "unassigned"}] {finding.Issue} Expected: {finding.Expected ?? finding.Suggestion ?? "remove the defect"}"))
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .ToList();
        if (solverFailed)
            lines.Add("- [solution] A blind player could not identify the culprit reliably. Strengthen independent player-visible corroboration without leaking the answer.");
        return string.Join("\n", lines);
    }
}
