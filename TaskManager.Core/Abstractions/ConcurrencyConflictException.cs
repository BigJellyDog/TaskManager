namespace TaskManager.Core.Abstractions;

/// <summary>
/// The data was changed between reading and saving (optimistic locking).
/// Core knows nothing about EF Core, so the infrastructure turns <c>DbUpdateConcurrencyException</c> into this exception.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Данные были изменены в другом месте. Обновите их и повторите действие.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
