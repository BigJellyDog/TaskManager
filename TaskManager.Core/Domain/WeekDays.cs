namespace TaskManager.Core.Domain;

/// <summary>Weekdays as bit flags: convenient to store a set of days as one number in the database.</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
}
