using System.Collections.ObjectModel;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
    private readonly ThemeService _themeService;
    private readonly TraySettingsService _traySettings;
    private readonly OverviewSettingsService _overviewSettings;
    private readonly CsvBackupService _backup;
    private readonly TestDataService _testData;
    private readonly DatabaseService _database;
    private readonly IUserDialogService _dialogs;
    private bool _isDarkMode;
    private bool _minimizeToTray;
    private bool _showTaskbarLabel;
    private bool _showWeekendsOnStartPage;
    private int _maximumDailyWorkHours;
    private int _testDataMonths = 3;

    public SettingsViewModel(ThemeService themeService, TraySettingsService traySettings, OverviewSettingsService overviewSettings, CsvBackupService backup, TestDataService testData, DatabaseService database, IUserDialogService dialogs)
    {
        _themeService = themeService;
        _traySettings = traySettings;
        _overviewSettings = overviewSettings;
        _backup = backup;
        _testData = testData;
        _database = database;
        _dialogs = dialogs;
        IsDarkMode = themeService.IsDarkMode;
        MinimizeToTray = traySettings.MinimizeToTray;
        ShowTaskbarLabel = traySettings.ShowTaskbarLabel;
        ShowWeekendsOnStartPage = overviewSettings.ShowWeekendsOnStartPage;
        MaximumDailyWorkHours = overviewSettings.MaximumDailyWorkHours;
        Schemes = new ObservableCollection<ThemeSchemeItem>(Enum.GetValues<ColorScheme>()
            .Select(scheme => new ThemeSchemeItem(themeService.CreateOption(scheme), scheme == themeService.SelectedScheme)));
        SelectSchemeCommand = new AsyncCommand<ThemeSchemeItem>(SelectSchemeAsync);
        ExportCommand = new AsyncCommand(ExportAsync);
        ImportCommand = new AsyncCommand(ImportAsync);
        LoadTestDataCommand = new AsyncCommand(LoadTestDataAsync);
        DeleteAllDataCommand = new AsyncCommand(DeleteAllDataAsync);
    }

    public ObservableCollection<ThemeSchemeItem> Schemes { get; }
    public AsyncCommand<ThemeSchemeItem> SelectSchemeCommand { get; }
    public AsyncCommand ExportCommand { get; }
    public AsyncCommand ImportCommand { get; }
    public AsyncCommand LoadTestDataCommand { get; }
    public AsyncCommand DeleteAllDataCommand { get; }

    private string? _dataMessage;

    public string? DataMessage
    {
        get => _dataMessage;
        private set
        {
            if (SetProperty(ref _dataMessage, value))
            {
                OnPropertyChanged(nameof(HasDataMessage));
            }
        }
    }

    public bool HasDataMessage => !string.IsNullOrWhiteSpace(DataMessage);

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                _themeService.SetDarkMode(value);
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                _traySettings.MinimizeToTray = value;
            }
        }
    }

    public bool ShowTaskbarLabel
    {
        get => _showTaskbarLabel;
        set
        {
            if (SetProperty(ref _showTaskbarLabel, value))
            {
                _traySettings.ShowTaskbarLabel = value;
            }
        }
    }

    public bool ShowWeekendsOnStartPage
    {
        get => _showWeekendsOnStartPage;
        set
        {
            if (SetProperty(ref _showWeekendsOnStartPage, value))
            {
                _overviewSettings.ShowWeekendsOnStartPage = value;
            }
        }
    }

    public int MaximumDailyWorkHours
    {
        get => _maximumDailyWorkHours;
        set
        {
            if (SetProperty(ref _maximumDailyWorkHours, value))
            {
                _overviewSettings.MaximumDailyWorkHours = value;
                OnPropertyChanged(nameof(MaximumDailyWorkDurationText));
            }
        }
    }

    public string MaximumDailyWorkDurationText => $"{MaximumDailyWorkHours} Stunden";

    public int TestDataMonths
    {
        get => _testDataMonths;
        set
        {
            if (SetProperty(ref _testDataMonths, Math.Clamp(value, 1, 12)))
            {
                OnPropertyChanged(nameof(TestDataDurationText));
            }
        }
    }

    public string TestDataDurationText => $"{TestDataMonths} Monate";

    private Task SelectSchemeAsync(ThemeSchemeItem? selection)
    {
        if (selection is null)
        {
            return Task.CompletedTask;
        }

        _themeService.SetScheme(selection.Scheme);
        foreach (var scheme in Schemes)
        {
            scheme.IsSelected = scheme == selection;
        }

        return Task.CompletedTask;
    }

    private async Task ExportAsync()
    {
        await RunBusyAsync(async () =>
        {
            DataMessage = null;
            await _backup.ExportAsync();
            DataMessage = "Die CSV-Sicherung wurde zur Ablage oder Weitergabe geöffnet.";
        });
    }

    private async Task ImportAsync()
    {
        await RunBusyAsync(async () =>
        {
            DataMessage = null;
            var result = await _backup.ImportAsync();
            if (result is not null)
            {
                DataMessage = $"Import abgeschlossen: {result.ProjectsCreated} Projekte und {result.EntriesCreated} Zeiteinträge ergänzt; {result.EntriesSkipped} Duplikate übersprungen.";
            }
        });
    }

    private async Task LoadTestDataAsync()
    {
        await RunBusyAsync(async () =>
        {
            DataMessage = null;
            var result = await _testData.LoadAsync(TestDataMonths);
            DataMessage = result.EntriesCreated == 0
                ? "Die Testdaten für diesen Zeitraum sind bereits vorhanden."
                : $"Testdaten geladen: {result.ProjectsCreated} neue Projekte und {result.EntriesCreated} Zeiteinträge.";
        });
    }

    private async Task DeleteAllDataAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Alle Daten löschen?",
            "Alle Projekte, Zeiteinträge und laufenden Timer werden unwiderruflich gelöscht. Diese Aktion kann nicht rückgängig gemacht werden.",
            "Alle Daten löschen");
        if (!confirmed)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            DataMessage = null;
            await _database.ClearAllDataAsync();
            DataMessage = "Alle erfassten Daten wurden gelöscht.";
        });
    }
}

public sealed class ThemeSchemeItem : BaseViewModel
{
    private bool _isSelected;

    public ThemeSchemeItem(ThemeService.ThemeOption option, bool isSelected)
    {
        Scheme = option.Scheme;
        Name = option.Name;
        Description = option.Description;
        PreviewPrimary = option.PreviewPrimary;
        PreviewContainer = option.PreviewContainer;
        _isSelected = isSelected;
    }

    public ColorScheme Scheme { get; }
    public string Name { get; }
    public string Description { get; }
    public Color PreviewPrimary { get; }
    public Color PreviewContainer { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
