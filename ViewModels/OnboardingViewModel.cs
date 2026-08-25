using ProjectTimer.Services;

namespace ProjectTimer.ViewModels;

public sealed class OnboardingViewModel
{
    public OnboardingViewModel(OnboardingService onboarding)
    {
        Title = onboarding.IsUpdate ? "ProjectTimer wurde aktualisiert" : "Willkommen bei ProjectTimer";
        Introduction = onboarding.IsUpdate
            ? "Sieh dir die wichtigsten Funktionen an und starte anschließend direkt mit deiner Zeiterfassung."
            : "Erfasse Arbeitszeit einfach, projektbezogen und sicher auf deinem Gerät.";
        Version = $"Version {onboarding.CurrentVersion}";
        FinishCommand = new Command(() => Completed?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? Completed;

    public string Title { get; }
    public string Introduction { get; }
    public string Version { get; }
    public Command FinishCommand { get; }
}
