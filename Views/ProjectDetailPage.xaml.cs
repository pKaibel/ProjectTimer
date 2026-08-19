using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class ProjectDetailPage : ContentPage, IQueryAttributable
{
    private readonly ProjectDetailViewModel _viewModel;

    public ProjectDetailPage(ProjectDetailViewModel viewModel)
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

    protected override void OnDisappearing()
    {
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }
}
