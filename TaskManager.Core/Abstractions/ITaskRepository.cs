using TaskManager.Core.Domain;
using TaskManager.Core.Querying;
using TaskManager.Core.Specifications;

namespace TaskManager.Core.Abstractions;

/// <summary>
/// Access to tasks, dependencies and history. The repository saves nothing itself:
/// changes are written through <see cref="IUnitOfWork"/>, so a task and its history end up in one transaction.
/// </summary>
public interface ITaskRepository
{
    /// <summary>A task without related data. Deleted ones are not returned by default.</summary>
    Task<TaskItem?> GetByIdAsync(int id, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>A task together with its parent, subtasks, tags and dependencies in both directions.</summary>
    /// <summary>How many tasks match the condition. One COUNT query, without loading the tasks.</summary>
    Task<int> CountAsync(ISpecification<TaskItem> filter, CancellationToken cancellationToken = default);

    Task<TaskItem?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of tasks by filter and sorting. Filtering, sorting and paging are done in the database.
    /// The tasks arrive together with tags and subtasks (for a list with progress).
    /// </summary>
    Task<PagedResult<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>Tasks by a list of Ids. Deleted ones are not returned by default.</summary>
    Task<IReadOnlyList<TaskItem>> GetByIdsAsync(IReadOnlyCollection<int> ids, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// All tasks in the order of Id together with tags and the recurrence rule, without change tracking (for export).
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetAllAsync(bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>Direct subtasks (deleted ones are not returned).</summary>
    Task<IReadOnlyList<TaskItem>> GetChildrenAsync(int parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// "By calendar" recurring tasks whose next copy has not been created yet (cancelled and deleted ones are not included).
    /// Needed to catch up on missed periods.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetPendingCalendarRecurrencesAsync(CancellationToken cancellationToken = default);

    /// <summary>Deleted tasks ("the trash").</summary>
    Task<IReadOnlyList<TaskItem>> GetDeletedAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task<TaskDependency?> GetDependencyAsync(int taskId, int dependsOnTaskId, CancellationToken cancellationToken = default);

    Task AddDependencyAsync(TaskDependency dependency, CancellationToken cancellationToken = default);

    void RemoveDependency(TaskDependency dependency);

    /// <summary>
    /// All edges of the dependency graph, including the edges of deleted tasks: a cycle must not appear
    /// after a task is restored. Needed to find cycles.
    /// </summary>
    Task<IReadOnlyList<(int TaskId, int DependsOnTaskId)>> GetDependencyGraphAsync(CancellationToken cancellationToken = default);

    Task AddHistoryAsync(IEnumerable<TaskHistoryEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>The history of a task in chronological order.</summary>
    Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(int taskId, CancellationToken cancellationToken = default);
}
