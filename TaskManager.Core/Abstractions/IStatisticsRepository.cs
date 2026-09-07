using TaskManager.Core.Services;

namespace TaskManager.Core.Abstractions;

public interface IStatisticsRepository
{
    /// <summary>
    /// Считает статистику агрегатными запросами к БД (без загрузки задач в память).
    /// Удалённые задачи не учитываются.
    /// </summary>
    /// <param name="now">Текущий момент: от него считается просрочка.</param>
    /// <param name="weekSince">Начало окна "за неделю".</param>
    /// <param name="monthSince">Начало окна "за месяц".</param>
    Task<TaskStatistics> GetAsync(DateTimeOffset now, DateTimeOffset weekSince, DateTimeOffset monthSince, CancellationToken cancellationToken = default);
}
