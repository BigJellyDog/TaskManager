namespace TaskManager.App.Platforms.Windows;

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

/// <summary>A Windows system notification (the notification centre). Any failure is only written to the log: the banner in the application stays.</summary>
public static class SystemToast
{
    private static bool _registered;

    /// <summary>Shows a notification made of a title and one or two lines of text.</summary>
    public static void Show(string title, params string[] lines)
    {
        try
        {
            if (!_registered)
            {
                // The application starts without an MSIX package: before the first show it has to be registered with the notification system.
                AppNotificationManager.Default.Register();
                _registered = true;
            }

            var builder = new AppNotificationBuilder().AddText(title);
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
            {
                builder.AddText(line);
            }

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
#pragma warning disable CA1031 // A notification is optional: a failure of the notification system must not break the reminders.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Serilog.Log.Warning(ex, "Не удалось показать системное уведомление");
        }
    }
}
