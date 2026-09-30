using TaskManager.Core.Domain;

namespace TaskManager.Core.Abstractions;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>A search by name, ignoring case (Cyrillic included).</summary>
    Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>All tags in alphabetical order.</summary>
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Tags that no task has (a deleted one included).</summary>
    Task<IReadOnlyList<Tag>> ListUnusedAsync(CancellationToken cancellationToken = default);

    /// <summary>All tags with the number of live tasks and a flag "not used anywhere".</summary>
    Task<IReadOnlyList<TaskManager.Core.Services.TagUsage>> ListUsageAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    /// <summary>Deletes a tag physically. Tags are not soft-deleted: it is a reference list without history.</summary>
    void Remove(Tag tag);
}
