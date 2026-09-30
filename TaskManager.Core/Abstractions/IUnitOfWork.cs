namespace TaskManager.Core.Abstractions;

/// <summary>A unit of work: writes the accumulated changes in one transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all changes (tasks, tags, history) atomically: all or nothing.
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">The data was changed since it was read.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs several steps (several saves included) in one transaction.
    /// An exception inside rolls everything back. A nested call joins the transaction that is already open.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
