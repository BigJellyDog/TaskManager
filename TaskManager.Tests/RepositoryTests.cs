using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Infrastructure;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Tests;

public sealed class RepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private TaskItem NewTask(string title) => TaskItem.Create(title, Priority.Medium, _clock);

    private static (TaskRepository Tasks, TagRepository Tags, UnitOfWork Uow) Create(TaskManagerDbContext context) =>
        (new TaskRepository(context), new TagRepository(context), new UnitOfWork(context));

    // ---- Tasks ----

    [Fact]
    public async Task AddAndGetById_ReturnsTask()
    {
        int id;
        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);
            var task = NewTask("задача");
            await tasks.AddAsync(task);
            await uow.SaveChangesAsync();
            id = task.Id;
        }

        await using var read = _db.CreateContext();
        var loaded = await Create(read).Tasks.GetByIdAsync(id);

        Assert.Equal("задача", loaded?.Title);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        await using var context = _db.CreateContext();

        Assert.Null(await Create(context).Tasks.GetByIdAsync(12345));
    }

    [Fact]
    public async Task GetById_DeletedTask_NeedsIncludeDeleted()
    {
        int id;
        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);
            var task = NewTask("удалю");
            await tasks.AddAsync(task);
            await uow.SaveChangesAsync();
            task.Delete();
            await uow.SaveChangesAsync();
            id = task.Id;
        }

        await using var read = _db.CreateContext();
        var repo = Create(read).Tasks;

        Assert.Null(await repo.GetByIdAsync(id));
        Assert.NotNull(await repo.GetByIdAsync(id, includeDeleted: true));
        Assert.Equal(id, Assert.Single(await repo.GetDeletedAsync()).Id);
    }

    [Fact]
    public async Task GetWithDetails_LoadsRelations()
    {
        var parent = NewTask("родитель");
        var child = NewTask("ребёнок");
        child.AddTag(new Tag("важное"));
        parent.Children.Add(child);
        var blocker = NewTask("блокер");
        int childId;

        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);
            await tasks.AddAsync(parent);
            await tasks.AddAsync(blocker);
            await uow.SaveChangesAsync();
            await tasks.AddDependencyAsync(new TaskDependency(child.Id, blocker.Id));
            await uow.SaveChangesAsync();
            childId = child.Id;
        }

        await using var read = _db.CreateContext();
        var loaded = await Create(read).Tasks.GetWithDetailsAsync(childId);

        Assert.NotNull(loaded);
        Assert.Equal("родитель", loaded.Parent?.Title);
        Assert.Equal("важное", Assert.Single(loaded.Tags).Name);
        Assert.Equal("блокер", Assert.Single(loaded.Dependencies).DependsOnTask?.Title);
    }

    // ---- Dependencies ----

    [Fact]
    public async Task Dependency_AddFindRemove()
    {
        var a = NewTask("a");
        var b = NewTask("b");
        await using var context = _db.CreateContext();
        var (tasks, _, uow) = Create(context);
        await tasks.AddAsync(a);
        await tasks.AddAsync(b);
        await uow.SaveChangesAsync();

        await tasks.AddDependencyAsync(new TaskDependency(a.Id, b.Id));
        await uow.SaveChangesAsync();
        var found = await tasks.GetDependencyAsync(a.Id, b.Id);
        Assert.NotNull(found);
        Assert.Null(await tasks.GetDependencyAsync(b.Id, a.Id));

        tasks.RemoveDependency(found);
        await uow.SaveChangesAsync();

        Assert.Null(await tasks.GetDependencyAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task DependencyGraph_IncludesEdgesOfDeletedTasks()
    {
        var a = NewTask("a");
        var b = NewTask("b");
        await using var context = _db.CreateContext();
        var (tasks, _, uow) = Create(context);
        await tasks.AddAsync(a);
        await tasks.AddAsync(b);
        await uow.SaveChangesAsync();
        await tasks.AddDependencyAsync(new TaskDependency(a.Id, b.Id));
        b.Delete();
        await uow.SaveChangesAsync();

        var graph = await tasks.GetDependencyGraphAsync();

        Assert.Equal((a.Id, b.Id), Assert.Single(graph));
        Assert.Null(await tasks.GetDependencyAsync(a.Id, b.Id));
    }

    // ---- History ----

    [Fact]
    public async Task History_IsReturnedInChronologicalOrder_PerTask()
    {
        var task = NewTask("с историей");
        var other = NewTask("другая");
        await using var context = _db.CreateContext();
        var (tasks, _, uow) = Create(context);
        await tasks.AddAsync(task);
        await tasks.AddAsync(other);
        await uow.SaveChangesAsync();

        await tasks.AddHistoryAsync(
        [
            new TaskHistoryEntry(task.Id, Now.AddMinutes(2), "edit", "Priority", "Medium", "High"),
            new TaskHistoryEntry(task.Id, Now.AddMinutes(1), "edit", "Title", "a", "b"),
            new TaskHistoryEntry(other.Id, Now, "edit", "Title", "x", "y"),
        ]);
        await uow.SaveChangesAsync();

        var history = await tasks.GetHistoryAsync(task.Id);

        Assert.Equal(["Title", "Priority"], history.Select(h => h.FieldName));
    }

    // ---- Tags ----

    [Fact]
    public async Task Tag_FindByName_IgnoresCaseIncludingCyrillic()
    {
        await using var context = _db.CreateContext();
        var (_, tags, uow) = Create(context);
        await tags.AddAsync(new Tag("Работа"));
        await uow.SaveChangesAsync();

        var found = await tags.GetByNameAsync("  рАБОТА ");

        Assert.Equal("Работа", found?.Name);
        Assert.Null(await tags.GetByNameAsync("дом"));
    }

    [Fact]
    public async Task Tag_List_IsSortedByName()
    {
        await using var context = _db.CreateContext();
        var (_, tags, uow) = Create(context);
        await tags.AddAsync(new Tag("яблоко"));
        await tags.AddAsync(new Tag("Арбуз"));
        await tags.AddAsync(new Tag("банан"));
        await uow.SaveChangesAsync();

        Assert.Equal(["Арбуз", "банан", "яблоко"], (await tags.ListAsync()).Select(t => t.Name));
    }

    [Fact]
    public async Task Tag_Rename_AffectsAllTasks()
    {
        var shared = new Tag("work");
        var first = NewTask("1");
        var second = NewTask("2");
        first.AddTag(shared);
        second.AddTag(shared);
        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);
            await tasks.AddAsync(first);
            await tasks.AddAsync(second);
            await uow.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            var (_, tags, uow) = Create(context);
            var tag = await tags.GetByNameAsync("work");
            tag!.Rename("job");
            await uow.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loaded = await read.Tasks.Include(t => t.Tags).ToListAsync();
        Assert.All(loaded, t => Assert.Equal("job", Assert.Single(t.Tags).Name));
    }

    [Fact]
    public async Task Tag_ListUnused_AndRemove()
    {
        var used = new Tag("используется");
        var task = NewTask("с тегом");
        task.AddTag(used);
        await using var context = _db.CreateContext();
        var (tasks, tags, uow) = Create(context);
        await tasks.AddAsync(task);
        await tags.AddAsync(new Tag("лишний"));
        await uow.SaveChangesAsync();

        var unused = await tags.ListUnusedAsync();
        Assert.Equal("лишний", Assert.Single(unused).Name);

        tags.Remove(unused[0]);
        await uow.SaveChangesAsync();

        Assert.Equal("используется", Assert.Single(await tags.ListAsync()).Name);
    }

    [Fact]
    public async Task Tag_OfDeletedTask_IsNotUnused()
    {
        var tag = new Tag("тег");
        var task = NewTask("будет удалена");
        task.AddTag(tag);
        await using var context = _db.CreateContext();
        var (tasks, tags, uow) = Create(context);
        await tasks.AddAsync(task);
        await uow.SaveChangesAsync();
        task.Delete();
        await uow.SaveChangesAsync();

        Assert.Empty(await tags.ListUnusedAsync());
    }

    // ---- UnitOfWork ----

    [Fact]
    public async Task UnitOfWork_TaskAndHistory_AreSavedTogether()
    {
        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);
            var task = NewTask("задача");
            await tasks.AddAsync(task);
            await uow.SaveChangesAsync();

            task.SetTitle("новое");
            await tasks.AddHistoryAsync([new TaskHistoryEntry(task.Id, Now, "edit", "Title", "задача", "новое")]);
            await uow.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        Assert.Equal("новое", (await read.Tasks.SingleAsync()).Title);
        Assert.Single(await read.HistoryEntries.ToListAsync());
    }

    [Fact]
    public async Task UnitOfWork_FailureInsideTransaction_RollsEverythingBack()
    {
        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
            {
                await tasks.AddAsync(NewTask("первая"), ct);
                await uow.SaveChangesAsync(ct);
                throw new InvalidOperationException("сбой на втором шаге");
            }));
        }

        await using var read = _db.CreateContext();
        Assert.Empty(await read.Tasks.ToListAsync());
    }

    [Fact]
    public async Task UnitOfWork_SuccessfulTransaction_CommitsAllSteps()
    {
        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);

            var count = await uow.ExecuteInTransactionAsync(async ct =>
            {
                await tasks.AddAsync(NewTask("первая"), ct);
                await uow.SaveChangesAsync(ct);
                await tasks.AddAsync(NewTask("вторая"), ct);
                await uow.SaveChangesAsync(ct);
                return 2;
            });

            Assert.Equal(2, count);
        }

        await using var read = _db.CreateContext();
        Assert.Equal(2, await read.Tasks.CountAsync());
    }

    [Fact]
    public async Task UnitOfWork_NestedTransaction_JoinsOuterOne()
    {
        await using (var context = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
            {
                await uow.ExecuteInTransactionAsync(async inner =>
                {
                    await tasks.AddAsync(NewTask("во внутренней"), inner);
                    await uow.SaveChangesAsync(inner);
                }, ct);
                throw new InvalidOperationException("сбой во внешней");
            }));
        }

        await using var read = _db.CreateContext();
        Assert.Empty(await read.Tasks.ToListAsync());
    }

    [Fact]
    public async Task UnitOfWork_ConcurrentChange_ThrowsConcurrencyConflictException()
    {
        int id;
        await using (var seed = _db.CreateContext())
        {
            var (tasks, _, uow) = Create(seed);
            var task = NewTask("задача");
            await tasks.AddAsync(task);
            await uow.SaveChangesAsync();
            id = task.Id;
        }

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var (firstTasks, _, firstUow) = Create(first);
        var (secondTasks, _, secondUow) = Create(second);
        var inFirst = await firstTasks.GetByIdAsync(id);
        var inSecond = await secondTasks.GetByIdAsync(id);

        inFirst!.SetTitle("первый");
        await firstUow.SaveChangesAsync();
        inSecond!.SetPriority(Priority.Critical);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => secondUow.SaveChangesAsync());
        Assert.IsType<DbUpdateConcurrencyException>(ex.InnerException);
    }

    // ---- DI ----

    [Fact]
    public async Task AddInfrastructure_ResolvesRepositoriesOnSharedContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"taskmanager-test-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection().AddInfrastructure(path).BuildServiceProvider();

            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>().Database.MigrateAsync();
                var tasks = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                await tasks.AddAsync(NewTask("через DI"));
                await uow.SaveChangesAsync();
            }

            await using var readScope = services.CreateAsyncScope();
            var loaded = await readScope.ServiceProvider.GetRequiredService<ITaskRepository>().GetByIdAsync(1);
            Assert.Equal("через DI", loaded?.Title);

            await services.DisposeAsync();
        }
        finally
        {
            SqliteConnectionPoolCleanup();
            File.Delete(path);
        }
    }

    private static void SqliteConnectionPoolCleanup() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}
