using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Данные для создания задачи.</summary>
public sealed record CreateTaskRequest(
    string Title,
    Priority Priority = Priority.Medium,
    string? Description = null,
    DateTimeOffset? Deadline = null,
    IReadOnlyCollection<string>? Tags = null,
    RecurrenceRule? Recurrence = null);

/// <summary>
/// Данные для редактирования: форма целиком, как её отправляет интерфейс.
/// Сервис сам сравнит с текущей задачей и запишет в историю только то, что реально изменилось.
/// </summary>
public sealed record EditTaskRequest(
    int Id,
    string Title,
    Priority Priority,
    string? Description,
    DateTimeOffset? Deadline,
    IReadOnlyCollection<string> Tags,
    RecurrenceRule? Recurrence);
