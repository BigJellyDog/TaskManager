namespace TaskManager.Core.Domain;

/// <summary>The recurrence rule. Stored inside the task as part of its data (an owned type in EF Core).</summary>
public class RecurrenceRule
{
    public RecurrenceRule(
        RecurrenceFrequency frequency,
        int interval,
        RecurrenceMode mode,
        WeekDays weekDays = WeekDays.None,
        DateTime? anchor = null)
    {
        if (interval < 1)
        {
            throw new DomainValidationException("Шаг повторения должен быть не меньше 1.");
        }

        if (frequency == RecurrenceFrequency.Weekly && weekDays == WeekDays.None)
        {
            throw new DomainValidationException("Для еженедельного повторения выберите хотя бы один день недели.");
        }

        if (frequency != RecurrenceFrequency.Weekly && weekDays != WeekDays.None)
        {
            throw new DomainValidationException("Дни недели можно выбирать только для еженедельного повторения.");
        }

        Frequency = frequency;
        Interval = interval;
        Mode = mode;
        WeekDays = weekDays;

        // The anchor is stored as a local date and time without a time zone.
        Anchor = anchor is { } a ? DateTime.SpecifyKind(a, DateTimeKind.Unspecified) : null;
    }

    // Needed by EF Core.
    private RecurrenceRule()
    {
    }

    public RecurrenceFrequency Frequency { get; private set; }

    /// <summary>The step: 1 = every day/week/month/year, 2 = every other, etc.</summary>
    public int Interval { get; private set; } = 1;

    public RecurrenceMode Mode { get; private set; }

    /// <summary>The weekdays, only for <see cref="RecurrenceFrequency.Weekly"/>.</summary>
    public WeekDays WeekDays { get; private set; }

    /// <summary>
    /// The local date and time of the first occurrence. From them the day of month is taken (to come back to the 31st
    /// after 28 February) and the time of day (so that the task does not drift by an hour after a clock change). If not set,
    /// the calculator takes them from the current date.
    /// </summary>
    public DateTime? Anchor { get; private set; }

    /// <summary>A copy of the rule with the given anchor (the rule is immutable).</summary>
    public RecurrenceRule WithAnchor(DateTime anchor) => new(Frequency, Interval, Mode, WeekDays, anchor);
}
