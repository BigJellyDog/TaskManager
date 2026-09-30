using System.Linq.Expressions;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Specifications;

/// <summary>Open tasks: not done and not cancelled.</summary>
public sealed class OpenTaskSpecification : Specification<TaskItem>
{
    public override Expression<Func<TaskItem, bool>> ToExpression() =>
        t => t.Status != TaskStatus.Done && t.Status != TaskStatus.Cancelled;
}

/// <summary>Overdue: the deadline is earlier than <c>now</c>, the task is still open.</summary>
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
/// Ready-made list views: "Overdue", "For today", "For the week". The time is taken from <see cref="TimeProvider"/>,
/// the day boundaries are counted by its local time zone.
/// </summary>
public static class TaskViews
{
    /// <summary>Open tasks with a deadline in the past.</summary>
    public static Specification<TaskItem> Overdue(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new OverdueSpecification(clock.GetUtcNow());
    }

    /// <summary>Open tasks with a deadline within today's (local) day.</summary>
    public static Specification<TaskItem> Today(TimeProvider clock) => DueWithinDays(clock, 1);

    /// <summary>Open tasks with a deadline in the next 7 calendar days, counting from the start of today.</summary>
    public static Specification<TaskItem> Week(TimeProvider clock) => DueWithinDays(clock, 7);

    private static Specification<TaskItem> DueWithinDays(TimeProvider clock, int days)
    {
        ArgumentNullException.ThrowIfNull(clock);

        // The end of the range in DeadlineRangeSpecification is inclusive, so we subtract one tick
        // from the start of the next day: a task exactly at midnight of the next day is no longer "today's".
        var from = StartOfLocalDay(clock, 0);
        var to = StartOfLocalDay(clock, days).AddTicks(-1);

        return new DeadlineRangeSpecification(from, to).And(new OpenTaskSpecification());
    }

    /// <summary>The start of a local day: today plus <paramref name="addDays"/> calendar days, as a point in time.</summary>
    private static DateTimeOffset StartOfLocalDay(TimeProvider clock, int addDays)
    {
        var zone = clock.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone);

        // We add exactly calendar days to the local date, not 24 hours: at a clock change a day is 23 or 25 hours long.
        var date = localNow.Date.AddDays(addDays);
        return new DateTimeOffset(date, zone.GetUtcOffset(date));
    }
}
