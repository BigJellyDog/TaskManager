namespace TaskManager.Core.Domain;

/// <summary>Зависимость: задачу <see cref="TaskId"/> нельзя начать, пока <see cref="DependsOnTaskId"/> не Done.</summary>
public class TaskDependency
{
    public TaskDependency(int taskId, int dependsOnTaskId)
    {
        TaskId = taskId;
        DependsOnTaskId = dependsOnTaskId;
    }

    // Нужен EF Core.
    private TaskDependency()
    {
    }

    /// <summary>Зависимая задача ("A").</summary>
    public int TaskId { get; private set; }

    public TaskItem? Task { get; private set; }

    /// <summary>Задача, от которой зависят ("B").</summary>
    public int DependsOnTaskId { get; private set; }

    public TaskItem? DependsOnTask { get; private set; }
}
