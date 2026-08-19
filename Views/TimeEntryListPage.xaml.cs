using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class TimeEntryListPage : ContentPage, IQueryAttributable
{
    private readonly TimeEntryListViewModel _viewModel;

    public TimeEntryListPage(TimeEntryListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.ApplyQueryAttributes(query);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
