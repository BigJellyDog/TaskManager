using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>The data for creating a task.</summary>
public sealed record CreateTaskRequest(
    string Title,
    Priority Priority = Priority.Medium,
    string? Description = null,
    DateTimeOffset? Deadline = null,
    IReadOnlyCollection<string>? Tags = null,
    RecurrenceRule? Recurrence = null,
    int? ParentId = null);

/// <summary>
/// The data for editing: the whole form, as the interface sends it.
/// The service compares it with the current task itself and writes to the history only what really changed.
/// <paramref name="ExpectedRowVersion"/>: the task version (<see cref="TaskItem.RowVersion"/>) that the form saw.
/// If the task was changed since then, the save is rejected (optimistic locking, FR-10).
/// </summary>
public sealed record EditTaskRequest(
    int Id,
    string Title,
    Priority Priority,
    string? Description,
    DateTimeOffset? Deadline,
    IReadOnlyCollection<string> Tags,
    RecurrenceRule? Recurrence,
    long? ExpectedRowVersion = null);
