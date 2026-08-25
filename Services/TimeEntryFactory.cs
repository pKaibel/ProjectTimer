using ProjectTimer.Models;

namespace ProjectTimer.Services;

public sealed class TimeEntryFactory
{
    public TimeEntry CreateManual(
        int projectId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        string? note,
        int id = 0,
        DateTime? createdAtUtc = null)
    {
        if (projectId <= 0)
        {
            throw new ArgumentException("Das Projekt ist ungültig.");
        }

        if (startTime < TimeSpan.Zero || startTime >= TimeSpan.FromDays(1)
            || endTime < TimeSpan.Zero || endTime >= TimeSpan.FromDays(1))
        {
            throw new ArgumentException("Start- und Endzeit müssen gültige Uhrzeiten sein.");
        }

        if (endTime <= startTime)
        {
            throw new ArgumentException("Die Endzeit muss nach der Startzeit liegen.");
        }

        var localStart = DateTime.SpecifyKind(date.Date.Add(startTime), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(date.Date.Add(endTime), DateTimeKind.Unspecified);

        if (TimeZoneInfo.Local.IsInvalidTime(localStart) || TimeZoneInfo.Local.IsInvalidTime(localEnd))
        {
            throw new ArgumentException("Die gewählte Uhrzeit existiert wegen der Zeitumstellung nicht.");
        }

        var entry = new TimeEntry
        {
            Id = id,
            ProjectId = projectId,
            Note = NormalizeNote(note),
            StartAtUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local),
            EndAtUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, TimeZoneInfo.Local),
            CreatedAt = createdAtUtc ?? DateTime.UtcNow
        };

        Validate(entry);
        return entry;
    }

    public TimeEntry CreateTracked(ActiveTimerState activeTimer, DateTime endedAtUtc)
    {
        var entry = new TimeEntry
        {
            ProjectId = activeTimer.ProjectId,
            Note = NormalizeNote(activeTimer.Note),
            StartAtUtc = activeTimer.StartedAtUtc,
            EndAtUtc = endedAtUtc.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow
        };

        Validate(entry);
        return entry;
    }

    public void Validate(TimeEntry entry)
    {
        if (entry.ProjectId <= 0)
        {
            throw new ArgumentException("Das Projekt ist ungültig.");
        }

        if (entry.StartUtcTicks <= 0 || entry.EndUtcTicks <= entry.StartUtcTicks)
        {
            throw new ArgumentException("Der Zeitraum ist ungültig.");
        }

        if (entry.Duration > TimeSpan.FromDays(366))
        {
            throw new ArgumentException("Der Zeitraum ist ungewöhnlich lang und kann nicht gespeichert werden.");
        }

        if (entry.Note?.Length > 2000)
        {
            throw new ArgumentException("Die Notiz darf höchstens 2.000 Zeichen lang sein.");
        }
    }

    public string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        if (trimmed?.Length > 2000)
        {
            throw new ArgumentException("Die Notiz darf höchstens 2.000 Zeichen lang sein.");
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
