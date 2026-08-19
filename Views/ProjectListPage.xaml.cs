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
    }
}
