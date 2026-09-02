using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Tests;

/// <summary>
/// SQLite in-memory для тестов. База живёт, пока открыто соединение,
/// поэтому его держим открытым до Dispose. Каждый тест получает свою чистую БД.
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

    /// <summary>Новый контекст на ту же БД. Разные контексты помогают проверить, что данные реально записаны, а не лежат в кэше.</summary>
    public TaskManagerDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
