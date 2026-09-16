using TaskManager.Core.Services;

namespace TaskManager.Core.Abstractions;

/// <summary>Корзина: просмотр удалённых задач и окончательное удаление.</summary>
public interface ITrashRepository
{
    /// <summary>
    /// Удалённые задачи верхнего уровня (корни удалённых поддеревьев), свежие сверху.
    /// Подзадачи удалённой задачи отдельными строками не показываются, они учтены в счётчике.
    /// </summary>
    Task<IReadOnlyList<TrashEntry>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Сколько строк в корзине (удалённых задач верхнего уровня). Дешёвый счётчик для боковой панели.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Идентификаторы удалённых потомков задачи (подзадачи любой глубины), родители раньше детей.</summary>
    Task<IReadOnlyList<int>> GetDeletedDescendantIdsAsync(int rootId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Окончательно удаляет задачи вместе со всеми потомками, историей, напоминаниями и связями зависимостей.
    /// Возвращает, сколько задач удалено.
    /// </summary>
    Task<int> PurgeAsync(IReadOnlyCollection<int> rootIds, CancellationToken cancellationToken = default);
}
