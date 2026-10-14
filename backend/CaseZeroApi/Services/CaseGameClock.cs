using System.Globalization;
using CaseZeroApi.Models;

namespace CaseZeroApi.Services;

public sealed record GameClockSnapshot(DateTime GameTime, double ElapsedMinutes, bool IsRunning, int Multiplier, DateTime AsOfUtc);

public sealed class CaseGameClock(TimeProvider timeProvider)
{
    public const int Multiplier = 60;

    public void Initialize(CaseSession session, DateTime openedAt)
    {
        if (session.GameClockUpdatedAt is not null) return;
        var elapsed = session.GameTimeMinutes;
        // Legacy sessions store a saved calendar clock, not accumulated game minutes.
        if (!string.IsNullOrWhiteSpace(session.GameTimeAtEnd) && !string.IsNullOrWhiteSpace(session.GameTimeAtStart))
            elapsed = Math.Max(elapsed, (Parse(session.GameTimeAtEnd) - Parse(session.GameTimeAtStart)).TotalMinutes);
        session.GameTimeMinutes = Math.Max(0, elapsed);
        var baseline = string.IsNullOrWhiteSpace(session.GameTimeAtStart) ? openedAt.ToUniversalTime() : Parse(session.GameTimeAtStart);
        session.GameTimeAtStart = baseline.ToString("o");
        session.GameClockUpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public GameClockSnapshot Read(CaseSession session)
    {
        if (session.GameClockUpdatedAt is null)
            throw new InvalidDataException("The session game clock has not been initialized.");
        if (!double.IsFinite(session.GameTimeMinutes) || session.GameTimeMinutes < 0)
            throw new InvalidDataException("The session game clock has invalid elapsed minutes.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var running = session.Status == SessionStatus.Active;
        var elapsed = session.GameTimeMinutes +
            (running ? Math.Max(0, (now - session.GameClockUpdatedAt.Value).TotalMinutes) * Multiplier : 0);
        return new(Parse(session.GameTimeAtStart).AddMinutes(elapsed), elapsed, running, Multiplier, now);
    }

    public GameClockSnapshot Capture(CaseSession session)
    {
        var snapshot = Read(session);
        session.GameTimeMinutes = snapshot.ElapsedMinutes;
        session.GameClockUpdatedAt = snapshot.AsOfUtc;
        session.GameTimeAtEnd = snapshot.GameTime.ToString("o");
        return snapshot;
    }

    public void Resume(CaseSession session)
    {
        if (session.Status == SessionStatus.Active) return;
        if (session.Status != SessionStatus.Paused)
            throw new InvalidDataException("Only a paused session can resume its game clock.");
        session.GameClockUpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        session.Status = SessionStatus.Active;
        session.IsActive = true;
        session.SessionEnd = null;
    }

    public static DateTime Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            throw new InvalidDataException("The case/session game-clock date is invalid.");
        return parsed.UtcDateTime;
    }
}
