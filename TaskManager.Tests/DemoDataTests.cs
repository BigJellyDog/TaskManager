using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Tests;

/// <summary>Demo data: creation, translation of the tag into the interface language, deleting to the trash.</summary>
public sealed class DemoDataTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private DemoDataService Demo(TaskManagerDbContext context) =>
        new(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock);

    private async Task<T> RunAsync<T>(Func<DemoDataService, Task<T>> action)
    {
        await using var context = _db.CreateContext();
        return await action(Demo(context));
    }

    [Fact]
    public async Task Seed_CreatesTasksTaggedAsDemo()
    {
        Assert.False(await RunAsync(d => d.HasDemoDataAsync()));

        var created = await RunAsync(d => d.SeedAsync());

        Assert.True(created > 0);
        Assert.True(await RunAsync(d => d.HasDemoDataAsync()));
    }

    [Fact]
    public async Task Seed_WithTranslation_IsStillRecognisedAsDemo()
    {
        var created = await RunAsync(d => d.SeedAsync(text => text == DemoDataService.DemoTag ? "demo" : text));

        Assert.True(created > 0);
        Assert.True(await RunAsync(d => d.HasDemoDataAsync()));
    }

    [Fact]
    public async Task Remove_MovesDemoTasksToTrash()
    {
        await RunAsync(d => d.SeedAsync());

        var removed = await RunAsync(d => d.RemoveDemoDataAsync());

        Assert.True(removed > 0);
        Assert.False(await RunAsync(d => d.HasDemoDataAsync()));

        await using var context = _db.CreateContext();
        var trash = new TrashService(new TrashRepository(context), new TaskService(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock));
        Assert.True(await trash.CountAsync() > 0);
    }
}
