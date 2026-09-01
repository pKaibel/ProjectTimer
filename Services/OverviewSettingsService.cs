namespace ProjectTimer.Services;

public sealed class OverviewSettingsService
{
    private const string ShowWeekendsOnStartPageKey = "show_weekends_on_start_page";

    public bool ShowWeekendsOnStartPage
    {
        get => Preferences.Default.Get(ShowWeekendsOnStartPageKey, false);
        set => Preferences.Default.Set(ShowWeekendsOnStartPageKey, value);
    }
}
