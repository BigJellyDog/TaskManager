using System.Linq.Expressions;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Specifications;

/// <summary>Открытые задачи: не выполнены и не отменены.</summary>
public sealed class OpenTaskSpecification : Specification<TaskItem>
{
    public override Expression<Func<TaskItem, bool>> ToExpression() =>
        t => t.Status != TaskStatus.Done && t.Status != TaskStatus.Cancelled;
}

/// <summary>Просроченные: дедлайн раньше <c>now</c>, задача ещё открыта.</summary>
public sealed class OverdueSpecification : Specification<TaskItem>
{
    private readonly DateTimeOffset _now;

    public OverdueSpecification(DateTimeOffset now)
    {
        _now = now;
    }

    public override Expression<Func<TaskItem, bool>> ToExpression()
    {
        var now = _now;
        return Specification.From<TaskItem>(t => t.Deadline != null && t.Deadline < now)
            .And(new OpenTaskSpecification())
            .ToExpression();
    }
}

/// <summary>
/// Готовые виды списка: "Просроченные", "На сегодня", "На неделю". Время берётся из <see cref="TimeProvider"/>,
/// границы дней считаются по его локальному часовому поясу.
/// </summary>
public static class TaskViews
{
    /// <summary>Открытые задачи с дедлайном в прошлом.</summary>
    public static Specification<TaskItem> Overdue(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new OverdueSpecification(clock.GetUtcNow());
    }

    /// <summary>Открытые задачи с дедлайном в течение сегодняшнего (локального) дня.</summary>
    public static Specification<TaskItem> Today(TimeProvider clock) => DueWithinDays(clock, 1);

    /// <summary>Открытые задачи с дедлайном в ближайшие 7 календарных дней, считая с начала сегодняшнего дня.</summary>
    public static Specification<TaskItem> Week(TimeProvider clock) => DueWithinDays(clock, 7);

    private static Specification<TaskItem> DueWithinDays(TimeProvider clock, int days)
    {
        ArgumentNullException.ThrowIfNull(clock);

        // Конец диапазона у DeadlineRangeSpecification включительный, поэтому отнимаем один тик
        // от начала следующего дня: задача ровно в полночь следующего дня уже не "сегодняшняя".
        var from = StartOfLocalDay(clock, 0);
        var to = StartOfLocalDay(clock, days).AddTicks(-1);

        return new DeadlineRangeSpecification(from, to).And(new OpenTaskSpecification());
    }

    /// <summary>Начало локального дня: сегодня плюс <paramref name="addDays"/> календарных дней, в виде момента времени.</summary>
    private static DateTimeOffset StartOfLocalDay(TimeProvider clock, int addDays)
    {
        var zone = clock.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone);

        // Прибавляем именно календарные дни к локальной дате, а не 24 часа: при смене времени сутки бывают 23 или 25 часов.
        var date = localNow.Date.AddDays(addDays);
        return new DateTimeOffset(date, zone.GetUtcOffset(date));
    }
}
