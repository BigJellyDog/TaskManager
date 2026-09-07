using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Правило: какое напоминание сейчас уместно для задачи с таким дедлайном.</summary>
public static class ReminderPolicy
{
    /// <summary>За сколько до дедлайна напоминаем в первый раз.</summary>
    public static TimeSpan DayWindow { get; } = TimeSpan.FromHours(24);

    /// <summary>За сколько до дедлайна напоминаем во второй раз.</summary>
    public static TimeSpan HourWindow { get; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Самое срочное из подходящих напоминаний или null, если напоминать рано. Задача с дедлайном через 30 минут
    /// получит "меньше часа", а не "меньше суток": устаревшее напоминание не имеет смысла.
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
