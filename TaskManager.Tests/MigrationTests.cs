using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Tests;

public sealed class MigrationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public MigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private TaskManagerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TaskManagerDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Migrate_CreatesAllTables()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        var tables = new List<string>();
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("Tasks", tables);
        Assert.Contains("Tags", tables);
        Assert.Contains("TaskTags", tables);
        Assert.Contains("TaskDependencies", tables);
        Assert.Contains("TaskHistory", tables);
    }

    // Если поменяли сущности или конфигурации и забыли добавить миграцию, этот тест упадёт.
    [Fact]
    public async Task Migrations_MatchCurrentModel()
    {
        await using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
        await context.Database.MigrateAsync();
    }
}
