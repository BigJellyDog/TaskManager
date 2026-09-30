namespace TaskManager.Core.Domain;

/// <summary>
/// A record that a reminder has already been shown. The key: the task, the kind and the deadline for which it was reminded.
/// If the deadline was moved, it is a new reminder, and it will fire again.
/// </summary>
public class SentReminder
{
    public SentReminder(int taskId, ReminderKind kind, DateTimeOffset deadline, DateTimeOffset sentAt)
    {
        TaskId = taskId;
        Kind = kind;
        Deadline = deadline;
        SentAt = sentAt;
    }

    // Needed by EF Core.
    private SentReminder()
    {
    }

    public int TaskId { get; private set; }

    public ReminderKind Kind { get; private set; }

    /// <summary>The deadline of the task at the moment of the reminder.</summary>
    public DateTimeOffset Deadline { get; private set; }

    public DateTimeOffset SentAt { get; private set; }
}
