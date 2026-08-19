using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class TimeOverviewPage : ContentPage
{
    private readonly TimeOverviewViewModel _viewModel;

    public TimeOverviewPage(TimeOverviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
