namespace ProjectTimer.Services;

public sealed class OverviewSettingsService
{
    private const string ShowWeekendsOnStartPageKey = "show_weekends_on_start_page";
    private const string MaximumDailyWorkHoursKey = "maximum_daily_work_hours";

    public bool ShowWeekendsOnStartPage
    {
        get => Preferences.Default.Get(ShowWeekendsOnStartPageKey, false);
        set => Preferences.Default.Set(ShowWeekendsOnStartPageKey, value);
    }

    public int MaximumDailyWorkHours
    {
        get => Math.Clamp(Preferences.Default.Get(MaximumDailyWorkHoursKey, 10), 1, 24);
        set => Preferences.Default.Set(MaximumDailyWorkHoursKey, Math.Clamp(value, 1, 24));
    }
}
