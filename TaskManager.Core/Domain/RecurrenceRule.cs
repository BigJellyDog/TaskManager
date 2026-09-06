namespace TaskManager.Core.Domain;

/// <summary>Правило повторения. Хранится внутри задачи как часть её данных (owned-тип в EF Core).</summary>
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

        // Якорь хранится как локальная дата и время без часового пояса.
        Anchor = anchor is { } a ? DateTime.SpecifyKind(a, DateTimeKind.Unspecified) : null;
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

    /// <summary>
    /// Локальные дата и время первого срабатывания. Из них берутся день месяца (чтобы после 28 февраля вернуться
    /// к 31-му) и время суток (чтобы после перевода часов задача не уезжала на час). Если не задан,
    /// калькулятор берёт их из текущей даты.
    /// </summary>
    public DateTime? Anchor { get; private set; }

    /// <summary>Копия правила с заданным якорем (правило неизменяемо).</summary>
    public RecurrenceRule WithAnchor(DateTime anchor) => new(Frequency, Interval, Mode, WeekDays, anchor);
}
