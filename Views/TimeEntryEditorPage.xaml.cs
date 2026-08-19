using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class TimeEntryEditorPage : ContentPage, IQueryAttributable
{
    private readonly TimeEntryEditorViewModel _viewModel;

    public TimeEntryEditorPage(TimeEntryEditorViewModel viewModel)
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
