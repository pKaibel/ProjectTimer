using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.UI.Windowing;
using Microsoft.Windows.AppLifecycle;
using ProjectTimer.Services;
using MauiColor = Microsoft.Maui.Graphics.Color;
using WindowsColor = Windows.UI.Color;
using WinUiWindow = Microsoft.UI.Xaml.Window;

namespace ProjectTimer.WinUI;

public partial class App : MauiWinUIApplication
{
    private const string SingleInstanceKey = "ProjectTimer.Main";
    private const string TaskbarWindowTitle = "Zeiterfassung";
    private const string WindowIconFileName = "appicon.ico";
    private const string PendingPowerSuspendUtcTicksKey = "pending_power_suspend_utc_ticks";

    private AppInstance? _appInstance;
    private MauiApp? _mauiApp;
    private NativeTrayIcon? _trayIcon;
    private WinUiWindow? _mainWindow;
    private AppWindow? _appWindow;
    private TraySettingsService? _traySettings;
    private ThemeService? _themeService;
    private TimeTrackingService? _tracking;
    private IntPtr _mainWindowHandle;
    private int _restoreRequested;

    public App()
    {
        InitializeComponent();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (!mainInstance.IsCurrent)
        {
            try
            {
                var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
                await mainInstance.RedirectActivationToAsync(activation);
            }
            catch
            {
                // Even if forwarding fails, a second app window must not be created.
            }

            Environment.Exit(0);
            return;
        }

        _appInstance = mainInstance;
        _appInstance.Activated += OnAppInstanceActivated;
        base.OnLaunched(args);
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
        ConfigureWindowIcon(window, appWindow);
        appWindow.Changed += OnAppWindowChanged;
        _traySettings = _mauiApp?.Services.GetRequiredService<TraySettingsService>();
        if (_traySettings is not null)
        {
            _traySettings.TaskbarLabelVisibilityChanged += OnTaskbarLabelVisibilityChanged;
            SetTaskbarWindowTitle(_traySettings.ShowTaskbarLabel);
        }
        _themeService = _mauiApp?.Services.GetRequiredService<ThemeService>();
        if (_themeService is not null)
        {
            _themeService.ThemeChanged += OnThemeChanged;
            ApplyTitleBarColors();
        }
        _trayIcon ??= new NativeTrayIcon(_mainWindowHandle, RestoreMainWindow);
        _tracking = _mauiApp?.Services.GetRequiredService<TimeTrackingService>();
        if (_tracking is not null)
        {
            _tracking.StatusChanged += OnTrackingStatusChanged;
            RecoverTimerAfterPowerSuspend();
            _ = SynchronizeTrayIconAsync();
        }
        window.Closed += OnMainWindowClosed;
        window.Activated += OnMainWindowActivated;

        if (Interlocked.Exchange(ref _restoreRequested, 0) == 1)
        {
            RestoreMainWindow();
        }
    }

    private void OnMainWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        StopRunningTimerSafely();
        if (sender is WinUiWindow window)
        {
            window.Activated -= OnMainWindowActivated;
        }
        _trayIcon?.Dispose();
        _trayIcon = null;
        if (_traySettings is not null)
        {
            _traySettings.TaskbarLabelVisibilityChanged -= OnTaskbarLabelVisibilityChanged;
        }
        if (_themeService is not null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }
        if (_tracking is not null)
        {
            _tracking.StatusChanged -= OnTrackingStatusChanged;
        }
        if (_appInstance is not null)
        {
            _appInstance.Activated -= OnAppInstanceActivated;
        }
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnMainWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (_mainWindow is null || _appWindow is null)
        {
            return;
        }

        _mainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow is null || _appWindow is null)
            {
                return;
            }

            // MAUI applies its own title-bar settings during startup. Reapply
            // these settings after its activation handlers have completed.
            ConfigureWindowIcon(_mainWindow, _appWindow);
            ApplyTitleBarColors();
            _trayIcon?.RefreshWindowIcons();
        });
    }

    private void OnAppInstanceActivated(object? sender, AppActivationArguments args)
    {
        var dispatcher = _mainWindow?.DispatcherQueue;
        if (dispatcher is null)
        {
            Interlocked.Exchange(ref _restoreRequested, 1);
            return;
        }

        dispatcher.TryEnqueue(RestoreMainWindow);
    }

    private async Task SynchronizeTrayIconAsync()
    {
        try
        {
            if (_tracking is not null)
            {
                OnTrackingStatusChanged(await _tracking.GetStatusAsync());
            }
        }
        catch
        {
            // The tray icon remains blue if the local database is not available yet.
        }
    }

    private void OnTrackingStatusChanged(TimeTrackingStatus status)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() => _trayIcon?.SetStatus(status));
    }

    private void OnTaskbarLabelVisibilityChanged(bool isVisible)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() => SetTaskbarWindowTitle(isVisible));
    }

    private void OnThemeChanged(object? sender, EventArgs args)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(ApplyTitleBarColors);
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

    private static void ConfigureWindowIcon(WinUiWindow window, AppWindow appWindow)
    {
        window.ExtendsContentIntoTitleBar = false;
        appWindow.TitleBar.ExtendsContentIntoTitleBar = false;
        appWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;

        var iconPath = new[]
            {
                Path.Combine(AppContext.BaseDirectory, WindowIconFileName),
                Path.Combine(AppContext.BaseDirectory, "AppX", WindowIconFileName)
            }
            .FirstOrDefault(File.Exists);
        if (iconPath is not null)
        {
            appWindow.SetIcon(iconPath);
        }
    }

    private void ApplyTitleBarColors()
    {
        if (_appWindow is null
            || GetResourceColor("SurfaceContainer") is not { } surface
            || GetResourceColor("OnSurface") is not { } onSurface)
        {
            return;
        }

        var background = ToWindowsColor(surface);
        var foreground = ToWindowsColor(onSurface);
        var titleBar = _appWindow.TitleBar;
        titleBar.BackgroundColor = background;
        titleBar.InactiveBackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveForegroundColor = foreground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonHoverBackgroundColor = background;
        titleBar.ButtonPressedBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    private static MauiColor? GetResourceColor(string key)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources is not { } resources)
        {
            return null;
        }

        if (!resources.TryGetValue(key, out var value) || value is not MauiColor color)
        {
            return null;
        }

        return color;
    }

    private static WindowsColor ToWindowsColor(MauiColor color) => WindowsColor.FromArgb(
        (byte)Math.Round(color.Alpha * byte.MaxValue),
        (byte)Math.Round(color.Red * byte.MaxValue),
        (byte)Math.Round(color.Green * byte.MaxValue),
        (byte)Math.Round(color.Blue * byte.MaxValue));

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
        if (_mauiApp is null)
        {
            return;
        }

        if (e.Mode == PowerModes.Suspend)
        {
            // Store the timestamp first. If Windows freezes the process before
            // the database write completes, it is recovered on resume.
            var suspendedAtUtc = DateTime.UtcNow;
            Preferences.Default.Set(PendingPowerSuspendUtcTicksKey, suspendedAtUtc.Ticks);
            if (StopRunningTimerSafely(suspendedAtUtc))
            {
                Preferences.Default.Remove(PendingPowerSuspendUtcTicksKey);
            }
        }
        else if (e.Mode == PowerModes.Resume)
        {
            RecoverTimerAfterPowerSuspend();
        }
    }

    private void RecoverTimerAfterPowerSuspend()
    {
        if (!Preferences.Default.ContainsKey(PendingPowerSuspendUtcTicksKey))
        {
            return;
        }

        var ticks = Preferences.Default.Get(PendingPowerSuspendUtcTicksKey, 0L);
        if (ticks <= 0)
        {
            Preferences.Default.Remove(PendingPowerSuspendUtcTicksKey);
            return;
        }

        var suspendedAtUtc = new DateTime(ticks, DateTimeKind.Utc);
        if (StopRunningTimerSafely(suspendedAtUtc))
        {
            Preferences.Default.Remove(PendingPowerSuspendUtcTicksKey);
        }
    }

    private bool StopRunningTimerSafely(DateTime? stoppedAtUtc = null)
    {
        try
        {
            var tracking = _tracking ?? _mauiApp?.Services.GetRequiredService<TimeTrackingService>();
            if (tracking is not null)
            {
                // This handler runs on the UI thread. Running the asynchronous
                // database write there and then blocking on it can deadlock the
                // window shutdown. The tracking service has its own lock, so it
                // is safe to complete the write on a worker thread instead.
                Task.Run(() => tracking.StopRunningTimerAsync(stoppedAtUtc ?? DateTime.UtcNow))
                    .GetAwaiter()
                    .GetResult();
            }

            return true;
        }
        catch
        {
            // Closing the window or sleeping must still succeed if the local data store is unavailable.
            return false;
        }
    }
}
