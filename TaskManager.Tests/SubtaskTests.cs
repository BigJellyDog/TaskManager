using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Tests;

/// <summary>Доменные правила подзадач, без базы данных.</summary>
public class SubtaskDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);

    private TaskItem NewTask(string title = "задача") => TaskItem.Create(title, Priority.Medium, _clock);

    private TaskItem Done(string title)
    {
        var task = NewTask(title);
        task.ChangeStatus(TaskStatus.InProgress, _clock);
        task.ChangeStatus(TaskStatus.Done, _clock);
        return task;
    }

    // ---- Прогресс ----

    [Fact]
    public void Progress_WithoutSubtasks_IsEmptyAndComplete()
    {
        var progress = NewTask().GetProgress();

        Assert.Equal(new TaskProgress(0, 0), progress);
        Assert.False(progress.HasSubtasks);
        Assert.True(progress.IsComplete);
        Assert.Equal(0, progress.Percent);
    }

    [Fact]
    public void Progress_CountsDoneOutOfTotal()
    {
        var parent = NewTask("родитель");
        parent.Children.Add(Done("1"));
        parent.Children.Add(Done("2"));
        parent.Children.Add(Done("3"));
        parent.Children.Add(NewTask("4"));
        parent.Children.Add(NewTask("5"));

        var progress = parent.GetProgress();

        Assert.Equal("3/5", progress.ToString());
        Assert.Equal(60, progress.Percent);
        Assert.False(progress.IsComplete);
    }

    [Fact]
    public void Progress_IgnoresCancelledAndDeletedSubtasks()
    {
        var parent = NewTask("родитель");
        var cancelled = NewTask("отменена");
        cancelled.ChangeStatus(TaskStatus.Cancelled, _clock);
        var deleted = NewTask("удалена");
        deleted.Delete();
        parent.Children.Add(Done("выполнена"));
        parent.Children.Add(NewTask("открыта"));
        parent.Children.Add(cancelled);
        parent.Children.Add(deleted);

        Assert.Equal("1/2", parent.GetProgress().ToString());
    }

    // ---- Закрытие родителя ----

    [Fact]
    public void ChangeStatus_Done_WithOpenSubtasks_Throws()
    {
        var parent = NewTask("родитель");
        parent.Children.Add(Done("1"));
        parent.Children.Add(NewTask("2"));
        parent.ChangeStatus(TaskStatus.InProgress, _clock);

        var ex = Assert.Throws<DomainValidationException>(() => parent.ChangeStatus(TaskStatus.Done, _clock));

        Assert.Contains("1/2", ex.Message, StringComparison.Ordinal);
        Assert.Equal(TaskStatus.InProgress, parent.Status);
    }

    [Fact]
    public void ChangeStatus_Done_WhenAllSubtasksFinished_Succeeds()
    {
        var parent = NewTask("родитель");
        var cancelled = NewTask("отменена");
        cancelled.ChangeStatus(TaskStatus.Cancelled, _clock);
        parent.Children.Add(Done("1"));
        parent.Children.Add(cancelled);
        parent.ChangeStatus(TaskStatus.InProgress, _clock);

        parent.ChangeStatus(TaskStatus.Done, _clock);

        Assert.Equal(TaskStatus.Done, parent.Status);
    }

    [Fact]
    public void ChangeStatus_Cancelled_WithOpenSubtasks_IsAllowed()
    {
        var parent = NewTask("родитель");
        parent.Children.Add(NewTask("открытая"));

        parent.ChangeStatus(TaskStatus.Cancelled, _clock);

        Assert.Equal(TaskStatus.Cancelled, parent.Status);
    }

    // ---- Привязка к родителю ----

    [Fact]
    public void SetParent_Self_Throws()
    {
        var task = NewTask();

        Assert.Throws<DomainValidationException>(() => task.SetParent(task));
    }

    [Fact]
    public void SetParent_DeletedParent_Throws()
    {
        var parent = NewTask("родитель");
        parent.Delete();

        Assert.Throws<DomainValidationException>(() => NewTask().SetParent(parent));
    }

    [Fact]
    public void SetParent_OpenTaskUnderDoneParent_Throws()
    {
        var parent = Done("родитель");

        Assert.Throws<DomainValidationException>(() => NewTask("открытая").SetParent(parent));
    }

    [Fact]
    public void SetParent_DoneTaskUnderDoneParent_IsAllowed()
    {
        var parent = Done("родитель");
        var child = Done("ребёнок");

        child.SetParent(parent);

        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void SetParent_Null_DetachesFromParent()
    {
        var parent = NewTask("родитель");
        var child = NewTask("ребёнок");
        child.SetParent(parent);

        child.SetParent(null);

        Assert.Null(child.Parent);
        Assert.Null(child.ParentId);
    }

    [Fact]
    public void Reopen_SubtaskOfDoneParent_Throws()
    {
        var parent = NewTask("родитель");
        var child = Done("ребёнок");
        child.SetParent(parent);
        parent.Children.Add(child);
        parent.ChangeStatus(TaskStatus.InProgress, _clock);
        parent.ChangeStatus(TaskStatus.Done, _clock);

        Assert.Throws<DomainValidationException>(child.Reopen);
        Assert.Equal(TaskStatus.Done, child.Status);
    }
}

/// <summary>Сценарии подзадач через сервис и реальную БД.</summary>
public sealed class SubtaskServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose() => _db.Dispose();

    private TaskService NewService(TaskManagerDbContext context) =>
        new(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock);

    private async Task<int> CreateAsync(string title, int? parentId = null)
    {
        await using var context = _db.CreateContext();
        return (await NewService(context).CreateAsync(new CreateTaskRequest(title, ParentId: parentId))).Id;
    }

    private async Task FinishAsync(int id)
    {
        await using var context = _db.CreateContext();
        var service = NewService(context);
        await service.ChangeStatusAsync(id, TaskStatus.InProgress);
        await service.ChangeStatusAsync(id, TaskStatus.Done);
    }

    private async Task<TaskItem> GetAsync(int id)
    {
        await using var context = _db.CreateContext();
        return await NewService(context).GetAsync(id);
    }

    private async Task<List<TaskHistoryEntry>> HistoryAsync(int id)
    {
        await using var context = _db.CreateContext();
        return (await NewService(context).GetHistoryAsync(id)).ToList();
    }

    // ---- Создание и глубина ----

    [Fact]
    public async Task Create_Subtask_IsLinkedToParent()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);

        Assert.Equal(parent, (await GetAsync(child)).ParentId);
        Assert.Equal("ребёнок", Assert.Single((await GetAsync(parent)).Children).Title);
    }

    [Fact]
    public async Task Create_ThreeLevels_IsAllowed_FourthIsRejected()
    {
        var level1 = await CreateAsync("1");
        var level2 = await CreateAsync("2", level1);
        var level3 = await CreateAsync("3", level2);

        await using var context = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => NewService(context).CreateAsync(new CreateTaskRequest("4", ParentId: level3)));

        Assert.Contains("3", ex.Message, StringComparison.Ordinal);
        await using var read = _db.CreateContext();
        Assert.Equal(3, await read.Tasks.CountAsync());
    }

    [Fact]
    public async Task Create_UnderMissingOrDeletedParent_ThrowsNotFound()
    {
        var parent = await CreateAsync("родитель");
        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(parent);
        }

        await using var read = _db.CreateContext();
        var service = NewService(read);
        await Assert.ThrowsAsync<TaskNotFoundException>(() => service.CreateAsync(new CreateTaskRequest("x", ParentId: 999)));
        await Assert.ThrowsAsync<TaskNotFoundException>(() => service.CreateAsync(new CreateTaskRequest("x", ParentId: parent)));
    }

    [Fact]
    public async Task Create_OpenSubtask_UnderDoneParent_IsRejected()
    {
        var parent = await CreateAsync("родитель");
        await FinishAsync(parent);

        await using var context = _db.CreateContext();
        await Assert.ThrowsAsync<DomainValidationException>(
            () => NewService(context).CreateAsync(new CreateTaskRequest("новая", ParentId: parent)));
    }

    // ---- Прогресс и закрытие родителя ----

    [Fact]
    public async Task ParentProgress_FollowsSubtaskStatuses()
    {
        var parent = await CreateAsync("родитель");
        var first = await CreateAsync("1", parent);
        await CreateAsync("2", parent);
        await CreateAsync("3", parent);

        await FinishAsync(first);

        await using var context = _db.CreateContext();
        var progress = await NewService(context).GetProgressAsync(parent);
        Assert.Equal("1/3", progress.ToString());
        Assert.Equal(33, progress.Percent);
    }

    [Fact]
    public async Task Parent_CannotBeDone_UntilAllSubtasksAreDone()
    {
        var parent = await CreateAsync("родитель");
        var first = await CreateAsync("1", parent);
        var second = await CreateAsync("2", parent);
        await using (var context = _db.CreateContext())
        {
            await NewService(context).ChangeStatusAsync(parent, TaskStatus.InProgress);
        }

        await FinishAsync(first);

        await using (var context = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => NewService(context).ChangeStatusAsync(parent, TaskStatus.Done));
            Assert.Contains("1/2", ex.Message, StringComparison.Ordinal);
        }

        Assert.Equal(TaskStatus.InProgress, (await GetAsync(parent)).Status);

        await FinishAsync(second);
        await using (var context = _db.CreateContext())
        {
            await NewService(context).ChangeStatusAsync(parent, TaskStatus.Done);
        }

        Assert.Equal(TaskStatus.Done, (await GetAsync(parent)).Status);
    }

    [Fact]
    public async Task Reopen_Subtask_WhileParentIsDone_IsRejected()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);
        await FinishAsync(child);
        await FinishAsync(parent);

        await using var context = _db.CreateContext();
        await Assert.ThrowsAsync<DomainValidationException>(() => NewService(context).ReopenAsync(child));
    }

    // ---- Перенос ----

    [Fact]
    public async Task SetParent_MovesTask_AndRecordsHistory()
    {
        var oldParent = await CreateAsync("старый");
        var newParent = await CreateAsync("новый");
        var child = await CreateAsync("ребёнок", oldParent);

        await using (var context = _db.CreateContext())
        {
            await NewService(context).SetParentAsync(child, newParent);
        }

        Assert.Equal(newParent, (await GetAsync(child)).ParentId);
        Assert.Empty((await GetAsync(oldParent)).Children);

        var entry = (await HistoryAsync(child)).Last();
        Assert.Equal(TaskService.MoveCommand, entry.Command);
        Assert.Equal("ParentId", entry.FieldName);
        Assert.Equal(oldParent.ToString(CultureInfo.InvariantCulture), entry.OldValue);
        Assert.Equal(newParent.ToString(CultureInfo.InvariantCulture), entry.NewValue);
    }

    [Fact]
    public async Task SetParent_Null_MakesTaskTopLevel()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);

        await using (var context = _db.CreateContext())
        {
            await NewService(context).SetParentAsync(child, null);
        }

        Assert.Null((await GetAsync(child)).ParentId);
    }

    [Fact]
    public async Task SetParent_IntoOwnSubtree_IsRejected()
    {
        var a = await CreateAsync("a");
        var b = await CreateAsync("b", a);
        var c = await CreateAsync("c", b);

        await using var context = _db.CreateContext();
        var service = NewService(context);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.SetParentAsync(a, a));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.SetParentAsync(a, b));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.SetParentAsync(a, c));
    }

    [Fact]
    public async Task SetParent_ConsidersHeightOfMovedSubtree()
    {
        // Дерево A -> B (высота 2) и дерево X -> Y. Перенос A под Y даёт X -> Y -> A -> B: 4 уровня.
        var a = await CreateAsync("A");
        await CreateAsync("B", a);
        var x = await CreateAsync("X");
        var y = await CreateAsync("Y", x);

        await using (var context = _db.CreateContext())
        {
            await Assert.ThrowsAsync<DomainValidationException>(() => NewService(context).SetParentAsync(a, y));
        }

        // Одиночную задачу под Y перенести можно: X -> Y -> Z это 3 уровня.
        var z = await CreateAsync("Z");
        await using (var context = _db.CreateContext())
        {
            await NewService(context).SetParentAsync(z, y);
        }

        Assert.Equal(y, (await GetAsync(z)).ParentId);
    }

    // ---- Удаление и восстановление ----

    [Fact]
    public async Task Delete_Parent_DeletesWholeSubtask_WithHistoryForEach()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);
        var grandchild = await CreateAsync("внук", child);
        var other = await CreateAsync("посторонняя");

        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(parent);
        }

        await using var read = _db.CreateContext();
        Assert.Equal(["посторонняя"], (await read.Tasks.ToListAsync()).Select(t => t.Title));
        Assert.Equal(3, (await read.Tasks.IgnoreQueryFilters().CountAsync(t => t.IsDeleted)));

        foreach (var id in new[] { parent, child, grandchild })
        {
            Assert.Contains(await HistoryAsync(id), h => h.Command == TaskService.DeleteCommand);
        }

        Assert.DoesNotContain(await HistoryAsync(other), h => h.Command == TaskService.DeleteCommand);
    }

    [Fact]
    public async Task Restore_Parent_LeavesSubtasksInTrash()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);
        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(parent);
        }

        await using (var context = _db.CreateContext())
        {
            await NewService(context).RestoreAsync(parent);
        }

        Assert.Empty((await GetAsync(parent)).Children);
        await using var read = _db.CreateContext();
        Assert.True((await read.Tasks.IgnoreQueryFilters().SingleAsync(t => t.Id == child)).IsDeleted);
    }

    [Fact]
    public async Task Restore_Subtask_RequiresRestoredParent()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);
        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(parent);
        }

        await using (var context = _db.CreateContext())
        {
            await Assert.ThrowsAsync<DomainValidationException>(() => NewService(context).RestoreAsync(child));
        }

        await using (var context = _db.CreateContext())
        {
            var service = NewService(context);
            await service.RestoreAsync(parent);
            await service.RestoreAsync(child);
        }

        Assert.Equal("ребёнок", Assert.Single((await GetAsync(parent)).Children).Title);
    }

    [Fact]
    public async Task Restore_OpenSubtask_OfDoneParent_IsRejected()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);
        await using (var context = _db.CreateContext())
        {
            // Удаляем только подзадачу, потом закрываем родителя без неё.
            await NewService(context).DeleteAsync(child);
        }

        await FinishAsync(parent);

        await using var read = _db.CreateContext();
        await Assert.ThrowsAsync<DomainValidationException>(() => NewService(read).RestoreAsync(child));
    }

    [Fact]
    public async Task DeletedSubtask_DoesNotBlockParentCompletion()
    {
        var parent = await CreateAsync("родитель");
        var child = await CreateAsync("ребёнок", parent);
        await using (var context = _db.CreateContext())
        {
            await NewService(context).DeleteAsync(child);
        }

        await FinishAsync(parent);

        Assert.Equal(TaskStatus.Done, (await GetAsync(parent)).Status);
    }
}
