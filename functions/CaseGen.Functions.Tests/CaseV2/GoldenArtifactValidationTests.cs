using System.Reflection;
using System.Text.Json;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class GoldenArtifactValidationTests
{
    [Theory]
    [InlineData("Rookie")]
    [InlineData("Detective")]
    public void PersistedGoldenArtifacts_LoadAndMatchExpectedTraces(string difficulty)
    {
        var bundle = GoldenCaseArtifactLoader.Load(GoldenDirectory(difficulty));

        var expectations = GoldenCaseArtifactLoader.ValidateExpectations(bundle);
        var final = CaseV2FinalValidator.CreateDefault().Validate(
            bundle.Draft,
            bundle.PublicCaseJson,
            bundle.ExpectedSolver);

        Assert.Empty(expectations.Errors);
        Assert.DoesNotContain(final.Issues, issue => issue.Gate == FinalValidationGate.Schema);
        Assert.NotEmpty(bundle.RenderedText);
        Assert.NotEmpty(bundle.ExpectedDerivationSignatures);
        Assert.NotEmpty(bundle.ExpectedReachability.FinalSourceIds);
        Assert.NotEmpty(bundle.ExpectedSolver.Trace.Entries);
        if (difficulty == "Rookie")
        {
            Assert.Equal("approved-regression", bundle.Manifest.ArtifactStatus);
            Assert.Empty(RookieRegressionValidator.Validate(bundle.Draft, bundle.PublicCaseJson).Errors);
            Assert.True(final.IsValid, string.Join(Environment.NewLine, final.Issues.Select(issue => issue.Message)));
        }
        else
        {
            Assert.Equal("candidate-not-approved", bundle.Manifest.ArtifactStatus);
            Assert.False(bundle.Manifest.RealGenerated);
            var approval = DifficultyApprovalGate.Evaluate(
                "Detective",
                batch: null,
                hasPersistedGolden: true,
                hasRealGeneratedGolden: bundle.Manifest.RealGenerated);
            Assert.Equal(DifficultyApprovalStatus.Blocked, approval.Status);
        }
    }

    [Fact]
    public void UpdateGoldens_WhenExplicitlyRequested()
    {
        if (Environment.GetEnvironmentVariable("CASEZERO_UPDATE_GOLDENS") != "1")
            return;
        WriteGolden("Rookie");
        WriteGolden("Detective");
    }

    [Fact]
    public void ValidateArtifactDirectory_WhenProvided()
    {
        var directory = Environment.GetEnvironmentVariable("CASEZERO_CASEV2_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            return;
        var report = CaseV2ArtifactValidationHarness.ValidateDirectory(directory);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues.Select(issue =>
            $"{issue.Gate}/{issue.Code} [{issue.NodeId}]: {issue.Message}")));
    }

    private static void WriteGolden(string difficulty)
    {
        var fixtureType = typeof(Phase9MigrationTests);
        var draftMethod = fixtureType.GetMethod(
            difficulty == "Rookie" ? "ValidRookieDraft" : "ValidDetectiveDraft",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Golden draft factory not found.");
        var jsonMethod = fixtureType.GetMethod(
            difficulty == "Rookie" ? "PublicRookieJson" : "PublicDetectiveJson",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Golden public JSON factory not found.");
        var draft = (CaseDraft)(draftMethod.Invoke(null, null)
                    ?? throw new InvalidOperationException("Golden draft factory returned null."));
        var publicJson = (string)(jsonMethod.Invoke(null, null)
                         ?? throw new InvalidOperationException("Golden JSON factory returned null."));
        draft.CaseGraph.Origin = CaseGraphOrigin.HandAuthored;
        var directory = GoldenDirectory(difficulty);
        Directory.CreateDirectory(directory);
        var options = new JsonSerializerOptions { WriteIndented = true };

        var rendered = draft.CaseGraph.Sources
            .Where(source => source.Kind == EvidenceSourceKind.Asset)
            .ToDictionary(
                source => source.Id,
                source =>
                {
                    var statements = source.ObservationIds
                        .Select(id => draft.CaseGraph.Observations.First(observation => observation.Id == id))
                        .Select(observation => draft.CaseGraph.Facts.First(fact => fact.Id == observation.FactId).CanonicalValue)
                        .Where(value => !string.IsNullOrWhiteSpace(value));
                    return string.Join("\n", statements.DefaultIfEmpty($"Rendered evidence for {source.Id}."));
                },
                StringComparer.Ordinal);
        var reachability = ReachabilityClosureEngine.Calculate(draft.CaseGraph);
        var closure = DerivationClosureEngine.Calculate(draft.CaseGraph, reachability.FinalObservationIds);
        var signatures = draft.CaseGraph.Derivations
            .SelectMany(derivation => closure.GetPaths(derivation.ConclusionFactId)
                .Where(path => path.FinalDerivationId == derivation.Id)
                .Select(path => path.Signature))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var expectedReachability = new ExpectedReachabilityArtifact
        {
            InitialObservationIds = reachability.InitialObservationIds.ToList(),
            FinalSourceIds = reachability.FinalSourceIds.ToList(),
            FinalObservationIds = reachability.FinalObservationIds.ToList(),
            ExecutedActionIds = reachability.ExecutedActionIds.ToList()
        };
        var solver = difficulty == "Rookie"
            ? RookieSolver(draft)
            : DetectiveSolver(draft);
        var manifest = new GoldenCaseManifest
        {
            CaseId = draft.CaseId,
            Difficulty = difficulty,
            ArtifactStatus = difficulty == "Rookie" ? "approved-regression" : "candidate-not-approved",
            RealGenerated = false,
            Language = "en-US",
            TimeZoneId = "America/New_York",
            UtcOffset = "-04:00",
            PoliceAgency = "Boston Police Department",
            InvestigatorName = "Alex Morgan",
            InvestigatorEmail = "alex@casezero.local",
            Notes = difficulty == "Rookie"
                ? "Deterministic Rookie regression golden."
                : "Detective validation candidate; not approved until a real generated case and multi-seed batch pass."
        };

        Write("golden.manifest.json", manifest);
        Write("case.graph.private.json", draft.CaseGraph);
        File.WriteAllText(Path.Combine(directory, "case.json"), publicJson);
        Write("rendered-text.json", rendered);
        Write("expected-derivations.json", signatures);
        Write("expected-reachability.json", expectedReachability);
        Write("expected-solver-trace.json", solver);

        void Write<T>(string name, T value) =>
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, options));
    }

    private static SolverTask.SolverResult RookieSolver(CaseDraft draft) => new()
    {
        Correct = true,
        Score = 1,
        LogicalProofPassed = true,
        NarrativeSimulationPassed = true,
        Attempt = new SolverTask.Attempt
        {
            SuspectId = draft.CulpritId,
            EvidenceIds = { "asset.initial", "asset.second" },
            ObservationIds = { "observation.identity_a", "observation.identity_b" }
        },
        Trace = new SolverDiagnosticTrace
        {
            LogicalProofPassed = true,
            NarrativeSimulationPassed = true,
            FinalReachableSourceIds = draft.CaseGraph.Sources.Select(source => source.Id).Order(StringComparer.Ordinal).ToList(),
            FinalReachableObservationIds = draft.CaseGraph.Observations.Select(observation => observation.Id).Order(StringComparer.Ordinal).ToList(),
            Entries =
            {
                new PlayerActionTraceEntry(
                    1, PlayerActionKind.AccuseSuspect, "accuse:suspect.culprit", "suspect.culprit",
                    true, "accepted", "Action accepted.", Array.Empty<string>(),
                    new[] { "observation.identity_a", "observation.identity_b" })
            }
        }
    };

    private static SolverTask.SolverResult DetectiveSolver(CaseDraft draft) => new()
    {
        Correct = true,
        Score = 1,
        LogicalProofPassed = true,
        NarrativeSimulationPassed = true,
        Attempt = new SolverTask.Attempt
        {
            SuspectId = draft.CulpritId,
            EvidenceIds = { "asset.initial_a", "asset.result_a" },
            AnalysisIds = { "asset.initial_a:MetadataAnalysis" },
            ObservationIds = { "observation.initial_a", "observation.forensic_a" }
        },
        Trace = new SolverDiagnosticTrace
        {
            LogicalProofPassed = true,
            NarrativeSimulationPassed = true,
            Entries =
            {
                new PlayerActionTraceEntry(1, PlayerActionKind.InspectAsset, "inspect:asset.initial_a", "asset.initial_a", true, "accepted", "Action accepted.", Array.Empty<string>(), Array.Empty<string>()),
                new PlayerActionTraceEntry(2, PlayerActionKind.RequestAnalysis, "action.analysis_a", "asset.initial_a", true, "accepted", "Action accepted.", new[] { "asset.result_a" }, Array.Empty<string>()),
                new PlayerActionTraceEntry(3, PlayerActionKind.InspectAsset, "inspect:asset.result_a", "asset.result_a", true, "accepted", "Action accepted.", Array.Empty<string>(), Array.Empty<string>()),
                new PlayerActionTraceEntry(4, PlayerActionKind.AccuseSuspect, "accuse:suspect.culprit", "suspect.culprit", true, "accepted", "Action accepted.", Array.Empty<string>(), new[] { "observation.initial_a", "observation.forensic_a" })
            }
        }
    };

    private static string GoldenDirectory(string difficulty) =>
        Path.Combine(RepositoryRoot(), "functions", "CaseGen.Functions.Tests", "CaseV2", "Goldens", difficulty);

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                && Directory.Exists(Path.Combine(current.FullName, "functions")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
