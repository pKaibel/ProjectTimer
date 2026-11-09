using Foundation;
using ProjectTimer.Services;
using UserNotifications;

namespace ProjectTimer.Platforms.iOS;

public static class PomodoroReminder
{
    private const string Identifier = "pomodoro";

    public static async Task ScheduleAsync(ActiveTimerStateInfo phase)
    {
        var center = UNUserNotificationCenter.Current;
        var (granted, error) = await center.RequestAuthorizationAsync(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound);
        if (!granted || error is not null)
        {
            throw new InvalidOperationException("Bitte erlaube Benachrichtigungen für Pomodoro-Erinnerungen.");
        }
        var content = new UNMutableNotificationContent
        {
            Title = "Pomodoro",
            Body = phase.IsBreak ? "Die Pause ist vorbei. Starte die nächste Fokuszeit." : "Die Fokuszeit ist vorbei. Starte deine Pause.",
            Sound = UNNotificationSound.Default
        };
        var seconds = Math.Max(1, (phase.DeadlineUtc - DateTimeOffset.UtcNow).TotalSeconds);
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(seconds, false);
        var request = UNNotificationRequest.FromIdentifier(Identifier, content, trigger);
        var completion = new TaskCompletionSource<NSError?>(TaskCreationOptions.RunContinuationsAsynchronously);
        center.AddNotificationRequest(request, error => completion.SetResult(error));
        if (await completion.Task is not null)
        {
            throw new InvalidOperationException("Die Pomodoro-Erinnerung konnte nicht eingerichtet werden.");
        }
    }

    public static void Cancel()
    {
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([Identifier]);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications([Identifier]);
    }
}
