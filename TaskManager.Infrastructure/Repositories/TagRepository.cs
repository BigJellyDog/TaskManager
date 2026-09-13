using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Repositories;

public class TagRepository : ITagRepository
{
    private readonly TaskManagerDbContext _context;

    public TagRepository(TaskManagerDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = Tag.Normalize(name);
        return await _context.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Tags.OrderBy(t => t.NormalizedName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Tag>> ListUnusedAsync(CancellationToken cancellationToken = default)
    {
        // Удалённые задачи тоже считаются: после восстановления у них должны остаться теги.
        var usedTagIds = _context.Tasks
            .IgnoreQueryFilters()
            .SelectMany(t => t.Tags)
            .Select(tag => tag.Id);

        return await _context.Tags
            .Where(t => !usedTagIds.Contains(t.Id))
            .OrderBy(t => t.NormalizedName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskManager.Core.Services.TagUsage>> ListUsageAsync(CancellationToken cancellationToken = default)
    {
        // Фильтр мягкого удаления скрывает удалённые задачи: так считаются только живые.
        var live = await _context.Tasks
            .SelectMany(t => t.Tags)
            .GroupBy(tag => tag.Id)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var unusedIds = (await ListUnusedAsync(cancellationToken)).Select(t => t.Id).ToHashSet();

        return (await ListAsync(cancellationToken))
            .Select(t => new TaskManager.Core.Services.TagUsage(
                t.Id, t.Name, live.FirstOrDefault(x => x.Id == t.Id)?.Count ?? 0, unusedIds.Contains(t.Id)))
            .ToList();
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default) =>
        await _context.Tags.AddAsync(tag, cancellationToken);

    public void Remove(Tag tag) => _context.Tags.Remove(tag);
}
