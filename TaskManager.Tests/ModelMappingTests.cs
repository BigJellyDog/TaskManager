using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Domain;

namespace TaskManager.Tests;

public sealed class ModelMappingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private TaskItem NewTask(string title, DateTimeOffset? deadline = null) =>
        TaskItem.Create(title, Priority.Medium, _clock, deadline: deadline);

    [Fact]
    public async Task Task_WithAllFields_RoundTrips()
    {
        var task = TaskItem.Create("Оплатить аренду", Priority.High, _clock, "каждый месяц", Now.AddDays(3));
        task.SetRecurrence(new RecurrenceRule(RecurrenceFrequency.Monthly, 2, RecurrenceMode.ByCalendar));
        task.AddTag(new Tag("дом"));

        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(task);
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loaded = await read.Tasks.Include(t => t.Tags).SingleAsync();

        Assert.Equal("Оплатить аренду", loaded.Title);
        Assert.Equal("каждый месяц", loaded.Description);
        Assert.Equal(Priority.High, loaded.Priority);
        Assert.Equal(Now, loaded.CreatedAt);
        Assert.Equal(Now.AddDays(3), loaded.Deadline);
        Assert.Equal("дом", Assert.Single(loaded.Tags).Name);
        Assert.NotNull(loaded.Recurrence);
        Assert.Equal(RecurrenceFrequency.Monthly, loaded.Recurrence.Frequency);
        Assert.Equal(2, loaded.Recurrence.Interval);
        Assert.Equal(RecurrenceMode.ByCalendar, loaded.Recurrence.Mode);
    }

    [Fact]
    public async Task Task_WithoutRecurrence_LoadsNullRecurrence()
    {
        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(NewTask("Обычная"));
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();

        Assert.Null((await read.Tasks.SingleAsync()).Recurrence);
    }

    [Fact]
    public async Task Deadline_CanBeFilteredAndSortedInDatabase()
    {
        await using (var write = _db.CreateContext())
        {
            write.Tasks.AddRange(
                NewTask("поздно", Now.AddDays(10)),
                NewTask("рано", Now.AddDays(1)),
                NewTask("средне", Now.AddDays(5)));
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var limit = Now.AddDays(7);
        var titles = await read.Tasks
            .Where(t => t.Deadline < limit)
            .OrderBy(t => t.Deadline)
            .Select(t => t.Title)
            .ToListAsync();

        Assert.Equal(["рано", "средне"], titles);
    }

    [Fact]
    public async Task Subtasks_AreLinkedToParent()
    {
        var parent = NewTask("родитель");
        var child = NewTask("ребёнок");
        parent.Children.Add(child);

        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(parent);
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loadedChild = await read.Tasks.Include(t => t.Parent).SingleAsync(t => t.Title == "ребёнок");

        Assert.Equal("родитель", loadedChild.Parent?.Title);
        Assert.Equal(loadedChild.Parent?.Id, loadedChild.ParentId);
    }

    [Fact]
    public async Task Dependencies_AreStoredInBothDirections()
    {
        var tests = NewTask("написать тесты");
        var deploy = NewTask("задеплоить");

        await using (var write = _db.CreateContext())
        {
            write.Tasks.AddRange(tests, deploy);
            await write.SaveChangesAsync();
            write.TaskDependencies.Add(new TaskDependency(deploy.Id, tests.Id));
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loadedDeploy = await read.Tasks
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOnTask)
            .SingleAsync(t => t.Title == "задеплоить");
        var loadedTests = await read.Tasks
            .Include(t => t.Dependents).ThenInclude(d => d.Task)
            .SingleAsync(t => t.Title == "написать тесты");

        Assert.Equal("написать тесты", Assert.Single(loadedDeploy.Dependencies).DependsOnTask?.Title);
        Assert.Equal("задеплоить", Assert.Single(loadedTests.Dependents).Task?.Title);
    }

    [Fact]
    public async Task Dependency_AddedTwice_ViolatesPrimaryKey()
    {
        var a = NewTask("a");
        var b = NewTask("b");
        await using var context = _db.CreateContext();
        context.Tasks.AddRange(a, b);
        await context.SaveChangesAsync();
        context.TaskDependencies.Add(new TaskDependency(a.Id, b.Id));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        context.TaskDependencies.Add(new TaskDependency(a.Id, b.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Tag_SharedBetweenTasks_IsStoredOnce()
    {
        var shared = new Tag("работа");
        var first = NewTask("1");
        var second = NewTask("2");
        first.AddTag(shared);
        second.AddTag(shared);

        await using (var write = _db.CreateContext())
        {
            write.Tasks.AddRange(first, second);
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var tag = await read.Tags.Include(t => t.Tasks).SingleAsync();

        Assert.Equal(2, tag.Tasks.Count);
    }

    [Fact]
    public async Task Tag_NamesDifferingOnlyInCase_AreDuplicates()
    {
        await using var context = _db.CreateContext();
        context.Tags.Add(new Tag("Работа"));
        await context.SaveChangesAsync();

        context.Tags.Add(new Tag("работа"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task HistoryEntry_RoundTrips()
    {
        var task = NewTask("с историей");
        await using (var write = _db.CreateContext())
        {
            write.Tasks.Add(task);
            await write.SaveChangesAsync();
            write.HistoryEntries.Add(new TaskHistoryEntry(task.Id, Now, "edit", "Title", "старое", "новое"));
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var entry = await read.HistoryEntries.SingleAsync();

        Assert.Equal(task.Id, entry.TaskId);
        Assert.Equal(Now, entry.ChangedAt);
        Assert.Equal("Title", entry.FieldName);
        Assert.Equal("старое", entry.OldValue);
        Assert.Equal("новое", entry.NewValue);
    }
}
