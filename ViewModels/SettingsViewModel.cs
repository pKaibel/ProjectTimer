using System.Collections.ObjectModel;
using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
    private readonly ThemeService _themeService;
    private readonly TraySettingsService _traySettings;
    private readonly OverviewSettingsService _overviewSettings;
    private readonly CsvBackupService _backup;
    private bool _isDarkMode;
    private bool _minimizeToTray;
    private bool _showTaskbarLabel;
    private bool _showWeekendsOnStartPage;

    public SettingsViewModel(ThemeService themeService, TraySettingsService traySettings, OverviewSettingsService overviewSettings, CsvBackupService backup)
    {
        _themeService = themeService;
        _traySettings = traySettings;
        _overviewSettings = overviewSettings;
        _backup = backup;
        IsDarkMode = themeService.IsDarkMode;
        MinimizeToTray = traySettings.MinimizeToTray;
        ShowTaskbarLabel = traySettings.ShowTaskbarLabel;
        ShowWeekendsOnStartPage = overviewSettings.ShowWeekendsOnStartPage;
        Schemes = new ObservableCollection<ThemeSchemeItem>(Enum.GetValues<ColorScheme>()
            .Select(scheme => new ThemeSchemeItem(themeService.CreateOption(scheme), scheme == themeService.SelectedScheme)));
        SelectSchemeCommand = new AsyncCommand<ThemeSchemeItem>(SelectSchemeAsync);
        ExportCommand = new AsyncCommand(ExportAsync);
        ImportCommand = new AsyncCommand(ImportAsync);
    }

    public ObservableCollection<ThemeSchemeItem> Schemes { get; }
    public AsyncCommand<ThemeSchemeItem> SelectSchemeCommand { get; }
    public AsyncCommand ExportCommand { get; }
    public AsyncCommand ImportCommand { get; }

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
