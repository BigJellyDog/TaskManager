using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Tests;

/// <summary>A database backup and restore from it (real files in a temporary folder).</summary>
public sealed class BackupTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tm-backup-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly string _databasePath;

    public BackupTests()
    {
        Directory.CreateDirectory(_folder);
        _databasePath = Path.Combine(_folder, "taskmanager.db");
    }

    public void Dispose()
    {
        // The connection pool keeps the file open: without this the folder cannot be deleted.
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private static TaskManagerDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<TaskManagerDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);

    private SqliteDatabaseBackup NewBackup() => new(_databasePath, _clock, NullLogger<SqliteDatabaseBackup>.Instance);

    private async Task CreateDatabaseWithTasksAsync(params string[] titles)
    {
        await using var context = Open(_databasePath);
        await context.Database.MigrateAsync();
        await AddTasksAsync(context, titles);
    }

    private async Task AddTasksAsync(TaskManagerDbContext context, params string[] titles)
    {
        var service = new TaskService(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock);
        foreach (var title in titles)
        {
            await service.CreateAsync(new CreateTaskRequest(title));
        }
    }

    [Fact]
    public async Task CreateBackup_ProducesDatabaseThatInspectRecognises()
    {
        await CreateDatabaseWithTasksAsync("a", "b");
        var copy = Path.Combine(_folder, "copy.db");

        await NewBackup().CreateBackupAsync(copy);
        var info = await NewBackup().InspectAsync(copy);

        Assert.Equal(2, info.TaskCount);
        Assert.True(info.MigrationCount > 0);
    }

    [Fact]
    public async Task Inspect_FileThatIsNotADatabase_IsRejected()
    {
        await CreateDatabaseWithTasksAsync("a");
        var garbage = Path.Combine(_folder, "garbage.db");
        await File.WriteAllTextAsync(garbage, "это не база данных");

        await Assert.ThrowsAsync<DomainValidationException>(() => NewBackup().InspectAsync(garbage));
    }

    [Fact]
    public async Task ApplyPendingRestore_ReplacesDatabase_AndKeepsSafetyCopy()
    {
        await CreateDatabaseWithTasksAsync("a", "b");
        var copy = Path.Combine(_folder, "copy.db");
        await NewBackup().CreateBackupAsync(copy);

        await using (var context = Open(_databasePath))
        {
            await AddTasksAsync(context, "c");
        }

        var backup = NewBackup();
        await backup.StageRestoreAsync(copy);
        var message = await backup.ApplyPendingRestoreAsync();

        Assert.NotNull(message);
        await using var restored = Open(_databasePath);
        Assert.Equal(2, await restored.Tasks.CountAsync());
        Assert.Single(Directory.GetFiles(_folder, "*.before-restore-*.bak"));
        Assert.Null(await NewBackup().ApplyPendingRestoreAsync());
    }
}
