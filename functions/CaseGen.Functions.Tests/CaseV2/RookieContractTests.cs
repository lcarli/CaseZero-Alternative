using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text.Json.Nodes;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class RookieContractTests
{
    [Fact]
    public void PlayerTimelineCompiler_ExcludesPrivateAndCulpritLeakingEvents()
    {
        var draft = new CaseDraft
        {
            Metadata = new PlotMetadata
            {
                IncidentDate = "2026-01-01T10:00:00Z",
                OpenedAt = "2026-01-01T12:00:00Z",
                Briefing = "A manager was reported missing."
            },
            CulpritId = "suspect.diego",
            SuspectStubs =
            {
                new SuspectStub { Id = "suspect.diego", Name = "Diego Siqueira", IsCulprit = true }
            },
            Blueprint = new InvestigationBlueprint
            {
                IncidentSequence =
                {
                    new CanonicalIncidentBeat
                    {
                        Id = "event.private_attack",
                        Time = "2026-01-01T10:10:00Z",
                        Event = "Diego restrained the victim.",
                        Visibility = "private",
                        Source = "investigation"
                    },
                    new CanonicalIncidentBeat
                    {
                        Id = "event.leaking_public",
                        Time = "2026-01-01T10:20:00Z",
                        Event = "A witness saw a technician.",
                        PublicDescription = "Diego Siqueira entered the service corridor.",
                        Visibility = "public",
                        Source = "witness"
                    },
                    new CanonicalIncidentBeat
                    {
                        Id = "event.safe_public",
                        Time = "2026-01-01T10:30:00Z",
                        Event = "A badge event was recorded.",
                        PublicDescription = "An internal badge reader recorded an unexplained event.",
                        Visibility = "public",
                        Source = "sensor",
                        Verified = true,
                        Importance = "high"
                    }
                }
            }
        };

        var timeline = PlayerTimelineCompiler.Compile(draft, NullLogger.Instance);

        Assert.DoesNotContain(timeline, entry => entry.Event.Contains("Diego", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(timeline, entry => entry.Event.Contains("badge reader", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConsistencyValidator_RejectsForensicDependenciesInRookieCase()
    {
        var draft = new CaseDraft
        {
            Metadata = new PlotMetadata { Difficulty = "Rookie", RequiredRank = "Rookie" },
            CulpritId = "suspect.culprit",
            SuspectFull = { new PlotSuspect { Id = "suspect.culprit", Name = "Culprit" } },
            AnalysisTypes =
            {
                new ForensicsAnalysisType { Type = "DigitalForensics", DurationMinutes = 30, AvailableFor = { "digital" } }
            },
            RequiredAnalysisIds = { "asset.log:DigitalForensics" }
        };

        var validator = new ConsistencyValidator(NullLogger<ConsistencyValidator>.Instance);
        var report = validator.Validate(draft);

        Assert.Contains(report.Errors, error => error.Contains("Rookie case contains forensic", StringComparison.Ordinal));
        Assert.Contains(report.Errors, error => error.Contains("requiredAnalysisIds", StringComparison.Ordinal));
    }

    [Fact]
    public void RefineNormalizer_RestoresRookieRuntimeAndScoringContract()
    {
        const string refinedJson = """
        {
          "suspects": [{ "id": "suspect.culprit", "name": "Marina Saad" }],
          "timeline": [
            { "time": "2026-01-01T10:00:00Z", "event": "Marina Saad edited the log." },
            { "time": "2026-01-01T10:05:00Z", "event": "A side door opened." }
          ],
          "rules": [
            { "trigger": { "type": "forensics_complete" }, "actions": [] }
          ],
          "forensicsDefaults": {
            "analysisTypes": [{ "type": "none", "durationMinutes": 1, "availableFor": ["rookie"] }]
          },
          "forensicOutcomes": [{ "inputAssetId": "asset.log" }],
          "solution": {
            "culpritId": "suspect.culprit",
            "requiredAnalysisIds": ["asset.log:DigitalForensics"],
            "partialCreditRules": {
              "culpritWeight": 0.4,
              "evidenceWeight": 0.2,
              "analysisWeight": 0.0,
              "questionsWeight": 0.2
            }
          }
        }
        """;

        var method = typeof(CaseV2GeneratorService).GetMethod(
            "EnforceDifficultyContract",
            BindingFlags.NonPublic | BindingFlags.Static);

        var normalized = Assert.IsType<string>(method!.Invoke(null, new object[] { refinedJson, true }));
        var root = JsonNode.Parse(normalized)!.AsObject();

        Assert.Empty(root["forensicsDefaults"]!["analysisTypes"]!.AsArray());
        Assert.Empty(root["forensicOutcomes"]!.AsArray());
        Assert.Empty(root["solution"]!["requiredAnalysisIds"]!.AsArray());
        Assert.Empty(root["rules"]!.AsArray());
        Assert.Equal(0.2, root["solution"]!["partialCreditRules"]!["analysisWeight"]!.GetValue<double>());
        Assert.Single(root["timeline"]!.AsArray());
        Assert.Equal("A side door opened.", root["timeline"]![0]!["event"]!.GetValue<string>());
    }

    [Fact]
    public void RedTeamCompactContext_PreservesRevealActionsAndEmailContent()
    {
        const string assembledJson = """
        {
          "metadata": { "difficulty": "Detective" },
          "assets": [],
          "suspects": [{ "id": "suspect.culprit", "name": "Culprit" }],
          "emails": [{
            "id": "email.result",
            "from": "Lab",
            "subject": "Result",
            "body": "The result is available.",
            "attachments": ["asset.result"],
            "visibility": "hidden"
          }],
          "timeline": [],
          "temporalEvents": [],
          "rules": [{
            "ruleId": "rule.reveal_result",
            "description": "Reveal the result",
            "trigger": {
              "type": "forensics_complete",
              "inputAssetId": "asset.input",
              "analysisType": "DigitalForensics"
            },
            "actions": [
              { "type": "reveal_email", "emailId": "email.result" },
              { "type": "reveal_asset", "assetId": "asset.result" }
            ]
          }],
          "forensicOutcomes": [],
          "solution": { "culpritId": "suspect.culprit" }
        }
        """;

        var method = typeof(RedTeamTask).GetMethod(
            "BuildCompactContextFromJson",
            BindingFlags.NonPublic | BindingFlags.Static);

        var compactJson = Assert.IsType<string>(method!.Invoke(null, new object?[] { assembledJson, null }));
        var compact = JsonNode.Parse(compactJson)!.AsObject();

        Assert.Equal("reveal_email", compact["rules"]![0]!["actions"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("asset.result", compact["rules"]![0]!["actions"]![1]!["assetId"]!.GetValue<string>());
        Assert.Equal("The result is available.", compact["emails"]![0]!["body"]!.GetValue<string>());
    }
}
