namespace TaskManager.Core.Domain;

/// <summary>A dependency: the task <see cref="TaskId"/> cannot be started until <see cref="DependsOnTaskId"/> is Done.</summary>
public class TaskDependency
{
    public TaskDependency(int taskId, int dependsOnTaskId)
    {
        TaskId = taskId;
        DependsOnTaskId = dependsOnTaskId;
    }

    // Needed by EF Core.
    private TaskDependency()
    {
    }

    /// <summary>The dependent task ("A").</summary>
    public int TaskId { get; private set; }

    public TaskItem? Task { get; private set; }

    /// <summary>The task it depends on ("B").</summary>
    public int DependsOnTaskId { get; private set; }

    public TaskItem? DependsOnTask { get; private set; }
}
