using ProjectTimer.Models;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class ProjectEditorViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly INavigationService _navigation;
    private int _projectId;
    private DateTime _createdAtUtc;
    private bool _loaded;
    private string _name = string.Empty;
    private string? _description;
    private string _title = "Neues Projekt";

    public ProjectEditorViewModel(DatabaseService database, INavigationService navigation)
    {
        _database = database;
        _navigation = navigation;
        SaveCommand = new AsyncCommand(SaveAsync);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string SaveButtonText => _projectId == 0 ? "Projekt erstellen" : "Änderungen speichern";
    public AsyncCommand SaveCommand { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _projectId = QueryValue.GetInt(query, "projectId");
        Title = _projectId == 0 ? "Neues Projekt" : "Projekt bearbeiten";
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
            if (_projectId == 0)
            {
                _createdAtUtc = DateTime.UtcNow;
                _loaded = true;
                return;
            }

            var project = await _database.GetProjectAsync(_projectId)
                ?? throw new InvalidOperationException("Das Projekt wurde nicht gefunden.");
            Name = project.Name;
            Description = project.Description;
            _createdAtUtc = project.CreatedAt;
            _loaded = true;
        });
    }

    private async Task SaveAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException("Bitte geben Sie einen Projektnamen ein.");
            }

            var project = new Project
            {
                Id = _projectId,
                Name = Name,
                Description = Description,
                CreatedAt = _createdAtUtc
            };
            await _database.SaveProjectAsync(project);
            await _navigation.GoBackAsync();
        });
    }
}
