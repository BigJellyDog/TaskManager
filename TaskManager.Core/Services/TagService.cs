using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>A tag and how many tasks use it.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="TaskCount">Tasks (not deleted) with this tag.</param>
/// <param name="CanDelete">The tag is not used anywhere, even in the trash, and can be deleted.</param>
public sealed record TagUsage(int Id, string Name, int TaskCount, bool CanDelete);

/// <summary>Tag management: a list with the number of tasks, renaming, deleting the unused ones.</summary>
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

    /// <summary>Renames a tag in all tasks. A name that another tag already has is not allowed.</summary>
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

    /// <summary>Deletes a tag that is not used anywhere.</summary>
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

    /// <summary>
    /// Deletes several tags at once. A tag that is used (or no longer exists) is skipped, never an error:
    /// the list on screen may be a moment old. Returns how many tags were removed.
    /// </summary>
    public async Task<int> DeleteManyAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var wanted = ids.ToHashSet();
        var removable = (await _tags.ListUnusedAsync(cancellationToken)).Where(t => wanted.Contains(t.Id)).ToList();

        foreach (var tag in removable)
        {
            _tags.Remove(tag);
        }

        if (removable.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return removable.Count;
    }
}
