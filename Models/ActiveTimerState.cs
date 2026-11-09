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

    public int PomodoroPhase { get; set; }

    public long PomodoroDeadlineUtcTicks { get; set; }

    public int PomodoroFocusMinutes { get; set; }

    public int PomodoroBreakMinutes { get; set; }

    public bool PomodoroCountBreaks { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }

    [Ignore]
    public DateTime StartedAtUtc
    {
        get => new(StartedAtUtcTicks, DateTimeKind.Utc);
        set => StartedAtUtcTicks = value.ToUniversalTime().Ticks;
    }
}
