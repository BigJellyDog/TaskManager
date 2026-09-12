using TaskManager.Core.Abstractions;

namespace TaskManager.Core.Services;

/// <summary>Статистика по задачам. Задаёт окна «за неделю» и «за месяц» от текущего времени.</summary>
public class StatisticsService
{
    /// <summary>«За неделю»: последние 7 суток, а не календарная неделя (иначе в понедельник утром было бы 0).</summary>
    public static TimeSpan WeekWindow { get; } = TimeSpan.FromDays(7);

    /// <summary>«За месяц»: последние 30 суток.</summary>
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

        // График за всё время ограничен 30 днями: больше столбцов в панель не поместится.
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
