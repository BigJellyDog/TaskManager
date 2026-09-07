using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Сводная статистика по задачам (FR-11).</summary>
/// <param name="CompletedLastWeek">Выполнено за последние 7 суток.</param>
/// <param name="CompletedLastMonth">Выполнено за последние 30 суток.</param>
/// <param name="CompletedWithDeadline">Сколько всего выполнено задач, у которых был дедлайн (база для процента).</param>
/// <param name="CompletedOnTime">Из них выполнено не позже дедлайна.</param>
/// <param name="AverageCompletionTime">Среднее время от создания до выполнения; null, если выполненных задач нет.</param>
/// <param name="OverdueCount">Открытые задачи с прошедшим дедлайном.</param>
/// <param name="BlockedCount">Задачи в статусе Blocked.</param>
/// <param name="OpenByPriority">Открытые задачи по приоритетам. Всегда содержит все четыре приоритета.</param>
public sealed record TaskStatistics(
    int CompletedLastWeek,
    int CompletedLastMonth,
    int CompletedWithDeadline,
    int CompletedOnTime,
    TimeSpan? AverageCompletionTime,
    int OverdueCount,
    int BlockedCount,
    IReadOnlyDictionary<Priority, int> OpenByPriority)
{
    /// <summary>Процент выполненных вовремя среди выполненных с дедлайном (0-100); null, если таких задач ещё нет.</summary>
    public double? OnTimePercent =>
        CompletedWithDeadline == 0 ? null : Math.Round(CompletedOnTime * 100.0 / CompletedWithDeadline, 1);

    /// <summary>Всего открытых задач.</summary>
    public int OpenCount => OpenByPriority.Values.Sum();
}
