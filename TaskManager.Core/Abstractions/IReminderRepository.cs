using TaskManager.Core.Domain;

namespace TaskManager.Core.Abstractions;

public interface IReminderRepository
{
    /// <summary>Open (not done and not cancelled) tasks with a deadline not later than <paramref name="until"/>. Deleted ones are not included.</summary>
    Task<IReadOnlyList<TaskItem>> GetRemindableTasksAsync(DateTimeOffset until, CancellationToken cancellationToken = default);

    /// <summary>The reminders already shown for the listed tasks.</summary>
    Task<IReadOnlyList<SentReminder>> GetSentAsync(IReadOnlyCollection<int> taskIds, CancellationToken cancellationToken = default);

    Task AddSentAsync(IEnumerable<SentReminder> reminders, CancellationToken cancellationToken = default);

    /// <summary>Forgets all shown reminders: at the next start of the application they are shown again.</summary>
    Task ClearSentAsync(CancellationToken cancellationToken = default);
}
