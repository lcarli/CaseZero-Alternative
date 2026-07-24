using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace CaseGen.Functions.Services.CaseV2;

public enum FinalValidationGate
{
    Schema,
    CaseBible,
    Graph,
    Proof,
    Reachability,
    Forensic,
    Evidence,
    Difficulty,
    Locale,
    Solver,
    Specialist,
    RookieRegression,
    PublicParity
}

public sealed record FinalValidationIssue(
    FinalValidationGate Gate,
    string Code,
    string NodeId,
    string Message,
    bool Blocking);

public sealed class FinalValidationReport
{
    public string CaseId { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public DateTimeOffset ValidatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<FinalValidationIssue> Issues { get; set; } = new();
    public bool IsValid => Issues.All(issue => !issue.Blocking);
    public Dictionary<string, int> IssueCountsByGate =>
        Issues.GroupBy(issue => issue.Gate.ToString())
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}

public sealed class CaseV2FinalValidator
{
    private readonly JSchema _schema;

    public CaseV2FinalValidator(string schemaJson)
    {
        _schema = JSchema.Parse(schemaJson);
    }

    public static CaseV2FinalValidator CreateDefault()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Schemas", "case.v2.schema.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "functions", "CaseGen.Functions", "Schemas", "case.v2.schema.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Schemas", "case.v2.schema.json")
        };
        var path = candidates.FirstOrDefault(File.Exists)
                   ?? throw new FileNotFoundException("Could not locate case.v2.schema.json.");
        return new CaseV2FinalValidator(File.ReadAllText(path));
    }

    public FinalValidationReport Validate(
        CaseDraft draft,
        string publicCaseJson,
        SolverTask.SolverResult? solver = null,
        RedTeamTask.Report? specialistAdapterReport = null,
        PublicContractParityReport? parity = null)
    {
        var report = new FinalValidationReport
        {
            CaseId = draft.CaseId,
            Difficulty = DifficultyProfileCatalog.Get(draft).Name
        };
        AddSchema(publicCaseJson, report);
        AddCaseBible(
            CaseBibleValidator.Validate(
                draft,
                required: draft.CaseGraph.Origin == CaseGraphOrigin.Generated),
            report);
        AddGraph(CaseGraphValidator.Validate(draft.CaseGraph), report);
        AddProof(draft, report);
        AddStage(FinalValidationGate.Forensic, "forensic", ForensicContractValidator.Validate(draft), report);
        AddStage(FinalValidationGate.Evidence, "evidence", EvidenceContractValidator.Validate(draft), report);
        AddStage(FinalValidationGate.Difficulty, "difficulty", DifficultyTopologyValidator.Validate(draft), report);
        AddStage(FinalValidationGate.Locale, "locale", LocaleProfileCatalog.Validate(draft), report);

        if (solver is null)
        {
            Add(report, FinalValidationGate.Solver, "solver.missing", "solver.trace", "sequential solver result is missing");
        }
        else
        {
            if (!solver.LogicalProofPassed)
                Add(
                    report,
                    FinalValidationGate.Solver,
                    "solver.logical_failed",
                    "solver.trace",
                    string.IsNullOrWhiteSpace(solver.Notes)
                        ? "deterministic logical solvability gate failed"
                        : $"deterministic logical solvability gate failed: {solver.Notes}");
            if (!solver.NarrativeSimulationPassed)
                Add(report, FinalValidationGate.Solver, "solver.narrative_failed", "solver.trace", "narrative player simulation failed");
            if (!solver.Correct)
                Add(report, FinalValidationGate.Solver, "solver.score_failed", "solver.trace", $"sequential solver failed with score {solver.Score:0.####}");
            if (solver.Attempt.ObservationIds.Count == 0)
                Add(report, FinalValidationGate.Solver, "solver.citations_missing", "solver.trace", "successful solver attempt has no exact observation citations");
        }

        if (specialistAdapterReport is null)
        {
            foreach (var finding in ObjectiveQualityValidator.Validate(draft))
            {
                if (finding.Severity == ReviewSeverity.Low)
                    continue;
                Add(
                    report,
                    FinalValidationGate.Specialist,
                    finding.ConstraintId,
                    finding.OwningNodeId,
                    finding.Message,
                    finding.Severity == ReviewSeverity.High);
            }
        }
        else
        {
            foreach (var finding in specialistAdapterReport.Findings.Where(finding =>
                         !string.Equals(finding.Severity, "low", StringComparison.OrdinalIgnoreCase)))
            {
                Add(
                    report,
                    FinalValidationGate.Specialist,
                    finding.ConstraintId ?? finding.Area,
                    finding.NodeId ?? finding.FactRefs.FirstOrDefault() ?? "review.root",
                    finding.Issue,
                    finding.Blocking);
            }
        }

        if (parity is not null)
        {
            foreach (var issue in parity.Issues)
                Add(report, FinalValidationGate.PublicParity, $"parity.{issue.Surface}", $"public.{issue.Surface}", issue.Message);
        }

        if (string.Equals(report.Difficulty, "Rookie", StringComparison.Ordinal))
            AddStage(FinalValidationGate.RookieRegression, "rookie", RookieRegressionValidator.Validate(draft, publicCaseJson), report);

        report.Issues = report.Issues
            .Distinct()
            .OrderBy(issue => issue.Gate)
            .ThenBy(issue => issue.NodeId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToList();
        return report;
    }

    private void AddSchema(string json, FinalValidationReport report)
    {
        try
        {
            var value = JObject.Parse(json);
            if (!value.IsValid(_schema, out IList<string> messages))
            {
                foreach (var message in messages)
                    Add(report, FinalValidationGate.Schema, "schema.invalid", "public.case.json", message);
            }
        }
        catch (Exception exception)
        {
            Add(report, FinalValidationGate.Schema, "schema.parse", "public.case.json", exception.Message);
        }
    }

    private static void AddGraph(GraphValidationReport graph, FinalValidationReport report)
    {
        foreach (var error in graph.Errors)
            Add(report, FinalValidationGate.Graph, error.Code, error.NodeId, error.Message);
    }

    private static void AddCaseBible(CaseBibleValidationReport bible, FinalValidationReport report)
    {
        foreach (var issue in bible.Issues)
            Add(report, FinalValidationGate.CaseBible, issue.Code, issue.NodeId, issue.Message);
    }

    private static void AddProof(CaseDraft draft, FinalValidationReport report)
    {
        var profile = DifficultyProfileCatalog.Get(draft);
        foreach (var failure in CulpritUniquenessGate.Validate(draft.CaseGraph, draft.CulpritId, profile.Name).Failures)
            Add(report, FinalValidationGate.Proof, failure.Code, failure.NodeId, failure.Message);
        foreach (var failure in IndependentProofPathGate.Validate(draft.CaseGraph, draft.CulpritId, profile.Name).Failures)
            Add(report, FinalValidationGate.Proof, failure.Code, failure.NodeId, failure.Message);
        foreach (var failure in PrematureSolutionLeakageGate.Validate(draft.CaseGraph, draft.CulpritId, profile.Name).Failures)
            Add(report, FinalValidationGate.Proof, failure.Code, failure.NodeId, failure.Message);
        foreach (var failure in ReachabilityGate.Validate(draft.CaseGraph, draft.CulpritId).Failures)
            Add(report, FinalValidationGate.Reachability, failure.Code, failure.NodeId, failure.Message);
    }

    private static void AddStage(
        FinalValidationGate gate,
        string prefix,
        StageValidationReport stage,
        FinalValidationReport report)
    {
        foreach (var error in stage.Errors)
            Add(report, gate, $"{prefix}.invalid", ExtractNode(error) ?? $"{prefix}.root", error);
    }

    private static void Add(
        FinalValidationReport report,
        FinalValidationGate gate,
        string code,
        string node,
        string message,
        bool blocking = true) =>
        report.Issues.Add(new FinalValidationIssue(gate, code, node, message, blocking));

    private static string? ExtractNode(string message)
    {
        var start = message.IndexOf('\'');
        var end = start < 0 ? -1 : message.IndexOf('\'', start + 1);
        return start >= 0 && end > start ? message[(start + 1)..end] : null;
    }
}

public static class RookieRegressionValidator
{
    public static StageValidationReport Validate(CaseDraft draft, string publicCaseJson)
    {
        var report = new StageValidationReport();
        if (draft.AnalysisTypes.Count != 0)
            report.Errors.Add("Rookie regression requires zero analysis types");
        if (draft.ForensicFull.Count != 0 || draft.CaseGraph.ForensicTransforms.Count != 0)
            report.Errors.Add("Rookie regression requires zero forensic outcomes and transforms");
        if (draft.RequiredAnalysisIds.Count != 0)
            report.Errors.Add("Rookie regression requires zero required analysis IDs");
        if (draft.CaseGraph.Sources.Any(source => source.AvailableAt != ReachabilityState.Initial))
            report.Errors.Add("Rookie regression requires all solving evidence initially reachable");
        var leakage = PrematureSolutionLeakageGate.Validate(draft.CaseGraph, draft.CulpritId, "Rookie");
        report.Errors.AddRange(leakage.Failures.Select(failure => failure.Message));

        try
        {
            var root = JsonNode.Parse(publicCaseJson);
            if ((root?["forensicsDefaults"]?["analysisTypes"] as JsonArray)?.Count != 0)
                report.Errors.Add("Rookie public contract contains analysis types");
            if ((root?["forensicOutcomes"] as JsonArray)?.Count != 0)
                report.Errors.Add("Rookie public contract contains forensic outcomes");
            if ((root?["solution"]?["requiredAnalysisIds"] as JsonArray)?.Count != 0)
                report.Errors.Add("Rookie public contract contains required analysis IDs");
            if (root?["metadata"]?["unlockMode"]?.GetValue<string>() != "all_initial")
                report.Errors.Add("Rookie public contract must use all_initial unlock mode");
            var partial = root?["solution"]?["partialCreditRules"];
            if (partial is null
                || partial["culpritWeight"]?.GetValue<double>() != 0.4
                || partial["evidenceWeight"]?.GetValue<double>() != 0.2
                || partial["analysisWeight"]?.GetValue<double>() != 0.2
                || partial["questionsWeight"]?.GetValue<double>() != 0.2)
            {
                report.Errors.Add("Rookie scoring behavior differs from the v2 default weights");
            }
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Rookie public contract parse failed: {exception.Message}");
        }
        return report;
    }
}

public static class PrivateCaseArtifactPersistence
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Write(string caseDirectory, CaseGraph graph, FinalValidationReport report)
    {
        Directory.CreateDirectory(caseDirectory);
        File.WriteAllText(
            Path.Combine(caseDirectory, "case.graph.private.json"),
            JsonSerializer.Serialize(graph, Options));
        File.WriteAllText(
            Path.Combine(caseDirectory, "validation-report.private.json"),
            JsonSerializer.Serialize(report, Options));
    }

    public static void Write(
        string caseDirectory,
        CaseDraft draft,
        FinalValidationReport report,
        SolverTask.SolverResult? solver)
    {
        Write(caseDirectory, draft.CaseGraph, report);
        File.WriteAllText(
            Path.Combine(caseDirectory, "case.draft.private.json"),
            JsonSerializer.Serialize(draft, Options));
        File.WriteAllText(
            Path.Combine(caseDirectory, "rendered-text.private.json"),
            JsonSerializer.Serialize(
                draft.AssetFull.Concat(draft.ResultAssets)
                    .ToDictionary(asset => asset.Id, EvidenceContentText.Extract, StringComparer.Ordinal),
                Options));
        File.WriteAllText(
            Path.Combine(caseDirectory, "solver-trace.private.json"),
            JsonSerializer.Serialize(solver, Options));
    }
}
