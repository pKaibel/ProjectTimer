using ProjectTimer.Models;

namespace ProjectTimer.Services;

public enum TimeTrackingStatus
{
    Idle,
    Running,
    Paused
}

public sealed class TimeTrackingService
{
    private readonly DatabaseService _database;
    private readonly TimeEntryFactory _timeEntryFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TimeTrackingService(DatabaseService database, TimeEntryFactory timeEntryFactory)
    {
        _database = database;
        _timeEntryFactory = timeEntryFactory;
    }

    public Task<ActiveTimerState?> GetActiveTimerAsync() => _database.GetActiveTimerAsync();

    public event EventHandler? RunningTimerPaused;
    public event Action<TimeTrackingStatus>? StatusChanged;

    public Task<ActiveTimerState?> GetTimerForProjectAsync(int projectId) => _database.GetTimerForProjectAsync(projectId);

    public Task<List<ActiveTimerState>> GetTimerStatesAsync() => _database.GetTimerStatesAsync();

    public async Task<TimeTrackingStatus> GetStatusAsync()
    {
        var timers = await _database.GetTimerStatesAsync();
        return timers.Any(timer => !timer.IsPaused)
            ? TimeTrackingStatus.Running
            : timers.Any()
                ? TimeTrackingStatus.Paused
                : TimeTrackingStatus.Idle;
    }

    public async Task<ActiveTimerState> StartAsync(int projectId)
    {
        await _gate.WaitAsync();
        try
        {
            var existing = await _database.GetActiveTimerAsync();
            if (existing is not null)
            {
                throw new InvalidOperationException("Es läuft bereits eine Zeiterfassung.");
            }

            var state = new ActiveTimerState
            {
                ProjectId = projectId,
                StartedAtUtc = DateTime.UtcNow,
                AccumulatedTicks = 0,
                IsPaused = false
            };
            await _database.StartTimerAsync(state);
            await NotifyStatusChangedAsync();
            return state;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(int projectId)
    {
        await _gate.WaitAsync();
        try
        {
            var active = await _database.GetTimerForProjectAsync(projectId)
                ?? throw new InvalidOperationException("Es läuft keine Zeiterfassung.");

            TimeEntry? entry = active.IsPaused ? null : _timeEntryFactory.CreateTracked(active, DateTime.UtcNow);
            await _database.CompleteTimerAsync(active, entry);
            await NotifyStatusChangedAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PauseAsync(int projectId)
    {
        await _gate.WaitAsync();
        try
        {
            var active = await _database.GetActiveTimerAsync()
                ?? throw new InvalidOperationException("Es läuft keine Zeiterfassung.");
            if (active.ProjectId != projectId)
            {
                throw new InvalidOperationException("Die laufende Zeiterfassung gehört zu einem anderen Projekt.");
            }

            if (active.IsPaused)
            {
                throw new InvalidOperationException("Die Zeiterfassung ist bereits pausiert.");
            }

            var entry = _timeEntryFactory.CreateTracked(active, DateTime.UtcNow);
            await _database.PauseTimerAsync(active, entry);
            await NotifyStatusChangedAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> PauseRunningTimerAsync(DateTime? pausedAtUtc = null)
    {
        await _gate.WaitAsync();
        try
        {
            var active = await _database.GetActiveTimerAsync();
            if (active is null)
            {
                return false;
            }

            var entry = _timeEntryFactory.CreateTracked(active, pausedAtUtc ?? DateTime.UtcNow);
            await _database.PauseTimerAsync(active, entry);
            RunningTimerPaused?.Invoke(this, EventArgs.Empty);
            await NotifyStatusChangedAsync();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ActiveTimerState> SwitchAsync(int fromProjectId, int toProjectId)
    {
        if (fromProjectId == toProjectId)
        {
            throw new ArgumentException("Bitte wählen Sie ein anderes Projekt.");
        }

        await _gate.WaitAsync();
        try
        {
            var running = await _database.GetActiveTimerAsync()
                ?? throw new InvalidOperationException("Es läuft keine Zeiterfassung.");
            if (running.ProjectId != fromProjectId)
            {
                throw new InvalidOperationException("Die laufende Zeiterfassung wurde zwischenzeitlich geändert.");
            }

            var target = await _database.GetTimerForProjectAsync(toProjectId);
            if (target is { IsPaused: false })
            {
                throw new InvalidOperationException("Für das Zielprojekt läuft bereits eine Zeiterfassung.");
            }

            var entry = _timeEntryFactory.CreateTracked(running, DateTime.UtcNow);
            ActiveTimerState switchedTimer;
            if (target is not null)
            {
                // Returning to a paused project finishes the temporary timer.
                await _database.CompleteTimerAsync(running, entry);
                switchedTimer = await _database.ResumeTimerAsync(toProjectId, DateTime.UtcNow);
            }
            else
            {
                // A new target keeps the source available as a paused timer.
                await _database.PauseTimerAsync(running, entry);
                switchedTimer = await StartAsyncCore(toProjectId);
            }

            await NotifyStatusChangedAsync();
            return switchedTimer;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ActiveTimerState> ResumeAsync(int projectId)
    {
        await _gate.WaitAsync();
        try
        {
            var resumedTimer = await _database.ResumeTimerAsync(projectId, DateTime.UtcNow);
            await NotifyStatusChangedAsync();
            return resumedTimer;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ActiveTimerState> StartAsyncCore(int projectId)
    {
        var state = new ActiveTimerState
        {
            ProjectId = projectId,
            StartedAtUtc = DateTime.UtcNow,
            AccumulatedTicks = 0,
            IsPaused = false
        };
        await _database.StartTimerAsync(state);
        return state;
    }

    private async Task NotifyStatusChangedAsync() => StatusChanged?.Invoke(await GetStatusAsync());

    public static TimeSpan GetElapsed(ActiveTimerState state, DateTime? nowUtc = null)
    {
        var elapsed = TimeSpan.FromTicks(Math.Max(0, state.AccumulatedTicks));
        if (!state.IsPaused)
        {
            elapsed += (nowUtc ?? DateTime.UtcNow).ToUniversalTime() - state.StartedAtUtc;
        }
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }
}
