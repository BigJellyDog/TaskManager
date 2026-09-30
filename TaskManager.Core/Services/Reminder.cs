using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>One reminder to show to the user.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Title">The task title.</param>
/// <param name="Deadline">Its deadline.</param>
/// <param name="Kind">The kind of the reminder.</param>
public sealed record Reminder(int TaskId, string Title, DateTimeOffset Deadline, ReminderKind Kind);
