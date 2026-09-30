namespace TaskManager.Core.Querying;

/// <summary>A field by which the task list can be sorted.</summary>
public enum TaskSortField
{
    Priority = 0,

    /// <summary>Tasks without a deadline are always at the end of the list, in any sort direction.</summary>
    Deadline = 1,

    CreatedAt = 2,

    /// <summary>Alphabetical order ignoring case (Cyrillic included).</summary>
    Title = 3,

    Status = 4,
}

/// <summary>One sorting rule: a field and a direction.</summary>
public sealed record TaskSort(TaskSortField Field, bool Descending = false);
