using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly TaskManagerDbContext _context;

    public UnitOfWork(TaskManagerDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Один SaveChanges это одна транзакция: задача и записи истории сохраняются вместе.
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Данные были изменены в другом месте. Обновите их и повторите действие.",
                ex);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Уже внутри транзакции: просто выполняем, коммитом займётся внешний вызов.
        if (_context.Database.CurrentTransaction is not null)
        {
            return await action(cancellationToken);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
        // Если action бросил исключение, до CommitAsync не дойдём и Dispose откатит транзакцию.
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return ExecuteInTransactionAsync(
            async ct =>
            {
                await action(ct);
                return true;
            },
            cancellationToken);
    }
}
