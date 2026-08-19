using System.Collections.ObjectModel;
using System.Globalization;
using ProjectTimer.Navigation;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class TimeEntryListViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IUserDialogService _dialogs;
    private int _projectId;
    private string _title = "Zeiteinträge";

    public TimeEntryListViewModel(
        DatabaseService database,
        INavigationService navigation,
        IUserDialogService dialogs)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;
        AddCommand = new AsyncCommand(AddAsync);
        EditCommand = new AsyncCommand<TimeEntryListItemViewModel>(EditAsync);
        DeleteCommand = new AsyncCommand<TimeEntryListItemViewModel>(DeleteAsync);
        RefreshCommand = new AsyncCommand(LoadAsync);
    }

    public ObservableCollection<TimeEntryGroup> Groups { get; } = [];

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public bool IsEmpty => Groups.Count == 0 && !IsBusy;
    public AsyncCommand AddCommand { get; }
    public AsyncCommand<TimeEntryListItemViewModel> EditCommand { get; }
    public AsyncCommand<TimeEntryListItemViewModel> DeleteCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _projectId = QueryValue.GetInt(query, "projectId");
    }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            var project = await _database.GetProjectAsync(_projectId)
                ?? throw new InvalidOperationException("Das Projekt wurde nicht gefunden.");
            Title = $"Zeiten · {project.Name}";
            var entries = await _database.GetTimeEntriesAsync(_projectId);
            var items = entries.Select(entry => new TimeEntryListItemViewModel(entry));

            Groups.Clear();
            foreach (var group in items.GroupBy(item => new
                     {
                         item.Entry.Date.Year,
                         item.Entry.Date.Month
                     }))
            {
                var month = new DateTime(group.Key.Year, group.Key.Month, 1)
                    .ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                month = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(month);
                Groups.Add(new TimeEntryGroup(month, group));
            }
        });
        OnPropertyChanged(nameof(IsEmpty));
    }

    private Task AddAsync()
    {
        return _navigation.GoToAsync(Routes.TimeEntryEditor, new Dictionary<string, object>
        {
            ["projectId"] = _projectId
        });
    }

    private Task EditAsync(TimeEntryListItemViewModel item)
    {
        return _navigation.GoToAsync(Routes.TimeEntryEditor, new Dictionary<string, object>
        {
            ["projectId"] = _projectId,
            ["entryId"] = item.Entry.Id
        });
    }

    private async Task DeleteAsync(TimeEntryListItemViewModel item)
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Zeiteintrag löschen",
            $"Den Eintrag vom {item.DateText} wirklich löschen?");
        if (!confirmed)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _database.DeleteTimeEntryAsync(item.Entry);
            Groups.Clear();
            var entries = await _database.GetTimeEntriesAsync(_projectId);
            var items = entries.Select(entry => new TimeEntryListItemViewModel(entry));
            foreach (var group in items.GroupBy(entry => new { entry.Entry.Date.Year, entry.Entry.Date.Month }))
            {
                var month = new DateTime(group.Key.Year, group.Key.Month, 1)
                    .ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                Groups.Add(new TimeEntryGroup(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(month), group));
            }
        });
        OnPropertyChanged(nameof(IsEmpty));
    }
}
