using Foundation;
using UserNotifications;

namespace ProjectTimer;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    private readonly NotificationDelegate _notificationDelegate = new();

    protected override MauiApp CreateMauiApp()
    {
        UNUserNotificationCenter.Current.Delegate = _notificationDelegate;
        return MauiProgram.CreateMauiApp();
    }

    private sealed class NotificationDelegate : UNUserNotificationCenterDelegate
    {
        public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
        {
            completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound);
        }
    }
}
