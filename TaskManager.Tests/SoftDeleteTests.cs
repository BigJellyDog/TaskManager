using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Domain;

namespace TaskManager.Tests;

public sealed class SoftDeleteTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private TaskItem NewTask(string title) => TaskItem.Create(title, Priority.Medium, _clock);

    private async Task DeleteAsync(string title)
    {
        await using var context = _db.CreateContext();
        var task = await context.Tasks.SingleAsync(t => t.Title == title);
        task.Delete();
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task DeletedTask_IsHiddenFromQueries()
    {
        await using (var write = _db.CreateContext())
        {
            write.Tasks.AddRange(NewTask("живая"), NewTask("удалённая"));
            await write.SaveChangesAsync();
        }

        await DeleteAsync("удалённая");

        await using var read = _db.CreateContext();
        Assert.Equal("живая", Assert.Single(await read.Tasks.ToListAsync()).Title);
        Assert.Null(await read.Tasks.FirstOrDefaultAsync(t => t.Title == "удалённая"));
    }

    [Fact]
    public async Task DeletedTask_StaysInDatabase_AndCanBeFoundIgnoringFilters()
    {
        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(NewTask("удалённая"));
            await write.SaveChangesAsync();
        }

        await DeleteAsync("удалённая");

        await using var read = _db.CreateContext();
        var deleted = await read.Tasks.IgnoreQueryFilters().SingleAsync();
        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task RestoredTask_IsVisibleAgain()
    {
        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(NewTask("задача"));
            await write.SaveChangesAsync();
        }

        await DeleteAsync("задача");

        await using (var context = _db.CreateContext())
        {
            var task = await context.Tasks.IgnoreQueryFilters().SingleAsync();
            task.Restore();
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        Assert.Equal("задача", Assert.Single(await read.Tasks.ToListAsync()).Title);
    }

    [Fact]
    public async Task DeletedSubtask_IsHiddenFromParentsChildren()
    {
        var parent = NewTask("родитель");
        parent.Children.Add(NewTask("живой ребёнок"));
        parent.Children.Add(NewTask("удалённый ребёнок"));
        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(parent);
            await write.SaveChangesAsync();
        }

        await DeleteAsync("удалённый ребёнок");

        await using var read = _db.CreateContext();
        var loaded = await read.Tasks.Include(t => t.Children).SingleAsync(t => t.Title == "родитель");
        Assert.Equal("живой ребёнок", Assert.Single(loaded.Children).Title);
    }

    [Fact]
    public async Task Dependency_OnDeletedTask_IsHidden_AndReturnsAfterRestore()
    {
        var tests = NewTask("тесты");
        var deploy = NewTask("деплой");
        await using (var write = _db.CreateContext())
        {
            write.Tasks.AddRange(tests, deploy);
            await write.SaveChangesAsync();
            write.TaskDependencies.Add(new TaskDependency(deploy.Id, tests.Id));
            await write.SaveChangesAsync();
        }

        await DeleteAsync("тесты");

        await using (var read = _db.CreateContext())
        {
            Assert.Empty(await read.TaskDependencies.ToListAsync());
        }

        await using (var context = _db.CreateContext())
        {
            var task = await context.Tasks.IgnoreQueryFilters().SingleAsync(t => t.Title == "тесты");
            task.Restore();
            await context.SaveChangesAsync();
        }

        await using var readAgain = _db.CreateContext();
        Assert.Single(await readAgain.TaskDependencies.ToListAsync());
    }

    [Fact]
    public async Task Delete_IncrementsRowVersion()
    {
        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(NewTask("задача"));
            await write.SaveChangesAsync();
        }

        await DeleteAsync("задача");

        await using var read = _db.CreateContext();
        Assert.Equal(1, (await read.Tasks.IgnoreQueryFilters().SingleAsync()).RowVersion);
    }
}
