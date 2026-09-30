using TaskManager.Core.Abstractions;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Services;

/// <summary>A deleted top-level task in the trash.</summary>
/// <param name="Id">The task identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Status">The status in which the task was deleted.</param>
/// <param name="DeletedAt">When it was deleted (by the history); null if there is no record.</param>
/// <param name="DeletedSubtasks">How many subtasks were deleted together with it (of any depth).</param>
public sealed record TrashEntry(int Id, string Title, TaskStatus Status, DateTimeOffset? DeletedAt, int DeletedSubtasks);

/// <summary>The trash: a list, restore together with subtasks, delete for good.</summary>
public class TrashService
{
    private readonly ITrashRepository _trash;
    private readonly TaskService _tasks;

    public TrashService(ITrashRepository trash, TaskService tasks)
    {
        _trash = trash;
        _tasks = tasks;
    }

    public Task<IReadOnlyList<TrashEntry>> ListAsync(CancellationToken cancellationToken = default) =>
        _trash.ListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => _trash.CountAsync(cancellationToken);

    /// <summary>Restores tasks together with their deleted subtasks. Returns the number of restored tasks.</summary>
    public async Task<int> RestoreAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var restored = 0;
        foreach (var id in ids)
        {
            // The root first, then the descendants: a subtask cannot be returned while its parent is deleted.
            await _tasks.RestoreAsync(id, cancellationToken);
            restored++;

            foreach (var childId in await _trash.GetDeletedDescendantIdsAsync(id, cancellationToken))
            {
                await _tasks.RestoreAsync(childId, cancellationToken);
                restored++;
            }
        }

        return restored;
    }

    /// <summary>Deletes the chosen tasks (with subtasks) for good. Returns how many tasks were erased.</summary>
    public Task<int> PurgeAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        _trash.PurgeAsync(ids, cancellationToken);

    /// <summary>Empties the whole trash. Returns how many tasks were erased.</summary>
    public async Task<int> EmptyAsync(CancellationToken cancellationToken = default)
    {
        var all = await _trash.ListAsync(cancellationToken);
        return await _trash.PurgeAsync(all.Select(e => e.Id).ToList(), cancellationToken);
    }
}
