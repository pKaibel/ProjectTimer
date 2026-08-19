# ProjectTimer

ProjectTimer ist eine vollständig lokale .NET-MAUI-App zur projektbezogenen Zeiterfassung für Windows, Android und iOS.

## Architektur

- MVVM mit typisierten XAML-Bindings
- Shell-Navigation und Dependency Injection
- SQLite über `sqlite-net-pcl`
- UTC-basierte Speicherung von Zeitpunkten, lokale Darstellung in der UI
- Berechnete Dauer statt redundanter Speicherung
- Ein einzelner persistenter `ActiveTimerState` für die Wiederherstellung nach einem App-Neustart
- Transaktionaler Timerabschluss und transaktionales Löschen von Projektdaten

Die Datenbank `projecttimer.db3` wird beim ersten Zugriff automatisch in `FileSystem.AppDataDirectory` erzeugt. Es werden keine Netzwerk- oder Clouddienste verwendet.

## Build

```powershell
dotnet restore ProjectTimer.sln
dotnet build ProjectTimer.sln -p:TargetFramework=net10.0-windows10.0.19041.0 -p:WindowsPackageType=None
dotnet build ProjectTimer.csproj -f net10.0-android
```

Für einen iOS-Build ist wie bei .NET MAUI üblich ein Mac mit Xcode erforderlich.
