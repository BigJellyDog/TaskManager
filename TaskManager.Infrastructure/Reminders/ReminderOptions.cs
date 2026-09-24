namespace TaskManager.Infrastructure.Reminders;

/// <summary>Настройки фонового сервиса напоминаний (секция "Reminders" в конфигурации).</summary>
public class ReminderOptions
{
    public const string SectionName = "Reminders";

    /// <summary>Как часто проверять дедлайны, в секундах. По умолчанию раз в минуту.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Показывать ли напоминания о дедлайнах. Если выключено, сервис по-прежнему создаёт копии повторяющихся задач
    /// (в том же цикле), но дедлайны не проверяет и ничего не показывает.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
