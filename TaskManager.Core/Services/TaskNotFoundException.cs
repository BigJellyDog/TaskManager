namespace TaskManager.Core.Services;

/// <summary>Задачи с таким Id нет (или она удалена).</summary>
public class TaskNotFoundException : Exception
{
    public TaskNotFoundException()
    {
    }

    public TaskNotFoundException(string message)
        : base(message)
    {
    }

    public TaskNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TaskNotFoundException(int taskId)
        : base($"Задача #{taskId} не найдена.")
    {
        TaskId = taskId;
    }

    public int? TaskId { get; }
}
