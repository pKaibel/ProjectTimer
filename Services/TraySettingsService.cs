namespace ProjectTimer.Services;

public sealed class TraySettingsService
{
    private const string MinimizeToTrayKey = "minimize_to_tray";
    private const string ShowTaskbarLabelKey = "show_taskbar_label";

    public event Action<bool>? TaskbarLabelVisibilityChanged;

    public bool MinimizeToTray
    {
        get => Preferences.Default.Get(MinimizeToTrayKey, false);
        set => Preferences.Default.Set(MinimizeToTrayKey, value);
    }

    public bool ShowTaskbarLabel
    {
        get => Preferences.Default.Get(ShowTaskbarLabelKey, true);
        set
        {
            if (ShowTaskbarLabel == value)
            {
                return;
            }

            Preferences.Default.Set(ShowTaskbarLabelKey, value);
            TaskbarLabelVisibilityChanged?.Invoke(value);
        }
    }
}
