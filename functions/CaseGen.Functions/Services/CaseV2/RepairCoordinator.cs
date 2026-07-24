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

        var caseBible = validationStages.Contains("caseBible");
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

        var plan = BuildPlan(draft, findings, validationIssues, solverFailed);
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

        if (plan.CaseBible)
        {
            await new CaseBibleTask(_llm, _logger).RunAsync(draft, ct, guidance);
            repaired.Add("caseBible");
        }

        if (plan.Blueprint)
        {
            await new PlotOutlineTask(_llm, _logger).RunAsync(draft, ct, guidance);
            draft.SuspectFull.Clear();
            draft.AssetStubs.Clear();
            draft.AssetFull.Clear();
            draft.Timeline.Clear();
            draft.TemporalEvents.Clear();
            ClearForensics(draft);
            if (draft.CaseGraph.Origin != CaseGraphOrigin.HandAuthored)
                draft.CaseGraph = new CaseGraph { Origin = CaseGraphOrigin.TransitionalClueLadder };
            repaired.Add("plotOutline");
        }

        if (plan.Suspects)
        {
            var task = new SuspectCardTask(_llm, _logger);
            draft.SuspectFull = (await Task.WhenAll(
                draft.SuspectStubs.Select(stub => task.RunAsync(draft, stub, ct, guidance)))).ToList();
            repaired.Add("suspectCards");
        }

        if (plan.AssetPlan)
        {
            await new AssetPlanTask(_llm, _logger).RunAsync(draft, ct, guidance);
            EvidenceGraphCompiler.Compile(draft);
            draft.AssetFull.Clear();
            repaired.Add("assetPlan");
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
            ApplyDocumentLanguage(draft);
        }
        else if (plan.Assets)
        {
            var task = new AssetCardTask(_llm, _logger);
            draft.AssetFull = (await Task.WhenAll(
                draft.AssetStubs.Select(stub => task.RunAsync(draft, stub, ct, guidance)))).ToList();
            ApplyDocumentLanguage(draft);
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
            ClearForensics(draft);
            await new ForensicsPlanTask(_llm, _logger).RunAsync(draft, ct, guidance);
            var outcomeTask = new ForensicOutcomeTask(_llm, _logger);
            var details = await Task.WhenAll(
                draft.ForensicStubs.Select(stub => outcomeTask.RunAsync(draft, stub, ct)));
            foreach (var (full, asset, email) in details)
            {
                draft.ForensicFull.Add(full);
                if (asset is not null) draft.ResultAssets.Add(asset);
                if (email is not null) draft.ResultEmails.Add(email);
            }
            ApplyDocumentLanguage(draft);
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
            draft.RequiredEvidenceIds.Clear();
            draft.RequiredAnalysisIds.Clear();
            draft.QuestionTopics.Clear();
            draft.Questions.Clear();
            draft.Explanation = string.Empty;
            await new SolutionSkeletonTask(_llm, _logger).RunAsync(draft, ct);
            var questionTask = new QuestionTask(_llm, _logger);
            draft.Questions = (await Task.WhenAll(
                draft.QuestionTopics.Select(topic => questionTask.RunAsync(draft, topic, ct)))).ToList();
            await new ExplanationTask(_llm, _logger).RunAsync(draft, ct);
            repaired.Add("solution");
        }

        CaseGraphProjection.RefreshGeneratedGraph(draft);
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

    private static void ClearForensics(CaseDraft draft)
    {
        draft.AnalysisTypes.Clear();
        draft.ForensicStubs.Clear();
        draft.ForensicFull.Clear();
        draft.ResultAssets.Clear();
        draft.ResultEmails.Clear();
    }

    private static void ApplyDocumentLanguage(CaseDraft draft)
    {
        var language = string.IsNullOrWhiteSpace(draft.Request.Language) ? "en-US" : draft.Request.Language!;
        foreach (var asset in draft.AssetFull.Concat(draft.ResultAssets))
            if (asset.BodyDoc is not null)
                asset.BodyDoc.Language = language;
    }
}
