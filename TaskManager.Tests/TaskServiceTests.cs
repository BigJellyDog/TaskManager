using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Tests;

public sealed class TaskServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    /// <summary>A service on a new context. A new context for every operation imitates a separate user request.</summary>
    private TaskService NewService(TaskManagerDbContext context) =>
        new(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock);

    private async Task<TaskItem> CreateAsync(CreateTaskRequest request)
    {
        await using var context = _db.CreateContext();
        return await NewService(context).CreateAsync(request);
    }

    private async Task<List<TaskHistoryEntry>> HistoryAsync(int id)
    {
        await using var context = _db.CreateContext();
        return (await NewService(context).GetHistoryAsync(id)).ToList();
    }

    private static EditTaskRequest EditOf(TaskItem task, string? title = null, Priority? priority = null,
        string? description = null, DateTimeOffset? deadline = null, string[]? tags = null) =>
        new(
            task.Id,
            title ?? task.Title,
            priority ?? task.Priority,
            description ?? task.Description,
            deadline ?? task.Deadline,
            tags ?? task.Tags.Select(t => t.Name).ToArray(),
            task.Recurrence);

    // ---- Creation ----

    [Fact]
    public async Task Create_SavesTask_TagsAndHistory()
    {
        var created = await CreateAsync(new CreateTaskRequest(
            "Оплатить аренду",
            Priority.High,
            "до 5 числа",
            Now.AddDays(3),
            ["дом", "Деньги"]));

        await using var context = _db.CreateContext();
        var loaded = await NewService(context).GetAsync(created.Id);
        Assert.Equal("Оплатить аренду", loaded.Title);
        Assert.Equal(Priority.High, loaded.Priority);
        Assert.Equal(["Деньги", "дом"], loaded.Tags.Select(t => t.Name).Order());

        var history = await HistoryAsync(created.Id);
        var entry = Assert.Single(history);
        Assert.Equal(TaskService.CreateCommand, entry.Command);
        Assert.Equal("Created", entry.FieldName);
        Assert.Equal("Оплатить аренду", entry.NewValue);
        Assert.Equal(Now, entry.ChangedAt);
    }

    [Fact]
    public async Task Create_ReusesExistingTag_IgnoringCase()
    {
        await CreateAsync(new CreateTaskRequest("первая", Tags: ["Работа"]));
        await CreateAsync(new CreateTaskRequest("вторая", Tags: ["работа"]));

        await using var context = _db.CreateContext();
        var tag = await context.Tags.Include(t => t.Tasks).SingleAsync();
        Assert.Equal(2, tag.Tasks.Count);
    }

    [Fact]
    public async Task Create_DuplicateTagNamesInRequest_AreAddedOnce()
    {
        var created = await CreateAsync(new CreateTaskRequest("задача", Tags: ["дом", "ДОМ", " дом "]));

        await using var context = _db.CreateContext();
        Assert.Single((await NewService(context).GetAsync(created.Id)).Tags);
    }

    [Fact]
    public async Task Create_WithRecurrence_StoresRule()
    {
        var rule = new RecurrenceRule(RecurrenceFrequency.Monthly, 1, RecurrenceMode.AfterCompletion);

        var created = await CreateAsync(new CreateTaskRequest("аренда", Recurrence: rule));

        await using var context = _db.CreateContext();
        Assert.Equal(RecurrenceFrequency.Monthly, (await NewService(context).GetAsync(created.Id)).Recurrence?.Frequency);
    }

    [Fact]
    public async Task Create_InvalidTitle_SavesNothing()
    {
        await using (var context = _db.CreateContext())
        {
            await Assert.ThrowsAsync<DomainValidationException>(
                () => NewService(context).CreateAsync(new CreateTaskRequest("  ", Tags: ["тег"])));
        }

        await using var read = _db.CreateContext();
        Assert.Empty(await read.Tasks.ToListAsync());
        Assert.Empty(await read.Tags.ToListAsync());
        Assert.Empty(await read.HistoryEntries.ToListAsync());
    }

    [Fact]
    public async Task Create_DeadlineInPast_Throws()
    {
        await using var context = _db.CreateContext();

        await Assert.ThrowsAsync<DomainValidationException>(
            () => NewService(context).CreateAsync(new CreateTaskRequest("t", Deadline: Now.AddHours(-1))));
    }

    // ---- Editing ----

    [Fact]
    public async Task Edit_RecordsOnlyChangedFields_WithOldAndNewValues()
    {
        var task = await CreateAsync(new CreateTaskRequest("старое", Priority.Low, "описание"));
        _clock.Advance(TimeSpan.FromHours(1));

        await using (var context = _db.CreateContext())
        {
            var service = NewService(context);
            var loaded = await service.GetAsync(task.Id);
            await service.EditAsync(EditOf(loaded, title: "новое", priority: Priority.Critical));
        }

        var changes = (await HistoryAsync(task.Id)).Where(h => h.Command == TaskService.EditCommand).ToList();
        Assert.Equal(["Priority", "Title"], changes.Select(h => h.FieldName).Order());

        var title = changes.Single(h => h.FieldName == "Title");
        Assert.Equal("старое", title.OldValue);
        Assert.Equal("новое", title.NewValue);
        Assert.Equal(Now.AddHours(1), title.ChangedAt);

        var priority = changes.Single(h => h.FieldName == "Priority");
        Assert.Equal("Low", priority.OldValue);
        Assert.Equal("Critical", priority.NewValue);
    }

    [Fact]
    public async Task Edit_NothingChanged_WritesNoHistoryAndKeepsVersion()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача", Tags: ["тег"]));

        await using (var context = _db.CreateContext())
        {
            var service = NewService(context);
            var loaded = await service.GetAsync(task.Id);
            await service.EditAsync(EditOf(loaded));
        }

        Assert.Single(await HistoryAsync(task.Id));
        await using var read = _db.CreateContext();
        Assert.Equal(0, (await read.Tasks.SingleAsync()).RowVersion);
    }

    [Fact]
    public async Task Edit_Tags_AddsAndRemoves()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача", Tags: ["a", "b"]));

        await using (var context = _db.CreateContext())
        {
            var service = NewService(context);
            var loaded = await service.GetAsync(task.Id);
            await service.EditAsync(EditOf(loaded, tags: ["b", "c"]));
        }

        await using var read = _db.CreateContext();
        var saved = await NewService(read).GetAsync(task.Id);
        Assert.Equal(["b", "c"], saved.Tags.Select(t => t.Name).Order());

        var entry = (await HistoryAsync(task.Id)).Single(h => h.FieldName == "Tags");
        Assert.Equal("a, b", entry.OldValue);
        Assert.Equal("b, c", entry.NewValue);
    }

    [Fact]
    public async Task Edit_ClearDescriptionAndDeadline_IsRecorded()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача", Description: "текст", Deadline: Now.AddDays(1)));

        await using (var context = _db.CreateContext())
        {
            var service = NewService(context);
            var loaded = await service.GetAsync(task.Id);
            await service.EditAsync(new EditTaskRequest(loaded.Id, loaded.Title, loaded.Priority, null, null, [], null));
        }

        await using var read = _db.CreateContext();
        var saved = await NewService(read).GetAsync(task.Id);
        Assert.Null(saved.Description);
        Assert.Null(saved.Deadline);

        var fields = (await HistoryAsync(task.Id)).Select(h => h.FieldName);
        Assert.Contains("Description", fields);
        Assert.Contains("Deadline", fields);
    }

    [Fact]
    public async Task Edit_InvalidTitle_ThrowsAndChangesNothing()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));

        await using (var context = _db.CreateContext())
        {
            var service = NewService(context);
            var loaded = await service.GetAsync(task.Id);
            await Assert.ThrowsAsync<DomainValidationException>(() => service.EditAsync(EditOf(loaded, title: " ")));
        }

        await using var read = _db.CreateContext();
        Assert.Equal("задача", (await read.Tasks.SingleAsync()).Title);
        Assert.Single(await HistoryAsync(task.Id));
    }

    [Fact]
    public async Task Edit_UnknownTask_ThrowsNotFound()
    {
        await using var context = _db.CreateContext();

        var ex = await Assert.ThrowsAsync<TaskNotFoundException>(() =>
            NewService(context).EditAsync(new EditTaskRequest(999, "t", Priority.Low, null, null, [], null)));
        Assert.Equal(999, ex.TaskId);
    }

    // ---- Statuses ----

    [Fact]
    public async Task ChangeStatus_AllowedTransition_IsSavedAndRecorded()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));

        await using (var context = _db.CreateContext())
        {
            await NewService(context).ChangeStatusAsync(task.Id, TaskStatus.InProgress);
        }

        _clock.Advance(TimeSpan.FromHours(2));
        await using (var context = _db.CreateContext())
        {
            await NewService(context).ChangeStatusAsync(task.Id, TaskStatus.Done);
        }

        await using var read = _db.CreateContext();
        var saved = await NewService(read).GetAsync(task.Id);
        Assert.Equal(TaskStatus.Done, saved.Status);
        Assert.Equal(Now.AddHours(2), saved.CompletedAt);

        var statusChanges = (await HistoryAsync(task.Id)).Where(h => h.FieldName == "Status").ToList();
        Assert.Equal(["Todo→InProgress", "InProgress→Done"], statusChanges.Select(h => $"{h.OldValue}→{h.NewValue}"));
        Assert.All(statusChanges, h => Assert.Equal(TaskService.ChangeStatusCommand, h.Command));
    }

    [Fact]
    public async Task ChangeStatus_ForbiddenTransition_ThrowsAndWritesNothing()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));

        await using (var context = _db.CreateContext())
        {
            await Assert.ThrowsAsync<InvalidStatusTransitionException>(
                () => NewService(context).ChangeStatusAsync(task.Id, TaskStatus.Done));
        }

        await using var read = _db.CreateContext();
        Assert.Equal(TaskStatus.Todo, (await read.Tasks.SingleAsync()).Status);
        Assert.Single(await HistoryAsync(task.Id));
    }

    [Fact]
    public async Task Reopen_DoneTask_ReturnsToInProgress()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));
        await using (var context = _db.CreateContext())
        {
            var service = NewService(context);
            await service.ChangeStatusAsync(task.Id, TaskStatus.InProgress);
            await service.ChangeStatusAsync(task.Id, TaskStatus.Done);
        }

        await using (var context = _db.CreateContext())
        {
            await NewService(context).ReopenAsync(task.Id);
        }

        await using var read = _db.CreateContext();
        var saved = await NewService(read).GetAsync(task.Id);
        Assert.Equal(TaskStatus.InProgress, saved.Status);
        Assert.Null(saved.CompletedAt);

        var last = (await HistoryAsync(task.Id)).Last();
        Assert.Equal(TaskService.ReopenCommand, last.Command);
        Assert.Equal("Done", last.OldValue);
        Assert.Equal("InProgress", last.NewValue);
    }

    [Fact]
    public async Task Reopen_OpenTask_Throws()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));
        await using var context = _db.CreateContext();

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(() => NewService(context).ReopenAsync(task.Id));
    }

    // ---- Deleting and restoring ----

    [Fact]
    public async Task Delete_HidesTask_AndRecordsHistory()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));

        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(task.Id);
        }

        await using var read = _db.CreateContext();
        await Assert.ThrowsAsync<TaskNotFoundException>(() => NewService(read).GetAsync(task.Id));

        var last = (await HistoryAsync(task.Id)).Last();
        Assert.Equal(TaskService.DeleteCommand, last.Command);
        Assert.Equal("IsDeleted", last.FieldName);
        Assert.Equal("False", last.OldValue);
        Assert.Equal("True", last.NewValue);
    }

    [Fact]
    public async Task Delete_AlreadyDeleted_ThrowsNotFound()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));
        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(task.Id);
        }

        await using var again = _db.CreateContext();
        await Assert.ThrowsAsync<TaskNotFoundException>(() => NewService(again).DeleteAsync(task.Id));
    }

    [Fact]
    public async Task Restore_ReturnsTask_AndRecordsHistory()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));
        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(task.Id);
        }

        await using (var context = _db.CreateContext())
        {
            await NewService(context).RestoreAsync(task.Id);
        }

        await using var read = _db.CreateContext();
        Assert.Equal("задача", (await NewService(read).GetAsync(task.Id)).Title);
        Assert.Equal(
            [TaskService.CreateCommand, TaskService.DeleteCommand, TaskService.RestoreCommand],
            (await HistoryAsync(task.Id)).Select(h => h.Command));
    }

    [Fact]
    public async Task Restore_NotDeletedTask_Throws()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));
        await using var context = _db.CreateContext();

        await Assert.ThrowsAsync<DomainValidationException>(() => NewService(context).RestoreAsync(task.Id));
    }

    [Fact]
    public async Task EditAndStatusChange_OnDeletedTask_ThrowNotFound()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));
        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(task.Id);
        }

        await using var read = _db.CreateContext();
        var service = NewService(read);
        await Assert.ThrowsAsync<TaskNotFoundException>(() => service.ChangeStatusAsync(task.Id, TaskStatus.InProgress));
        await Assert.ThrowsAsync<TaskNotFoundException>(
            () => service.EditAsync(new EditTaskRequest(task.Id, "x", Priority.Low, null, null, [], null)));
    }

    // ---- Concurrency ----

    [Fact]
    public async Task ConcurrentEdit_IsRejected_AndSecondHistoryIsNotWritten()
    {
        var task = await CreateAsync(new CreateTaskRequest("задача"));

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var firstService = NewService(first);
        var secondService = NewService(second);

        // The second user has read the task and keeps it in memory.
        var staleCopy = await secondService.GetAsync(task.Id);
        Assert.NotNull(staleCopy);

        // The first one managed to change and save.
        var loaded = await firstService.GetAsync(task.Id);
        await firstService.EditAsync(EditOf(loaded, title: "первый"));

        // The second one saves over stale data.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => secondService.ChangeStatusAsync(task.Id, TaskStatus.InProgress));

        // The second one's history record did not appear: the task and the history are saved in one transaction.
        var history = await HistoryAsync(task.Id);
        Assert.DoesNotContain(history, h => h.Command == TaskService.ChangeStatusCommand);
        await using var read = _db.CreateContext();
        var saved = await read.Tasks.SingleAsync();
        Assert.Equal("первый", saved.Title);
        Assert.Equal(TaskStatus.Todo, saved.Status);
    }
}
