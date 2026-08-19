using ProjectTimer.Services;
using ProjectTimer.ViewModels;
using ProjectTimer.Views;

namespace ProjectTimer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<TimeEntryFactory>();
        builder.Services.AddSingleton<TimeTrackingService>();
        builder.Services.AddSingleton<ThemeService>();
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

        builder.Services.AddTransient<ProjectListPage>();
        builder.Services.AddTransient<ProjectDetailPage>();
        builder.Services.AddTransient<ProjectEditorPage>();
        builder.Services.AddTransient<TimeEntryEditorPage>();
        builder.Services.AddTransient<TimeEntryListPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<InfoPage>();
        builder.Services.AddTransient<ProjectSwitchPage>();
        builder.Services.AddTransient<TimeOverviewPage>();

        builder.Services.AddSingleton<AppShell>();
        return builder.Build();
    }
}
