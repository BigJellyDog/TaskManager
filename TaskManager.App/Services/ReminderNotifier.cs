using TaskManager.App.ViewModels;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Services;

namespace TaskManager.App.Services;

/// <summary>
/// Показ напоминаний пользователю: баннер в боковой панели. Заодно пишет их в журнал, как раньше.
/// Регистрируется до <c>AddInfrastructure</c>, поэтому заменяет запись в журнал, которая используется по умолчанию.
/// </summary>
public sealed class ReminderNotifier(ReminderBannerViewModel banner, ILogger<ReminderNotifier> logger)
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
    }
}
