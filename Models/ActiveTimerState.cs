using SQLite;

namespace ProjectTimer.Models;

[Table("ActiveTimeEntry")]
public sealed class ActiveTimerState
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public int ProjectId { get; set; }

    public long StartedAtUtcTicks { get; set; }

    public long AccumulatedTicks { get; set; }

    public bool IsPaused { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [Ignore]
    public DateTime StartedAtUtc
    {
        get => new(StartedAtUtcTicks, DateTimeKind.Utc);
        set => StartedAtUtcTicks = value.ToUniversalTime().Ticks;
    }
}
