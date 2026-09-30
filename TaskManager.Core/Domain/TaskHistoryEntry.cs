namespace TaskManager.Core.Domain;

/// <summary>One audit record: which field of the task changed, from which value to which, when and by which command.</summary>
public class TaskHistoryEntry
{
    public TaskHistoryEntry(int taskId, DateTimeOffset changedAt, string command, string fieldName, string? oldValue, string? newValue)
    {
        TaskId = taskId;
        ChangedAt = changedAt;
        Command = command;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    // Needed by EF Core.
    private TaskHistoryEntry()
    {
        Command = string.Empty;
        FieldName = string.Empty;
    }

    public int Id { get; private set; }

    public int TaskId { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>The command that caused the change (add, edit, done...).</summary>
    public string Command { get; private set; }

    public string FieldName { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }
}
