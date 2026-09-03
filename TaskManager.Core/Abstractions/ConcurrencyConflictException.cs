namespace TaskManager.Core.Abstractions;

/// <summary>
/// Данные изменили между чтением и сохранением (оптимистичная блокировка).
/// Core не знает про EF Core, поэтому инфраструктура превращает <c>DbUpdateConcurrencyException</c> в это исключение.
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
