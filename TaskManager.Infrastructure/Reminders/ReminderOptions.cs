namespace TaskManager.Infrastructure.Reminders;

/// <summary>The settings of the background reminder service ("Reminders" section in the configuration).</summary>
public class ReminderOptions
{
    public const string SectionName = "Reminders";

    /// <summary>How often to check deadlines, in seconds. Once a minute by default.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to show deadline reminders. If switched off, the service still creates copies of recurring tasks
    /// (in the same loop), but does not check deadlines and shows nothing.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
