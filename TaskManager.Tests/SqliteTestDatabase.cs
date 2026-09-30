using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Tests;

/// <summary>
/// SQLite in-memory for tests. The database lives while the connection is open,
/// so we keep it open until Dispose. Each test gets its own clean database.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TaskManagerDbContext> _options;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TaskManagerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A new context on the same database. Different contexts help to check that the data is really written and not sitting in a cache.</summary>
    public TaskManagerDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
