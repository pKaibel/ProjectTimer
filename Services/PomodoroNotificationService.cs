namespace ProjectTimer.Services;

public sealed class PomodoroNotificationService
{
#if WINDOWS
    private readonly object _reminderLock = new();
    private CancellationTokenSource? _reminderCancellation;

    public event Action<ActiveTimerStateInfo, CancellationToken>? ReminderDue;
#endif

    public async Task ScheduleAsync(ActiveTimerStateInfo phase)
    {
#if WINDOWS
        CancellationToken token;
        lock (_reminderLock)
        {
            _reminderCancellation?.Cancel();
            _reminderCancellation?.Dispose();
            _reminderCancellation = new CancellationTokenSource();
            token = _reminderCancellation.Token;
        }
        _ = WaitForReminderAsync(phase, token);
        await Task.CompletedTask;
#elif ANDROID
        await ProjectTimer.Platforms.Android.PomodoroReminder.ScheduleAsync(phase);
#elif IOS
        await ProjectTimer.Platforms.iOS.PomodoroReminder.ScheduleAsync(phase);
#else
        throw new PlatformNotSupportedException("Pomodoro-Erinnerungen sind auf dieser Plattform nicht verfügbar.");
#endif
    }

    public void Cancel()
    {
#if WINDOWS
        lock (_reminderLock)
        {
            _reminderCancellation?.Cancel();
            _reminderCancellation?.Dispose();
            _reminderCancellation = null;
        }
#elif ANDROID
        ProjectTimer.Platforms.Android.PomodoroReminder.Cancel();
#elif IOS
        ProjectTimer.Platforms.iOS.PomodoroReminder.Cancel();
#endif
    }

#if WINDOWS
    private async Task WaitForReminderAsync(ActiveTimerStateInfo phase, CancellationToken cancellationToken)
    {
        try
        {
            var delay = phase.DeadlineUtc - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
            if (!cancellationToken.IsCancellationRequested)
            {
                ReminderDue?.Invoke(phase, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
#endif
}

public sealed record ActiveTimerStateInfo(DateTimeOffset DeadlineUtc, bool IsBreak);
