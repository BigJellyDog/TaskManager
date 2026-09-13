using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Тег и сколько задач его используют.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Name">Название.</param>
/// <param name="TaskCount">Задач (не удалённых) с этим тегом.</param>
/// <param name="CanDelete">Тег нигде не используется, даже в корзине, и его можно удалить.</param>
public sealed record TagUsage(int Id, string Name, int TaskCount, bool CanDelete);

/// <summary>Управление тегами: список с числом задач, переименование, удаление неиспользуемых.</summary>
public class TagService
{
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;

    public TagService(ITagRepository tags, IUnitOfWork unitOfWork)
    {
        _tags = tags;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<TagUsage>> ListAsync(CancellationToken cancellationToken = default) =>
        _tags.ListUsageAsync(cancellationToken);

    /// <summary>Переименовывает тег во всех задачах. Имя, которое уже есть у другого тега, не допускается.</summary>
    public async Task RenameAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        var tag = await _tags.GetByIdAsync(id, cancellationToken) ?? throw new DomainValidationException("Тег не найден.");
        var name = newName?.Trim().TrimStart('#') ?? string.Empty;

        var existing = await _tags.GetByNameAsync(name, cancellationToken);
        if (existing is not null && existing.Id != id)
        {
            throw new DomainValidationException($"Тег «{existing.Name}» уже есть. Выберите другое название.");
        }

        tag.Rename(name);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Удаляет тег, который нигде не используется.</summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tags.GetByIdAsync(id, cancellationToken) ?? throw new DomainValidationException("Тег не найден.");
        var unused = await _tags.ListUnusedAsync(cancellationToken);

        if (unused.All(t => t.Id != id))
        {
            throw new DomainValidationException($"Тег «{tag.Name}» используется в задачах, удалить его нельзя.");
        }

        _tags.Remove(tag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
