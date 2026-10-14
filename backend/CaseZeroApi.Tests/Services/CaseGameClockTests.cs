using CaseZeroApi.Models;
using CaseZeroApi.Services;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class CaseGameClockTests
{
    [Fact]
    public void CalendarDateDoesNotMakeAnAnalysisInstantlyDue()
    {
        var time = new TestGameTimeProvider();
        var clock = new CaseGameClock(time);
        var session = new CaseSession();
        clock.Initialize(session, new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc));
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(15, clock.Read(session).ElapsedMinutes);
        Assert.Equal(new DateTime(2024, 1, 1, 8, 15, 0, DateTimeKind.Utc), clock.Read(session).GameTime);
    }

    [Fact]
    public void PauseAndResume_PreserveElapsedTimeWithoutOfflineCatchup()
    {
        var time = new TestGameTimeProvider();
        var clock = new CaseGameClock(time);
        var session = new CaseSession();
        clock.Initialize(session, time.GetUtcNow().UtcDateTime);
        time.Advance(TimeSpan.FromSeconds(12));
        clock.Capture(session);
        session.Status = SessionStatus.Paused;
        time.Advance(TimeSpan.FromDays(3));
        Assert.Equal(12, clock.Read(session).ElapsedMinutes);
        clock.Resume(session);
        time.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal(20, clock.Read(session).ElapsedMinutes);
    }

    [Fact]
    public void CaptureAndRestore_PersistClockExactlyAcrossInstances()
    {
        var time = new TestGameTimeProvider();
        var session = new CaseSession();
        new CaseGameClock(time).Initialize(session, time.GetUtcNow().UtcDateTime);
        time.Advance(TimeSpan.FromSeconds(10.5));
        new CaseGameClock(time).Capture(session);
        Assert.Equal(10.5, new CaseGameClock(time).Read(session).ElapsedMinutes);
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(12.5, new CaseGameClock(time).Read(session).ElapsedMinutes);
    }

    [Fact]
    public void ExistingSavedDates_KeepTheirElapsedMinutesAndLegacyDeadlineBaseline()
    {
        var time = new TestGameTimeProvider();
        var session = new CaseSession
        {
            Status = SessionStatus.Paused, GameTimeAtStart = "2025-01-01T08:00:00Z",
            GameTimeAtEnd = "2025-01-01T10:30:00Z"
        };
        var clock = new CaseGameClock(time);
        clock.Initialize(session, new DateTime(2024, 2, 1, 8, 0, 0, DateTimeKind.Utc));
        Assert.Equal(150, clock.Read(session).ElapsedMinutes);
        Assert.Equal(new DateTime(2025, 1, 1, 10, 30, 0, DateTimeKind.Utc), clock.Read(session).GameTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void InvalidClockDates_AreNotSilentlyReset(string value) =>
        Assert.Throws<InvalidDataException>(() => CaseGameClock.Parse(value));

    [Fact]
    public void ReinitializingAnActiveClock_DoesNotResetProgress()
    {
        var time = new TestGameTimeProvider();
        var clock = new CaseGameClock(time);
        var session = new CaseSession();
        clock.Initialize(session, time.GetUtcNow().UtcDateTime);
        time.Advance(TimeSpan.FromSeconds(9));
        clock.Initialize(session, time.GetUtcNow().UtcDateTime.AddYears(1));
        Assert.Equal(9, clock.Read(session).ElapsedMinutes);
    }

    [Fact]
    public void ResumingAnActiveClock_IsIdempotentAndDoesNotDiscardElapsedMinutes()
    {
        var time = new TestGameTimeProvider();
        var clock = new CaseGameClock(time);
        var session = new CaseSession();
        clock.Initialize(session, time.GetUtcNow().UtcDateTime);
        time.Advance(TimeSpan.FromSeconds(9));
        clock.Resume(session);
        Assert.Equal(9, clock.Read(session).ElapsedMinutes);
    }

    [Fact]
    public void CompletedClock_CannotBeReactivated()
    {
        var clock = new CaseGameClock(new TestGameTimeProvider());
        var session = new CaseSession { Status = SessionStatus.Completed };
        Assert.Throws<InvalidDataException>(() => clock.Resume(session));
        Assert.Equal(SessionStatus.Completed, session.Status);
    }
}

internal sealed class TestGameTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 13, 20, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan span) => now += span;
}
