using System.Collections.ObjectModel;
using ProjectTimer.Navigation;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class ProjectListViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly TimeOverviewService _overview;
    private readonly INavigationService _navigation;

    public ProjectListViewModel(DatabaseService database, TimeOverviewService overview, INavigationService navigation)
    {
        _database = database;
        _overview = overview;
        _navigation = navigation;
        AddProjectCommand = new AsyncCommand(AddProjectAsync);
        OpenSettingsCommand = new AsyncCommand(OpenSettingsAsync);
        OpenInfoCommand = new AsyncCommand(OpenInfoAsync);
        OpenOverviewCommand = new AsyncCommand(OpenOverviewAsync);
        OpenProjectCommand = new AsyncCommand<ProjectListItemViewModel>(OpenProjectAsync);
        RefreshCommand = new AsyncCommand(LoadAsync);
    }

    public ObservableCollection<ProjectListItemViewModel> Projects { get; } = [];
    public ObservableCollection<TimeChartBar> WeeklyBars { get; } = [];
    private string _weeklyTotalText = "0 Stunden 0 Minuten";
    public string WeeklyTotalText
    {
        get => _weeklyTotalText;
        private set => SetProperty(ref _weeklyTotalText, value);
    }
    public bool IsEmpty => Projects.Count == 0 && !IsBusy;
    public AsyncCommand AddProjectCommand { get; }
    public AsyncCommand OpenSettingsCommand { get; }
    public AsyncCommand OpenInfoCommand { get; }
    public AsyncCommand OpenOverviewCommand { get; }
    public AsyncCommand<ProjectListItemViewModel> OpenProjectCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            var projects = await _database.GetProjectsWithTotalsAsync();
            var timerStates = await _database.GetTimerStatesAsync();
            var overview = await _overview.GetOverviewAsync(OverviewPeriod.Week);
            Projects.Clear();
            WeeklyBars.Clear();
            foreach (var bar in overview.Bars)
            {
                WeeklyBars.Add(bar);
            }
            WeeklyTotalText = overview.TotalDurationText;
            foreach (var project in projects)
            {
                Projects.Add(new ProjectListItemViewModel(project, timerStates.FirstOrDefault(timer => timer.ProjectId == project.Id)));
            }
        });
        OnPropertyChanged(nameof(IsEmpty));
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
}
