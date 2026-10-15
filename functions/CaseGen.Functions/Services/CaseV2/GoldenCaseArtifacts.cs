using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class GoldenCaseManifest
{
    public string CaseId { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string ArtifactStatus { get; set; } = "candidate";
    public bool RealGenerated { get; set; }
    public string Language { get; set; } = "en-US";
    public string TimeZoneId { get; set; } = "UTC";
    public string UtcOffset { get; set; } = "+00:00";
    public string PoliceAgency { get; set; } = "Police Department";
    public string InvestigatorName { get; set; } = "Alex Morgan";
    public string InvestigatorEmail { get; set; } = "investigator@casezero.local";
    public string Notes { get; set; } = string.Empty;
}

public sealed class ExpectedReachabilityArtifact
{
    public List<string> InitialObservationIds { get; set; } = new();
    public List<string> FinalSourceIds { get; set; } = new();
    public List<string> FinalObservationIds { get; set; } = new();
    public List<string> ExecutedActionIds { get; set; } = new();
}

public sealed class GoldenCaseBundle
{
    public string DirectoryPath { get; init; } = string.Empty;
    public GoldenCaseManifest Manifest { get; init; } = new();
    public CaseDraft Draft { get; init; } = new();
    public string PublicCaseJson { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> RenderedText { get; init; } =
        new Dictionary<string, string>();
    public IReadOnlyList<string> ExpectedDerivationSignatures { get; init; } = Array.Empty<string>();
    public ExpectedReachabilityArtifact ExpectedReachability { get; init; } = new();
    public SolverTask.SolverResult ExpectedSolver { get; init; } = new();
}

public static class GoldenCaseArtifactLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static GoldenCaseBundle Load(string directory)
    {
        var manifest = Read<GoldenCaseManifest>("golden.manifest.json");
        var graph = Read<CaseGraph>("case.graph.private.json");
        var publicJson = File.ReadAllText(Path.Combine(directory, "case.json"));
        var rendered = Read<Dictionary<string, string>>("rendered-text.json");
        var derivations = Read<List<string>>("expected-derivations.json");
        var reachability = Read<ExpectedReachabilityArtifact>("expected-reachability.json");
        var solver = Read<SolverTask.SolverResult>("expected-solver-trace.json");
        var draft = BuildDraft(publicJson, graph, rendered, solver.Trace, manifest);
        return new GoldenCaseBundle
        {
            DirectoryPath = directory,
            Manifest = manifest,
            Draft = draft,
            PublicCaseJson = publicJson,
            RenderedText = rendered,
            ExpectedDerivationSignatures = derivations,
            ExpectedReachability = reachability,
            ExpectedSolver = solver
        };

        T Read<T>(string fileName) =>
            JsonSerializer.Deserialize<T>(
                File.ReadAllText(Path.Combine(directory, fileName)),
                Options) ?? throw new InvalidOperationException($"Could not load golden artifact '{fileName}'.");
    }

    public static StageValidationReport ValidateExpectations(GoldenCaseBundle bundle)
    {
        var report = new StageValidationReport();
        var reachability = ReachabilityClosureEngine.Calculate(bundle.Draft.CaseGraph);
        Compare("initial observations", bundle.ExpectedReachability.InitialObservationIds, reachability.InitialObservationIds);
        Compare("final sources", bundle.ExpectedReachability.FinalSourceIds, reachability.FinalSourceIds);
        Compare("final observations", bundle.ExpectedReachability.FinalObservationIds, reachability.FinalObservationIds);
        Compare("executed actions", bundle.ExpectedReachability.ExecutedActionIds, reachability.ExecutedActionIds);

        var closure = DerivationClosureEngine.Calculate(bundle.Draft.CaseGraph, reachability.FinalObservationIds);
        var actualSignatures = bundle.Draft.CaseGraph.Derivations
            .SelectMany(derivation => closure.GetPaths(derivation.ConclusionFactId)
                .Where(path => path.FinalDerivationId == derivation.Id)
                .Select(path => path.Signature))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Compare("derivations", bundle.ExpectedDerivationSignatures, actualSignatures);
        if (bundle.ExpectedSolver.Trace.Entries.Count == 0)
            report.Errors.Add("golden solver trace is empty");
        return report;

        void Compare(string name, IEnumerable<string> expected, IEnumerable<string> actual)
        {
            var expectedArray = expected.Order(StringComparer.Ordinal).ToArray();
            var actualArray = actual.Order(StringComparer.Ordinal).ToArray();
            if (!expectedArray.SequenceEqual(actualArray, StringComparer.Ordinal))
                report.Errors.Add($"golden {name} mismatch: expected [{string.Join(", ", expectedArray)}], actual [{string.Join(", ", actualArray)}]");
        }
    }

    private static CaseDraft BuildDraft(
        string publicJson,
        CaseGraph graph,
        IReadOnlyDictionary<string, string> rendered,
        SolverDiagnosticTrace solverTrace,
        GoldenCaseManifest manifest)
    {
        var root = JsonNode.Parse(publicJson)?.AsObject()
                   ?? throw new InvalidOperationException("Golden case.json is invalid.");
        var metadata = root["metadata"]?.Deserialize<PlotMetadata>(Options) ?? new PlotMetadata();
        var solution = root["solution"]?.Deserialize<SolutionResult>(Options) ?? new SolutionResult();
        var draft = new CaseDraft
        {
            CaseId = root["caseId"]?.GetValue<string>() ?? string.Empty,
            Metadata = metadata,
            Request = new GenerateCaseV2Request
            {
                Difficulty = metadata.Difficulty,
                RequiredRank = metadata.RequiredRank,
                Language = manifest.Language
            },
            Blueprint = new InvestigationBlueprint
            {
                Locale = new CaseLocaleContext
                {
                    TimeZoneId = manifest.TimeZoneId,
                    UtcOffset = manifest.UtcOffset,
                    PoliceAgency = manifest.PoliceAgency,
                    InvestigatorName = manifest.InvestigatorName,
                    InvestigatorEmail = manifest.InvestigatorEmail
                }
            },
            CulpritId = solution.CulpritId,
            CaseGraph = graph,
            RequiredEvidenceIds = solution.RequiredEvidenceIds,
            RequiredAnalysisIds = solution.RequiredAnalysisIds,
            Questions = solution.Questions,
            Explanation = solution.Explanation,
            SolverTrace = solverTrace
        };
        foreach (var suspect in root["suspects"]?.AsArray() ?? [])
            draft.SuspectFull.Add(suspect?.Deserialize<PlotSuspect>(Options) ?? new PlotSuspect());
        foreach (var assetNode in root["assets"]?.AsArray() ?? [])
        {
            if (assetNode is null)
                continue;
            var id = assetNode["id"]?.GetValue<string>() ?? string.Empty;
            var asset = new EvidenceAsset
            {
                Id = id,
                Type = assetNode["type"]?.GetValue<string>() ?? "document",
                Title = assetNode["title"]?.GetValue<string>() ?? id,
                Description = assetNode["description"]?.GetValue<string>(),
                Visibility = assetNode["visibility"]?.GetValue<string>() ?? "initial",
                Category = assetNode["category"]?.GetValue<string>(),
                Body = rendered.GetValueOrDefault(id)
            };
            if (asset.Visibility == "hidden")
                draft.ResultAssets.Add(asset);
            else
                draft.AssetFull.Add(asset);
        }
        foreach (var emailNode in root["emails"]?.AsArray() ?? [])
        {
            if (emailNode is null)
                continue;
            var email = emailNode.Deserialize<EvidenceEmail>(Options) ?? new EvidenceEmail();
            if (email.Id == "email.briefing")
            {
                draft.Briefing = new PlotBriefingEmail
                {
                    From = email.From,
                    Subject = email.Subject,
                    Body = email.Body
                };
            }
            else if (email.Visibility == "hidden")
                draft.ResultEmails.Add(email);
            else
                draft.FollowUpEmails.Add(email);
        }
        draft.Timeline = root["timeline"]?.Deserialize<List<EvidenceTimelineEntry>>(Options) ?? new();
        draft.TemporalEvents = root["temporalEvents"]?.Deserialize<List<EvidenceTemporalEvent>>(Options) ?? new();
        draft.Rules = root["rules"]?.Deserialize<List<RulesRule>>(Options) ?? new();
        draft.AnalysisTypes = root["forensicsDefaults"]?["analysisTypes"]?.Deserialize<List<ForensicsAnalysisType>>(Options) ?? new();
        draft.ForensicFull = root["forensicOutcomes"]?.Deserialize<List<ForensicsOutcome>>(Options) ?? new();
        return draft;
    }
}

public static class CaseV2ArtifactValidationHarness
{
    public static FinalValidationReport ValidateDirectory(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "golden.manifest.json")))
            return ValidateGeneratedDirectory(directory);
        var bundle = GoldenCaseArtifactLoader.Load(directory);
        var report = CaseV2FinalValidator.CreateDefault().Validate(
            bundle.Draft,
            bundle.PublicCaseJson,
            bundle.ExpectedSolver);
        foreach (var error in GoldenCaseArtifactLoader.ValidateExpectations(bundle).Errors)
        {
            report.Issues.Add(new FinalValidationIssue(
                FinalValidationGate.Graph,
                "golden.expectation_mismatch",
                "golden.manifest",
                error,
                true));
        }
        File.WriteAllText(
            Path.Combine(directory, "validation-report.private.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }

    private static FinalValidationReport ValidateGeneratedDirectory(string directory)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var draftPath = Path.Combine(directory, "case.draft.private.json");
        var graphPath = Path.Combine(directory, "case.graph.private.json");
        var reportPath = Path.Combine(directory, "validation-report.private.json");
        var casePath = Path.Combine(directory, "case.json");
        if (!File.Exists(draftPath) || !File.Exists(graphPath) || !File.Exists(reportPath) || !File.Exists(casePath))
            throw new InvalidOperationException("Generated artifact directory is missing case.json or private graph validation artifacts.");
        var draft = JsonSerializer.Deserialize<CaseDraft>(File.ReadAllText(draftPath), options)
                    ?? throw new InvalidOperationException("Could not load private CaseDraft.");
        var graph = JsonSerializer.Deserialize<CaseGraph>(File.ReadAllText(graphPath), options)
                    ?? throw new InvalidOperationException("Could not load private CaseGraph.");
        draft.CaseGraph = graph;
        var solverPath = Path.Combine(directory, "solver-trace.private.json");
        var solver = File.Exists(solverPath)
            ? JsonSerializer.Deserialize<SolverTask.SolverResult>(File.ReadAllText(solverPath), options)
            : null;
        var rerun = CaseV2FinalValidator.CreateDefault().Validate(
            draft,
            File.ReadAllText(casePath),
            solver);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(rerun, new JsonSerializerOptions { WriteIndented = true }));
        return rerun;
    }
}
