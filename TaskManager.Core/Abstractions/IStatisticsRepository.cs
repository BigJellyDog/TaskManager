using TaskManager.Core.Services;

namespace TaskManager.Core.Abstractions;

/// <summary>Параметры подсчёта статистики.</summary>
/// <param name="Now">Текущий момент: от него считается просрочка.</param>
/// <param name="WeekSince">Начало окна «за неделю».</param>
/// <param name="MonthSince">Начало окна «за месяц».</param>
/// <param name="PeriodSince">Начало выбранного периода для «Вовремя», «Среднее время» и графика; null — всё время.</param>
/// <param name="ChartDays">Сколько последних дней показывает график выполненных.</param>
/// <param name="TimeZone">Часовой пояс, по которому задачи раскладываются по календарным дням.</param>
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
    /// Считает статистику запросами к БД. Удалённые задачи не учитываются.
    /// </summary>
    Task<TaskStatistics> GetAsync(StatisticsQuery query, CancellationToken cancellationToken = default);
}
