using ProjectTimer.Services;
using ProjectTimer.Views;

namespace ProjectTimer;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ThemeService _themeService;
    private readonly OnboardingService _onboarding;

    public App(IServiceProvider services, ThemeService themeService, OnboardingService onboarding)
    {
        InitializeComponent();
        _services = services;
        _themeService = themeService;
        _onboarding = onboarding;
        _themeService.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window();
        ShowInitialPage(window);
        return window;
    }

    private void ShowInitialPage(Window window)
    {
        if (!_onboarding.ShouldShow)
        {
            window.Page = _services.GetRequiredService<AppShell>();
            return;
        }

        var onboardingPage = _services.GetRequiredService<OnboardingPage>();
        onboardingPage.Completed += OnboardingCompleted;
        window.Page = onboardingPage;
    }

    private void OnboardingCompleted(object? sender, EventArgs e)
    {
        if (sender is not OnboardingPage onboardingPage)
        {
            return;
        }

        onboardingPage.Completed -= OnboardingCompleted;
        _onboarding.MarkCurrentVersionAsSeen();
        var window = Windows.FirstOrDefault(currentWindow => currentWindow.Page == onboardingPage);
        if (window is not null)
        {
            window.Page = _services.GetRequiredService<AppShell>();
        }
    }
}
