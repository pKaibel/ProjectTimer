using System.Collections.ObjectModel;
using ProjectTimer.Models;
using ProjectTimer.Navigation;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class ProjectSwitchViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly TimeTrackingService _tracking;
    private readonly INavigationService _navigation;
    private int _sourceProjectId;
    private string _sourceProjectName = string.Empty;

    public ProjectSwitchViewModel(DatabaseService database, TimeTrackingService tracking, INavigationService navigation)
    {
        _database = database;
        _tracking = tracking;
        _navigation = navigation;
        SelectProjectCommand = new AsyncCommand<ProjectSwitchItem>(SelectProjectAsync);
    }

    public ObservableCollection<ProjectSwitchItem> Projects { get; } = [];
    public AsyncCommand<ProjectSwitchItem> SelectProjectCommand { get; }

    public string SourceProjectName
    {
        get => _sourceProjectName;
        private set => SetProperty(ref _sourceProjectName, value);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _sourceProjectId = QueryValue.GetInt(query, "sourceProjectId");
    }

    public Task OnAppearingAsync() => RunBusyAsync(LoadAsync);

    private async Task LoadAsync()
    {
        var source = await _database.GetProjectAsync(_sourceProjectId)
            ?? throw new InvalidOperationException("Das Ausgangsprojekt wurde nicht gefunden.");
        SourceProjectName = source.Name;

        var timerStates = await _tracking.GetTimerStatesAsync();
        var projects = await _database.GetProjectsAsync();
        Projects.Clear();
        foreach (var project in projects.Where(project => project.Id != _sourceProjectId))
        {
            Projects.Add(new ProjectSwitchItem(project, timerStates.FirstOrDefault(timer => timer.ProjectId == project.Id)));
        }
    }

    private async Task SelectProjectAsync(ProjectSwitchItem target)
    {
        await RunBusyAsync(async () =>
        {
            await _tracking.SwitchAsync(_sourceProjectId, target.Id);
            await _navigation.GoBackAsync();
            await _navigation.GoToAsync(Routes.ProjectDetail, new Dictionary<string, object>
            {
                ["projectId"] = target.Id
            });
        });
    }
}

public sealed class ProjectSwitchItem
{
    public ProjectSwitchItem(Project project, ActiveTimerState? timerState)
    {
        Id = project.Id;
        Name = project.Name;
        Description = project.Description;
        IsPaused = timerState?.IsPaused == true;
    }

    public int Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool IsPaused { get; }
    public string ActionText => IsPaused ? "Fortsetzen" : "Wechseln";
    public string StateText => IsPaused ? "Pausierte Zeiterfassung fortsetzen" : "Neue Zeiterfassung starten";
}
