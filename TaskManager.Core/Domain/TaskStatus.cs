namespace TaskManager.Core.Domain;

/// <summary>
/// The task status. The allowed transitions between statuses are described by <c>TaskStateMachine</c>.
/// Note: the name matches <see cref="System.Threading.Tasks.TaskStatus"/>, in other namespaces use an alias.
/// </summary>
public enum TaskStatus
{
    Todo = 0,
    InProgress = 1,
    Blocked = 2,
    Done = 3,
    Cancelled = 4,
}
