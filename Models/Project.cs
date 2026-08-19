using SQLite;

namespace ProjectTimer.Models;

[Table("Projects")]
public sealed class Project
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(200), NotNull]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public long CreatedAtUtcTicks { get; set; }

    [Ignore]
    public DateTime CreatedAt
    {
        get => new(CreatedAtUtcTicks, DateTimeKind.Utc);
        set => CreatedAtUtcTicks = value.ToUniversalTime().Ticks;
    }
}
