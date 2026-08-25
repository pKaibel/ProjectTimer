using ProjectTimer.Formatting;
using ProjectTimer.Models;
using ProjectTimer.Navigation;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class ProjectDetailViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly TimeTrackingService _tracking;
    private readonly INavigationService _navigation;
    private readonly IUserDialogService _dialogs;
    private Project? _project;
    private ActiveTimerState? _activeTimer;
    private CancellationTokenSource? _timerCancellation;
    private int _projectId;
    private string _projectName = string.Empty;
    private string? _description;
    private string _totalDurationText = "0 min";
    private string _elapsedText = "00:00:00";
    private string _startedAtText = string.Empty;
    private string _otherTimerText = string.Empty;
    private string? _startNote;
    private bool _isTrackingHere;
    private bool _isTrackingAnotherProject;
    private bool _isPaused;
    private bool _isObservingTimerPause;
    private string _timerStatusText = "Zeiterfassung läuft";

    public ProjectDetailViewModel(
        DatabaseService database,
        TimeTrackingService tracking,
        INavigationService navigation,
        IUserDialogService dialogs)
    {
        _database = database;
        _tracking = tracking;
        _navigation = navigation;
        _dialogs = dialogs;
        StartTimerCommand = new AsyncCommand(StartTimerAsync, () => CanStartTimer);
        StopTimerCommand = new AsyncCommand(StopTimerAsync, () => IsTrackingHere);
        PauseTimerCommand = new AsyncCommand(PauseTimerAsync, () => IsTrackingHere && !IsPaused);
        ResumeTimerCommand = new AsyncCommand(ResumeTimerAsync, () => IsTrackingHere && IsPaused);
        SwitchProjectCommand = new AsyncCommand(OpenProjectSwitchAsync, () => IsRunning);
        AddManualCommand = new AsyncCommand(AddManualAsync);
        ShowEntriesCommand = new AsyncCommand(ShowEntriesAsync);
        EditProjectCommand = new AsyncCommand(EditProjectAsync);
        DeleteProjectCommand = new AsyncCommand(DeleteProjectAsync);
    }

    public string ProjectName
    {
        get => _projectName;
        private set => SetProperty(ref _projectName, value);
    }

    public string? Description
    {
        get => _description;
        private set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(HasDescription));
            }
        }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string TotalDurationText
    {
        get => _totalDurationText;
        private set => SetProperty(ref _totalDurationText, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public string StartedAtText
    {
        get => _startedAtText;
        private set => SetProperty(ref _startedAtText, value);
    }

    public string OtherTimerText
    {
        get => _otherTimerText;
        private set => SetProperty(ref _otherTimerText, value);
    }

    public string? StartNote
    {
        get => _startNote;
        set => SetProperty(ref _startNote, value);
    }

    public bool IsTrackingHere
    {
        get => _isTrackingHere;
        private set
        {
            if (SetProperty(ref _isTrackingHere, value))
            {
                OnPropertyChanged(nameof(CanStartTimer));
                OnPropertyChanged(nameof(IsRunning));
                SwitchProjectCommand.RaiseCanExecuteChanged();
                StartTimerCommand.RaiseCanExecuteChanged();
                StopTimerCommand.RaiseCanExecuteChanged();
                PauseTimerCommand.RaiseCanExecuteChanged();
                ResumeTimerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsTrackingAnotherProject
    {
        get => _isTrackingAnotherProject;
        private set
        {
            if (SetProperty(ref _isTrackingAnotherProject, value))
            {
                OnPropertyChanged(nameof(CanStartTimer));
                StartTimerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                PauseTimerCommand.RaiseCanExecuteChanged();
                ResumeTimerCommand.RaiseCanExecuteChanged();
                SwitchProjectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string TimerStatusText
    {
        get => _timerStatusText;
        private set => SetProperty(ref _timerStatusText, value);
    }

    public bool CanStartTimer => !IsTrackingHere && !IsTrackingAnotherProject;
    public bool IsRunning => IsTrackingHere && !IsPaused;
    public AsyncCommand StartTimerCommand { get; }
    public AsyncCommand StopTimerCommand { get; }
    public AsyncCommand PauseTimerCommand { get; }
    public AsyncCommand ResumeTimerCommand { get; }
    public AsyncCommand SwitchProjectCommand { get; }
    public AsyncCommand AddManualCommand { get; }
    public AsyncCommand ShowEntriesCommand { get; }
    public AsyncCommand EditProjectCommand { get; }
    public AsyncCommand DeleteProjectCommand { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _projectId = QueryValue.GetInt(query, "projectId");
    }

    public async Task OnAppearingAsync()
    {
        await LoadAsync();
        StartObservingTimerPause();
        StartDisplayTimer();
    }

    public void OnDisappearing()
    {
        StopObservingTimerPause();
        _timerCancellation?.Cancel();
        _timerCancellation?.Dispose();
        _timerCancellation = null;
    }

    private Task LoadAsync() => RunBusyAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        if (_projectId <= 0)
        {
            throw new InvalidOperationException("Es wurde kein gültiges Projekt ausgewählt.");
        }

        _project = await _database.GetProjectAsync(_projectId)
            ?? throw new InvalidOperationException("Das Projekt wurde nicht gefunden.");
        ProjectName = _project.Name;
        Description = _project.Description;
        TotalDurationText = DurationFormatter.FormatLong(await _database.GetTotalDurationAsync(_projectId));
        await RefreshActiveTimerAsync();
    }

    private async Task RefreshActiveTimerAsync()
    {
        _activeTimer = await _tracking.GetTimerForProjectAsync(_projectId);
        var runningTimer = await _tracking.GetActiveTimerAsync();
        IsTrackingHere = _activeTimer is not null;
        IsTrackingAnotherProject = runningTimer is not null && runningTimer.ProjectId != _projectId;
        IsPaused = IsTrackingHere && _activeTimer?.IsPaused == true;

        if (IsTrackingHere && _activeTimer is not null)
        {
            TimerStatusText = IsPaused ? "Zeiterfassung pausiert" : "Zeiterfassung läuft";
            StartedAtText = IsPaused
                ? "Die bisherige Arbeitszeit ist gespeichert."
                : $"Gestartet um {_activeTimer.StartedAtUtc.ToLocalTime():HH:mm} Uhr";
            UpdateElapsed();
        }
        else
        {
            ElapsedText = "00:00:00";
            StartedAtText = string.Empty;
            TimerStatusText = "Zeiterfassung läuft";
        }

        if (IsTrackingAnotherProject && runningTimer is not null)
        {
            var activeProject = await _database.GetProjectAsync(runningTimer.ProjectId);
            OtherTimerText = activeProject is null
                ? "Für ein anderes Projekt läuft bereits eine Zeiterfassung."
                : $"Für „{activeProject.Name}“ läuft bereits eine Zeiterfassung.";
        }
        else
        {
            OtherTimerText = string.Empty;
        }
    }

    private async Task StartTimerAsync()
    {
        await RunBusyAsync(async () =>
        {
            _activeTimer = await _tracking.StartAsync(_projectId, StartNote);
            StartNote = null;
            await RefreshActiveTimerAsync();
            StartDisplayTimer();
        });
    }

    private async Task StopTimerAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _tracking.StopAsync(_projectId);
            await LoadCoreAsync();
        });
    }

    private async Task PauseTimerAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _tracking.PauseAsync(_projectId);
            await LoadCoreAsync();
        });
    }

    private async Task ResumeTimerAsync()
    {
        await RunBusyAsync(async () =>
        {
            var runningTimer = await _tracking.GetActiveTimerAsync();
            _activeTimer = runningTimer is not null && runningTimer.ProjectId != _projectId
                ? await _tracking.SwitchAsync(runningTimer.ProjectId, _projectId)
                : await _tracking.ResumeAsync(_projectId);
            await RefreshActiveTimerAsync();
            StartDisplayTimer();
        });
    }

    private Task OpenProjectSwitchAsync()
    {
        return _navigation.GoToAsync(Navigation.Routes.ProjectSwitch, new Dictionary<string, object>
        {
            ["sourceProjectId"] = _projectId
        });
    }

    private Task AddManualAsync()
    {
        return _navigation.GoToAsync(Navigation.Routes.TimeEntryEditor, new Dictionary<string, object>
        {
            ["projectId"] = _projectId
        });
    }

    private Task ShowEntriesAsync()
    {
        return _navigation.GoToAsync(Navigation.Routes.TimeEntryList, new Dictionary<string, object>
        {
            ["projectId"] = _projectId
        });
    }

    private Task EditProjectAsync()
    {
        return _navigation.GoToAsync(Navigation.Routes.ProjectEditor, new Dictionary<string, object>
        {
            ["projectId"] = _projectId
        });
    }

    private async Task DeleteProjectAsync()
    {
        if (_project is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Projekt löschen",
            $"„{_project.Name}“ und alle zugehörigen Zeiteinträge wirklich löschen?");
        if (!confirmed)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _database.DeleteProjectAsync(_project);
            await _navigation.GoBackAsync();
        });
    }

    private void StartDisplayTimer()
    {
        _timerCancellation?.Cancel();
        _timerCancellation?.Dispose();
        _timerCancellation = new CancellationTokenSource();
        _ = RunDisplayTimerAsync(_timerCancellation.Token);
    }

    private void StartObservingTimerPause()
    {
        if (_isObservingTimerPause)
        {
            return;
        }

        _tracking.RunningTimerPaused += OnRunningTimerPaused;
        _isObservingTimerPause = true;
    }

    private void StopObservingTimerPause()
    {
        if (!_isObservingTimerPause)
        {
            return;
        }

        _tracking.RunningTimerPaused -= OnRunningTimerPaused;
        _isObservingTimerPause = false;
    }

    private void OnRunningTimerPaused(object? sender, EventArgs e)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_projectId <= 0)
            {
                return;
            }

            try
            {
                await RefreshActiveTimerAsync();
            }
            catch
            {
                // The next navigation or refresh reloads the persisted timer state.
            }
        });
    }

    private async Task RunDisplayTimerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (IsTrackingHere && !IsPaused)
                {
                    UpdateElapsed();
                }

                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The page stopped displaying the timer.
        }
    }

    private void UpdateElapsed()
    {
        if (_activeTimer is not null)
        {
            ElapsedText = DurationFormatter.FormatTimer(TimeTrackingService.GetElapsed(_activeTimer));
        }
    }
}
