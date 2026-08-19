using SQLite;

namespace ProjectTimer.Models;

[Table("TimeEntries")]
public sealed class TimeEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public int ProjectId { get; set; }

    public long StartUtcTicks { get; set; }

    public long EndUtcTicks { get; set; }

    public long CreatedAtUtcTicks { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [Ignore]
    public DateTime StartAtUtc
    {
        get => new(StartUtcTicks, DateTimeKind.Utc);
        set => StartUtcTicks = value.ToUniversalTime().Ticks;
    }

    [Ignore]
    public DateTime EndAtUtc
    {
        get => new(EndUtcTicks, DateTimeKind.Utc);
        set => EndUtcTicks = value.ToUniversalTime().Ticks;
    }

    [Ignore]
    public DateTime Date => StartAtUtc.ToLocalTime().Date;

    [Ignore]
    public TimeSpan StartTime => StartAtUtc.ToLocalTime().TimeOfDay;

    [Ignore]
    public TimeSpan EndTime => EndAtUtc.ToLocalTime().TimeOfDay;

    [Ignore]
    public TimeSpan Duration => EndUtcTicks > StartUtcTicks
        ? TimeSpan.FromTicks(EndUtcTicks - StartUtcTicks)
        : TimeSpan.Zero;

    [Ignore]
    public DateTime CreatedAt
    {
        get => new(CreatedAtUtcTicks, DateTimeKind.Utc);
        set => CreatedAtUtcTicks = value.ToUniversalTime().Ticks;
    }
}
