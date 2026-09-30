namespace TaskManager.Core.Domain;

/// <summary>
/// The status state machine. The only place that describes which transitions are allowed.
/// Ordinary transitions are in <see cref="Transitions"/>, reopening (Done and Cancelled) is separate
/// and available only through <see cref="EnsureReopen"/>.
/// </summary>
public static class TaskStateMachine
{
    private static readonly Dictionary<TaskStatus, TaskStatus[]> Transitions =
        new Dictionary<TaskStatus, TaskStatus[]>
        {
            [TaskStatus.Todo] = [TaskStatus.InProgress, TaskStatus.Blocked, TaskStatus.Cancelled],
            [TaskStatus.InProgress] = [TaskStatus.Todo, TaskStatus.Blocked, TaskStatus.Done, TaskStatus.Cancelled],
            [TaskStatus.Blocked] = [TaskStatus.Todo, TaskStatus.Cancelled],
            [TaskStatus.Done] = [],
            [TaskStatus.Cancelled] = [],
        };

    // The status to which a task returns when reopened.
    private static readonly Dictionary<TaskStatus, TaskStatus> ReopenTargets =
        new Dictionary<TaskStatus, TaskStatus>
        {
            [TaskStatus.Done] = TaskStatus.InProgress,
            [TaskStatus.Cancelled] = TaskStatus.Todo,
        };

    /// <summary>Whether an ordinary transition (without reopening) is allowed.</summary>
    public static bool CanTransition(TaskStatus from, TaskStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>The statuses reachable from <paramref name="from"/> in the ordinary way.</summary>
    public static IReadOnlyCollection<TaskStatus> GetAllowedTransitions(TaskStatus from) =>
        Transitions.TryGetValue(from, out var allowed) ? allowed : [];

    /// <summary>Whether a task in this status can be reopened and, if so, to which status it returns.</summary>
    public static bool TryGetReopenTarget(TaskStatus from, out TaskStatus target) =>
        ReopenTargets.TryGetValue(from, out target);

    /// <summary>Throws <see cref="InvalidStatusTransitionException"/> if an ordinary transition is forbidden.</summary>
    public static void EnsureTransition(TaskStatus from, TaskStatus to)
    {
        if (CanTransition(from, to))
        {
            return;
        }

        var reason = from == to
            ? "задача уже в этом статусе."
            : TryGetReopenTarget(from, out var target) && target == to
                ? "для этого используйте «переоткрыть»."
                : from is TaskStatus.Done or TaskStatus.Cancelled
                    ? "завершённую или отменённую задачу можно только переоткрыть."
                    : "такой переход не разрешён.";

        throw new InvalidStatusTransitionException(from, to, reason);
    }

    /// <summary>Checks that the task can be reopened and returns the status it will return to.</summary>
    public static TaskStatus EnsureReopen(TaskStatus from)
    {
        if (TryGetReopenTarget(from, out var target))
        {
            return target;
        }

        throw new InvalidStatusTransitionException(
            from,
            from,
            "переоткрыть можно только выполненную (Done) или отменённую (Cancelled) задачу.");
    }
}
