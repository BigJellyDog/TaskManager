using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Tests;

/// <summary>The trash: a list, restore, delete for good (including a parent with subtasks and dependencies).</summary>
public sealed class TrashTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private TaskService Tasks(TaskManager.Infrastructure.Persistence.TaskManagerDbContext context) =>
        new(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock);

    private TrashService Trash(TaskManager.Infrastructure.Persistence.TaskManagerDbContext context) =>
        new(new TrashRepository(context), Tasks(context));

    private async Task<int> CreateAsync(string title, int? parentId = null, params string[] tags)
    {
        await using var context = _db.CreateContext();
        return (await Tasks(context).CreateAsync(new CreateTaskRequest(title, ParentId: parentId, Tags: tags))).Id;
    }

    private async Task DeleteAsync(int id)
    {
        await using var context = _db.CreateContext();
        await Tasks(context).DeleteAsync(id);
    }

    private async Task<List<int>> AllTaskIdsAsync()
    {
        await using var context = _db.CreateContext();
        return await context.Tasks.IgnoreQueryFilters().OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
    }

    [Fact]
    public async Task Purge_ParentWithSubtasksAndDependencies_RemovesThemAndKeepsOthers()
    {
        // Regression: SQLite checks ON DELETE RESTRICT row by row, and the soft-delete filter was hiding the dependency links.
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("подзадача", parent);
        var other = await CreateAsync("другая");

        await using (var context = _db.CreateContext())
        {
            var tasks = Tasks(context);
            await tasks.AddDependencyAsync(other, parent);
            await tasks.AddDependencyAsync(child, other);
        }

        await DeleteAsync(parent);

        await using (var context = _db.CreateContext())
        {
            Assert.Equal(2, await Trash(context).PurgeAsync([parent]));
        }

        Assert.Equal([other], await AllTaskIdsAsync());

        await using var check = _db.CreateContext();
        Assert.Equal(0, await check.TaskDependencies.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await check.HistoryEntries.CountAsync(h => h.TaskId == parent || h.TaskId == child));
    }

    [Fact]
    public async Task Purge_TaskNotInTrash_IsIgnored()
    {
        var alive = await CreateAsync("живая");

        await using (var context = _db.CreateContext())
        {
            Assert.Equal(0, await Trash(context).PurgeAsync([alive]));
        }

        Assert.Equal([alive], await AllTaskIdsAsync());
    }

    [Fact]
    public async Task Empty_RemovesEveryTaskInTrash()
    {
        var a = await CreateAsync("a");
        var b = await CreateAsync("b");
        var alive = await CreateAsync("живая");
        await DeleteAsync(a);
        await DeleteAsync(b);

        await using (var context = _db.CreateContext())
        {
            Assert.Equal(2, await Trash(context).EmptyAsync());
        }

        Assert.Equal([alive], await AllTaskIdsAsync());
    }

    [Fact]
    public async Task Restore_ReturnsParentTogetherWithSubtasks()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("подзадача", parent);
        await DeleteAsync(parent);

        await using (var context = _db.CreateContext())
        {
            Assert.Equal(2, await Trash(context).RestoreAsync([parent]));
        }

        await using var check = _db.CreateContext();
        Assert.Equal(0, await Trash(check).CountAsync());
        Assert.Equal(2, await check.Tasks.CountAsync(t => t.Id == parent || t.Id == child));
    }

    [Fact]
    public async Task List_ShowsOnlyTopLevelDeletedTasks_WithSubtaskCount()
    {
        var parent = await CreateAsync("родитель");
        await CreateAsync("подзадача 1", parent);
        await CreateAsync("подзадача 2", parent);
        await DeleteAsync(parent);

        await using var context = _db.CreateContext();
        var entry = Assert.Single(await Trash(context).ListAsync());
        Assert.Equal(parent, entry.Id);
        Assert.Equal(2, entry.DeletedSubtasks);
    }

    [Fact]
    public async Task Purge_FreesTagsOfRemovedTasks()
    {
        var task = await CreateAsync("с тегом", null, "работа");
        await DeleteAsync(task);

        await using (var context = _db.CreateContext())
        {
            await Trash(context).PurgeAsync([task]);
        }

        await using var check = _db.CreateContext();
        var tag = Assert.Single(await new TagService(new TagRepository(check), new UnitOfWork(check)).ListAsync());
        Assert.Equal(0, tag.TaskCount);
        Assert.True(tag.CanDelete);
    }
}
