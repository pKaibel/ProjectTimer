namespace ProjectTimer.Services;

public sealed class OnboardingService
{
    private const string SeenVersionKey = "onboarding_seen_version";
    private readonly string _currentVersion = GetCurrentVersion();

    public string CurrentVersion => _currentVersion;

    public bool ShouldShow => !string.Equals(
        Preferences.Default.Get(SeenVersionKey, string.Empty),
        CurrentVersion,
        StringComparison.Ordinal);

    public bool IsUpdate => !string.IsNullOrWhiteSpace(Preferences.Default.Get(SeenVersionKey, string.Empty));

    public void MarkCurrentVersionAsSeen() => Preferences.Default.Set(SeenVersionKey, CurrentVersion);

    private static string GetCurrentVersion()
    {
#if WINDOWS
        try
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            // Unpackaged Windows starts can fall back to the MAUI version metadata.
        }
#endif
        return AppInfo.Current.VersionString;
    }
}
