using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class SolutionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICaseV2StorageService> _storageMock;
    private readonly SolutionService _sut;

    private const string UserId = "user-solution-test";
    private const string CaseId = "case-solution-test";

    // Default partial-credit weights matching CaseV2PartialCreditRules defaults
    private const double CulpritW   = 0.4;
    private const double EvidenceW  = 0.2;
    private const double AnalysisW  = 0.2;
    private const double QuestionsW = 0.2;

    public SolutionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _storageMock = new Mock<ICaseV2StorageService>();
        _sut = new SolutionService(_context, _storageMock.Object,
            new PromotionService(_context, NullLogger<PromotionService>.Instance),
            NullLogger<SolutionService>.Instance);
    }

    // ──────────────────────────────────────────────────────────────
    // Fixture helpers
    // ──────────────────────────────────────────────────────────────

    private static CaseV2 BuildFixtureCase(int maxAttempts = 3) => new()
    {
        CaseId = CaseId,
        Metadata = new CaseV2Metadata { Title = "Solution Test", Description = "D" },
        Solution = new CaseV2Solution
        {
            CulpritId = "suspect.marcus",
            RequiredEvidenceIds = new List<string> { "asset.phone_data", "asset.report" },
            RequiredAnalysisIds = new List<string> { "asset.phone_data:Digital" },
            Questions = new List<CaseV2Question>
            {
                new()
                {
                    Id = "q1", Prompt = "Motive?",
                    Options = new List<CaseV2QuestionOption>
                    {
                        new() { Id = "opt-blackmail", Label = "Blackmail" },
                        new() { Id = "opt-other", Label = "Other" }
                    },
                    CorrectOptionId = "opt-blackmail",
                    Weight = 1.0
                }
            },
            Explanation = "Marcus committed the crime because of blackmail.",
            MinimumScore = 0.7,
            MaxAttempts = maxAttempts,
            PartialCreditRules = new CaseV2PartialCreditRules
            {
                CulpritWeight   = CulpritW,
                EvidenceWeight  = EvidenceW,
                AnalysisWeight  = AnalysisW,
                QuestionsWeight = QuestionsW
            }
        }
    };

    private void SetupCase(CaseV2 c) =>
        _storageMock.Setup(s => s.GetRawAsync(CaseId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(c);

    private static SubmitCaseRequest FullyCorrectRequest() => new(
        SuspectId: "suspect.marcus",
        EvidenceIds: new[] { "asset.phone_data", "asset.report" },
        AnalysisIds: new[] { "asset.phone_data:Digital" },
        Answers: new[] { new AnswerDto("q1", "opt-blackmail") }
    );

    // ──────────────────────────────────────────────────────────────
    // Full-correct → score 1.0, correct == true
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullyCorrectSubmission_ScoreIsOne_CorrectIsTrue()
    {
        SetupCase(BuildFixtureCase());

        var result = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());

        Assert.True(result.Correct);
        Assert.Equal(1.0, result.Score, precision: 4);
    }

    [Fact]
    public async Task FullyCorrectSubmission_BreakdownSumsToOne()
    {
        SetupCase(BuildFixtureCase());

        var result = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());

        var sum = result.Breakdown.CulpritScore + result.Breakdown.EvidenceScore
                + result.Breakdown.AnalysisScore + result.Breakdown.QuestionsScore;
        Assert.Equal(1.0, sum, precision: 4);
    }

    // ──────────────────────────────────────────────────────────────
    // Wrong culprit → score ~0.6 (evidence + analysis + questions full credit)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task WrongCulprit_EverythingElseCorrect_ScoreIsPoint6()
    {
        SetupCase(BuildFixtureCase());

        var req = new SubmitCaseRequest(
            SuspectId: "suspect.other",         // wrong culprit
            EvidenceIds: new[] { "asset.phone_data", "asset.report" },
            AnalysisIds: new[] { "asset.phone_data:Digital" },
            Answers: new[] { new AnswerDto("q1", "opt-blackmail") }
        );

        var result = await _sut.SubmitAsync(CaseId, UserId, req);

        Assert.False(result.Correct);
        Assert.Equal(EvidenceW + AnalysisW + QuestionsW, result.Score, precision: 4);
        Assert.Equal(0.0, result.Breakdown.CulpritScore, precision: 4);
    }

    // ──────────────────────────────────────────────────────────────
    // Partial evidence: 1 of 2 required
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PartialEvidence_1Of2_EvidenceScoreIsHalfWeight()
    {
        SetupCase(BuildFixtureCase());

        var req = new SubmitCaseRequest(
            SuspectId: "suspect.marcus",
            EvidenceIds: new[] { "asset.phone_data" },   // only 1 of 2
            AnalysisIds: new[] { "asset.phone_data:Digital" },
            Answers: new[] { new AnswerDto("q1", "opt-blackmail") }
        );

        var result = await _sut.SubmitAsync(CaseId, UserId, req);

        Assert.Equal(EvidenceW * 0.5, result.Breakdown.EvidenceScore, precision: 4);
    }

    // ──────────────────────────────────────────────────────────────
    // Empty requiredEvidenceIds → full evidence credit
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyRequiredEvidenceIds_FullEvidenceCredit()
    {
        var c = BuildFixtureCase();
        c.Solution!.RequiredEvidenceIds = new List<string>();
        SetupCase(c);

        var req = new SubmitCaseRequest(
            SuspectId: "suspect.marcus",
            EvidenceIds: Array.Empty<string>(),
            AnalysisIds: new[] { "asset.phone_data:Digital" },
            Answers: new[] { new AnswerDto("q1", "opt-blackmail") }
        );

        var result = await _sut.SubmitAsync(CaseId, UserId, req);

        Assert.Equal(EvidenceW, result.Breakdown.EvidenceScore, precision: 4);
    }

    // ──────────────────────────────────────────────────────────────
    // Out-of-attempts behaviour: submissions are NOT blocked once the
    // graded attempts run out — they are accepted ungraded.
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ThreeAttempts_FourthIsAcceptedUngraded()
    {
        SetupCase(BuildFixtureCase(maxAttempts: 3));

        // Submit 3 graded attempts
        for (int i = 0; i < 3; i++)
            await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());

        // 4th attempt no longer throws — it is recorded as ungraded.
        var fourth = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());

        Assert.False(fourth.Graded);
        Assert.Equal(0, fourth.AttemptsRemaining);
    }

    [Fact]
    public async Task FirstThreeAttempts_AreGraded()
    {
        SetupCase(BuildFixtureCase(maxAttempts: 3));

        var first  = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());
        var second = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());
        var third  = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());

        Assert.True(first.Graded);
        Assert.True(second.Graded);
        Assert.True(third.Graded);
        Assert.Equal(0, third.AttemptsRemaining);
    }

    [Fact]
    public async Task CorrectAfterAttemptsExhausted_FeedbackCodeIsCorrectUngraded()
    {
        SetupCase(BuildFixtureCase(maxAttempts: 1));

        var wrongReq = new SubmitCaseRequest("suspect.other", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<AnswerDto>());
        await _sut.SubmitAsync(CaseId, UserId, wrongReq);

        var second = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());

        Assert.True(second.Correct);
        Assert.False(second.Graded);
        Assert.Equal("correct_ungraded", second.FeedbackCode);
    }

    [Fact]
    public async Task IncorrectAfterAttemptsExhausted_FeedbackCodeIsIncorrectUngraded()
    {
        SetupCase(BuildFixtureCase(maxAttempts: 1));

        var wrongReq = new SubmitCaseRequest("suspect.other", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<AnswerDto>());
        await _sut.SubmitAsync(CaseId, UserId, wrongReq);

        var second = await _sut.SubmitAsync(CaseId, UserId, wrongReq);

        Assert.False(second.Correct);
        Assert.False(second.Graded);
        Assert.Equal("incorrect_ungraded", second.FeedbackCode);
    }

    // ──────────────────────────────────────────────────────────────
    // Rookie cases: analysis category is auto-credited even when the
    // case author left requiredAnalysisIds populated.
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RookieCase_AnalysisIsAutoCredited_EvenIfRequiredIdsPresent()
    {
        var c = BuildFixtureCase();
        c.Metadata.Difficulty = "Rookie";
        // RequiredAnalysisIds left populated on purpose to verify defensive bypass
        SetupCase(c);

        var req = new SubmitCaseRequest(
            SuspectId: "suspect.marcus",
            EvidenceIds: new[] { "asset.phone_data", "asset.report" },
            AnalysisIds: Array.Empty<string>(),                  // no analyses submitted
            Answers: new[] { new AnswerDto("q1", "opt-blackmail") }
        );

        var result = await _sut.SubmitAsync(CaseId, UserId, req);

        Assert.Equal(AnalysisW, result.Breakdown.AnalysisScore, precision: 4);
        Assert.Equal(1.0, result.Score, precision: 4);
    }

    [Fact]
    public async Task NonRookieCase_AnalysisStillRequired()
    {
        var c = BuildFixtureCase();
        c.Metadata.Difficulty = "Detective";
        SetupCase(c);

        var req = new SubmitCaseRequest(
            SuspectId: "suspect.marcus",
            EvidenceIds: new[] { "asset.phone_data", "asset.report" },
            AnalysisIds: Array.Empty<string>(),
            Answers: new[] { new AnswerDto("q1", "opt-blackmail") }
        );

        var result = await _sut.SubmitAsync(CaseId, UserId, req);

        Assert.Equal(0.0, result.Breakdown.AnalysisScore, precision: 4);
    }

    // ──────────────────────────────────────────────────────────────
    // Explanation visibility rules
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Explanation_ReturnedWhenCorrect()
    {
        SetupCase(BuildFixtureCase());

        var result = await _sut.SubmitAsync(CaseId, UserId, FullyCorrectRequest());

        Assert.True(result.Correct);
        Assert.NotNull(result.ExplanationMarkdown);
        Assert.Contains("blackmail", result.ExplanationMarkdown);
    }

    [Fact]
    public async Task Explanation_NotReturnedWhenAttemptsExhausted_WrongAnswer()
    {
        // Policy change: running out of graded attempts no longer spoils the
        // explanation. The player can keep guessing ungraded; the explanation
        // is only revealed when they actually solve the case.
        SetupCase(BuildFixtureCase(maxAttempts: 1));

        var wrongReq = new SubmitCaseRequest("suspect.other", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<AnswerDto>());
        var result = await _sut.SubmitAsync(CaseId, UserId, wrongReq);

        Assert.False(result.Correct);
        Assert.Equal(0, result.AttemptsRemaining);
        Assert.Null(result.ExplanationMarkdown);
    }

    [Fact]
    public async Task Explanation_NotReturnedOnWrongWithAttemptsRemaining()
    {
        SetupCase(BuildFixtureCase(maxAttempts: 3));

        var wrongReq = new SubmitCaseRequest("suspect.other", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<AnswerDto>());
        var result = await _sut.SubmitAsync(CaseId, UserId, wrongReq);

        Assert.False(result.Correct);
        Assert.True(result.AttemptsRemaining > 0);
        Assert.Null(result.ExplanationMarkdown);
    }

    // ──────────────────────────────────────────────────────────────
    // Breakdown manual calculation check
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Breakdown_MatchesManualCalculation()
    {
        SetupCase(BuildFixtureCase());

        // culprit correct, 1 of 2 evidence, 1 of 1 analysis, wrong question
        var req = new SubmitCaseRequest(
            SuspectId: "suspect.marcus",
            EvidenceIds: new[] { "asset.phone_data" },
            AnalysisIds: new[] { "asset.phone_data:Digital" },
            Answers: new[] { new AnswerDto("q1", "opt-other") }   // wrong answer
        );

        var result = await _sut.SubmitAsync(CaseId, UserId, req);

        Assert.Equal(CulpritW,        result.Breakdown.CulpritScore,   precision: 4);
        Assert.Equal(EvidenceW * 0.5, result.Breakdown.EvidenceScore,  precision: 4);
        Assert.Equal(AnalysisW,       result.Breakdown.AnalysisScore,  precision: 4);
        Assert.Equal(0.0,             result.Breakdown.QuestionsScore, precision: 4);

        var expectedTotal = CulpritW + EvidenceW * 0.5 + AnalysisW + 0.0;
        Assert.Equal(expectedTotal, result.Score, precision: 4);
    }

    public void Dispose() => _context.Dispose();
}
