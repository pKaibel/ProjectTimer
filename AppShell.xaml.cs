using ProjectTimer.Navigation;
using ProjectTimer.Views;

namespace ProjectTimer;

public partial class AppShell : Shell
{
    public AppShell(ProjectListPage projectListPage)
    {
        InitializeComponent();

        Items.Add(new ShellContent
        {
            Route = Routes.ProjectList,
            Content = projectListPage
        });

        Routing.RegisterRoute(Routes.ProjectDetail, typeof(ProjectDetailPage));
        Routing.RegisterRoute(Routes.ProjectEditor, typeof(ProjectEditorPage));
        Routing.RegisterRoute(Routes.TimeEntryEditor, typeof(TimeEntryEditorPage));
        Routing.RegisterRoute(Routes.TimeEntryList, typeof(TimeEntryListPage));
        Routing.RegisterRoute(Routes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(Routes.Info, typeof(InfoPage));
        Routing.RegisterRoute(Routes.ProjectSwitch, typeof(ProjectSwitchPage));
        Routing.RegisterRoute(Routes.TimeOverview, typeof(TimeOverviewPage));
    }
}
