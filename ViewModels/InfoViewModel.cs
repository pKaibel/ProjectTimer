using System.Reflection;

namespace ProjectTimer.ViewModels;

public sealed class InfoViewModel
{
    public string AppVersion => $"Version {AppInfo.Current.VersionString}";

    public string BuildDate { get; } = typeof(InfoViewModel).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "BuildDate")?.Value ?? "Nicht verfügbar";
}
