using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class TimeEntryEditorViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly TimeEntryFactory _factory;
    private readonly INavigationService _navigation;
    private int _projectId;
    private int _entryId;
    private DateTime _createdAtUtc;
    private bool _loaded;
    private DateTime _date = DateTime.Today;
    private TimeSpan _startTime = new(DateTime.Now.Hour, 0, 0);
    private TimeSpan _endTime = new(DateTime.Now.Hour + 1 > 23 ? 23 : DateTime.Now.Hour + 1, 0, 0);
    private string? _note;
    private string _title = "Zeit manuell erfassen";

    public TimeEntryEditorViewModel(
        DatabaseService database,
        TimeEntryFactory factory,
        INavigationService navigation)
    {
        _database = database;
        _factory = factory;
        _navigation = navigation;
        SaveCommand = new AsyncCommand(SaveAsync);
    }

    public DateTime Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    public TimeSpan StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    public TimeSpan EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, value);
    }

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string SaveButtonText => _entryId == 0 ? "Zeiteintrag speichern" : "Änderungen speichern";
    public AsyncCommand SaveCommand { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _projectId = QueryValue.GetInt(query, "projectId");
        _entryId = QueryValue.GetInt(query, "entryId");
        Title = _entryId == 0 ? "Zeit manuell erfassen" : "Zeiteintrag bearbeiten";
        OnPropertyChanged(nameof(SaveButtonText));
    }

    public async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (_projectId <= 0 || await _database.GetProjectAsync(_projectId) is null)
            {
                throw new InvalidOperationException("Das Projekt wurde nicht gefunden.");
            }

            if (_entryId > 0)
            {
                var entry = await _database.GetTimeEntryAsync(_entryId)
                    ?? throw new InvalidOperationException("Der Zeiteintrag wurde nicht gefunden.");
                if (entry.ProjectId != _projectId)
                {
                    throw new InvalidOperationException("Der Zeiteintrag gehört nicht zu diesem Projekt.");
                }

                Date = entry.Date;
                StartTime = entry.StartTime;
                EndTime = entry.EndTime;
                Note = entry.Note;
                _createdAtUtc = entry.CreatedAt;
            }
            else
            {
                _createdAtUtc = DateTime.UtcNow;
            }

            _loaded = true;
        });
    }

    private async Task SaveAsync()
    {
        await RunBusyAsync(async () =>
        {
            var entry = _factory.CreateManual(
                _projectId,
                Date,
                StartTime,
                EndTime,
                Note,
                _entryId,
                _createdAtUtc);
            await _database.SaveTimeEntryAsync(entry);
            await _navigation.GoBackAsync();
        });
    }
}
