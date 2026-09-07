namespace TaskManager.Core.Domain;

/// <summary>
/// Запись о том, что напоминание уже показано. Ключ: задача, вид и дедлайн, на который напоминали.
/// Если дедлайн перенесли, это новое напоминание, и оно сработает заново.
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

    // Нужен EF Core.
    private SentReminder()
    {
    }

    public int TaskId { get; private set; }

    public ReminderKind Kind { get; private set; }

    /// <summary>Дедлайн задачи на момент напоминания.</summary>
    public DateTimeOffset Deadline { get; private set; }

    public DateTimeOffset SentAt { get; private set; }
}
