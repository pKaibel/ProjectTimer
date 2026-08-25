using ProjectTimer.Models;
using SQLite;

namespace ProjectTimer.Services;

public sealed class DatabaseService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly TimeEntryFactory _timeEntryFactory;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public DatabaseService(TimeEntryFactory timeEntryFactory)
    {
        _timeEntryFactory = timeEntryFactory;
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "projecttimer.db3");
        _database = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache | SQLiteOpenFlags.FullMutex);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await _database.CreateTableAsync<Project>();
            await _database.CreateTableAsync<TimeEntry>();
            await _database.CreateTableAsync<ActiveTimerState>();
            await MigrateActiveTimerStateAsync();
            await MigrateActiveTimerColumnsAsync();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task MigrateActiveTimerStateAsync()
    {
        // The first development version used ActiveTimer. Keep a running timer if
        // an existing installation is updated to the canonical ActiveTimeEntry table.
        var legacyTableExists = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?",
            "ActiveTimer");
        if (legacyTableExists == 0)
        {
            return;
        }

        await _database.ExecuteAsync(
            """
            INSERT OR IGNORE INTO ActiveTimeEntry (Id, ProjectId, StartedAtUtcTicks)
            SELECT Id, ProjectId, StartedAtUtcTicks
            FROM ActiveTimer
            WHERE Id = 1
            """);
    }

    private async Task MigrateActiveTimerColumnsAsync()
    {
        var columns = await _database.QueryAsync<DatabaseColumn>("PRAGMA table_info(ActiveTimeEntry)");
        if (columns.All(column => column.Name != "AccumulatedTicks"))
        {
            await _database.ExecuteAsync(
                "ALTER TABLE ActiveTimeEntry ADD COLUMN AccumulatedTicks INTEGER NOT NULL DEFAULT 0");
        }

        if (columns.All(column => column.Name != "IsPaused"))
        {
            await _database.ExecuteAsync(
                "ALTER TABLE ActiveTimeEntry ADD COLUMN IsPaused INTEGER NOT NULL DEFAULT 0");
        }

        if (columns.All(column => column.Name != "Note"))
        {
            await _database.ExecuteAsync(
                "ALTER TABLE ActiveTimeEntry ADD COLUMN Note TEXT NULL");
        }
    }

    public async Task<List<Project>> GetProjectsAsync()
    {
        await InitializeAsync();
        return await _database.Table<Project>().OrderBy(project => project.Name).ToListAsync();
    }

    public async Task<List<ProjectTotal>> GetProjectsWithTotalsAsync()
    {
        await InitializeAsync();
        const string sql = """
            WITH ProjectTotals AS
            (
                SELECT p.Id, p.Name, p.Description, p.CreatedAtUtcTicks,
                       COALESCE(SUM(t.EndUtcTicks - t.StartUtcTicks), 0) AS TotalTicks,
                       MAX(CASE
                               WHEN t.EndUtcTicks > t.CreatedAtUtcTicks THEN t.EndUtcTicks
                               ELSE t.CreatedAtUtcTicks
                           END) AS LastEntryUtcTicks
                FROM Projects p
                LEFT JOIN TimeEntries t ON t.ProjectId = p.Id
                GROUP BY p.Id, p.Name, p.Description, p.CreatedAtUtcTicks
            )
            SELECT p.Id, p.Name, p.Description, p.CreatedAtUtcTicks, p.TotalTicks
            FROM ProjectTotals p
            LEFT JOIN ActiveTimeEntry a ON a.ProjectId = p.Id
            ORDER BY MAX(
                         p.CreatedAtUtcTicks,
                         COALESCE(p.LastEntryUtcTicks, 0),
                         COALESCE(a.StartedAtUtcTicks, 0)
                     ) DESC,
                     p.Name COLLATE NOCASE
            """;
        return await _database.QueryAsync<ProjectTotal>(sql);
    }

    public async Task<Project?> GetProjectAsync(int id)
    {
        await InitializeAsync();
        return await _database.FindAsync<Project>(id);
    }

    public async Task<int> SaveProjectAsync(Project project)
    {
        await InitializeAsync();
        project.Name = project.Name.Trim();
        project.Description = string.IsNullOrWhiteSpace(project.Description) ? null : project.Description.Trim();

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            throw new ArgumentException("Bitte geben Sie einen Projektnamen ein.");
        }

        if (project.Name.Length > 200 || project.Description?.Length > 2000)
        {
            throw new ArgumentException("Projektname oder Beschreibung ist zu lang.");
        }

        if (project.Id == 0)
        {
            project.CreatedAt = DateTime.UtcNow;
            return await _database.InsertAsync(project);
        }

        if (await _database.FindAsync<Project>(project.Id) is null)
        {
            throw new InvalidOperationException("Das Projekt wurde nicht gefunden.");
        }

        return await _database.UpdateAsync(project);
    }

    public async Task<int> DeleteProjectAsync(Project project)
    {
        await InitializeAsync();
        if (project.Id <= 0)
        {
            return 0;
        }

        var deleted = 0;
        await _database.RunInTransactionAsync(connection =>
        {
            var active = connection.Table<ActiveTimerState>().FirstOrDefault(timer => timer.ProjectId == project.Id);
            if (active is not null)
            {
                throw new InvalidOperationException("Ein Projekt mit laufender Zeiterfassung kann nicht gelöscht werden.");
            }

            connection.Execute("DELETE FROM TimeEntries WHERE ProjectId = ?", project.Id);
            deleted = connection.Delete<Project>(project.Id);
        });
        return deleted;
    }

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(int projectId)
    {
        await InitializeAsync();
        return await _database.Table<TimeEntry>()
            .Where(entry => entry.ProjectId == projectId)
            .OrderByDescending(entry => entry.StartUtcTicks)
            .ToListAsync();
    }

    public async Task<List<TimeEntry>> GetAllTimeEntriesAsync()
    {
        await InitializeAsync();
        return await _database.Table<TimeEntry>().ToListAsync();
    }

    public async Task<TimeEntry?> GetTimeEntryAsync(int id)
    {
        await InitializeAsync();
        return await _database.FindAsync<TimeEntry>(id);
    }

    public async Task<TimeSpan> GetTotalDurationAsync(int projectId)
    {
        await InitializeAsync();
        var totalTicks = await _database.ExecuteScalarAsync<long>(
            "SELECT COALESCE(SUM(EndUtcTicks - StartUtcTicks), 0) FROM TimeEntries WHERE ProjectId = ?",
            projectId);
        return TimeSpan.FromTicks(Math.Max(0, totalTicks));
    }

    public async Task<int> SaveTimeEntryAsync(TimeEntry entry)
    {
        await InitializeAsync();
        _timeEntryFactory.Validate(entry);

        if (await _database.FindAsync<Project>(entry.ProjectId) is null)
        {
            throw new InvalidOperationException("Das zugehörige Projekt existiert nicht mehr.");
        }

        if (entry.Id == 0)
        {
            entry.CreatedAt = DateTime.UtcNow;
            return await _database.InsertAsync(entry);
        }

        var existing = await _database.FindAsync<TimeEntry>(entry.Id)
            ?? throw new InvalidOperationException("Der Zeiteintrag wurde nicht gefunden.");
        if (existing.ProjectId != entry.ProjectId)
        {
            throw new InvalidOperationException("Ein Zeiteintrag kann nicht in ein anderes Projekt verschoben werden.");
        }

        return await _database.UpdateAsync(entry);
    }

    public async Task<int> DeleteTimeEntryAsync(TimeEntry entry)
    {
        await InitializeAsync();
        return entry.Id <= 0 ? 0 : await _database.DeleteAsync(entry);
    }

    public async Task<ActiveTimerState?> GetActiveTimerAsync()
    {
        await InitializeAsync();
        return await _database.Table<ActiveTimerState>()
            .Where(timer => !timer.IsPaused)
            .FirstOrDefaultAsync();
    }

    public async Task<ActiveTimerState?> GetTimerForProjectAsync(int projectId)
    {
        await InitializeAsync();
        return await _database.Table<ActiveTimerState>()
            .Where(timer => timer.ProjectId == projectId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ActiveTimerState>> GetTimerStatesAsync()
    {
        await InitializeAsync();
        return await _database.Table<ActiveTimerState>().ToListAsync();
    }

    public async Task StartTimerAsync(ActiveTimerState state)
    {
        await InitializeAsync();
        state.Note = _timeEntryFactory.NormalizeNote(state.Note);
        await _database.RunInTransactionAsync(connection =>
        {
            if (connection.Find<Project>(state.ProjectId) is null)
            {
                throw new InvalidOperationException("Das Projekt existiert nicht mehr.");
            }

            if (connection.Table<ActiveTimerState>().Any(timer => !timer.IsPaused))
            {
                throw new InvalidOperationException("Es läuft bereits eine Zeiterfassung.");
            }

            if (connection.Table<ActiveTimerState>().Any(timer => timer.ProjectId == state.ProjectId))
            {
                throw new InvalidOperationException("Für dieses Projekt ist bereits eine Zeiterfassung pausiert.");
            }

            connection.Insert(state);
        });
    }

    public async Task PauseTimerAsync(ActiveTimerState expectedState, TimeEntry pausedEntry)
    {
        await InitializeAsync();
        _timeEntryFactory.Validate(pausedEntry);

        await _database.RunInTransactionAsync(connection =>
        {
            var current = FindExpectedActiveTimer(connection, expectedState);
            if (current.IsPaused)
            {
                throw new InvalidOperationException("Die Zeiterfassung ist bereits pausiert.");
            }

            connection.Insert(pausedEntry);
            current.AccumulatedTicks += pausedEntry.Duration.Ticks;
            current.IsPaused = true;
            connection.Update(current);
        });
    }

    public async Task<ActiveTimerState> ResumeTimerAsync(int projectId, DateTime resumedAtUtc)
    {
        await InitializeAsync();
        ActiveTimerState? resumedTimer = null;
        await _database.RunInTransactionAsync(connection =>
        {
            var current = connection.Table<ActiveTimerState>()
                .FirstOrDefault(timer => timer.ProjectId == projectId)
                ?? throw new InvalidOperationException("Es läuft keine Zeiterfassung.");
            if (current.ProjectId != projectId)
            {
                throw new InvalidOperationException("Die pausierte Zeiterfassung gehört zu einem anderen Projekt.");
            }

            if (!current.IsPaused)
            {
                throw new InvalidOperationException("Die Zeiterfassung läuft bereits.");
            }

            if (connection.Table<ActiveTimerState>().Any(timer => !timer.IsPaused))
            {
                throw new InvalidOperationException("Für ein anderes Projekt läuft bereits eine Zeiterfassung.");
            }

            current.StartedAtUtc = resumedAtUtc;
            current.IsPaused = false;
            connection.Update(current);
            resumedTimer = current;
        });
        return resumedTimer ?? throw new InvalidOperationException("Die Zeiterfassung konnte nicht fortgesetzt werden.");
    }

    public async Task CompleteTimerAsync(ActiveTimerState expectedState, TimeEntry? completedEntry)
    {
        await InitializeAsync();
        if (completedEntry is not null)
        {
            _timeEntryFactory.Validate(completedEntry);
        }

        await _database.RunInTransactionAsync(connection =>
        {
            var current = FindExpectedActiveTimer(connection, expectedState);
            if (current.IsPaused && completedEntry is not null)
            {
                throw new InvalidOperationException("Eine pausierte Zeiterfassung kann nicht weiterlaufen.");
            }

            if (!current.IsPaused && completedEntry is null)
            {
                throw new InvalidOperationException("Der laufende Timer kann nicht ohne Zeiteintrag beendet werden.");
            }

            if (connection.Find<Project>(current.ProjectId) is null)
            {
                throw new InvalidOperationException("Das zugehörige Projekt existiert nicht mehr.");
            }

            if (completedEntry is not null)
            {
                connection.Insert(completedEntry);
            }
            connection.Delete(current);
        });
    }

    private static ActiveTimerState FindExpectedActiveTimer(SQLiteConnection connection, ActiveTimerState expectedState)
    {
        var current = connection.Find<ActiveTimerState>(expectedState.Id)
            ?? throw new InvalidOperationException("Es läuft keine Zeiterfassung.");
        if (current.ProjectId != expectedState.ProjectId
            || current.StartedAtUtcTicks != expectedState.StartedAtUtcTicks
            || current.AccumulatedTicks != expectedState.AccumulatedTicks
            || current.IsPaused != expectedState.IsPaused
            || current.Note != expectedState.Note)
        {
            throw new InvalidOperationException("Der laufende Timer wurde zwischenzeitlich geändert.");
        }

        return current;
    }

    private sealed class DatabaseColumn
    {
        public string Name { get; set; } = string.Empty;
    }
}
