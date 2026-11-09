namespace ProjectTimer.Services;

public sealed class PomodoroSettingsService
{
    public int FocusMinutes
    {
        get => Math.Clamp(Preferences.Default.Get("PomodoroFocusMinutes", 25), 1, 180);
        set => Preferences.Default.Set("PomodoroFocusMinutes", Math.Clamp(value, 1, 180));
    }

    public int BreakMinutes
    {
        get => Math.Clamp(Preferences.Default.Get("PomodoroBreakMinutes", 5), 1, 60);
        set => Preferences.Default.Set("PomodoroBreakMinutes", Math.Clamp(value, 1, 60));
    }

    public bool CountBreaks
    {
        get => Preferences.Default.Get("PomodoroCountBreaks", false);
        set => Preferences.Default.Set("PomodoroCountBreaks", value);
    }
}
