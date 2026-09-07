using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskManager.Core.Specifications;
using TaskManager.Infrastructure.Persistence;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Infrastructure.Repositories;

public class StatisticsRepository : IStatisticsRepository
{
    private readonly TaskManagerDbContext _context;

    public StatisticsRepository(TaskManagerDbContext context)
    {
        _context = context;
    }

    public async Task<TaskStatistics> GetAsync(
        DateTimeOffset now,
        DateTimeOffset weekSince,
        DateTimeOffset monthSince,
        CancellationToken cancellationToken = default)
    {
        // Глобальный фильтр мягкого удаления действует на все запросы ниже: удалённые задачи не считаются.
        // Запросы идут по очереди: один DbContext не поддерживает параллельные запросы.
        var tasks = _context.Tasks.AsNoTracking();
        var done = tasks.Where(t => t.Status == TaskStatus.Done && t.CompletedAt != null);

        var completedWeek = await done.CountAsync(t => t.CompletedAt >= weekSince, cancellationToken);
        var completedMonth = await done.CountAsync(t => t.CompletedAt >= monthSince, cancellationToken);

        // Выполнено вовремя: у задачи был дедлайн и её закрыли не позже него. Без дедлайна "вовремя" не определено.
        var withDeadline = done.Where(t => t.Deadline != null);
        var completedWithDeadline = await withDeadline.CountAsync(cancellationToken);
        var completedOnTime = await withDeadline.CountAsync(t => t.CompletedAt <= t.Deadline, cancellationToken);

        var overdue = await tasks.Where(new OverdueSpecification(now).ToExpression()).CountAsync(cancellationToken);
        var blocked = await tasks.CountAsync(t => t.Status == TaskStatus.Blocked, cancellationToken);

        var grouped = await tasks
            .Where(new OpenTaskSpecification().ToExpression())
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byPriority = Enum.GetValues<Priority>()
            .ToDictionary(p => p, p => grouped.FirstOrDefault(g => g.Priority == p)?.Count ?? 0);

        return new TaskStatistics(
            completedWeek,
            completedMonth,
            completedWithDeadline,
            completedOnTime,
            await GetAverageCompletionTimeAsync(cancellationToken),
            overdue,
            blocked,
            byPriority);
    }

    /// <summary>
    /// Среднее время от создания до выполнения. EF Core не умеет вычитать даты, которые мы храним как тики (число),
    /// поэтому считаем одним SQL-запросом AVG по этим колонкам. Фильтр мягкого удаления здесь надо указать вручную.
    /// </summary>
    private async Task<TimeSpan?> GetAverageCompletionTimeAsync(CancellationToken cancellationToken)
    {
        var done = (int)TaskStatus.Done;

        var result = await _context.Database
            .SqlQuery<double?>($"""
                SELECT AVG(CompletedAt - CreatedAt) AS Value
                FROM Tasks
                WHERE IsDeleted = 0 AND Status = {done} AND CompletedAt IS NOT NULL
                """)
            .ToListAsync(cancellationToken);

        return result.SingleOrDefault() is { } ticks ? TimeSpan.FromTicks((long)ticks) : null;
    }
}
