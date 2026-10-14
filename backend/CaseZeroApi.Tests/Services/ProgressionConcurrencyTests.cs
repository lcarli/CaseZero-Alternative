using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class ProgressionConcurrencyTests
{
    [Fact]
    public async Task StaleSessionWriterCannotDiscardObservedEventsOrAdvanceTheClock()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var first = new ApplicationDbContext(options);
        await first.Database.EnsureCreatedAsync();
        first.Users.Add(new User { Id = "player", UserName = "player", FirstName = "Test", LastName = "Player", PersonalEmail = "player@example.invalid" });
        first.CaseSessions.Add(new() { UserId = "player", CaseId = "case_concurrency" });
        await first.SaveChangesAsync();
        await using var second = new ApplicationDbContext(options);
        var original = await first.CaseSessions.SingleAsync();
        var stale = await second.CaseSessions.SingleAsync();
        original.ObservedTriggers = """[{"Type":"asset_viewed","AssetId":"asset.real"}]""";
        original.GameTimeMinutes = 15;
        await first.SaveChangesAsync();
        stale.GameTimeMinutes = 1;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        first.ChangeTracker.Clear();
        var persisted = await first.CaseSessions.SingleAsync();
        Assert.Equal(15, persisted.GameTimeMinutes);
        Assert.Contains("asset.real", persisted.ObservedTriggers);
        Assert.Equal(1, persisted.StateVersion);
    }

    [Fact]
    public async Task ConcurrentForensicCompletionCannotOverwriteCancellation()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var first = new ApplicationDbContext(options);
        await first.Database.EnsureCreatedAsync();
        first.Users.Add(new User { Id = "player", UserName = "player", FirstName = "Test", LastName = "Player", PersonalEmail = "player@example.invalid" });
        first.ForensicRequests.Add(new() { UserId = "player", CaseId = "case_concurrency" });
        await first.SaveChangesAsync();
        await using var second = new ApplicationDbContext(options);
        var cancel = await first.ForensicRequests.SingleAsync();
        var complete = await second.ForensicRequests.SingleAsync();
        cancel.Status = "cancelled";
        await first.SaveChangesAsync();
        complete.Status = "completed";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        first.ChangeTracker.Clear();
        Assert.Equal("cancelled", (await first.ForensicRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task StaleForensicCompletion_RollsBackResultsAndRevealsTogetherThenCanRetry()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var first = new ApplicationDbContext(options);
        await first.Database.EnsureCreatedAsync();
        first.Users.Add(new User { Id = "player", UserName = "player", FirstName = "Test", LastName = "Player", PersonalEmail = "player@example.invalid" });
        var time = new TestGameTimeProvider();
        var clock = new CaseGameClock(time);
        const string openedAt = "2024-01-01T08:00:00Z";
        var session = new CaseSession { UserId = "player", CaseId = "case_concurrency" };
        clock.Initialize(session, CaseGameClock.Parse(openedAt));
        var request = new ForensicRequest
        {
            UserId = "player", CaseId = session.CaseId, InputAssetId = "asset.input", AnalysisType = "Digital",
            RequestedAt = clock.Read(session).GameTime, EstimatedCompletionTime = clock.Read(session).GameTime.AddMinutes(15)
        };
        first.CaseSessions.Add(session);
        first.ForensicRequests.Add(request);
        await first.SaveChangesAsync();
        await using var worker = new ApplicationDbContext(options);
        await worker.CaseSessions.SingleAsync();
        await worker.ForensicRequests.SingleAsync();
        session.ObservedTriggers = """[{"Type":"email_opened","EmailId":"email.previous"}]""";
        await first.SaveChangesAsync();
        var caseData = new CaseV2
        {
            CaseId = session.CaseId, Metadata = new() { OpenedAt = openedAt, UnlockMode = "gated" },
            Assets = new() { new() { Id = "asset.input", Type = "digital" }, new() { Id = "asset.report" } },
            Emails = new() { new() { Id = "email.lab" } },
            ForensicsDefaults = new() { AnalysisTypes = new() { new() { Type = "Digital", DurationMinutes = 15, AvailableFor = new() { "digital" } } } },
            ForensicOutcomes = new() { new() { InputAssetId = "asset.input", AnalysisType = "Digital", Findings = true, ResultAssetId = "asset.report", ResultEmailId = "email.lab" } }
        };
        var storage = new Mock<ICaseV2StorageService>();
        storage.Setup(s => s.GetRawAsync(session.CaseId, It.IsAny<CancellationToken>())).ReturnsAsync(caseData);
        storage.Setup(s => s.GetAssetStreamAsync(session.CaseId, "asset.report", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("%PDF-1.7\nsynthetic test\n%%EOF"u8.ToArray()));
        var rules = new RulesEngineService(worker, storage.Object, NullLogger<RulesEngineService>.Instance);
        var processor = new ForensicRequestProcessor(worker, storage.Object, rules, clock, NullLogger<ForensicRequestProcessor>.Instance);
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.False(await processor.ProcessAsync(request.Id));
        first.ChangeTracker.Clear();
        Assert.Equal("pending", (await first.ForensicRequests.SingleAsync()).Status);
        Assert.Empty(await first.CaseSessionVisibleAssets.ToListAsync());
        Assert.Empty(await first.CaseSessionVisibleEmails.ToListAsync());
        Assert.Contains("email.previous", (await first.CaseSessions.SingleAsync()).ObservedTriggers);
        Assert.True(await processor.ProcessAsync(request.Id));
        first.ChangeTracker.Clear();
        Assert.Equal("completed", (await first.ForensicRequests.SingleAsync()).Status);
        Assert.Single(await first.CaseSessionVisibleAssets.ToListAsync());
        Assert.Single(await first.CaseSessionVisibleEmails.ToListAsync());
    }
}
