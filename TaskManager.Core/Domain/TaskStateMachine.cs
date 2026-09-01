namespace TaskManager.Core.Domain;

/// <summary>
/// Конечный автомат статусов. Единственное место, где описано, какие переходы разрешены.
/// Обычные переходы лежат в <see cref="Transitions"/>, переоткрытие (Done и Cancelled) вынесено отдельно
/// и доступно только через <see cref="EnsureReopen"/>.
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

    // Статус, в который задача возвращается при переоткрытии.
    private static readonly Dictionary<TaskStatus, TaskStatus> ReopenTargets =
        new Dictionary<TaskStatus, TaskStatus>
        {
            [TaskStatus.Done] = TaskStatus.InProgress,
            [TaskStatus.Cancelled] = TaskStatus.Todo,
        };

    /// <summary>Разрешён ли обычный переход (без переоткрытия).</summary>
    public static bool CanTransition(TaskStatus from, TaskStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Статусы, в которые можно перейти из <paramref name="from"/> обычным путём.</summary>
    public static IReadOnlyCollection<TaskStatus> GetAllowedTransitions(TaskStatus from) =>
        Transitions.TryGetValue(from, out var allowed) ? allowed : [];

    /// <summary>Можно ли переоткрыть задачу в этом статусе, и если да, то в какой статус она вернётся.</summary>
    public static bool TryGetReopenTarget(TaskStatus from, out TaskStatus target) =>
        ReopenTargets.TryGetValue(from, out target);

    /// <summary>Бросает <see cref="InvalidStatusTransitionException"/>, если обычный переход запрещён.</summary>
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

    /// <summary>Проверяет, что задачу можно переоткрыть, и возвращает статус, в который она вернётся.</summary>
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
