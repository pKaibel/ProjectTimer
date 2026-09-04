using ProjectTimer.Formatting;
using ProjectTimer.Models;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class ProjectListItemViewModel : BaseViewModel
{
    private readonly ActiveTimerState? _activeTimer;
    private string _currentDurationText = "00:00:00";

    public ProjectListItemViewModel(ProjectTotal project, ActiveTimerState? activeTimer)
    {
        Id = project.Id;
        Name = project.Name;
        Description = project.Description;
        _isQuickAccess = project.IsQuickAccess;
        QuickAccessOrder = project.QuickAccessOrder;
        IsArchived = project.IsArchived;
        TotalDuration = TimeSpan.FromTicks(Math.Max(0, project.TotalTicks));
        IsTimerActive = activeTimer?.ProjectId == project.Id;
        IsTimerPaused = IsTimerActive && activeTimer?.IsPaused == true;
        _activeTimer = IsTimerActive ? activeTimer : null;
        UpdateTimerDisplay();
    }

    public int Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public int QuickAccessOrder { get; }
    public bool IsArchived { get; }
    public TimeSpan TotalDuration { get; }
    public string TotalDurationText => DurationFormatter.Format(TotalDuration);
    public bool IsTimerActive { get; }
    public bool IsTimerPaused { get; }
    public bool IsTimerRunning => IsTimerActive && !IsTimerPaused;
    public bool IsTimerInactive => !IsTimerActive;
    public string QuickAccessStatusText => IsTimerActive ? TimerStatusText : "Bereit zur Zeiterfassung";
    public string CurrentDurationText
    {
        get => _currentDurationText;
        private set
        {
            if (SetProperty(ref _currentDurationText, value))
            {
                OnPropertyChanged(nameof(CurrentDurationDisplayText));
            }
        }
    }
    public string CurrentDurationDisplayText => IsTimerRunning ? $"Aktuell: {CurrentDurationText}" : string.Empty;
    public string TimerButtonText => IsTimerRunning ? "Stoppen" : IsTimerPaused ? "Zeiterfassung fortsetzen" : "Zeiterfassung starten";
    private bool _isQuickAccess;
    public bool IsQuickAccess
    {
        get => _isQuickAccess;
        set => SetProperty(ref _isQuickAccess, value);
    }
    public string TimerStatusText => IsTimerPaused ? "●  Zeiterfassung pausiert" : "●  Zeiterfassung läuft";

    public void UpdateTimerDisplay()
    {
        if (_activeTimer is not null && IsTimerRunning)
        {
            CurrentDurationText = DurationFormatter.FormatTimer(TimeTrackingService.GetElapsed(_activeTimer));
        }
    }
}
