using TaskManager.Core.Domain;
using TaskManager.Core.Querying;

namespace TaskManager.Core.Abstractions;

/// <summary>
/// Доступ к задачам, зависимостям и истории. Репозиторий ничего не сохраняет сам:
/// изменения записываются через <see cref="IUnitOfWork"/>, поэтому задача и её история попадают в одну транзакцию.
/// </summary>
public interface ITaskRepository
{
    /// <summary>Задача без связанных данных. Удалённые по умолчанию не возвращаются.</summary>
    Task<TaskItem?> GetByIdAsync(int id, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>Задача вместе с родителем, подзадачами, тегами и зависимостями в обе стороны.</summary>
    Task<TaskItem?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Страница задач по фильтру и сортировке. Фильтрация, сортировка и постраничность выполняются в БД.
    /// Задачи приходят вместе с тегами и подзадачами (для списка с прогрессом).
    /// </summary>
    Task<PagedResult<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>Задачи по списку Id. Удалённые по умолчанию не возвращаются.</summary>
    Task<IReadOnlyList<TaskItem>> GetByIdsAsync(IReadOnlyCollection<int> ids, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>Прямые подзадачи (удалённые не возвращаются).</summary>
    Task<IReadOnlyList<TaskItem>> GetChildrenAsync(int parentId, CancellationToken cancellationToken = default);

    /// <summary>Удалённые задачи ("корзина").</summary>
    Task<IReadOnlyList<TaskItem>> GetDeletedAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task<TaskDependency?> GetDependencyAsync(int taskId, int dependsOnTaskId, CancellationToken cancellationToken = default);

    Task AddDependencyAsync(TaskDependency dependency, CancellationToken cancellationToken = default);

    void RemoveDependency(TaskDependency dependency);

    /// <summary>
    /// Все рёбра графа зависимостей, включая рёбра удалённых задач: цикл не должен появиться
    /// после восстановления задачи. Нужно для поиска циклов.
    /// </summary>
    Task<IReadOnlyList<(int TaskId, int DependsOnTaskId)>> GetDependencyGraphAsync(CancellationToken cancellationToken = default);

    Task AddHistoryAsync(IEnumerable<TaskHistoryEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>История задачи в хронологическом порядке.</summary>
    Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(int taskId, CancellationToken cancellationToken = default);
}
