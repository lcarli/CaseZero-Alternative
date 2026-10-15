using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class PromotionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PromotionService _sut;
    private const string UserId = "user-promo-test";

    public PromotionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _sut = new PromotionService(_context, NullLogger<PromotionService>.Instance);

        _context.Users.Add(new User
        {
            Id = UserId,
            UserName = "promo@fic-police.gov",
            FirstName = "Promo",
            LastName = "Tester",
            PersonalEmail = "promo@example.com",
            Rank = DetectiveRank.Rook
        });
        _context.SaveChanges();
    }

    private void SeedResolvedCases(int count, bool graded = true, double score = 90.0)
    {
        for (var i = 0; i < count; i++)
        {
            _context.CaseSubmissions.Add(new CaseSubmission
            {
                CaseId = $"case-{i}",
                SubmittedByUserId = UserId,
                SuspectName = "x",
                KeyEvidenceDescription = "x",
                Reasoning = "x",
                IsCorrectSuspect = true,
                Graded = graded,
                Score = score
            });
        }
        _context.SaveChanges();
    }

    [Fact]
    public async Task NoResolves_StaysRook_NotPromoted()
    {
        var outcome = await _sut.SyncAsync(UserId);

        Assert.False(outcome.Promoted);
        Assert.Equal(DetectiveRank.Rook, outcome.NewRank);
        Assert.Equal(0, outcome.CasesResolved);
    }

    [Fact]
    public async Task ThreeResolves_PromotesToDetective_WithHistoryAndEmail()
    {
        SeedResolvedCases(3);

        var outcome = await _sut.SyncAsync(UserId);

        Assert.True(outcome.Promoted);
        Assert.Equal(DetectiveRank.Rook, outcome.PreviousRank);
        Assert.Equal(DetectiveRank.Detective, outcome.NewRank);

        var user = await _context.Users.FindAsync(UserId);
        Assert.Equal(DetectiveRank.Detective, user!.Rank);
        Assert.Equal(3, user.CasesResolved);
        Assert.NotNull(user.LastPromotionDate);

        var history = await _context.UserRankHistories.Where(h => h.UserId == UserId).ToListAsync();
        Assert.Single(history);
        Assert.Equal(DetectiveRank.Detective, history[0].NewRank);

        var email = await _context.Emails.SingleAsync(e => e.ToUserId == UserId);
        Assert.Equal(EmailType.PromotionNotice, email.Type);
        Assert.Null(email.CaseId);
        Assert.Contains("Detective", email.MetadataJson);
    }

    [Fact]
    public async Task MultiLevelJump_AddsOneHistoryRowPerLevel()
    {
        SeedResolvedCases(8); // threshold for Detective2

        var outcome = await _sut.SyncAsync(UserId);

        Assert.Equal(DetectiveRank.Detective2, outcome.NewRank);
        var history = await _context.UserRankHistories
            .Where(h => h.UserId == UserId)
            .OrderBy(h => h.NewRank)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(DetectiveRank.Detective, history[0].NewRank);
        Assert.Equal(DetectiveRank.Detective2, history[1].NewRank);
    }

    [Fact]
    public async Task SyncIsIdempotent_NoDuplicatePromotionOnSecondCall()
    {
        SeedResolvedCases(3);

        var first = await _sut.SyncAsync(UserId);
        var second = await _sut.SyncAsync(UserId);

        Assert.True(first.Promoted);
        Assert.False(second.Promoted);

        Assert.Single(await _context.UserRankHistories.Where(h => h.UserId == UserId).ToListAsync());
        Assert.Single(await _context.Emails.Where(e => e.ToUserId == UserId).ToListAsync());
    }

    [Fact]
    public async Task UngradedResolves_DoNotCountTowardPromotion()
    {
        SeedResolvedCases(3, graded: false);

        var outcome = await _sut.SyncAsync(UserId);

        Assert.False(outcome.Promoted);
        Assert.Equal(0, outcome.CasesResolved);
    }

    [Fact]
    public async Task ComputesFailedAndSuccessRate()
    {
        // 2 resolved cases + 1 failed (graded, incorrect) = 3 attempted.
        SeedResolvedCases(2);
        _context.CaseSubmissions.Add(new CaseSubmission
        {
            CaseId = "case-failed",
            SubmittedByUserId = UserId,
            SuspectName = "x",
            KeyEvidenceDescription = "x",
            Reasoning = "x",
            IsCorrectSuspect = false,
            Graded = true,
            Score = 20.0
        });
        _context.SaveChanges();

        await _sut.SyncAsync(UserId);

        var user = await _context.Users.FindAsync(UserId);
        Assert.Equal(2, user!.CasesResolved);
        Assert.Equal(1, user.CasesFailed);
        Assert.Equal(66.7, user.SuccessRate);
    }

    public void Dispose() => _context.Dispose();
}
