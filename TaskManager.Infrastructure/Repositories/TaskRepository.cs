using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Querying;
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

    public async Task<PagedResult<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = _context.Tasks.Where(query.Filter.ToExpression());
        var total = await filtered.CountAsync(cancellationToken);

        var items = await ApplySort(filtered, query.Sort)
            .Include(t => t.Tags)
            .Include(t => t.Children)
            .AsSplitQuery()
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskItem>(items, total, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByIdsAsync(IReadOnlyCollection<int> ids, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var query = includeDeleted ? _context.Tasks.IgnoreQueryFilters() : _context.Tasks;
        return await query.Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetChildrenAsync(int parentId, CancellationToken cancellationToken = default) =>
        await _context.Tasks
            .Where(t => t.ParentId == parentId)
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> GetDeletedAsync(CancellationToken cancellationToken = default) =>
        await _context.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Строит ORDER BY из списка правил: первое правило через OrderBy, остальные через ThenBy.
    /// В конце всегда добавляется Id: без однозначного порядка страницы могли бы пересекаться или терять записи.
    /// </summary>
    private static IOrderedQueryable<TaskItem> ApplySort(IQueryable<TaskItem> source, IReadOnlyList<TaskSort> sorts)
    {
        IOrderedQueryable<TaskItem>? ordered = null;

        foreach (var sort in sorts.DistinctBy(s => s.Field))
        {
            ordered = sort.Field switch
            {
                TaskSortField.Priority => Order(source, ordered, t => t.Priority, sort.Descending),
                TaskSortField.CreatedAt => Order(source, ordered, t => t.CreatedAt, sort.Descending),
                TaskSortField.Status => Order(source, ordered, t => t.Status, sort.Descending),

                // SearchText начинается с названия в верхнем регистре, а SQLite не умеет сортировать кириллицу
                // без учёта регистра, поэтому сортируем по нему. Перевод строки в тексте идёт раньше любой буквы,
                // так что "АБ" по-прежнему раньше "АБВ".
                TaskSortField.Title => Order(source, ordered, t => t.SearchText, sort.Descending),

                // Задачи без дедлайна в конце при любом направлении: сначала ключ "дедлайна нет" (false раньше true).
                TaskSortField.Deadline => Order(source, Order(source, ordered, t => t.Deadline == null, false), t => t.Deadline, sort.Descending),
                _ => throw new ArgumentOutOfRangeException(nameof(sorts), sort.Field, "Неизвестное поле сортировки."),
            };
        }

        return Order(source, ordered, t => t.Id, false);
    }

    private static IOrderedQueryable<TaskItem> Order<TKey>(
        IQueryable<TaskItem> source,
        IOrderedQueryable<TaskItem>? ordered,
        Expression<Func<TaskItem, TKey>> key,
        bool descending)
    {
        if (ordered is null)
        {
            return descending ? source.OrderByDescending(key) : source.OrderBy(key);
        }

        return descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
    }

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
