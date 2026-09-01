using ProjectTimer.Services;
using ProjectTimer.ViewModels;
using ProjectTimer.Views;

#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
#endif

namespace ProjectTimer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows => windows.OnWindowCreated(window =>
            {
                var handle = WindowNative.GetWindowHandle(window);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.Resize(new SizeInt32(960, 720));
                if (Microsoft.UI.Xaml.Application.Current is ProjectTimer.WinUI.App windowsApp)
                {
                    windowsApp.RegisterMainWindow(window, appWindow);
                }
            }));
        });
#endif

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<TimeEntryFactory>();
        builder.Services.AddSingleton<TimeTrackingService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<OnboardingService>();
        builder.Services.AddSingleton<TraySettingsService>();
        builder.Services.AddSingleton<OverviewSettingsService>();
        builder.Services.AddSingleton<CsvBackupService>();
        builder.Services.AddSingleton<TimeOverviewService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IUserDialogService, UserDialogService>();

        builder.Services.AddTransient<ProjectListViewModel>();
        builder.Services.AddTransient<ProjectDetailViewModel>();
        builder.Services.AddTransient<ProjectEditorViewModel>();
        builder.Services.AddTransient<TimeEntryEditorViewModel>();
        builder.Services.AddTransient<TimeEntryListViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<InfoViewModel>();
        builder.Services.AddTransient<ProjectSwitchViewModel>();
        builder.Services.AddTransient<TimeOverviewViewModel>();
        builder.Services.AddTransient<OnboardingViewModel>();

        builder.Services.AddTransient<ProjectListPage>();
        builder.Services.AddTransient<ProjectDetailPage>();
        builder.Services.AddTransient<ProjectEditorPage>();
        builder.Services.AddTransient<TimeEntryEditorPage>();
        builder.Services.AddTransient<TimeEntryListPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<InfoPage>();
        builder.Services.AddTransient<ProjectSwitchPage>();
        builder.Services.AddTransient<TimeOverviewPage>();
        builder.Services.AddTransient<OnboardingPage>();

        builder.Services.AddSingleton<AppShell>();
        return builder.Build();
    }
}
