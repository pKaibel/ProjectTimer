using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class ProjectEditorPage : ContentPage, IQueryAttributable
{
    private readonly ProjectEditorViewModel _viewModel;

    public ProjectEditorPage(ProjectEditorViewModel viewModel)
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
