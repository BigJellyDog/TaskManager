namespace TaskManager.Core.Domain;

/// <summary>Попытка перевести задачу в статус, который запрещён таблицей переходов.</summary>
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
