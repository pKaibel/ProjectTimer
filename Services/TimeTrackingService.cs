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
    private static readonly TimeSpan MinimumTrackedDuration = TimeSpan.FromMinutes(1);
    private readonly DatabaseService _database;
    private readonly TimeEntryFactory _timeEntryFactory;
    private readonly PomodoroSettingsService _pomodoroSettings;
    private readonly PomodoroNotificationService _pomodoroNotifications;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TimeTrackingService(DatabaseService database, TimeEntryFactory timeEntryFactory, PomodoroSettingsService pomodoroSettings, PomodoroNotificationService pomodoroNotifications)
    {
        _database = database;
        _timeEntryFactory = timeEntryFactory;
        _pomodoroSettings = pomodoroSettings;
        _pomodoroNotifications = pomodoroNotifications;
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

#if WINDOWS
    public async Task RestorePomodoroReminderAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var state = (await _database.GetTimerStatesAsync()).FirstOrDefault(timer => timer.PomodoroPhase is 1 or 2);
            if (state is not null && state.PomodoroDeadlineUtcTicks > 0)
            {
                await _pomodoroNotifications.ScheduleAsync(new ActiveTimerStateInfo(
                    new DateTimeOffset(state.PomodoroDeadlineUtcTicks, TimeSpan.Zero), state.PomodoroPhase == 2));
            }
        }
        finally
        {
            _gate.Release();
        }
    }
#endif

    public async Task<ActiveTimerState> StartAsync(int projectId, string? note = null)
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
                Note = _timeEntryFactory.NormalizeNote(note),
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

    public async Task<ActiveTimerState> StartPomodoroAsync(int projectId, string? note = null)
    {
        await _gate.WaitAsync();
        try
        {
            var states = await _database.GetTimerStatesAsync();
            if (states.Any(state => state.PomodoroPhase != 0))
            {
                throw new InvalidOperationException("Es läuft bereits ein Pomodoro-Timer.");
            }
            var now = DateTime.UtcNow;
            var state = new ActiveTimerState
            {
                ProjectId = projectId,
                Note = _timeEntryFactory.NormalizeNote(note),
                StartedAtUtc = now,
                PomodoroPhase = 1,
                PomodoroFocusMinutes = _pomodoroSettings.FocusMinutes,
                PomodoroBreakMinutes = _pomodoroSettings.BreakMinutes,
                PomodoroCountBreaks = _pomodoroSettings.CountBreaks
            };
            state.PomodoroDeadlineUtcTicks = now.AddMinutes(state.PomodoroFocusMinutes).Ticks;
            await _pomodoroNotifications.ScheduleAsync(new ActiveTimerStateInfo(new DateTimeOffset(state.PomodoroDeadlineUtcTicks, TimeSpan.Zero), false));
            try
            {
                await _database.StartTimerAsync(state);
            }
            catch
            {
                _pomodoroNotifications.Cancel();
                throw;
            }
            await NotifyStatusChangedAsync();
            return state;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ActiveTimerState> AdvancePomodoroAsync(int projectId)
    {
        await _gate.WaitAsync();
        try
        {
            var state = await _database.GetTimerForProjectAsync(projectId)
                ?? throw new InvalidOperationException("Es läuft keine Zeiterfassung.");
            if (state.PomodoroPhase is < 1 or > 2)
            {
                throw new InvalidOperationException("Es läuft kein Pomodoro-Timer.");
            }
            var now = DateTime.UtcNow;
            var entry = state.PomodoroPhase == 1 && !state.PomodoroCountBreaks
                ? CreateTrackedEntryIfLongEnough(state, now)
                : null;
            var nextIsBreak = state.PomodoroPhase == 1;
            var nextDeadline = now.AddMinutes(nextIsBreak ? state.PomodoroBreakMinutes : state.PomodoroFocusMinutes);
            await _pomodoroNotifications.ScheduleAsync(new ActiveTimerStateInfo(new DateTimeOffset(nextDeadline), nextIsBreak));
            ActiveTimerState updated;
            try
            {
                updated = await _database.AdvancePomodoroAsync(state, now, entry);
            }
            catch
            {
                if (state.PomodoroDeadlineUtcTicks > DateTime.UtcNow.Ticks)
                {
                    await _pomodoroNotifications.ScheduleAsync(new ActiveTimerStateInfo(
                        new DateTimeOffset(state.PomodoroDeadlineUtcTicks, TimeSpan.Zero), state.PomodoroPhase == 2));
                }
                else
                {
                    _pomodoroNotifications.Cancel();
                }
                throw;
            }
            await NotifyStatusChangedAsync();
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ActiveTimerState> StartOrResumeReplacingRunningTimerAsync(int projectId)
    {
        await _gate.WaitAsync();
        try
        {
            var running = await _database.GetActiveTimerAsync();
            if (running?.ProjectId == projectId)
            {
                return running;
            }

            if (running is not null)
            {
                var completedEntry = CreateTrackedEntryIfLongEnough(running, DateTime.UtcNow);
                await _database.CompleteTimerAsync(running, completedEntry);
            }

            var target = await _database.GetTimerForProjectAsync(projectId);
            var startedTimer = target is { IsPaused: true }
                ? await _database.ResumeTimerAsync(projectId, DateTime.UtcNow)
                : await StartAsyncCore(projectId);

            await NotifyStatusChangedAsync();
            return startedTimer;
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

            TimeEntry? entry = active.IsPaused ? null : CreateTrackedEntryIfLongEnough(active, DateTime.UtcNow);
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

            var pausedAtUtc = DateTime.UtcNow;
            var entry = CreateTrackedEntryIfLongEnough(active, pausedAtUtc);
            await _database.PauseTimerAsync(active, entry, GetRunningElapsed(active, pausedAtUtc));
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

            var stoppedAtUtc = pausedAtUtc ?? DateTime.UtcNow;
            var entry = CreateTrackedEntryIfLongEnough(active, stoppedAtUtc);
            await _database.PauseTimerAsync(active, entry, GetRunningElapsed(active, stoppedAtUtc));
            RunningTimerPaused?.Invoke(this, EventArgs.Empty);
            await NotifyStatusChangedAsync();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> StopRunningTimerAsync(DateTime? stoppedAtUtc = null)
    {
        await _gate.WaitAsync();
        try
        {
            var active = await _database.GetActiveTimerAsync();
            if (active is null)
            {
                return false;
            }

            var entry = CreateTrackedEntryIfLongEnough(active, stoppedAtUtc ?? DateTime.UtcNow);
            await _database.CompleteTimerAsync(active, entry);
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

            var switchedAtUtc = DateTime.UtcNow;
            var entry = CreateTrackedEntryIfLongEnough(running, switchedAtUtc);
            ActiveTimerState switchedTimer;
            if (target is not null)
            {
                // Returning to a paused project finishes the temporary timer.
                await _database.CompleteTimerAsync(running, entry);
                switchedTimer = await _database.ResumeTimerAsync(toProjectId, switchedAtUtc);
            }
            else
            {
                // A new target keeps the source available as a paused timer.
                await _database.PauseTimerAsync(running, entry, GetRunningElapsed(running, switchedAtUtc));
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

    public async Task<ActiveTimerState> UpdateNoteAsync(int projectId, string? note)
    {
        await _gate.WaitAsync();
        try
        {
            var active = await _database.GetTimerForProjectAsync(projectId)
                ?? throw new InvalidOperationException("Es läuft keine Zeiterfassung.");
            return await _database.UpdateTimerNoteAsync(active, note);
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

    private async Task NotifyStatusChangedAsync()
    {
        var states = await _database.GetTimerStatesAsync();
        if (states.All(state => state.PomodoroPhase == 0))
        {
            _pomodoroNotifications.Cancel();
        }
        var status = states.Any(state => !state.IsPaused) ? TimeTrackingStatus.Running
            : states.Any() ? TimeTrackingStatus.Paused : TimeTrackingStatus.Idle;
        StatusChanged?.Invoke(status);
    }

    private TimeEntry? CreateTrackedEntryIfLongEnough(ActiveTimerState activeTimer, DateTime endedAtUtc)
    {
        var entry = _timeEntryFactory.CreateTracked(activeTimer, endedAtUtc);
        return entry.Duration >= MinimumTrackedDuration ? entry : null;
    }

    private static TimeSpan GetRunningElapsed(ActiveTimerState activeTimer, DateTime endedAtUtc)
    {
        var elapsed = endedAtUtc.ToUniversalTime() - activeTimer.StartedAtUtc;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

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
