using TaskManager.Core.Domain;

namespace TaskManager.Core.Abstractions;

public interface IReminderRepository
{
    /// <summary>Открытые (не выполнены и не отменены) задачи с дедлайном не позже <paramref name="until"/>. Удалённые не включены.</summary>
    Task<IReadOnlyList<TaskItem>> GetRemindableTasksAsync(DateTimeOffset until, CancellationToken cancellationToken = default);

    /// <summary>Уже показанные напоминания по перечисленным задачам.</summary>
    Task<IReadOnlyList<SentReminder>> GetSentAsync(IReadOnlyCollection<int> taskIds, CancellationToken cancellationToken = default);

    Task AddSentAsync(IEnumerable<SentReminder> reminders, CancellationToken cancellationToken = default);
}
