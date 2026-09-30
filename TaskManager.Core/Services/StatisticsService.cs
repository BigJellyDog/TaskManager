using TaskManager.Core.Abstractions;

namespace TaskManager.Core.Services;

/// <summary>Task statistics. Sets the "last week" and "last month" windows from the current time.</summary>
public class StatisticsService
{
    /// <summary>"Last week": the last 7 days, not a calendar week (otherwise on Monday morning it would be 0).</summary>
    public static TimeSpan WeekWindow { get; } = TimeSpan.FromDays(7);

    /// <summary>"Last month": the last 30 days.</summary>
    public static TimeSpan MonthWindow { get; } = TimeSpan.FromDays(30);

    private readonly IStatisticsRepository _statistics;
    private readonly TimeProvider _clock;

    public StatisticsService(IStatisticsRepository statistics, TimeProvider clock)
    {
        _statistics = statistics;
        _clock = clock;
    }

    public Task<TaskStatistics> GetAsync(StatisticsPeriod period = StatisticsPeriod.Week, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();

        // The all-time chart is limited to 30 days: more bars will not fit into the panel.
        var (since, chartDays) = period switch
        {
            StatisticsPeriod.Week => ((DateTimeOffset?)(now - WeekWindow), 7),
            StatisticsPeriod.Month => (now - MonthWindow, 30),
            _ => (null, 30),
        };

        var query = new StatisticsQuery(now, now - WeekWindow, now - MonthWindow, since, chartDays, _clock.LocalTimeZone);
        return _statistics.GetAsync(query, cancellationToken);
    }
}
