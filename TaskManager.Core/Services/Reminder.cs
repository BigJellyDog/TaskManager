using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Одно напоминание для показа пользователю.</summary>
/// <param name="TaskId">Задача.</param>
/// <param name="Title">Название задачи.</param>
/// <param name="Deadline">Её дедлайн.</param>
/// <param name="Kind">Вид напоминания.</param>
public sealed record Reminder(int TaskId, string Title, DateTimeOffset Deadline, ReminderKind Kind);
