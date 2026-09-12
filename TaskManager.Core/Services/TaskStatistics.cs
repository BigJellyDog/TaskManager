using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Период, за который считаются «Вовремя», «Среднее время» и график выполненных по дням.</summary>
public enum StatisticsPeriod
{
    Week,
    Month,
    AllTime,
}

/// <summary>Сколько задач приходится на календарный день (в местном часовом поясе).</summary>
public sealed record DayCount(DateOnly Day, int Count);

/// <summary>Сколько открытых задач помечено тегом.</summary>
public sealed record TagCount(string Name, int Count);

/// <summary>Сводная статистика по задачам (FR-11).</summary>
/// <param name="CompletedLastWeek">Выполнено за последние 7 суток.</param>
/// <param name="CompletedLastMonth">Выполнено за последние 30 суток.</param>
/// <param name="CompletedWithDeadline">Сколько выполнено задач с дедлайном за выбранный период (база для процента).</param>
/// <param name="CompletedOnTime">Из них выполнено не позже дедлайна.</param>
/// <param name="AverageCompletionTime">Среднее время от создания до выполнения за период; null, если выполненных задач нет.</param>
/// <param name="OverdueCount">Открытые задачи с прошедшим дедлайном.</param>
/// <param name="BlockedCount">Задачи в статусе Blocked.</param>
/// <param name="OpenByPriority">Открытые задачи по приоритетам. Всегда содержит все четыре приоритета.</param>
/// <param name="OpenByStatus">Открытые задачи по статусам (К выполнению, В работе, Заблокирована). Содержит все три.</param>
/// <param name="CompletedByDay">Выполнено по дням: последний день — сегодня. Дни без задач присутствуют с нулём.</param>
/// <param name="DeadlinesByDay">Дедлайны открытых задач на ближайшие 7 дней, начиная с сегодняшнего (просроченные не входят).</param>
/// <param name="TopTags">Самые частые теги в открытых задачах, по убыванию.</param>
public sealed record TaskStatistics(
    int CompletedLastWeek,
    int CompletedLastMonth,
    int CompletedWithDeadline,
    int CompletedOnTime,
    TimeSpan? AverageCompletionTime,
    int OverdueCount,
    int BlockedCount,
    IReadOnlyDictionary<Priority, int> OpenByPriority,
    IReadOnlyDictionary<Domain.TaskStatus, int> OpenByStatus,
    IReadOnlyList<DayCount> CompletedByDay,
    IReadOnlyList<DayCount> DeadlinesByDay,
    IReadOnlyList<TagCount> TopTags)
{
    /// <summary>Процент выполненных вовремя среди выполненных с дедлайном (0-100); null, если таких задач ещё нет.</summary>
    public double? OnTimePercent =>
        CompletedWithDeadline == 0 ? null : Math.Round(CompletedOnTime * 100.0 / CompletedWithDeadline, 1);

    /// <summary>Всего открытых задач.</summary>
    public int OpenCount => OpenByPriority.Values.Sum();
}
