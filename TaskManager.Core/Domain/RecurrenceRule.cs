namespace TaskManager.Core.Domain;

/// <summary>Правило повторения. Хранится внутри задачи как часть её данных (owned-тип в EF Core).</summary>
public class RecurrenceRule
{
    public RecurrenceRule(RecurrenceFrequency frequency, int interval, RecurrenceMode mode, WeekDays weekDays = WeekDays.None)
    {
        Frequency = frequency;
        Interval = interval;
        Mode = mode;
        WeekDays = weekDays;
    }

    // Нужен EF Core.
    private RecurrenceRule()
    {
    }

    public RecurrenceFrequency Frequency { get; private set; }

    /// <summary>Шаг: 1 = каждый день/неделю/месяц/год, 2 = через один и т.д.</summary>
    public int Interval { get; private set; } = 1;

    public RecurrenceMode Mode { get; private set; }

    /// <summary>Дни недели, только для <see cref="RecurrenceFrequency.Weekly"/>.</summary>
    public WeekDays WeekDays { get; private set; }
}
