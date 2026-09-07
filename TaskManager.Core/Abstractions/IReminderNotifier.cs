using TaskManager.Core.Services;

namespace TaskManager.Core.Abstractions;

/// <summary>
/// Показ напоминаний пользователю. Core только вычисляет, что пора напомнить, а как показать (баннер,
/// уведомление Windows, лог) решает реализация, которую подключает приложение.
/// </summary>
public interface IReminderNotifier
{
    /// <summary>
    /// Показывает пачку напоминаний. Если метод бросит исключение, напоминания не считаются показанными
    /// и будут предложены снова на следующей проверке.
    /// </summary>
    Task NotifyAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default);
}
