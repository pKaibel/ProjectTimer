using ProjectTimer.Models;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class ProjectEditorViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly INavigationService _navigation;
    private readonly IUserDialogService _dialogs;
    private readonly CsvBackupService _backup;
    private Project? _project;
    private int _projectId;
    private DateTime _createdAtUtc;
    private bool _loaded;
    private string _name = string.Empty;
    private string? _description;
    private string _title = "Neues Projekt";

    public ProjectEditorViewModel(DatabaseService database, INavigationService navigation, IUserDialogService dialogs, CsvBackupService backup)
    {
        _database = database;
        _navigation = navigation;
        _dialogs = dialogs;
        _backup = backup;
        SaveCommand = new AsyncCommand(SaveAsync);
        ToggleArchiveCommand = new AsyncCommand(ToggleArchiveAsync, () => IsExistingProject);
        DeleteProjectCommand = new AsyncCommand(DeleteProjectAsync, () => IsExistingProject);
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
    public bool IsExistingProject => _projectId > 0;
    public bool IsArchived => _project?.IsArchived == true;
    public string ArchiveButtonText => IsArchived ? "Projekt wiederherstellen" : "Projekt archivieren";
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand ToggleArchiveCommand { get; }
    public AsyncCommand DeleteProjectCommand { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _projectId = QueryValue.GetInt(query, "projectId");
        Title = _projectId == 0 ? "Neues Projekt" : "Projekt bearbeiten";
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(IsExistingProject));
        ToggleArchiveCommand.RaiseCanExecuteChanged();
        DeleteProjectCommand.RaiseCanExecuteChanged();
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

            _project = await _database.GetProjectAsync(_projectId)
                ?? throw new InvalidOperationException("Das Projekt wurde nicht gefunden.");
            Name = _project.Name;
            Description = _project.Description;
            _createdAtUtc = _project.CreatedAt;
            OnPropertyChanged(nameof(IsArchived));
            OnPropertyChanged(nameof(ArchiveButtonText));
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

    private async Task ToggleArchiveAsync()
    {
        if (_project is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            IsArchived ? "Projekt wiederherstellen" : "Projekt archivieren",
            IsArchived
                ? $"„{_project.Name}“ wieder in der aktuellen Projektliste anzeigen?"
                : $"„{_project.Name}“ archivieren? Die erfassten Zeiten bleiben erhalten und das Projekt wird aus der Standardansicht ausgeblendet.",
            IsArchived ? "Wiederherstellen" : "Archivieren");
        if (!confirmed)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _database.SetProjectArchivedAsync(_project.Id, !IsArchived);
            await _navigation.GoBackAsync();
            await _navigation.GoBackAsync();
        });
    }

    private async Task DeleteProjectAsync()
    {
        if (_project is null)
        {
            return;
        }

        var entryCount = await _database.GetTimeEntryCountAsync(_project.Id);
        var choice = await _dialogs.ChooseProjectDeletionAsync(_project.Name, entryCount);
        if (choice == ProjectDeletionChoice.Cancel)
        {
            return;
        }

        if (choice == ProjectDeletionChoice.ExportBackup)
        {
            await _backup.ExportAsync();
            var confirmed = await _dialogs.ConfirmAsync(
                "Projekt endgültig löschen",
                $"Die CSV-Sicherung wurde geöffnet. „{_project.Name}“ und {entryCount} zugehörige Zeiteinträge jetzt unwiderruflich löschen?",
                "Endgültig löschen");
            if (!confirmed)
            {
                return;
            }
        }

        await RunBusyAsync(async () =>
        {
            await _database.DeleteProjectAsync(_project);
            await _navigation.GoBackAsync();
            await _navigation.GoBackAsync();
        });
    }
}
