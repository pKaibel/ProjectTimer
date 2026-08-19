using System.Globalization;
using System.Text;
using ProjectTimer.Models;

namespace ProjectTimer.Services;

public sealed class CsvBackupService
{
    private const string Header = "RecordType;ProjectName;ProjectDescription;StartUtc;EndUtc;Note";
    private readonly DatabaseService _database;

    public CsvBackupService(DatabaseService database)
    {
        _database = database;
    }

    public async Task ExportAsync()
    {
        var projects = await _database.GetProjectsAsync();
        var csv = new StringBuilder();
        csv.AppendLine(Header);

        foreach (var project in projects)
        {
            AppendRow(csv, "project", project.Name, project.Description, null, null, null);
            foreach (var entry in await _database.GetTimeEntriesAsync(project.Id))
            {
                AppendRow(csv, "entry", project.Name, project.Description, entry.StartAtUtc, entry.EndAtUtc, entry.Note);
            }
        }

        var fileName = $"ProjectTimer-Sicherung-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "ProjectTimer-Daten exportieren",
            File = new ShareFile(filePath)
        });
    }

    public async Task<ImportResult?> ImportAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "ProjectTimer-Sicherung auswählen"
        });

        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var rows = ParseCsv(await reader.ReadToEndAsync());
        if (rows.Count == 0 || !rows[0].SequenceEqual(Header.Split(';')))
        {
            throw new InvalidOperationException("Die Datei ist keine gültige ProjectTimer-CSV-Sicherung.");
        }

        var existingProjects = await _database.GetProjectsAsync();
        var projectsByName = existingProjects.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
        var existingEntries = new Dictionary<int, HashSet<string>>();
        var result = new ImportResult();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count != 6 || string.IsNullOrWhiteSpace(row[0]))
            {
                continue;
            }

            var projectName = row[1].Trim();
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new InvalidOperationException("Ein importiertes Projekt besitzt keinen Namen.");
            }

            if (!projectsByName.TryGetValue(projectName, out var project))
            {
                project = new Project { Name = projectName, Description = EmptyToNull(row[2]) };
                await _database.SaveProjectAsync(project);
                projectsByName.Add(project.Name, project);
                result.ProjectsCreated++;
            }

            if (!string.Equals(row[0], "entry", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = ParseUtc(row[3]);
            var end = ParseUtc(row[4]);
            var note = EmptyToNull(row[5]);
            if (!existingEntries.TryGetValue(project.Id, out var projectEntries))
            {
                projectEntries = (await _database.GetTimeEntriesAsync(project.Id))
                    .Select(CreateEntryKey)
                    .ToHashSet(StringComparer.Ordinal);
                existingEntries.Add(project.Id, projectEntries);
            }

            var entry = new TimeEntry
            {
                ProjectId = project.Id,
                StartAtUtc = start,
                EndAtUtc = end,
                Note = note,
                CreatedAt = DateTime.UtcNow
            };
            var key = CreateEntryKey(entry);
            if (projectEntries.Contains(key))
            {
                result.EntriesSkipped++;
                continue;
            }

            await _database.SaveTimeEntryAsync(entry);
            projectEntries.Add(key);
            result.EntriesCreated++;
        }

        return result;
    }

    private static void AppendRow(StringBuilder csv, string type, string projectName, string? description, DateTime? start, DateTime? end, string? note)
    {
        var fields = new[]
        {
            type,
            projectName,
            description ?? string.Empty,
            start?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            end?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            note ?? string.Empty
        };
        csv.AppendLine(string.Join(";", fields.Select(Escape)));
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ';' && !inQuotes)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((character == '\r' || character == '\n') && !inQuotes)
            {
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(character);
            }
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("Die CSV-Datei enthält ein unvollständiges Anführungszeichen.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static DateTime ParseUtc(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
        {
            throw new InvalidOperationException("Ein Zeiteintrag enthält ein ungültiges Datum.");
        }

        return date.ToUniversalTime();
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateEntryKey(TimeEntry entry) => $"{entry.StartUtcTicks}|{entry.EndUtcTicks}|{entry.Note}";
}

public sealed class ImportResult
{
    public int ProjectsCreated { get; set; }
    public int EntriesCreated { get; set; }
    public int EntriesSkipped { get; set; }
}
