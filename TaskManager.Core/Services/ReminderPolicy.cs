using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>The rule: which reminder is appropriate now for a task with such a deadline.</summary>
public static class ReminderPolicy
{
    /// <summary>How long before the deadline we remind for the first time.</summary>
    public static TimeSpan DayWindow { get; } = TimeSpan.FromHours(24);

    /// <summary>How long before the deadline we remind for the second time.</summary>
    public static TimeSpan HourWindow { get; } = TimeSpan.FromHours(1);

    /// <summary>
    /// The most urgent of the suitable reminders, or null if it is too early to remind. A task with a deadline in 30 minutes
    /// gets "less than an hour", not "less than a day": an outdated reminder makes no sense.
    /// </summary>
    public static ReminderKind? GetKind(DateTimeOffset deadline, DateTimeOffset now)
    {
        var left = deadline - now;

        if (left < TimeSpan.Zero)
        {
            return ReminderKind.Overdue;
        }

        if (left <= HourWindow)
        {
            return ReminderKind.DueIn1Hour;
        }

        return left <= DayWindow ? ReminderKind.DueIn24Hours : null;
    }
}
