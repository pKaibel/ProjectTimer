using ProjectTimer.ViewModels;

namespace ProjectTimer.Views;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _viewModel;

    public OnboardingPage(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.Completed += OnCompleted;
    }

    public event EventHandler? Completed;

    private void OnCompleted(object? sender, EventArgs e) => Completed?.Invoke(this, EventArgs.Empty);
}
