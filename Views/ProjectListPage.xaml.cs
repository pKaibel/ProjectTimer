using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class ProjectListPage : ContentPage
{
    private readonly ProjectListViewModel _viewModel;

    public ProjectListPage(ProjectListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        _viewModel.StartDisplayTimer();
    }

    protected override void OnDisappearing()
    {
        _viewModel.StopDisplayTimer();
        base.OnDisappearing();
    }

    private async void OnProjectQuickAccessToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is Switch { IsFocused: true, BindingContext: ProjectListItemViewModel project })
        {
            await _viewModel.SetProjectQuickAccessAsync(project, e.Value);
        }
    }

    private async void OnQuickAccessTimerClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ProjectListItemViewModel project })
        {
            await _viewModel.ToggleQuickAccessTimerAsync(project);
        }
    }

    private async void OnShowArchivedProjectsToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is Switch { IsFocused: true })
        {
            await _viewModel.SetShowArchivedProjectsAsync(e.Value);
        }
    }
}
