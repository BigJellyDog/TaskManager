using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly TaskManagerDbContext _context;

    public TaskRepository(TaskManagerDbContext context)
    {
        _context = context;
    }

    public async Task<TaskItem?> GetByIdAsync(int id, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var query = includeDeleted ? _context.Tasks.IgnoreQueryFilters() : _context.Tasks;
        return await query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<TaskItem?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Tasks
            .Include(t => t.Parent)
            .Include(t => t.Children)
            .Include(t => t.Tags)
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOnTask)
            .Include(t => t.Dependents).ThenInclude(d => d.Task)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> GetDeletedAsync(CancellationToken cancellationToken = default) =>
        await _context.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default) =>
        await _context.Tasks.AddAsync(task, cancellationToken);

    public Task<TaskDependency?> GetDependencyAsync(int taskId, int dependsOnTaskId, CancellationToken cancellationToken = default) =>
        _context.TaskDependencies
            .FirstOrDefaultAsync(d => d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId, cancellationToken);

    public async Task AddDependencyAsync(TaskDependency dependency, CancellationToken cancellationToken = default) =>
        await _context.TaskDependencies.AddAsync(dependency, cancellationToken);

    public void RemoveDependency(TaskDependency dependency) =>
        _context.TaskDependencies.Remove(dependency);

    public async Task<IReadOnlyList<(int TaskId, int DependsOnTaskId)>> GetDependencyGraphAsync(CancellationToken cancellationToken = default)
    {
        var edges = await _context.TaskDependencies
            .IgnoreQueryFilters()
            .Select(d => new { d.TaskId, d.DependsOnTaskId })
            .ToListAsync(cancellationToken);

        return edges.Select(e => (e.TaskId, e.DependsOnTaskId)).ToList();
    }

    public async Task AddHistoryAsync(IEnumerable<TaskHistoryEntry> entries, CancellationToken cancellationToken = default) =>
        await _context.HistoryEntries.AddRangeAsync(entries, cancellationToken);

    public async Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(int taskId, CancellationToken cancellationToken = default) =>
        await _context.HistoryEntries
            .Where(h => h.TaskId == taskId)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);
}
