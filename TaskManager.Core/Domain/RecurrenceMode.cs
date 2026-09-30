namespace TaskManager.Core.Domain;

/// <summary>When the next copy of a recurring task is created.</summary>
public enum RecurrenceMode
{
    /// <summary>After completion of the current one: the next one is created at the moment of closing.</summary>
    AfterCompletion = 0,

    /// <summary>By calendar: the next one is created on schedule, even if the previous one is not closed.</summary>
    ByCalendar = 1,
}
