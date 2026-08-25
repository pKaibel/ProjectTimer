using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.UI.Windowing;
using ProjectTimer.Services;
using WinUiWindow = Microsoft.UI.Xaml.Window;

namespace ProjectTimer.WinUI;

public partial class App : MauiWinUIApplication
{
    private const string TaskbarWindowTitle = "ProjectTimer – Zeiterfassung";

    private MauiApp? _mauiApp;
    private NativeTrayIcon? _trayIcon;
    private WinUiWindow? _mainWindow;
    private AppWindow? _appWindow;
    private TraySettingsService? _traySettings;
    private IntPtr _mainWindowHandle;

    public App()
    {
        InitializeComponent();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    protected override MauiApp CreateMauiApp()
    {
        _mauiApp = MauiProgram.CreateMauiApp();
        return _mauiApp;
    }

    internal void RegisterMainWindow(WinUiWindow window, AppWindow appWindow)
    {
        _mainWindow = window;
        _appWindow = appWindow;
        _mainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        appWindow.Changed += OnAppWindowChanged;
        _traySettings = _mauiApp?.Services.GetRequiredService<TraySettingsService>();
        if (_traySettings is not null)
        {
            _traySettings.TaskbarLabelVisibilityChanged += OnTaskbarLabelVisibilityChanged;
            SetTaskbarWindowTitle(_traySettings.ShowTaskbarLabel);
        }
        _trayIcon ??= new NativeTrayIcon(_mainWindowHandle, RestoreMainWindow);
        window.Closed += OnMainWindowClosed;
    }

    private void OnMainWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        if (_traySettings is not null)
        {
            _traySettings.TaskbarLabelVisibilityChanged -= OnTaskbarLabelVisibilityChanged;
        }
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnTaskbarLabelVisibilityChanged(bool isVisible)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() => SetTaskbarWindowTitle(isVisible));
    }

    private void SetTaskbarWindowTitle(bool showLabel)
    {
        var title = showLabel ? TaskbarWindowTitle : string.Empty;
        if (_mainWindow is not null)
        {
            _mainWindow.Title = title;
        }

        if (_appWindow is not null)
        {
            _appWindow.Title = title;
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs e)
    {
        if (!e.DidPresenterChange
            || sender.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Minimized }
            || _mauiApp?.Services.GetRequiredService<TraySettingsService>().MinimizeToTray != true)
        {
            return;
        }

        NativeTrayIcon.HideWindow(_mainWindowHandle);
    }

    private void RestoreMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        NativeTrayIcon.ShowWindow(_mainWindowHandle);
        _mainWindow.Activate();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Suspend || _mauiApp is null)
        {
            return;
        }

        try
        {
            // Windows sends this event immediately before sleep or hibernation.
            _mauiApp.Services
                .GetRequiredService<TimeTrackingService>()
                .PauseRunningTimerAsync(DateTime.UtcNow)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // A system power notification must never prevent Windows from sleeping.
        }
    }
}
