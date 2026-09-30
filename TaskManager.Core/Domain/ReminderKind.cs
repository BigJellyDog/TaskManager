namespace TaskManager.Core.Domain;

/// <summary>The kind of deadline reminder. The values go in ascending order of urgency.</summary>
public enum ReminderKind
{
    /// <summary>Less than 24 hours to the deadline.</summary>
    DueIn24Hours = 0,

    /// <summary>Less than an hour to the deadline.</summary>
    DueIn1Hour = 1,

    /// <summary>The deadline has already passed.</summary>
    Overdue = 2,
}
