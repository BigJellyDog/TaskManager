namespace TaskManager.App.Platforms.Windows;

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

/// <summary>Системное уведомление Windows (центр уведомлений). Любой сбой только пишется в журнал: баннер в приложении остаётся.</summary>
public static class SystemToast
{
    private static bool _registered;

    /// <summary>Показывает уведомление из заголовка и одной-двух строк текста.</summary>
    public static void Show(string title, params string[] lines)
    {
        try
        {
            if (!_registered)
            {
                // Приложение запускается без пакета MSIX: перед первым показом его нужно зарегистрировать в системе уведомлений.
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
#pragma warning disable CA1031 // Уведомление необязательно: сбой системы уведомлений не должен ломать напоминания.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Serilog.Log.Warning(ex, "Не удалось показать системное уведомление");
        }
    }
}
