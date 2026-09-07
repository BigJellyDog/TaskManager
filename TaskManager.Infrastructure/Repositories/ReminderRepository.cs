using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Infrastructure.Persistence;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Infrastructure.Repositories;

public class ReminderRepository : IReminderRepository
{
    private readonly TaskManagerDbContext _context;

    public ReminderRepository(TaskManagerDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TaskItem>> GetRemindableTasksAsync(DateTimeOffset until, CancellationToken cancellationToken = default) =>
        await _context.Tasks
            .AsNoTracking()
            .Where(t => t.Deadline != null
                && t.Deadline <= until
                && t.Status != TaskStatus.Done
                && t.Status != TaskStatus.Cancelled)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SentReminder>> GetSentAsync(IReadOnlyCollection<int> taskIds, CancellationToken cancellationToken = default) =>
        await _context.SentReminders
            .AsNoTracking()
            .Where(r => taskIds.Contains(r.TaskId))
            .ToListAsync(cancellationToken);

    public async Task AddSentAsync(IEnumerable<SentReminder> reminders, CancellationToken cancellationToken = default) =>
        await _context.SentReminders.AddRangeAsync(reminders, cancellationToken);
}
