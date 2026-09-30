using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>The period for which "On time", "Average time" and the done-by-day chart are counted.</summary>
public enum StatisticsPeriod
{
    Week,
    Month,
    AllTime,
}

/// <summary>How many tasks fall on a calendar day (in the local time zone).</summary>
public sealed record DayCount(DateOnly Day, int Count);

/// <summary>How many open tasks are marked with the tag.</summary>
public sealed record TagCount(string Name, int Count);

/// <summary>The summary statistics of tasks (FR-11).</summary>
/// <param name="CompletedLastWeek">Completed in the last 7 days.</param>
/// <param name="CompletedLastMonth">Completed in the last 30 days.</param>
/// <param name="CompletedWithDeadline">How many tasks with a deadline were completed in the chosen period (the base for the percentage).</param>
/// <param name="CompletedOnTime">Of them completed not later than the deadline.</param>
/// <param name="AverageCompletionTime">The average time from creation to completion over the period; null if there are no completed tasks.</param>
/// <param name="OverdueCount">Open tasks with a deadline that has passed.</param>
/// <param name="BlockedCount">Tasks with the Blocked status.</param>
/// <param name="OpenByPriority">Open tasks by priority. Always contains all four priorities.</param>
/// <param name="OpenByStatus">Open tasks by status (To do, In progress, Blocked). Contains all three.</param>
/// <param name="CompletedByDay">Completed by day: the last day is today. Days without tasks are present with zero.</param>
/// <param name="DeadlinesByDay">The deadlines of open tasks for the next 7 days, starting from today (overdue ones are not included).</param>
/// <param name="TopTags">The most frequent tags in open tasks, in descending order.</param>
public sealed record TaskStatistics(
    int CompletedLastWeek,
    int CompletedLastMonth,
    int CompletedWithDeadline,
    int CompletedOnTime,
    TimeSpan? AverageCompletionTime,
    int OverdueCount,
    int BlockedCount,
    IReadOnlyDictionary<Priority, int> OpenByPriority,
    IReadOnlyDictionary<Domain.TaskStatus, int> OpenByStatus,
    IReadOnlyList<DayCount> CompletedByDay,
    IReadOnlyList<DayCount> DeadlinesByDay,
    IReadOnlyList<TagCount> TopTags)
{
    /// <summary>The percentage completed on time among those completed with a deadline (0-100); null if there are no such tasks yet.</summary>
    public double? OnTimePercent =>
        CompletedWithDeadline == 0 ? null : Math.Round(CompletedOnTime * 100.0 / CompletedWithDeadline, 1);

    /// <summary>The total of open tasks.</summary>
    public int OpenCount => OpenByPriority.Values.Sum();
}
