namespace TaskManager.Core.Abstractions;

/// <summary>Единица работы: записывает накопленные изменения одной транзакцией.</summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Сохраняет все изменения (задачи, теги, история) атомарно: либо всё, либо ничего.
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">Данные успели изменить с момента чтения.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет несколько шагов (в том числе несколько сохранений) в одной транзакции.
    /// Исключение внутри откатывает всё. Вложенный вызов присоединяется к уже открытой транзакции.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
