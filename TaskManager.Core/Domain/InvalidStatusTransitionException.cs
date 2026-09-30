namespace TaskManager.Core.Domain;

/// <summary>An attempt to move a task to a status that the transition table forbids.</summary>
public class InvalidStatusTransitionException : InvalidOperationException
{
    public InvalidStatusTransitionException()
    {
    }

    public InvalidStatusTransitionException(string message)
        : base(message)
    {
    }

    public InvalidStatusTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public InvalidStatusTransitionException(TaskStatus from, TaskStatus to, string reason)
        : base($"Нельзя перевести задачу из {from} в {to}: {reason}")
    {
        From = from;
        To = to;
    }

    public TaskStatus? From { get; }

    public TaskStatus? To { get; }
}
