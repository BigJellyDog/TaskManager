using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>
/// Одна проверка дедлайнов: находит задачи, о которых пора напомнить, и ещё не показанные напоминания
/// отдаёт <see cref="IReminderNotifier"/>. Что показано, запоминается в БД, поэтому после перезапуска
/// приложения напоминание не повторится.
/// </summary>
public class ReminderChecker
{
    private readonly IReminderRepository _reminders;
    private readonly IReminderNotifier _notifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ReminderChecker(IReminderRepository reminders, IReminderNotifier notifier, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _reminders = reminders;
        _notifier = notifier;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <returns>Показанные при этой проверке напоминания (пусто, если нового нет).</returns>
    public async Task<IReadOnlyList<Reminder>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();

        var tasks = await _reminders.GetRemindableTasksAsync(now + ReminderPolicy.DayWindow, cancellationToken);
        var candidates = tasks
            .Where(t => t.Deadline is not null)
            .Select(t => (Task: t, Deadline: t.Deadline!.Value, Kind: ReminderPolicy.GetKind(t.Deadline!.Value, now)))
            .Where(c => c.Kind is not null)
            .ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        var sent = (await _reminders.GetSentAsync(candidates.Select(c => c.Task.Id).ToList(), cancellationToken))
            .Select(s => (s.TaskId, s.Kind, s.Deadline))
            .ToHashSet();

        var fresh = candidates
            .Where(c => !sent.Contains((c.Task.Id, c.Kind!.Value, c.Deadline)))
            .OrderBy(c => c.Deadline)
            .ToList();

        if (fresh.Count == 0)
        {
            return [];
        }

        var reminders = fresh.Select(c => new Reminder(c.Task.Id, c.Task.Title, c.Deadline, c.Kind!.Value)).ToList();

        // Сначала показываем, потом запоминаем: если показ упадёт, напоминание повторится, а не потеряется.
        await _notifier.NotifyAsync(reminders, cancellationToken);

        await _reminders.AddSentAsync(
            reminders.Select(r => new SentReminder(r.TaskId, r.Kind, r.Deadline, now)),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return reminders;
    }
}
