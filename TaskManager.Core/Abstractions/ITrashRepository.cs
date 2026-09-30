using TaskManager.Core.Services;

namespace TaskManager.Core.Abstractions;

/// <summary>The trash: viewing deleted tasks and deleting them for good.</summary>
public interface ITrashRepository
{
    /// <summary>
    /// Deleted top-level tasks (roots of deleted subtrees), the newest on top.
    /// The subtasks of a deleted task are not shown as separate rows, they are counted in the counter.
    /// </summary>
    Task<IReadOnlyList<TrashEntry>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>How many rows are in the trash (deleted top-level tasks). A cheap counter for the side panel.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>The identifiers of the deleted descendants of a task (subtasks of any depth), parents before children.</summary>
    Task<IReadOnlyList<int>> GetDeletedDescendantIdsAsync(int rootId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes tasks for good together with all descendants, history, reminders and dependency links.
    /// Returns how many tasks were deleted.
    /// </summary>
    Task<int> PurgeAsync(IReadOnlyCollection<int> rootIds, CancellationToken cancellationToken = default);
}
