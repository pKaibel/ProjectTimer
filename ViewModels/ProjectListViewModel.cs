using System.Collections.ObjectModel;
using ProjectTimer.Navigation;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class ProjectListViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly TimeOverviewService _overview;
    private readonly OverviewSettingsService _overviewSettings;
    private readonly TimeTrackingService _timeTracking;
    private readonly INavigationService _navigation;
    private CancellationTokenSource? _displayTimerCancellation;

    public ProjectListViewModel(DatabaseService database, TimeOverviewService overview, OverviewSettingsService overviewSettings, TimeTrackingService timeTracking, INavigationService navigation)
    {
        _database = database;
        _overview = overview;
        _overviewSettings = overviewSettings;
        _timeTracking = timeTracking;
        _navigation = navigation;
        AddProjectCommand = new AsyncCommand(AddProjectAsync);
        OpenSettingsCommand = new AsyncCommand(OpenSettingsAsync);
        OpenInfoCommand = new AsyncCommand(OpenInfoAsync);
        OpenOverviewCommand = new AsyncCommand(OpenOverviewAsync);
        OpenProjectCommand = new AsyncCommand<ProjectListItemViewModel>(OpenProjectAsync);
        RefreshCommand = new AsyncCommand(LoadAsync);
    }

    public ObservableCollection<ProjectListItemViewModel> Projects { get; } = [];
    public ObservableCollection<ProjectListItemViewModel> QuickAccessProjects { get; } = [];
    public ObservableCollection<TimeChartBar> WeeklyBars { get; } = [];
    private string _weeklyTotalText = "0 Stunden 0 Minuten";
    public string WeeklyTotalText
    {
        get => _weeklyTotalText;
        private set => SetProperty(ref _weeklyTotalText, value);
    }
    public bool IsEmpty => Projects.Count == 0 && !IsBusy;
    public bool HasQuickAccessProjects => QuickAccessProjects.Count > 0;
    public AsyncCommand AddProjectCommand { get; }
    public AsyncCommand OpenSettingsCommand { get; }
    public AsyncCommand OpenInfoCommand { get; }
    public AsyncCommand OpenOverviewCommand { get; }
    public AsyncCommand<ProjectListItemViewModel> OpenProjectCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(LoadCoreAsync);
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void StartDisplayTimer()
    {
        _displayTimerCancellation?.Cancel();
        _displayTimerCancellation?.Dispose();
        _displayTimerCancellation = new CancellationTokenSource();
        _ = RunDisplayTimerAsync(_displayTimerCancellation.Token);
    }

    public void StopDisplayTimer()
    {
        _displayTimerCancellation?.Cancel();
        _displayTimerCancellation?.Dispose();
        _displayTimerCancellation = null;
    }

    public async Task SetProjectQuickAccessAsync(ProjectListItemViewModel project, bool isQuickAccess)
    {
        try
        {
            await _database.SetProjectQuickAccessAsync(project.Id, isQuickAccess);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            project.IsQuickAccess = !isQuickAccess;
            ErrorMessage = exception.Message;
        }
    }

    public async Task ToggleQuickAccessTimerAsync(ProjectListItemViewModel project)
    {
        try
        {
            if (project.IsTimerRunning)
            {
                await _timeTracking.StopAsync(project.Id);
            }
            else
            {
                await _timeTracking.StartOrResumeReplacingRunningTimerAsync(project.Id);
            }

            await LoadAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task LoadCoreAsync()
    {
        var projects = await _database.GetProjectsWithTotalsAsync();
        var timerStates = await _database.GetTimerStatesAsync();
        var overview = await _overview.GetOverviewAsync(OverviewPeriod.Week, _overviewSettings.ShowWeekendsOnStartPage);
        Projects.Clear();
        QuickAccessProjects.Clear();
        WeeklyBars.Clear();
        foreach (var bar in overview.Bars)
        {
            WeeklyBars.Add(bar);
        }
        WeeklyTotalText = overview.TotalDurationText;
        var items = projects
            .Select(project => new ProjectListItemViewModel(project, timerStates.FirstOrDefault(timer => timer.ProjectId == project.Id)))
            .ToList();
        foreach (var item in items)
        {
            Projects.Add(item);
        }
        foreach (var item in items.Where(item => item.IsQuickAccess).OrderBy(item => item.QuickAccessOrder).ThenBy(item => item.Id))
        {
            QuickAccessProjects.Add(item);
        }

        OnPropertyChanged(nameof(HasQuickAccessProjects));
    }

    private Task AddProjectAsync() => _navigation.GoToAsync(Routes.ProjectEditor);

    private Task OpenSettingsAsync() => _navigation.GoToAsync(Routes.Settings);

    private Task OpenInfoAsync() => _navigation.GoToAsync(Routes.Info);

    private Task OpenOverviewAsync() => _navigation.GoToAsync(Routes.TimeOverview);

    private Task OpenProjectAsync(ProjectListItemViewModel project)
    {
        return _navigation.GoToAsync(Routes.ProjectDetail, new Dictionary<string, object>
        {
            ["projectId"] = project.Id
        });
    }

    private async Task RunDisplayTimerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var project in QuickAccessProjects)
                {
                    project.UpdateTimerDisplay();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The page is no longer visible.
        }
    }
}
