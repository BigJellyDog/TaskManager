using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Tests;

/// <summary>Теги: список с числом задач, переименование и удаление неиспользуемых.</summary>
public sealed class TagTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private TaskService Tasks(TaskManagerDbContext context) =>
        new(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock);

    private static TagService Tags(TaskManagerDbContext context) => new(new TagRepository(context), new UnitOfWork(context));

    private async Task<int> CreateAsync(string title, params string[] tags)
    {
        await using var context = _db.CreateContext();
        return (await Tasks(context).CreateAsync(new CreateTaskRequest(title, Tags: tags))).Id;
    }

    private async Task<IReadOnlyList<TagUsage>> ListAsync()
    {
        await using var context = _db.CreateContext();
        return await Tags(context).ListAsync();
    }

    [Fact]
    public async Task List_CountsLiveTasksPerTag()
    {
        await CreateAsync("a", "работа", "дом");
        await CreateAsync("b", "работа");

        var list = await ListAsync();

        Assert.Equal(2, list.Single(t => t.Name == "работа").TaskCount);
        Assert.Equal(1, list.Single(t => t.Name == "дом").TaskCount);
        Assert.All(list, t => Assert.False(t.CanDelete));
    }

    [Fact]
    public async Task Rename_ChangesTagForAllTasks_AndIgnoresLeadingHash()
    {
        await CreateAsync("a", "работа");
        var id = (await ListAsync()).Single().Id;

        await using (var context = _db.CreateContext())
        {
            await Tags(context).RenameAsync(id, "#офис");
        }

        Assert.Equal("офис", Assert.Single(await ListAsync()).Name);
    }

    [Fact]
    public async Task Rename_ToNameOfAnotherTag_IsRejected()
    {
        await CreateAsync("a", "работа", "дом");
        var work = (await ListAsync()).Single(t => t.Name == "работа").Id;

        await using var context = _db.CreateContext();
        await Assert.ThrowsAsync<DomainValidationException>(() => Tags(context).RenameAsync(work, "дом"));
    }

    [Fact]
    public async Task Delete_UsedTag_IsRejected_UnusedTag_IsRemoved()
    {
        var task = await CreateAsync("a", "работа");
        var id = (await ListAsync()).Single().Id;

        await using (var context = _db.CreateContext())
        {
            await Assert.ThrowsAsync<DomainValidationException>(() => Tags(context).DeleteAsync(id));
        }

        // Тег освобождается, когда задачу стёрли навсегда (в корзине он ещё считается занятым).
        await using (var context = _db.CreateContext())
        {
            await Tasks(context).DeleteAsync(task);
            await new TrashService(new TrashRepository(context), Tasks(context)).PurgeAsync([task]);
        }

        await using (var context = _db.CreateContext())
        {
            await Tags(context).DeleteAsync(id);
        }

        Assert.Empty(await ListAsync());
    }
}
