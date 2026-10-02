using ProjectTimer.Models;

namespace ProjectTimer.Services;

public sealed class TestDataService
{
    private static readonly (string Name, string Description)[] Projects =
    [
        ("[Testdaten] Kundenportal", "Weiterentwicklung eines Kundenportals"),
        ("[Testdaten] Mobile App", "Planung und Umsetzung einer mobilen Anwendung"),
        ("[Testdaten] Website Relaunch", "Neugestaltung der Unternehmenswebsite"),
        ("[Testdaten] Datenanalyse", "Auswertung und Aufbereitung von Projektdaten"),
        ("[Testdaten] Interne Prozesse", "Optimierung interner Arbeitsabläufe")
    ];

    private readonly DatabaseService _database;
    private readonly TimeEntryFactory _timeEntryFactory;

    public TestDataService(DatabaseService database, TimeEntryFactory timeEntryFactory)
    {
        _database = database;
        _timeEntryFactory = timeEntryFactory;
    }

    public async Task<TestDataResult> LoadAsync(int months)
    {
        months = Math.Clamp(months, 1, 12);
        var result = new TestDataResult();
        var existingProjects = (await _database.GetProjectsAsync())
            .ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
        var testProjects = new List<Project>(Projects.Length);

        foreach (var (name, description) in Projects)
        {
            if (!existingProjects.TryGetValue(name, out var project))
            {
                project = new Project { Name = name, Description = description };
                await _database.SaveProjectAsync(project);
                result.ProjectsCreated++;
            }

            testProjects.Add(project);
        }

        var knownEntries = new Dictionary<int, HashSet<string>>();
        foreach (var project in testProjects)
        {
            knownEntries[project.Id] = (await _database.GetTimeEntriesAsync(project.Id))
                .Select(CreateEntryKey)
                .ToHashSet(StringComparer.Ordinal);
        }

        var startDate = DateTime.Today.AddMonths(-months).AddDays(1).Date;
        for (var date = startDate; date <= DateTime.Today; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var dayIndex = (date - startDate).Days;
            var entryCount = dayIndex % 3 == 0 ? 3 : 2;
            for (var entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                var project = testProjects[(dayIndex + entryIndex * 2) % testProjects.Count];
                var start = TimeSpan.FromHours(8 + entryIndex * 3).Add(TimeSpan.FromMinutes(entryIndex * 15));
                var duration = TimeSpan.FromMinutes(90 + ((dayIndex + entryIndex) % 3) * 30);
                var entry = _timeEntryFactory.CreateManual(
                    project.Id,
                    date,
                    start,
                    start + duration,
                    "Automatisch erzeugte Testzeit");
                var key = CreateEntryKey(entry);
                if (knownEntries[project.Id].Contains(key))
                {
                    continue;
                }

                await _database.SaveTimeEntryAsync(entry);
                knownEntries[project.Id].Add(key);
                result.EntriesCreated++;
            }
        }

        return result;
    }

    private static string CreateEntryKey(TimeEntry entry) =>
        $"{entry.StartUtcTicks}|{entry.EndUtcTicks}|{entry.Note}";
}

public sealed class TestDataResult
{
    public int ProjectsCreated { get; set; }
    public int EntriesCreated { get; set; }
}
