using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>
/// Вычисляет следующую дату повторяющейся задачи. Чистая математика дат, без базы данных.
/// Все вычисления идут по локальному календарю часового пояса <c>zone</c>, а результат возвращается моментом времени.
/// </summary>
public static class RecurrenceCalculator
{
    /// <summary>
    /// Следующее срабатывание правила после момента <paramref name="current"/>.
    /// В режиме <see cref="RecurrenceMode.ByCalendar"/> <paramref name="current"/> это предыдущий срок задачи,
    /// в режиме <see cref="RecurrenceMode.AfterCompletion"/> это момент выполнения.
    /// </summary>
    /// <remarks>
    /// Крайние случаи:
    /// 31-е число: в коротком месяце берётся последний день месяца, а в следующем подходящем снова 31-е (день берётся из якоря);
    /// 29 февраля: в невисокосный год 28 февраля, в високосный снова 29-е;
    /// смена времени: время суток берётся из якоря, поэтому задача на 9:00 остаётся на 9:00 по местным часам.
    /// Если время попало в "дыру" весеннего перевода, оно сдвигается на час вперёд. Если время повторяется
    /// при осеннем переводе, берётся его первое наступление.
    /// </remarks>
    public static DateTimeOffset GetNext(RecurrenceRule rule, DateTimeOffset current, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(current, zone).DateTime;
        var timeOfDay = rule.Anchor?.TimeOfDay ?? local.TimeOfDay;

        // В режиме "по календарю" день месяца помним из якоря. "После выполнения" отсчитывает от даты выполнения.
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
    /// Следующий выбранный день недели: сначала ищем в той же неделе (неделя с понедельника) после текущего дня,
    /// если нет, то берём первый выбранный день недели через <paramref name="interval"/> недель.
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

        // Недостижимо: правило без дней недели создать нельзя.
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

    /// <summary>Сдвигает месяц, а день берёт из <paramref name="anchorDay"/>, но не дальше конца месяца.</summary>
    private static DateTime AddMonths(DateTime date, int months, int anchorDay)
    {
        var firstOfTarget = new DateTime(date.Year, date.Month, 1).AddMonths(months);
        var day = Math.Min(anchorDay, DateTime.DaysInMonth(firstOfTarget.Year, firstOfTarget.Month));
        return new DateTime(firstOfTarget.Year, firstOfTarget.Month, day);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime local, TimeZoneInfo zone)
    {
        // Весенний перевод: такого времени не существует, сдвигаем на час вперёд.
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        // Осенний перевод: время бывает дважды. Больший сдвиг от UTC это более раннее наступление.
        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset);
    }
}
