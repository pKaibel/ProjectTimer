using ProjectTimer.Services;

namespace ProjectTimer;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ThemeService _themeService;

    public App(IServiceProvider services, ThemeService themeService)
    {
        InitializeComponent();
        _services = services;
        _themeService = themeService;
        _themeService.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_services.GetRequiredService<AppShell>());
    }
}
