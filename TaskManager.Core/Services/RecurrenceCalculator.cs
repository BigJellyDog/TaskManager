using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>
/// Calculates the next date of a recurring task. Pure date arithmetic, without a database.
/// All calculations follow the local calendar of the time zone <c>zone</c>, and the result is returned as a point in time.
/// </summary>
public static class RecurrenceCalculator
{
    /// <summary>
    /// The next occurrence of the rule after the moment <paramref name="current"/>.
    /// In the <see cref="RecurrenceMode.ByCalendar"/> mode <paramref name="current"/> is the previous due date of the task,
    /// in the <see cref="RecurrenceMode.AfterCompletion"/> mode it is the moment of completion.
    /// </summary>
    /// <remarks>
    /// Edge cases:
    /// the 31st: in a short month the last day of the month is taken, and in the next suitable one the 31st again (the day is taken from the anchor);
    /// 29 February: in a non-leap year 28 February, in a leap year the 29th again;
    /// clock change: the time of day is taken from the anchor, so a task at 9:00 stays at 9:00 local time.
    /// If the time falls into the "gap" of the spring clock change, it is shifted an hour forward. If the time repeats
    /// at the autumn change, its first occurrence is taken.
    /// </remarks>
    public static DateTimeOffset GetNext(RecurrenceRule rule, DateTimeOffset current, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(current, zone).DateTime;
        var timeOfDay = rule.Anchor?.TimeOfDay ?? local.TimeOfDay;

        // In the "by calendar" mode we remember the day of month from the anchor. "After completion" counts from the completion date.
        var anchorDay = rule.Mode == RecurrenceMode.ByCalendar && rule.Anchor is { } anchor ? anchor.Day : local.Day;

        var date = rule.Frequency switch
        {
            RecurrenceFrequency.Daily => local.Date.AddDays(rule.Interval),
            RecurrenceFrequency.Weekly => NextWeeklyDate(local.Date, rule.WeekDays, rule.Interval),
            RecurrenceFrequency.Monthly => AddMonths(local.Date, rule.Interval, anchorDay),
            RecurrenceFrequency.Yearly => AddMonths(local.Date, rule.Interval * 12, anchorDay),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Frequency, "Неизвестная частота повторения."),
        };

        return ToDateTimeOffset(date + timeOfDay, zone);
    }

    /// <summary>
    /// The next chosen weekday: first we look in the same week (the week starts on Monday) after the current day,
    /// if there is none, we take the first chosen weekday after <paramref name="interval"/> weeks.
    /// </summary>
    private static DateTime NextWeeklyDate(DateTime date, WeekDays days, int interval)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        var monday = date.AddDays(-daysFromMonday);

        for (var offset = daysFromMonday + 1; offset < 7; offset++)
        {
            if (IsSelected(days, monday.AddDays(offset).DayOfWeek))
            {
                return monday.AddDays(offset);
            }
        }

        var nextWeekMonday = monday.AddDays(7 * interval);
        for (var offset = 0; offset < 7; offset++)
        {
            if (IsSelected(days, nextWeekMonday.AddDays(offset).DayOfWeek))
            {
                return nextWeekMonday.AddDays(offset);
            }
        }

        // Unreachable: a rule without weekdays cannot be created.
        throw new InvalidOperationException("Для еженедельного повторения не выбран ни один день недели.");
    }

    private static bool IsSelected(WeekDays days, DayOfWeek day)
    {
        var flag = day switch
        {
            DayOfWeek.Monday => WeekDays.Monday,
            DayOfWeek.Tuesday => WeekDays.Tuesday,
            DayOfWeek.Wednesday => WeekDays.Wednesday,
            DayOfWeek.Thursday => WeekDays.Thursday,
            DayOfWeek.Friday => WeekDays.Friday,
            DayOfWeek.Saturday => WeekDays.Saturday,
            _ => WeekDays.Sunday,
        };

        return days.HasFlag(flag);
    }

    /// <summary>Shifts the month and takes the day from <paramref name="anchorDay"/>, but not beyond the end of the month.</summary>
    private static DateTime AddMonths(DateTime date, int months, int anchorDay)
    {
        var firstOfTarget = new DateTime(date.Year, date.Month, 1).AddMonths(months);
        var day = Math.Min(anchorDay, DateTime.DaysInMonth(firstOfTarget.Year, firstOfTarget.Month));
        return new DateTime(firstOfTarget.Year, firstOfTarget.Month, day);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime local, TimeZoneInfo zone)
    {
        // The spring clock change: such a time does not exist, we shift it an hour forward.
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        // The autumn clock change: the time happens twice. The larger offset from UTC is the earlier occurrence.
        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset);
    }
}
