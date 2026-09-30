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
    private const int DeadlineDays = 7;
    private const int TopTagCount = 5;

    private readonly TaskManagerDbContext _context;

    public StatisticsRepository(TaskManagerDbContext context)
    {
        _context = context;
    }

    public async Task<TaskStatistics> GetAsync(StatisticsQuery query, CancellationToken cancellationToken = default)
    {
        // The global soft-delete filter applies to all queries below: deleted tasks are not counted.
        // The queries run one after another: one DbContext does not support parallel queries.
        var tasks = _context.Tasks.AsNoTracking();
        var done = tasks.Where(t => t.Status == TaskStatus.Done && t.CompletedAt != null);
        var open = tasks.Where(new OpenTaskSpecification().ToExpression());

        var weekSince = query.WeekSince;
        var monthSince = query.MonthSince;
        var completedWeek = await done.CountAsync(t => t.CompletedAt >= weekSince, cancellationToken);
        var completedMonth = await done.CountAsync(t => t.CompletedAt >= monthSince, cancellationToken);

        // "On time" is counted only over tasks completed in the chosen period.
        var inPeriod = query.PeriodSince is { } since ? done.Where(t => t.CompletedAt >= since) : done;

        // Completed on time: the task had a deadline and was closed not later than it. Without a deadline "on time" is undefined.
        var withDeadline = inPeriod.Where(t => t.Deadline != null);
        var completedWithDeadline = await withDeadline.CountAsync(cancellationToken);
        var completedOnTime = await withDeadline.CountAsync(t => t.CompletedAt <= t.Deadline, cancellationToken);

        var now = query.Now;
        var overdue = await tasks.Where(new OverdueSpecification(now).ToExpression()).CountAsync(cancellationToken);
        var blocked = await tasks.CountAsync(t => t.Status == TaskStatus.Blocked, cancellationToken);

        var byPriority = await open
            .GroupBy(t => t.Priority)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byStatus = await open
            .GroupBy(t => t.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var topTags = await open
            .SelectMany(t => t.Tags)
            .GroupBy(tag => new { tag.Id, tag.Name })
            .Select(g => new { g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(TopTagCount)
            .ToListAsync(cancellationToken);

        return new TaskStatistics(
            completedWeek,
            completedMonth,
            completedWithDeadline,
            completedOnTime,
            await GetAverageCompletionTimeAsync(query.PeriodSince, cancellationToken),
            overdue,
            blocked,
            Enum.GetValues<Priority>().ToDictionary(p => p, p => byPriority.FirstOrDefault(g => g.Key == p)?.Count ?? 0),
            new[] { TaskStatus.Todo, TaskStatus.InProgress, TaskStatus.Blocked }
                .ToDictionary(s => s, s => byStatus.FirstOrDefault(g => g.Key == s)?.Count ?? 0),
            await GetCompletedByDayAsync(done, query, cancellationToken),
            await GetDeadlinesByDayAsync(open, query, cancellationToken),
            topTags.Select(x => new TagCount(x.Name, x.Count)).ToList());
    }

    /// <summary>Tasks completed in the last N days, laid out by calendar days of local time.</summary>
    private static async Task<IReadOnlyList<DayCount>> GetCompletedByDayAsync(
        IQueryable<TaskItem> done, StatisticsQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(query.Now, query.TimeZone).DateTime);
        var first = today.AddDays(1 - query.ChartDays);
        var from = StartOfDay(first, query.TimeZone);

        var completed = await done
            .Where(t => t.CompletedAt >= from)
            .Select(t => t.CompletedAt!.Value)
            .ToListAsync(cancellationToken);

        return CountByDay(completed, first, query.ChartDays, query.TimeZone);
    }

    /// <summary>The deadlines of open tasks for today and the next 6 days. Overdue ones are counted separately.</summary>
    private static async Task<IReadOnlyList<DayCount>> GetDeadlinesByDayAsync(
        IQueryable<TaskItem> open, StatisticsQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(query.Now, query.TimeZone).DateTime);
        var end = StartOfDay(today.AddDays(DeadlineDays), query.TimeZone);
        var now = query.Now;

        var deadlines = await open
            .Where(t => t.Deadline != null && t.Deadline >= now && t.Deadline < end)
            .Select(t => t.Deadline!.Value)
            .ToListAsync(cancellationToken);

        return CountByDay(deadlines, today, DeadlineDays, query.TimeZone);
    }

    private static List<DayCount> CountByDay(IEnumerable<DateTimeOffset> moments, DateOnly first, int days, TimeZoneInfo zone)
    {
        var counts = moments
            .GroupBy(m => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(m, zone).DateTime))
            .ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, days)
            .Select(i => first.AddDays(i))
            .Select(day => new DayCount(day, counts.GetValueOrDefault(day)))
            .ToList();
    }

    private static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <summary>
    /// The average time from creation to completion. EF Core cannot subtract dates that we store as ticks (a number),
    /// so we count it with one SQL AVG query over these columns. The soft-delete filter has to be specified manually here.
    /// </summary>
    private async Task<TimeSpan?> GetAverageCompletionTimeAsync(DateTimeOffset? since, CancellationToken cancellationToken)
    {
        var done = (int)TaskStatus.Done;
        var sinceTicks = since?.UtcTicks ?? long.MinValue;

        var result = await _context.Database
            .SqlQuery<double?>($"""
                SELECT AVG(CompletedAt - CreatedAt) AS Value
                FROM Tasks
                WHERE IsDeleted = 0 AND Status = {done} AND CompletedAt IS NOT NULL AND CompletedAt >= {sinceTicks}
                """)
            .ToListAsync(cancellationToken);

        return result.SingleOrDefault() is { } ticks ? TimeSpan.FromTicks((long)ticks) : null;
    }
}
