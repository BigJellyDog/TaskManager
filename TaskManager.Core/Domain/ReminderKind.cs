namespace TaskManager.Core.Domain;

/// <summary>Вид напоминания о дедлайне. Значения идут по возрастанию срочности.</summary>
public enum ReminderKind
{
    /// <summary>До дедлайна меньше 24 часов.</summary>
    DueIn24Hours = 0,

    /// <summary>До дедлайна меньше часа.</summary>
    DueIn1Hour = 1,

    /// <summary>Дедлайн уже прошёл.</summary>
    Overdue = 2,
}
