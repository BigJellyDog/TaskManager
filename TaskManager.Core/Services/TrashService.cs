using TaskManager.Core.Abstractions;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Services;

/// <summary>Удалённая задача верхнего уровня в корзине.</summary>
/// <param name="Id">Идентификатор задачи.</param>
/// <param name="Title">Название.</param>
/// <param name="Status">Статус, в котором задачу удалили.</param>
/// <param name="DeletedAt">Когда удалена (по истории); null, если записи нет.</param>
/// <param name="DeletedSubtasks">Сколько подзадач удалено вместе с ней (любой глубины).</param>
public sealed record TrashEntry(int Id, string Title, TaskStatus Status, DateTimeOffset? DeletedAt, int DeletedSubtasks);

/// <summary>Корзина: список, восстановление вместе с подзадачами, окончательное удаление.</summary>
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

    /// <summary>Восстанавливает задачи вместе с их удалёнными подзадачами. Возвращает число восстановленных задач.</summary>
    public async Task<int> RestoreAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var restored = 0;
        foreach (var id in ids)
        {
            // Сначала корень, потом потомки: подзадачу нельзя вернуть, пока удалён её родитель.
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

    /// <summary>Удаляет навсегда выбранные задачи (с подзадачами). Возвращает, сколько задач стёрто.</summary>
    public Task<int> PurgeAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        _trash.PurgeAsync(ids, cancellationToken);

    /// <summary>Очищает корзину целиком. Возвращает, сколько задач стёрто.</summary>
    public async Task<int> EmptyAsync(CancellationToken cancellationToken = default)
    {
        var all = await _trash.ListAsync(cancellationToken);
        return await _trash.PurgeAsync(all.Select(e => e.Id).ToList(), cancellationToken);
    }
}
