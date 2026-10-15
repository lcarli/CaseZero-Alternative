using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CaseZero.Scoring;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace CaseZeroApi.IntegrationTests;

public class SubmissionScoringParityTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public SubmissionScoringParityTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("empty-requirements")]
    [InlineData("extras-and-duplicates")]
    [InlineData("weighted-questions")]
    [InlineData("duplicate-answers-and-ordinal-ids")]
    [InlineData("unanswered-null-option")]
    [InlineData("zero-question-weights")]
    [InlineData("round-below-threshold")]
    [InlineData("round-above-threshold")]
    [InlineData("round-to-even")]
    [InlineData("rookie-difficulty")]
    [InlineData("rookie-required-rank")]
    [InlineData("rook-is-not-rookie")]
    [InlineData("authorized-progression")]
    [InlineData("all-initial-keeps-analysis-gate")]
    [InlineData("correct-without-culprit-gate")]
    [InlineData("unnormalized-weights")]
    [InlineData("legacy-evidence-ulp")]
    [InlineData("legacy-analysis-ulp")]
    [InlineData("legacy-questions-ulp")]
    public async Task SubmitHttp_MatchesSharedScorerAndPublishedApiPolicy(string scenarioName)
    {
        var scenario = Scenario(scenarioName);
        var raw = scenario.Case;
        var request = scenario.Request;
        var solution = raw.Solution!;
        var rules = solution.PartialCreditRules ?? new CaseV2PartialCreditRules();
        var expected = SubmissionScoring.Evaluate(
            solution.CulpritId, request.SuspectId,
            solution.RequiredEvidenceIds, scenario.AuthorizedEvidenceIds,
            solution.RequiredAnalysisIds, scenario.CompletedAnalysisIds,
            solution.Questions.Select(q => new ScoredQuestion(q.Id, q.CorrectOptionId, q.Weight ?? 1)).ToArray(),
            request.Answers.Select(answer => new SubmittedAnswer(answer.QuestionId, answer.OptionId)),
            new SubmissionWeights(rules.CulpritWeight, rules.EvidenceWeight, rules.AnalysisWeight, rules.QuestionsWeight),
            solution.MinimumScore, scenario.RookieAnalysisCredit);

        var storage = new Mock<ICaseV2StorageService>();
        storage.Setup(s => s.GetRawAsync(raw.CaseId, It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICaseV2StorageService>();
            services.AddSingleton(storage.Object);
        }));
        using var client = host.CreateClient();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = $"scoring-{Guid.NewGuid():N}@fic-police.gov";
        var user = new User
        {
            Id = $"scoring-{Guid.NewGuid():N}", UserName = email, Email = email, PersonalEmail = email,
            FirstName = "Scoring", LastName = "Fixture", Rank = DetectiveRank.Detective
        };
        context.Users.Add(user);
        if (scenario.SeedProgression) SeedProgression(context, raw.CaseId, user.Id);
        await context.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<IJwtService>().GenerateToken(user, [UserRoles.Player]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var response = await client.PostAsJsonAsync($"/api/cases/{raw.CaseId}/submit", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SubmitCaseResult>();
            Assert.NotNull(result);
            Assert.Equal(expected.Correct, result!.Correct);
            Assert.Equal(expected.RoundedTotal, result.Score);
            Assert.Equal(new ScoreBreakdown(
                Math.Round(expected.Culprit, 4), Math.Round(expected.Evidence, 4),
                Math.Round(expected.Analysis, 4), Math.Round(expected.Questions, 4)), result.Breakdown);
            Assert.Equal(new ScoreWeights(
                Math.Round(rules.CulpritWeight, 4), Math.Round(rules.EvidenceWeight, 4),
                Math.Round(rules.AnalysisWeight, 4), Math.Round(rules.QuestionsWeight, 4)), result.MaxScores);
            Assert.Equal(attempt == 1, result.Graded);
            Assert.Equal(0, result.AttemptsRemaining);
            Assert.Equal(expected.Correct ? solution.Explanation : null, result.ExplanationMarkdown);
            Assert.Equal(expected.Correct
                ? attempt == 1 ? "correct" : "correct_ungraded"
                : attempt == 1 ? "incorrect_last_graded" : "incorrect_ungraded", result.FeedbackCode);
        }

        var saved = await context.CaseSubmissions.AsNoTracking()
            .Where(s => s.CaseId == raw.CaseId && s.SubmittedByUserId == user.Id)
            .OrderBy(s => s.AttemptNumber).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, submission =>
        {
            Assert.Equal(expected.RawTotal * 100, submission.Score);
            Assert.Equal<double?>(expected.Culprit, submission.CulpritScore);
            Assert.Equal<double?>(expected.Evidence, submission.EvidenceScore);
            Assert.Equal<double?>(expected.Analysis, submission.AnalysisScore);
            Assert.Equal<double?>(expected.Questions, submission.QuestionsScore);
            Assert.Equal(request.SuspectId == solution.CulpritId, submission.IsCorrectSuspect);
            Assert.Equal(expected.Evidence > 0, submission.IsValidEvidence);
            Assert.Equal(expected.Correct ? SubmissionStatus.Approved : SubmissionStatus.Rejected, submission.Status);
            Assert.Equal(JsonSerializer.Serialize(request), submission.RequestPayloadJson);
            Assert.Equal(JsonSerializer.Serialize(request.EvidenceIds), submission.SupportingEvidenceIds);
            Assert.Equal(string.Join(",", request.AnalysisIds), submission.Reasoning);
        });
        Assert.True(saved[0].Graded);
        Assert.False(saved[1].Graded);
        if (scenario.ExpectedRawTotal is double originalRaw)
            Assert.Equal(originalRaw, expected.RawTotal);
        Assert.Equal(scenario.ExpectedRoundedTotal, expected.RoundedTotal);
        Assert.Equal(scenario.ExpectedCorrect, expected.Correct);
    }

    private static ParityScenario Scenario(string name)
    {
        var raw = new CaseV2
        {
            CaseId = $"case_scoring_{Guid.NewGuid():N}",
            Metadata = new() { Title = "Scoring parity", Difficulty = "Detective", RequiredRank = "Detective" },
            Solution = new()
            {
                CulpritId = "suspect.expected", RequiredEvidenceIds = [], RequiredAnalysisIds = [], Questions = [],
                PartialCreditRules = new(), MinimumScore = .7, MaxAttempts = 1, Explanation = "Fixture explanation"
            }
        };
        var solution = raw.Solution;
        var request = new SubmitCaseRequest("suspect.expected", [], [], []);
        string[]? authorizedEvidence = null;
        string[]? completedAnalyses = null;
        var rookie = false;
        var rounded = .8;
        var correct = true;
        double? originalRaw = null;
        var seedProgression = false;
        switch (name)
        {
            case "empty-requirements":
                solution.PartialCreditRules = null;
                break;
            case "extras-and-duplicates":
                solution.RequiredEvidenceIds = ["asset.a", "asset.a", "asset.b"];
                solution.RequiredAnalysisIds = ["asset.a:Digital", "asset.a:Digital", "asset.b:DNA"];
                request = request with
                {
                    EvidenceIds = ["asset.a", "asset.a", "asset.extra"],
                    AnalysisIds = ["asset.a:Digital", "asset.a:Digital", "asset.extra:Digital"]
                };
                rounded = .5333;
                correct = false;
                break;
            case "weighted-questions":
                solution.Questions = [Question("q1", 2), Question("q2", 4), Question("q3")];
                request = request with
                {
                    Answers = [new("q1", "yes"), new("q2", "no"), new("q3", "yes"), new("extra", "yes")]
                };
                rounded = .8857;
                break;
            case "duplicate-answers-and-ordinal-ids":
                solution.RequiredEvidenceIds = ["asset.a"];
                solution.RequiredAnalysisIds = ["asset.a:Digital"];
                solution.Questions = [Question("q1"), Question("q2")];
                request = request with
                {
                    SuspectId = "suspect.Expected", EvidenceIds = ["ASSET.a"], AnalysisIds = ["asset.a:digital"],
                    Answers = [new("q1", "no"), new("q1", "yes"), new("Q2", "yes"), new("extra", "yes")]
                };
                rounded = 0;
                correct = false;
                break;
            case "zero-question-weights":
                solution.Questions = [Question("q1", 0), Question("q2", 0)];
                request = request with { Answers = [new("q1", "yes"), new("q2", "yes")] };
                break;
            case "unanswered-null-option":
                solution.Questions = [new() { Id = "q1" }];
                solution.MinimumScore = .9;
                correct = false;
                break;
            case "round-below-threshold":
            case "round-above-threshold":
                solution.PartialCreditRules = new()
                {
                    CulpritWeight = name == "round-below-threshold" ? .69996 : .70004,
                    EvidenceWeight = 0, AnalysisWeight = 0, QuestionsWeight = 0
                };
                rounded = .7;
                correct = name == "round-above-threshold";
                break;
            case "round-to-even":
                solution.PartialCreditRules = new()
                {
                    CulpritWeight = .15625, EvidenceWeight = 0, AnalysisWeight = 0, QuestionsWeight = 0
                };
                solution.MinimumScore = .15625;
                rounded = .1562;
                originalRaw = .15625;
                break;
            case "rookie-difficulty":
            case "rookie-required-rank":
            case "rook-is-not-rookie":
                raw.Metadata.UnlockMode = "gated";
                if (name == "rookie-difficulty") raw.Metadata.Difficulty = "rOoKiE";
                else raw.Metadata.RequiredRank = name == "rookie-required-rank" ? "rOoKiE" : "Rook";
                solution.RequiredAnalysisIds = ["asset.a:Digital"];
                rookie = name != "rook-is-not-rookie";
                if (!rookie) { rounded = .6; correct = false; }
                break;
            case "authorized-progression":
                raw.Metadata.UnlockMode = "gated";
                raw.Assets =
                [
                    new() { Id = "asset.initial", Visibility = "initial" },
                    new() { Id = "asset.revealed", Visibility = "hidden" },
                    new() { Id = "asset.hidden", Visibility = "hidden" },
                    new() { Id = "asset.other_user", Visibility = "hidden" },
                    new() { Id = "asset.other_case", Visibility = "hidden" }
                ];
                solution.RequiredEvidenceIds = raw.Assets.Select(a => a.Id).ToList();
                solution.RequiredAnalysisIds =
                [
                    "asset.initial:Digital", "asset.initial:DNA", "asset.revealed:Digital",
                    "asset.other_user:Digital", "asset.other_case:Digital"
                ];
                request = request with
                {
                    EvidenceIds = [..solution.RequiredEvidenceIds, "asset.initial", "asset.extra"],
                    AnalysisIds = [..solution.RequiredAnalysisIds, "asset.extra:DNA"]
                };
                authorizedEvidence = ["asset.initial", "asset.revealed"];
                completedAnalyses = ["asset.initial:Digital"];
                rounded = .52;
                correct = false;
                seedProgression = true;
                break;
            case "all-initial-keeps-analysis-gate":
                raw.Metadata.UnlockMode = "all_initial";
                raw.Assets = [new() { Id = "asset.a", Visibility = "hidden" }];
                solution.RequiredEvidenceIds = ["asset.a"];
                solution.RequiredAnalysisIds = ["asset.a:Digital"];
                request = request with { EvidenceIds = ["asset.a"], AnalysisIds = ["asset.a:Digital"] };
                completedAnalyses = [];
                rounded = .6;
                correct = false;
                break;
            case "correct-without-culprit-gate":
                solution.PartialCreditRules = new()
                {
                    CulpritWeight = .1, EvidenceWeight = .6, AnalysisWeight = .2, QuestionsWeight = .1
                };
                request = request with { SuspectId = "suspect.wrong" };
                break;
            case "unnormalized-weights":
                solution.PartialCreditRules = new()
                {
                    CulpritWeight = .6, EvidenceWeight = .8, AnalysisWeight = .5, QuestionsWeight = .3
                };
                solution.MinimumScore = 1.8;
                rounded = 1.9;
                break;
            case "legacy-evidence-ulp":
            case "legacy-analysis-ulp":
            case "legacy-questions-ulp":
                // The published API computed the ratio before multiplying the category weight.
                solution.PartialCreditRules = new()
                {
                    CulpritWeight = 0, EvidenceWeight = 0, AnalysisWeight = 0, QuestionsWeight = 0
                };
                originalRaw = .04000000000000001;
                solution.MinimumScore = originalRaw.Value;
                rounded = .04;
                if (name == "legacy-evidence-ulp")
                {
                    solution.PartialCreditRules.EvidenceWeight = .2;
                    solution.RequiredEvidenceIds = Enumerable.Range(1, 5).Select(i => $"asset.{i}").ToList();
                    request = request with { EvidenceIds = ["asset.1"] };
                }
                else if (name == "legacy-analysis-ulp")
                {
                    solution.PartialCreditRules.AnalysisWeight = .2;
                    solution.RequiredAnalysisIds = Enumerable.Range(1, 5).Select(i => $"asset.{i}:Digital").ToList();
                    request = request with { AnalysisIds = ["asset.1:Digital"] };
                }
                else
                {
                    solution.PartialCreditRules.QuestionsWeight = .2;
                    solution.Questions = Enumerable.Range(1, 5).Select(i => Question($"q{i}")).ToList();
                    request = request with { Answers = [new("q1", "yes")] };
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown parity fixture");
        }

        return new(raw, request, authorizedEvidence ?? request.EvidenceIds, completedAnalyses ?? request.AnalysisIds,
            rookie, rounded, correct, originalRaw, seedProgression);
    }

    private static CaseV2Question Question(string id, double? weight = null) => new()
    {
        Id = id, Prompt = id, CorrectOptionId = "yes", Weight = weight,
        Options = [new() { Id = "yes", Label = "Yes" }, new() { Id = "no", Label = "No" }]
    };

    private static void SeedProgression(ApplicationDbContext context, string caseId, string userId)
    {
        context.CaseSessionVisibleAssets.AddRange(
            new() { CaseId = caseId, UserId = userId, AssetId = "asset.revealed" },
            new() { CaseId = caseId, UserId = $"other-{userId}", AssetId = "asset.other_user" },
            new() { CaseId = $"{caseId}_other", UserId = userId, AssetId = "asset.other_case" });
        context.ForensicRequests.AddRange(
            new() { CaseId = caseId, UserId = userId, InputAssetId = "asset.initial", AnalysisType = "Digital", Status = "completed" },
            new() { CaseId = caseId, UserId = userId, InputAssetId = "asset.initial", AnalysisType = "DNA", Status = "pending" },
            new() { CaseId = caseId, UserId = userId, InputAssetId = "asset.revealed", AnalysisType = "Digital", Status = "failed" },
            new() { CaseId = caseId, UserId = $"other-{userId}", InputAssetId = "asset.other_user", AnalysisType = "Digital", Status = "completed" },
            new() { CaseId = $"{caseId}_other", UserId = userId, InputAssetId = "asset.other_case", AnalysisType = "Digital", Status = "completed" });
    }

    private sealed record ParityScenario(
        CaseV2 Case, SubmitCaseRequest Request, string[] AuthorizedEvidenceIds, string[] CompletedAnalysisIds,
        bool RookieAnalysisCredit, double ExpectedRoundedTotal, bool ExpectedCorrect,
        double? ExpectedRawTotal, bool SeedProgression);
}
