namespace ProjectTimer.Models;

public sealed class ProjectTotal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsQuickAccess { get; set; }
    public int QuickAccessOrder { get; set; }
    public bool IsArchived { get; set; }
    public long CreatedAtUtcTicks { get; set; }
    public long TotalTicks { get; set; }
}
