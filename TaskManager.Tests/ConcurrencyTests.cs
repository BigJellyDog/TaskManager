using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Domain;

namespace TaskManager.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private async Task<int> SeedTaskAsync()
    {
        var task = TaskItem.Create("Задача", Priority.Medium, _clock);
        await using var context = _db.CreateContext();
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    [Fact]
    public async Task NewTask_StartsAtVersionZero()
    {
        var id = await SeedTaskAsync();

        await using var context = _db.CreateContext();

        Assert.Equal(0, (await context.Tasks.FindAsync(id))!.RowVersion);
    }

    [Fact]
    public async Task Update_IncrementsRowVersion()
    {
        var id = await SeedTaskAsync();

        await using (var context = _db.CreateContext())
        {
            var task = await context.Tasks.FindAsync(id);
            task!.SetTitle("Новое название");
            await context.SaveChangesAsync();
            Assert.Equal(1, task.RowVersion);

            task.SetPriority(Priority.High);
            await context.SaveChangesAsync();
            Assert.Equal(2, task.RowVersion);
        }

        await using var read = _db.CreateContext();
        Assert.Equal(2, (await read.Tasks.FindAsync(id))!.RowVersion);
    }

    [Fact]
    public async Task SaveWithoutChanges_DoesNotChangeRowVersion()
    {
        var id = await SeedTaskAsync();

        await using var context = _db.CreateContext();
        var task = await context.Tasks.FindAsync(id);
        await context.SaveChangesAsync();

        Assert.Equal(0, task!.RowVersion);
    }

    [Fact]
    public async Task RecurrenceChange_IncrementsRowVersion()
    {
        var id = await SeedTaskAsync();

        await using var context = _db.CreateContext();
        var task = await context.Tasks.FindAsync(id);
        task!.SetRecurrence(new RecurrenceRule(RecurrenceFrequency.Daily, 1, RecurrenceMode.AfterCompletion));
        await context.SaveChangesAsync();

        Assert.Equal(1, task.RowVersion);
    }

    [Fact]
    public async Task RecurrenceRemoval_IncrementsRowVersion()
    {
        var id = await SeedTaskAsync();
        await using var context = _db.CreateContext();
        var task = await context.Tasks.FindAsync(id);
        task!.SetRecurrence(new RecurrenceRule(RecurrenceFrequency.Daily, 1, RecurrenceMode.AfterCompletion));
        await context.SaveChangesAsync();

        task.SetRecurrence(null);
        await context.SaveChangesAsync();

        Assert.Equal(2, task.RowVersion);
        await using var read = _db.CreateContext();
        Assert.Null((await read.Tasks.FindAsync(id))!.Recurrence);
    }

    [Fact]
    public async Task ConcurrentUpdate_SecondSaveIsRejected()
    {
        var id = await SeedTaskAsync();

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var taskInFirst = await first.Tasks.FindAsync(id);
        var taskInSecond = await second.Tasks.FindAsync(id);

        taskInFirst!.SetTitle("Изменил первый");
        await first.SaveChangesAsync();

        taskInSecond!.SetPriority(Priority.Critical);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        // Первое изменение не потеряно, второе не записано.
        await using var read = _db.CreateContext();
        var saved = await read.Tasks.FindAsync(id);
        Assert.Equal("Изменил первый", saved!.Title);
        Assert.Equal(Priority.Medium, saved.Priority);
    }

    [Fact]
    public async Task AfterConflict_ReloadAndRetrySucceeds()
    {
        var id = await SeedTaskAsync();

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var taskInFirst = await first.Tasks.FindAsync(id);
        var taskInSecond = await second.Tasks.FindAsync(id);
        taskInFirst!.SetTitle("Изменил первый");
        await first.SaveChangesAsync();
        taskInSecond!.SetPriority(Priority.Critical);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        // "Повторить": перечитываем актуальные данные и применяем своё изменение заново.
        await second.Entry(taskInSecond).ReloadAsync();
        taskInSecond.SetPriority(Priority.Critical);
        await second.SaveChangesAsync();

        await using var read = _db.CreateContext();
        var saved = await read.Tasks.FindAsync(id);
        Assert.Equal("Изменил первый", saved!.Title);
        Assert.Equal(Priority.Critical, saved.Priority);
        Assert.Equal(2, saved.RowVersion);
    }

    [Fact]
    public async Task DeleteOfStaleTask_IsRejected()
    {
        var id = await SeedTaskAsync();

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var taskInFirst = await first.Tasks.FindAsync(id);
        var taskInSecond = await second.Tasks.FindAsync(id);
        taskInFirst!.SetTitle("Изменил первый");
        await first.SaveChangesAsync();

        second.Tasks.Remove(taskInSecond!);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
