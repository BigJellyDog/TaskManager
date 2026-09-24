using TaskManager.App.ViewModels;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Services;

namespace TaskManager.App.Services;

/// <summary>
/// Показ напоминаний пользователю: баннер в боковой панели. Заодно пишет их в журнал, как раньше.
/// Регистрируется до <c>AddInfrastructure</c>, поэтому заменяет запись в журнал, которая используется по умолчанию.
/// </summary>
public sealed class ReminderNotifier(ReminderBannerViewModel banner, UserSettings settings, ILogger<ReminderNotifier> logger)
    : IReminderNotifier
{
    public async Task NotifyAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        foreach (var reminder in reminders)
        {
            logger.LogInformation(
                "Напоминание: задача #{TaskId} «{Title}», вид {Kind}, дедлайн {Deadline:u}",
                reminder.TaskId,
                reminder.Title,
                reminder.Kind,
                reminder.Deadline);
        }

        // Если показать не удалось, исключение вернётся сервису напоминаний: напоминания не отметятся показанными
        // и будут предложены снова на следующей проверке.
        await banner.ShowAsync(reminders);

#if WINDOWS
        // Системное уведомление: самое ближайшее напоминание и сколько ещё осталось. Включается в настройках.
        if (settings.ShowWindowsToast && reminders.Count > 0)
        {
            var first = reminders.OrderBy(r => r.Deadline).First();
            Platforms.Windows.SystemToast.Show(
                first.Title,
                first.Deadline.ToLocalTime().ToString("dd.MM HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                reminders.Count > 1 ? Loc.F("… и ещё {0}", reminders.Count - 1) : string.Empty);
        }
#endif
    }
}
