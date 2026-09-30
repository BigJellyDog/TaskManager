using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Repositories;

public class TrashRepository : ITrashRepository
{
    private readonly TaskManagerDbContext _context;

    public TrashRepository(TaskManagerDbContext context)
    {
        _context = context;
    }

    private IQueryable<TaskItem> AllTasks => _context.Tasks.IgnoreQueryFilters();

    public async Task<IReadOnlyList<TrashEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var deleted = await AllTasks.AsNoTracking()
            .Where(t => t.IsDeleted)
            .Select(t => new { t.Id, t.ParentId, t.Title, t.Status })
            .ToListAsync(cancellationToken);
        var deletedIds = deleted.Select(t => t.Id).ToHashSet();

        // Roots: the task has no parent or the parent is not in the trash.
        var roots = deleted.Where(t => t.ParentId is null || !deletedIds.Contains(t.ParentId.Value)).ToList();
        var childrenOf = deleted.Where(t => t.ParentId is not null).ToLookup(t => t.ParentId!.Value, t => t.Id);

        // When it was deleted: the last history record about deletion.
        var times = await _context.HistoryEntries.AsNoTracking()
            .Where(h => h.FieldName == "IsDeleted" && h.NewValue == "True" && deletedIds.Contains(h.TaskId))
            .Select(h => new { h.TaskId, h.ChangedAt })
            .ToListAsync(cancellationToken);
        var lastDeleted = times.GroupBy(x => x.TaskId).ToDictionary(g => g.Key, g => g.Max(x => x.ChangedAt));

        int CountDescendants(int id) => childrenOf[id].Sum(c => 1 + CountDescendants(c));

        return roots
            .Select(t => new TrashEntry(
                t.Id, t.Title, t.Status, lastDeleted.TryGetValue(t.Id, out var at) ? at : null, CountDescendants(t.Id)))
            .OrderByDescending(e => e.DeletedAt)
            .ThenByDescending(e => e.Id)
            .ToList();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var all = AllTasks;
        return await all
            .Where(t => t.IsDeleted && (t.ParentId == null || !all.Any(p => p.Id == t.ParentId && p.IsDeleted)))
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetDeletedDescendantIdsAsync(int rootId, CancellationToken cancellationToken = default)
    {
        var deleted = await AllTasks.AsNoTracking()
            .Where(t => t.IsDeleted && t.ParentId != null)
            .Select(t => new { t.Id, ParentId = t.ParentId!.Value })
            .ToListAsync(cancellationToken);
        var childrenOf = deleted.ToLookup(t => t.ParentId, t => t.Id);

        var result = new List<int>();
        var queue = new Queue<int>([rootId]);
        while (queue.Count > 0)
        {
            foreach (var child in childrenOf[queue.Dequeue()])
            {
                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }

    public async Task<int> PurgeAsync(IReadOnlyCollection<int> rootIds, CancellationToken cancellationToken = default)
    {
        if (rootIds.Count == 0)
        {
            return 0;
        }

        // We delete only what is really in the trash, and all descendants.
        var links = await AllTasks.AsNoTracking()
            .Where(t => t.ParentId != null)
            .Select(t => new { t.Id, ParentId = t.ParentId!.Value })
            .ToListAsync(cancellationToken);
        var childrenOf = links.ToLookup(t => t.ParentId, t => t.Id);
        var inTrash = (await AllTasks.AsNoTracking().Where(t => t.IsDeleted).Select(t => t.Id).ToListAsync(cancellationToken)).ToHashSet();

        var ids = new HashSet<int>();
        var queue = new Queue<int>(rootIds.Where(inTrash.Contains));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (ids.Add(id))
            {
                foreach (var child in childrenOf[id])
                {
                    queue.Enqueue(child);
                }
            }
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        var list = ids.ToList();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // The dependency links on both sides, then the history and the reminders, then the tasks themselves.
        // IgnoreQueryFilters is required: the link has a navigation to the task, and the soft-delete filter hides links
        // with deleted tasks. Without it they would not be deleted, and deleting the tasks themselves would fail on the foreign key.
        await _context.TaskDependencies.IgnoreQueryFilters().Where(d => list.Contains(d.TaskId) || list.Contains(d.DependsOnTaskId))
            .ExecuteDeleteAsync(cancellationToken);
        await _context.HistoryEntries.Where(h => list.Contains(h.TaskId)).ExecuteDeleteAsync(cancellationToken);
        await _context.SentReminders.Where(r => list.Contains(r.TaskId)).ExecuteDeleteAsync(cancellationToken);

        // The ParentId foreign key is ON DELETE RESTRICT, and SQLite checks it at once for every row, not at the end of the command:
        // a parent and subtasks in one DELETE fail with "FOREIGN KEY constraint failed" (it depends on the row order).
        // So we first break the links between the tasks being deleted.
        await AllTasks.Where(t => list.Contains(t.Id) && t.ParentId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ParentId, (int?)null), cancellationToken);
        var removed = await AllTasks.Where(t => list.Contains(t.Id)).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return removed;
    }
}
