using TaskManager.Core.Domain;

namespace TaskManager.Core.Abstractions;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Поиск по имени без учёта регистра (в том числе для кириллицы).</summary>
    Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Все теги по алфавиту.</summary>
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Теги, которых нет ни у одной задачи (в том числе удалённой).</summary>
    Task<IReadOnlyList<Tag>> ListUnusedAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    /// <summary>Физически удаляет тег. Теги не удаляются мягко: это справочник без истории.</summary>
    void Remove(Tag tag);
}
