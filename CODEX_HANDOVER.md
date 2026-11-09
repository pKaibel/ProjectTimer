# ProjectTimer – Codex-Übergabe

Stand dieser Übergabe: 5. Oktober 2026. Sie wurde durch eine lesende Repository-Analyse erstellt; es wurden dabei weder ein Build noch automatisierte oder manuelle Tests ausgeführt.

## Projektüberblick

ProjectTimer ist eine deutschsprachige, vollständig lokale .NET-MAUI-App für projektbezogene Zeiterfassung auf Windows, Android und iOS. Sie verwaltet Projekte und Zeiteinträge, unterstützt laufende und pausierte Timer, CSV-Sicherung sowie Auswertungen. Daten werden lokal in SQLite gespeichert; der Quellstand enthält keine Netzwerk- oder Cloud-Anbindung.

Wichtige Funktionen: Projekte anlegen, bearbeiten, archivieren und löschen; Zeiten manuell erfassen; Timer starten, pausieren, fortsetzen, stoppen und zwischen Projekten wechseln; Kommentare; Schnellzugriffe; Zeitübersichten; CSV-Import/-Export; Testdaten; Themes und Onboarding. Auf Windows gibt es zusätzlich Single-Instance-Verhalten, ein Tray-Icon und Optionen zum Minimieren in den Infobereich.

## Technische Architektur

- **UI und Navigation:** .NET MAUI mit XAML, typisierten Bindings und `Shell`. `App` entscheidet anhand von `OnboardingService`, ob zuerst das Onboarding oder `AppShell` erscheint.
- **MVVM:** `ViewModels/` enthält ViewModels und eigene `AsyncCommand`-Implementierungen. Seiten sind transient, Services überwiegend Singleton und über DI in `MauiProgram.CreateMauiApp` registriert.
- **Persistenz:** `DatabaseService` kapselt `sqlite-net-pcl` und verwaltet die Tabellen `Projects`, `TimeEntries` und `ActiveTimeEntry`. Migrationen erfolgen beim ersten Datenbankzugriff.
- **Zeiterfassung:** `TimeTrackingService` serialisiert Timeraktionen mit einem `SemaphoreSlim`; kritische Datenbankoperationen sind transaktional. Es kann höchstens ein laufender Timer existieren, zusätzlich können projektbezogene Timer pausiert vorliegen.
- **Auswertung:** `TimeOverviewService` berechnet lokale Tages-/Wochen-/Monats-/Jahres-Buckets aus UTC-Zeiteinträgen und stellt Projektsegmente für die UI bereit.
- **Plattformen:** Gemeinsame MAUI-App für Android/iOS/Windows. Windows-spezifischer Code in `Platforms/Windows/` verwendet WinUI-, Win32- und Shell-APIs für Fenster, Tray und Energieereignisse.

## Wichtige Dateien und Verzeichnisse

- `ProjectTimer.csproj` – Target Frameworks, Paketabhängigkeiten, App-Metadaten und Windows-Paketsignierung.
- `MauiProgram.cs` – Einstiegspunkt der DI-Registrierung und Windows-Fenster-/Toolbar-Konfiguration.
- `App.xaml.cs`, `AppShell.xaml.cs`, `Navigation/Routes.cs` – Startlogik und Shell-Routen.
- `Models/` – SQLite-Modelle `Project`, `TimeEntry` und `ActiveTimerState`.
- `Services/DatabaseService.cs` – Schemaerzeugung, Migrationen, Datenzugriff und Transaktionen; bei Datenänderungen besonders wichtig.
- `Services/TimeTrackingService.cs` und `Services/TimeEntryFactory.cs` – Timerzustände, Zeitregeln und Validierung.
- `Services/TimeOverviewService.cs` – Diagramm-/Übersichtsberechnung; `OverviewSettingsService` liefert deren Einstellungen.
- `Services/CsvBackupService.cs`, `TestDataService.cs` – lokale Sicherung/Import bzw. deterministische Beispielzeiterfassung.
- `ViewModels/` und `Views/` – UI-Flüsse für Projekte, Timer, Einträge, Einstellungen und Onboarding.
- `Platforms/Windows/App.xaml.cs` und `NativeTrayIcon.cs` – Windows-spezifische Single-Instance-, Tray- und Suspend-/Shutdown-Logik.
- `Platforms/Windows/Package*.appxmanifest` – getrennte Paketidentitäten für Release und Debug.
- `README.md` – kurze Architektur- und Build-Anleitung. Es gibt derzeit kein `AGENTS.md` und kein `docs/`-Verzeichnis.

## Entwicklungsumgebung

- **SDK/Frameworks:** Target Frameworks sind `net10.0-windows10.0.19041.0`, `net10.0-android` und `net10.0-ios`; das Repository referenziert `Microsoft.Maui.Controls` 10.0.60, `Microsoft.Win32.SystemEvents` 10.0.0 und `sqlite-net-pcl` 1.11.285.
- **Im analysierten Rechner verfügbar:** .NET SDK 8, 9 und 10 (10.0.401). Ein `dotnet workload list` konnte wegen fehlender Systemberechtigung nicht vollständig ausgeführt werden; installierte MAUI-Workloads sind daher nicht bestätigt.
- **Build laut README:**
  - `dotnet restore ProjectTimer.sln`
  - `dotnet build ProjectTimer.sln -p:TargetFramework=net10.0-windows10.0.19041.0 -p:WindowsPackageType=None`
  - `dotnet build ProjectTimer.csproj -f net10.0-android`
  - Für iOS werden macOS und Xcode benötigt.
- **Start/Debug:** `Properties/launchSettings.json` enthält das Profil `Windows Machine`. Das Debug-Appx-Manifest hat eine eigene Paketidentität, damit Debug- und Release-App parallel installiert werden können.
- **Tests:** Es gibt kein Testprojekt und keine CI-Konfiguration im Repository. Die Funktionsfähigkeit des aktuellen HEAD wurde in dieser Übergabe nicht durch Ausführung bestätigt.
- **Release-Signierung:** `ProjectTimer.csproj` aktiviert Windows-Paketsignierung über einen Zertifikat-Fingerprint. Im Zertifikatspeicher `CurrentUser/My` dieses Rechners wurde das passende Zertifikat nicht gefunden. Das Zertifikat samt privatem Schlüssel ist nicht im Repository und wird für einen signierten Windows-Release benötigt.

## Bisherige Entscheidungen

- **Lokale Datenhaltung statt Backend:** Code und README bestätigen SQLite in `FileSystem.AppDataDirectory`; Datumswerte werden in UTC-Ticks gespeichert und in der UI lokal dargestellt.
- **Dauer wird abgeleitet:** `TimeEntry.Duration` wird aus Start- und Ende berechnet; keine redundante Dauer wird persistiert.
- **Persistenter Timerzustand:** `ActiveTimerState` erlaubt, Timerzustände über Prozessunterbrechungen hinweg wiederherzustellen. Alte Tabelle `ActiveTimer` wird beim Start nach `ActiveTimeEntry` migriert.
- **Transaktionale Datenintegrität:** Timerabschluss, Pausieren, Projektlöschung und vollständige Datenlöschung erfolgen in Datenbanktransaktionen. Änderungen sollten diese Eigenschaften nicht umgehen.
- **Sicheres Löschen:** Vor dem Löschen eines Projekts wird ein eigener Dialog mit optionalem CSV-Export angeboten; alle Projektdaten können nur nach Bestätigung gelöscht werden.
- **Windows getrennt behandeln:** Tray-, Titelbar- und Energieverhalten ist bewusst durch `#if WINDOWS` und `Platforms/Windows/` isoliert; es darf nicht als plattformübergreifende MAUI-Funktion angenommen werden.

## Aktueller Entwicklungsstand

Der Quellstand implementiert die im Projektüberblick genannten Kernfunktionen. Der jüngste Commit `4a7ab70` vom 2. Oktober 2026 ergänzt bzw. überarbeitet insbesondere:

- projektsegmentierte Balken in der Zeitauswertung;
- konfigurierbare maximale tägliche Arbeitszeit als Balkenreferenz;
- Testdaten für 1–12 Monate;
- vollständige Datenlöschung aus den Einstellungen;
- Windows-Toolbar-Styling;
- Aktualisierung des Release-Manifests auf Version 1.0.33.0.

Ältere relevante Commits: `49cec71` führte Projektarchivierung und den Löschdialog ein; `388111f` ergänzte eine defensive Migration für `IsArchived` und passte Onboarding/Version an. Es gibt keine im Quellcode markierten TODOs oder erkennbaren halbfertigen Änderungen.

## Offene Aufgaben

Die folgenden Punkte sind aus dem Repository bzw. der Analyse abgeleitet, nicht als vom Produktteam priorisierte Anforderungen zu verstehen:

1. **Vor weiterer Feature-Arbeit:** Windows-Debug-Build und manuellen Smoke-Test des aktuellen HEAD ausführen, insbesondere Timer-Lebenszyklus, Projektarchivierung/Löschen, CSV und die im letzten Commit geänderte Übersicht.
2. **Release klären:** Verfügbarkeit und Verwaltung des Windows-Signaturzertifikats mit privatem Schlüssel klären, falls ein signiertes MSIX erzeugt werden soll.
3. **Qualität absichern:** Ein Testprojekt für Migrationen, Timerzustandswechsel, Zeitzonen-/DST-Grenzen, CSV-Deduplizierung und Auswertungsberechnung planen; derzeit gibt es keine automatisierten Tests.
4. **Produktprioritäten erfragen:** Es fehlen Anforderungen, bekannte Bugs und eine Roadmap aus früheren Gesprächen. Keine neuen Features allein aus dieser Datei ableiten.

## Bekannte Probleme und Fallstricke

- **Migrationen nicht entfernen:** Bestehende Installationen können alte Tabellen/Spalten besitzen. `DatabaseService.InitializeAsync` und die Migrationsmethoden müssen bei Schemaänderungen berücksichtigt werden.
- **Zeitregeln:** Manuelle Einträge dürfen nicht über Mitternacht reichen. Ungültige Ortszeiten bei Sommerzeitumstellungen werden abgewiesen. Zeiten werden intern als UTC gespeichert.
- **Mindestdauer:** Automatisch erfasste Timerabschnitte unter einer Minute werden nicht als `TimeEntry` gespeichert. Das ist im Onboarding dokumentiert und sollte bei Änderungen am Pausenverhalten bewusst geprüft werden.
- **Timerkonkurrenz:** `TimeTrackingService` und `DatabaseService` prüfen erwartete Zustände und nutzen Sperren/Transaktionen. Direkte Manipulation von `ActiveTimerState` kann diese Konsistenz verletzen.
- **Windows-Schließen/Standby:** Beim Schließen und bei Windows-Suspend wird ein laufender Timer gestoppt. Für Suspend wird zuerst ein Zeitstempel in `Preferences` geschrieben, damit ein abgebrochener Datenbankschreibvorgang beim Resume nachgeholt werden kann.
- **Windows-Workarounds:** `Platforms/Windows/App.xaml.cs` schreibt beim Stoppen nicht blockierend auf dem UI-Thread, um Shutdown-Deadlocks zu vermeiden. Titelleisten- und Fenstersymbolformatierung wird nach Aktivierung erneut angewandt, weil MAUI beim Start eigene Einstellungen setzen kann.
- **Versionsquellen:** Das Windows-Manifest führt derzeit 1.0.33.0, während die MAUI-Projektmetadaten `ApplicationDisplayVersion` 1.0 enthalten. Ob dies für die gewünschte plattformübergreifende Versionierung korrekt ist, wurde nicht per Laufzeitprüfung bestätigt.
- **Historische Fehlversuche:** Es gibt keine frühere Codex-Übergabe oder Chat-Historie im Repository. Konkrete verworfene Lösungsansätze sind daher nicht rekonstruierbar. Die oben genannten Windows-Workarounds sind als dokumentierte technische Gründe im Code vorhanden, nicht als nachgewiesene Fehlversuche.

## Aktueller Git-Zustand

Bei Erstellung dieser Datei war `master` auf `4a7ab70fe10e603d5cf2fad80458021f7df7b8db` und vollständig synchron mit `origin/master` (`+0/-0`). `origin/master` ist der einzige relevante Remote-Branch; es gibt keine weiteren lokalen Arbeitsbranches oder Tags.

Vor dieser Übergabedatei gab es keine uncommitted oder von Git erfassten untracked Dateien. Lokal vorhandene, durch `.gitignore` ausgeschlossene Artefakte umfassen `bin/`, `obj/`, `.vs/`, `AppPackages/` und `ProjectTimer.csproj.user`; sie sind nicht Teil des Quellstands und sollten nicht versehentlich versioniert werden. Diese Datei selbst ist die beabsichtigte neue Änderung und ist nach dem Erstellen noch uncommitted.

## Empfohlene nächste Schritte

1. `git status` prüfen und diese Übergabedatei bei Zustimmung zusammen mit keiner unbeabsichtigten Datei committen.
2. Vor funktionalen Änderungen die beschriebenen Build- und Smoke-Tests durchführen; dabei keine vorhandene Nutzdatenbank löschen.
3. Release-Zertifikat und Zielplattformen klären, wenn Paketierung Teil der nächsten Aufgabe ist.
4. Erst danach eine konkret priorisierte Produktaufgabe umsetzen und passende automatisierte Tests ergänzen.

## Kontext für die nächste Codex-Session

Lies zuerst diese Datei, `README.md`, `ProjectTimer.csproj`, `MauiProgram.cs`, `Services/DatabaseService.cs`, `Services/TimeTrackingService.cs` und bei Windows-Arbeit `Platforms/Windows/App.xaml.cs`. Der aktuelle Code ist die maßgebliche Quelle; diese Übergabe beschreibt eine statische Analyse und ersetzt keinen erfolgreichen Build oder manuellen Test.

Vor Änderungen an Persistenz oder Timerlogik sind Migrationen, UTC-/DST-Verhalten, Mindestdauer und die Transaktions-/Sperrstrategie zu prüfen. Vor Release-Arbeit sind das nicht versionierte Signaturzertifikat und die reale MAUI-Workload-Umgebung zu klären. Es wurden keine Secrets in diese Datei aufgenommen.

### Offene Fragen

- Welche konkreten Anforderungen, Bugs oder Prioritäten existierten in den nicht verfügbaren früheren Codex-Gesprächen?
- Soll die Versionsnummer für Android/iOS und Windows absichtlich unterschiedlich gepflegt werden?
- Wo wird das Windows-Release-Zertifikat mit privatem Schlüssel sicher verwaltet, und ist ein signiertes MSIX überhaupt das nächste Ziel?
- Welche Zielplattformen sollen vor allem getestet und weiterentwickelt werden?
