using ProjectTimer.Formatting;
using ProjectTimer.Models;

namespace ProjectTimer.ViewModels;

public sealed class ProjectListItemViewModel
{
    public ProjectListItemViewModel(ProjectTotal project, ActiveTimerState? activeTimer)
    {
        Id = project.Id;
        Name = project.Name;
        Description = project.Description;
        TotalDuration = TimeSpan.FromTicks(Math.Max(0, project.TotalTicks));
        IsTimerActive = activeTimer?.ProjectId == project.Id;
        IsTimerPaused = IsTimerActive && activeTimer?.IsPaused == true;
    }

    public int Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public TimeSpan TotalDuration { get; }
    public string TotalDurationText => DurationFormatter.Format(TotalDuration);
    public bool IsTimerActive { get; }
    public bool IsTimerPaused { get; }
    public string TimerStatusText => IsTimerPaused ? "●  Zeiterfassung pausiert" : "●  Zeiterfassung läuft";
}
