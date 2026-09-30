using TaskManager.Core.Domain;
using TaskManager.Core.Specifications;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Querying;

/// <summary>
/// Task list filters in the form in which the user sets them. Everything that is specified is combined with AND;
/// what is not set by the filter is not counted. One specification is built from them.
/// </summary>
public sealed record TaskFilter(
    IReadOnlyCollection<TaskStatus>? Statuses = null,
    Priority? Priority = null,
    string? Tag = null,
    DateTimeOffset? DeadlineFrom = null,
    DateTimeOffset? DeadlineTo = null,
    string? Text = null,
    bool OnlyOverdue = false,
    bool DueToday = false,
    bool DueThisWeek = false)
{
    public Specification<TaskItem> ToSpecification(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var result = Specification.All<TaskItem>();

        if (Statuses is { Count: > 0 })
        {
            result = result.And(new StatusSpecification([.. Statuses]));
        }

        if (Priority is { } priority)
        {
            result = result.And(new PrioritySpecification(priority));
        }

        if (!string.IsNullOrWhiteSpace(Tag))
        {
            result = result.And(new TagSpecification(Tag));
        }

        if (DeadlineFrom is not null || DeadlineTo is not null)
        {
            result = result.And(new DeadlineRangeSpecification(DeadlineFrom, DeadlineTo));
        }

        if (!string.IsNullOrWhiteSpace(Text))
        {
            result = result.And(new TextSpecification(Text));
        }

        if (OnlyOverdue)
        {
            result = result.And(TaskViews.Overdue(clock));
        }

        if (DueToday)
        {
            result = result.And(TaskViews.Today(clock));
        }

        if (DueThisWeek)
        {
            result = result.And(TaskViews.Week(clock));
        }

        return result;
    }
}
