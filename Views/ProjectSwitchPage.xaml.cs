using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class ProjectSwitchPage : ContentPage, IQueryAttributable
{
    private readonly ProjectSwitchViewModel _viewModel;

    public ProjectSwitchPage(ProjectSwitchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.ApplyQueryAttributes(query);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
