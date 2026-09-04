using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Services;

/// <summary>
/// Сценарии работы с задачей. Каждая операция сохраняет и саму задачу, и запись в истории одним
/// <see cref="IUnitOfWork.SaveChangesAsync"/>, то есть в одной транзакции.
/// </summary>
public class TaskService
{
    // Названия операций для поля "какая команда" в истории.
    public const string CreateCommand = "create";
    public const string EditCommand = "edit";
    public const string ChangeStatusCommand = "status";
    public const string ReopenCommand = "reopen";
    public const string DeleteCommand = "delete";
    public const string RestoreCommand = "restore";
    public const string MoveCommand = "move";

    private readonly ITaskRepository _tasks;
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public TaskService(ITaskRepository tasks, ITagRepository tags, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _tasks = tasks;
        _tags = tags;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<TaskItem> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await _tasks.GetWithDetailsAsync(id, cancellationToken) ?? throw new TaskNotFoundException(id);

    public Task<TaskItem> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Две записи в одной транзакции: Id задачи появляется только после первого сохранения,
        // а запись истории без Id задачи создать нельзя.
        return _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var task = TaskItem.Create(request.Title, request.Priority, _clock, request.Description, request.Deadline);

                if (request.ParentId is { } parentId)
                {
                    var parent = await _tasks.GetByIdAsync(parentId, includeDeleted: false, ct)
                        ?? throw new TaskNotFoundException(parentId);

                    // Новая подзадача: на уровень глубже родителя.
                    EnsureDepthAllowed(await GetDepthAsync(parent, ct) + 1);
                    task.SetParent(parent);
                }

                if (request.Recurrence is not null)
                {
                    task.SetRecurrence(request.Recurrence);
                }

                await SyncTagsAsync(task, request.Tags ?? [], ct);
                await _tasks.AddAsync(task, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                await _tasks.AddHistoryAsync([NewEntry(task.Id, CreateCommand, "Created", null, task.Title)], ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return task;
            },
            cancellationToken);
    }

    public async Task<TaskItem> EditAsync(EditTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var task = await GetAsync(request.Id, cancellationToken);
        var before = TaskSnapshot.From(task);

        task.SetTitle(request.Title);
        task.SetDescription(request.Description);
        task.SetPriority(request.Priority);
        task.SetDeadline(request.Deadline);
        task.SetRecurrence(request.Recurrence);
        await SyncTagsAsync(task, request.Tags, cancellationToken);

        await SaveWithHistoryAsync(task, before, EditCommand, cancellationToken);
        return task;
    }

    public async Task<TaskItem> ChangeStatusAsync(int id, TaskStatus newStatus, CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        var before = TaskSnapshot.From(task);

        task.ChangeStatus(newStatus, _clock);

        await SaveWithHistoryAsync(task, before, ChangeStatusCommand, cancellationToken);
        return task;
    }

    /// <summary>Переоткрывает выполненную или отменённую задачу.</summary>
    public async Task<TaskItem> ReopenAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        var before = TaskSnapshot.From(task);

        task.Reopen();

        await SaveWithHistoryAsync(task, before, ReopenCommand, cancellationToken);
        return task;
    }

    /// <summary>Прогресс подзадач задачи, например 3/5.</summary>
    public async Task<TaskProgress> GetProgressAsync(int id, CancellationToken cancellationToken = default) =>
        (await GetAsync(id, cancellationToken)).GetProgress();

    /// <summary>Делает задачу подзадачей другой (или задачей верхнего уровня при <paramref name="newParentId"/> = null).</summary>
    public async Task<TaskItem> SetParentAsync(int id, int? newParentId, CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        var before = TaskSnapshot.From(task);

        if (newParentId is { } parentId)
        {
            var parent = await _tasks.GetByIdAsync(parentId, includeDeleted: false, cancellationToken)
                ?? throw new TaskNotFoundException(parentId);

            if (await IsDescendantOrSelfAsync(parent, task.Id, cancellationToken))
            {
                throw new DomainValidationException("Нельзя сделать задачу подзадачей самой себя или своей подзадачи.");
            }

            // Итоговая глубина: уровень родителя + высота поддерева переносимой задачи.
            var depth = await GetDepthAsync(parent, cancellationToken) + await GetSubtreeHeightAsync(task.Id, cancellationToken);
            EnsureDepthAllowed(depth);

            task.SetParent(parent);
        }
        else
        {
            task.SetParent(null);
        }

        await SaveWithHistoryAsync(task, before, MoveCommand, cancellationToken);
        return task;
    }

    /// <summary>Удаляет задачу вместе со всеми её подзадачами, чтобы подзадачи не остались без родителя.</summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(id, includeDeleted: false, cancellationToken)
            ?? throw new TaskNotFoundException(id);

        var entries = new List<TaskHistoryEntry>();
        foreach (var item in await CollectSubtreeAsync(task, cancellationToken))
        {
            item.Delete();
            entries.Add(NewEntry(item.Id, DeleteCommand, "IsDeleted", "False", "True"));
        }

        await _tasks.AddHistoryAsync(entries, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Восстанавливает одну задачу. Её подзадачи остаются в корзине и восстанавливаются по отдельности.</summary>
    public async Task RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(id, includeDeleted: true, cancellationToken)
            ?? throw new TaskNotFoundException(id);

        if (task.ParentId is { } parentId)
        {
            var parent = await _tasks.GetByIdAsync(parentId, includeDeleted: true, cancellationToken);

            if (parent is { IsDeleted: true })
            {
                throw new DomainValidationException(
                    "Нельзя восстановить подзадачу, пока удалена родительская задача. Сначала восстановите родителя.");
            }

            if (parent is { Status: TaskStatus.Done } && task.Status is not (TaskStatus.Done or TaskStatus.Cancelled))
            {
                throw new DomainValidationException(
                    "Нельзя восстановить невыполненную подзадачу выполненной задачи. Сначала переоткройте родителя.");
            }
        }

        task.Restore();

        await _tasks.AddHistoryAsync([NewEntry(task.Id, RestoreCommand, "IsDeleted", "True", "False")], cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(int id, CancellationToken cancellationToken = default) =>
        _tasks.GetHistoryAsync(id, cancellationToken);

    private async Task SaveWithHistoryAsync(TaskItem task, TaskSnapshot before, string command, CancellationToken cancellationToken)
    {
        var changes = TaskSnapshot.Diff(before, TaskSnapshot.From(task));

        if (changes.Count == 0)
        {
            return;
        }

        var entries = changes.Select(c => NewEntry(task.Id, command, c.Field, c.OldValue, c.NewValue));
        await _tasks.AddHistoryAsync(entries, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Приводит набор тегов задачи к списку имён: лишние снимает, недостающие берёт из БД или создаёт.</summary>
    private async Task SyncTagsAsync(TaskItem task, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var wanted = new Dictionary<string, string>();
        foreach (var name in names)
        {
            wanted.TryAdd(Tag.Normalize(name), name);
        }

        foreach (var tag in task.Tags.Where(t => !wanted.ContainsKey(t.NormalizedName)).ToList())
        {
            task.RemoveTag(tag);
        }

        var present = task.Tags.Select(t => t.NormalizedName).ToHashSet();
        foreach (var (normalized, name) in wanted)
        {
            if (present.Contains(normalized))
            {
                continue;
            }

            var tag = await _tags.GetByNameAsync(name, cancellationToken);
            if (tag is null)
            {
                tag = new Tag(name);
                await _tags.AddAsync(tag, cancellationToken);
            }

            task.AddTag(tag);
        }
    }

    private static void EnsureDepthAllowed(int depth)
    {
        if (depth > TaskItem.MaxDepth)
        {
            throw new DomainValidationException(
                $"Вложенность подзадач не может быть больше {TaskItem.MaxDepth} уровней.");
        }
    }

    /// <summary>Уровень задачи в дереве: у задачи верхнего уровня 1. Идём вверх по родителям.</summary>
    private async Task<int> GetDepthAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var depth = 1;
        TaskItem? current = task;

        // Ограничение по числу шагов защищает от бесконечного цикла, если в БД окажется битое дерево.
        while (current?.ParentId is { } parentId && depth <= 100)
        {
            current = await _tasks.GetByIdAsync(parentId, includeDeleted: true, cancellationToken);
            if (current is not null)
            {
                depth++;
            }
        }

        return depth;
    }

    /// <summary>Высота поддерева: 1 для задачи без подзадач, 2 если есть дети, и так далее.</summary>
    private async Task<int> GetSubtreeHeightAsync(int taskId, CancellationToken cancellationToken)
    {
        var height = 1;
        var level = new List<int> { taskId };

        while (height <= TaskItem.MaxDepth + 1)
        {
            var next = new List<int>();
            foreach (var id in level)
            {
                next.AddRange((await _tasks.GetChildrenAsync(id, cancellationToken)).Select(c => c.Id));
            }

            if (next.Count == 0)
            {
                break;
            }

            height++;
            level = next;
        }

        return height;
    }

    /// <summary>Совпадает ли <paramref name="candidate"/> с задачей <paramref name="taskId"/> или лежит в её поддереве (идём вверх от кандидата).</summary>
    private async Task<bool> IsDescendantOrSelfAsync(TaskItem candidate, int taskId, CancellationToken cancellationToken)
    {
        TaskItem? current = candidate;

        for (var steps = 0; steps <= 100 && current is not null; steps++)
        {
            if (current.Id == taskId)
            {
                return true;
            }

            current = current.ParentId is { } parentId
                ? await _tasks.GetByIdAsync(parentId, includeDeleted: true, cancellationToken)
                : null;
        }

        return false;
    }

    /// <summary>Задача и все её неудалённые потомки.</summary>
    private async Task<List<TaskItem>> CollectSubtreeAsync(TaskItem root, CancellationToken cancellationToken)
    {
        var result = new List<TaskItem> { root };

        for (var i = 0; i < result.Count; i++)
        {
            result.AddRange(await _tasks.GetChildrenAsync(result[i].Id, cancellationToken));
        }

        return result;
    }

    private TaskHistoryEntry NewEntry(int taskId, string command, string field, string? oldValue, string? newValue) =>
        new(taskId, _clock.GetUtcNow(), command, field, oldValue, newValue);
}
