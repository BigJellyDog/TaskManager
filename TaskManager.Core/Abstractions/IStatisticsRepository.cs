using TaskManager.Core.Services;

namespace TaskManager.Core.Abstractions;

/// <summary>The parameters of the statistics calculation.</summary>
/// <param name="Now">The current moment: overdue is counted from it.</param>
/// <param name="WeekSince">The start of the "last week" window.</param>
/// <param name="MonthSince">The start of the "last month" window.</param>
/// <param name="PeriodSince">The start of the chosen period for "On time", "Average time" and the chart; null means all time.</param>
/// <param name="ChartDays">How many of the last days the completed-tasks chart shows.</param>
/// <param name="TimeZone">The time zone by which tasks are put on calendar days.</param>
public sealed record StatisticsQuery(
    DateTimeOffset Now,
    DateTimeOffset WeekSince,
    DateTimeOffset MonthSince,
    DateTimeOffset? PeriodSince,
    int ChartDays,
    TimeZoneInfo TimeZone);

public interface IStatisticsRepository
{
    /// <summary>
    /// Calculates statistics with database queries. Deleted tasks are not counted.
    /// </summary>
    Task<TaskStatistics> GetAsync(StatisticsQuery query, CancellationToken cancellationToken = default);
}
