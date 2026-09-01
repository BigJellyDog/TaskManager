namespace TaskManager.Core.Domain;

/// <summary>
/// Статус задачи. Допустимые переходы между статусами описывает <c>TaskStateMachine</c>.
/// Внимание: имя совпадает с <see cref="System.Threading.Tasks.TaskStatus"/>, в других пространствах имён используй алиас.
/// </summary>
public enum TaskStatus
{
    Todo = 0,
    InProgress = 1,
    Blocked = 2,
    Done = 3,
    Cancelled = 4,
}
