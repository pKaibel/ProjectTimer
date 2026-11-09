using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using ProjectTimer.Services;

namespace ProjectTimer.Platforms.Android;

public static class PomodoroReminder
{
    private const string ChannelId = "pomodoro";

    public static async Task ScheduleAsync(ActiveTimerStateInfo phase)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            await Permissions.RequestAsync<NotificationPermission>() != PermissionStatus.Granted)
        {
            throw new InvalidOperationException("Bitte erlaube Benachrichtigungen für Pomodoro-Erinnerungen.");
        }

        var context = global::Android.App.Application.Context;
        var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        if (OperatingSystem.IsAndroidVersionAtLeast(31) && !alarms.CanScheduleExactAlarms())
        {
            var settings = new Intent(Settings.ActionRequestScheduleExactAlarm);
            settings.SetData(global::Android.Net.Uri.Parse("package:" + context.PackageName));
            settings.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(settings);
            throw new InvalidOperationException("Bitte erlaube präzise Erinnerungen und starte Pomodoro erneut.");
        }

        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        if (!manager.AreNotificationsEnabled())
        {
            throw new InvalidOperationException("Bitte aktiviere Benachrichtigungen für ProjectTimer.");
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Pomodoro", NotificationImportance.High));
            if (manager.GetNotificationChannel(ChannelId)?.Importance == NotificationImportance.None)
            {
                throw new InvalidOperationException("Bitte aktiviere den Benachrichtigungskanal für Pomodoro.");
            }
        }
        var intent = new Intent(context, typeof(PomodoroAlarmReceiver));
        intent.PutExtra("isBreak", phase.IsBreak);
        var pending = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, phase.DeadlineUtc.ToUnixTimeMilliseconds(), pending);
    }

    public static void Cancel()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(PomodoroAlarmReceiver));
        var pending = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
        if (pending is not null)
        {
            ((AlarmManager)context.GetSystemService(Context.AlarmService)!).Cancel(pending);
            pending.Cancel();
        }
        ((NotificationManager)context.GetSystemService(Context.NotificationService)!).Cancel(1001);
    }

    private sealed class NotificationPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [("android.permission.POST_NOTIFICATIONS", true)];
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class PomodoroAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }
        var message = intent.GetBooleanExtra("isBreak", false)
            ? "Die Pause ist vorbei. Starte die nächste Fokuszeit."
            : "Die Fokuszeit ist vorbei. Starte deine Pause.";
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        var open = launch is null ? null : PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
#pragma warning disable CA1422
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(context, "pomodoro")
            : new Notification.Builder(context);
#pragma warning restore CA1422
        builder.SetContentTitle("Pomodoro").SetContentText(message)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true).SetContentIntent(open);
        ((NotificationManager)context.GetSystemService(Context.NotificationService)!).Notify(1001, builder.Build());
    }
}
