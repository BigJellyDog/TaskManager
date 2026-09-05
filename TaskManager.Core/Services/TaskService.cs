using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Querying;
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
    public const string AddDependencyCommand = "depend";
    public const string RemoveDependencyCommand = "undepend";
    public const string AutoBlockCommand = "auto-block";
    public const string AutoUnblockCommand = "auto-unblock";

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

    /// <summary>Страница задач по фильтру и сортировке.</summary>
    public Task<PagedResult<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default) =>
        _tasks.ListAsync(query, cancellationToken);

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

        // В Todo, InProgress и Done можно перейти только когда все зависимости выполнены.
        // Blocked и Cancelled доступны всегда.
        if (newStatus is TaskStatus.Todo or TaskStatus.InProgress or TaskStatus.Done)
        {
            await EnsureDependenciesDoneAsync(task, newStatus, cancellationToken);
        }

        task.ChangeStatus(newStatus, _clock);

        var entries = BuildEntries(task, before, ChangeStatusCommand);
        await ReconcileAfterStatusChangeAsync(task, before.Status, entries, cancellationToken);
        await CommitAsync(entries, cancellationToken);
        return task;
    }

    /// <summary>Переоткрывает выполненную или отменённую задачу.</summary>
    public async Task<TaskItem> ReopenAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        var before = TaskSnapshot.From(task);

        task.Reopen();

        var entries = BuildEntries(task, before, ReopenCommand);
        await ReconcileAfterStatusChangeAsync(task, before.Status, entries, cancellationToken);
        await CommitAsync(entries, cancellationToken);
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
        var deletedIds = new List<int>();
        foreach (var item in await CollectSubtreeAsync(task, cancellationToken))
        {
            item.Delete();
            deletedIds.Add(item.Id);
            entries.Add(NewEntry(item.Id, DeleteCommand, "IsDeleted", "False", "True"));
        }

        // Удалённая задача больше не блокирует тех, кто от неё зависел.
        await ReconcileAsync(await GetDependentIdsAsync(deletedIds, cancellationToken), entries, cancellationToken);

        await CommitAsync(entries, cancellationToken);
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

        var entries = new List<TaskHistoryEntry> { NewEntry(task.Id, RestoreCommand, "IsDeleted", "True", "False") };

        // Вернувшаяся задача может снова блокировать зависимых и сама может оказаться заблокированной.
        var affected = (await GetDependentIdsAsync([task.Id], cancellationToken)).Append(task.Id).ToList();
        await ReconcileAsync(affected, entries, cancellationToken);

        await CommitAsync(entries, cancellationToken);
    }

    // ---- Зависимости ----

    /// <summary>Добавляет зависимость "<paramref name="taskId"/> нельзя начать, пока <paramref name="dependsOnTaskId"/> не Done".</summary>
    public Task AddDependencyAsync(int taskId, int dependsOnTaskId, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var task = await _tasks.GetByIdAsync(taskId, includeDeleted: false, ct)
                    ?? throw new TaskNotFoundException(taskId);
                var target = await _tasks.GetByIdAsync(dependsOnTaskId, includeDeleted: false, ct)
                    ?? throw new TaskNotFoundException(dependsOnTaskId);

                if (task.Status is TaskStatus.Done or TaskStatus.Cancelled)
                {
                    throw new DomainValidationException("Нельзя добавить зависимость выполненной или отменённой задаче.");
                }

                if (target.Status == TaskStatus.Cancelled)
                {
                    throw new DomainValidationException(
                        "Нельзя зависеть от отменённой задачи: она никогда не будет выполнена.");
                }

                var edges = await _tasks.GetDependencyGraphAsync(ct);

                if (edges.Contains((taskId, dependsOnTaskId)))
                {
                    throw new DomainValidationException("Такая зависимость уже есть.");
                }

                var cycle = DependencyGraph.FindCyclePath(edges, taskId, dependsOnTaskId);
                if (cycle is not null)
                {
                    throw new DomainValidationException(
                        $"Нельзя добавить зависимость: она создаёт цикл ({string.Join(" → ", cycle.Select(i => $"#{i}"))}).");
                }

                await _tasks.AddDependencyAsync(new TaskDependency(taskId, dependsOnTaskId), ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Зависимость уже в БД, теперь можно пересчитать блокировку и записать историю одним сохранением.
                var entries = new List<TaskHistoryEntry>
                {
                    NewEntry(taskId, AddDependencyCommand, "DependsOn", null, Describe(target)),
                };
                await ReconcileAsync([taskId], entries, ct);
                await CommitAsync(entries, ct);
            },
            cancellationToken);

    public Task RemoveDependencyAsync(int taskId, int dependsOnTaskId, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var dependency = await _tasks.GetDependencyAsync(taskId, dependsOnTaskId, ct)
                    ?? throw new DomainValidationException("Такой зависимости нет.");
                var target = await _tasks.GetByIdAsync(dependsOnTaskId, includeDeleted: false, ct);

                _tasks.RemoveDependency(dependency);
                await _unitOfWork.SaveChangesAsync(ct);

                var entries = new List<TaskHistoryEntry>
                {
                    NewEntry(taskId, RemoveDependencyCommand, "DependsOn", target is null ? $"#{dependsOnTaskId}" : Describe(target), null),
                };
                await ReconcileAsync([taskId], entries, ct);
                await CommitAsync(entries, ct);
            },
            cancellationToken);

    /// <summary>Невыполненные зависимости задачи (удалённые задачи не считаются).</summary>
    public async Task<IReadOnlyList<TaskItem>> GetUnmetDependenciesAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var edges = await _tasks.GetDependencyGraphAsync(cancellationToken);
        var ids = edges.Where(e => e.TaskId == taskId).Select(e => e.DependsOnTaskId).ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var dependencies = await _tasks.GetByIdsAsync(ids, includeDeleted: false, cancellationToken);
        return dependencies.Where(d => !d.IsDeleted && d.Status != TaskStatus.Done).OrderBy(d => d.Id).ToList();
    }

    /// <summary>Дерево зависимостей задачи: от чего она зависит, и так далее вглубь.</summary>
    public async Task<DependencyNode> GetDependencyTreeAsync(int id, CancellationToken cancellationToken = default)
    {
        var edges = await _tasks.GetDependencyGraphAsync(cancellationToken);
        var root = await _tasks.GetByIdAsync(id, includeDeleted: false, cancellationToken)
            ?? throw new TaskNotFoundException(id);

        var ids = new HashSet<int> { id };
        var queue = new Queue<int>([id]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in edges.Where(e => e.TaskId == current))
            {
                if (ids.Add(edge.DependsOnTaskId))
                {
                    queue.Enqueue(edge.DependsOnTaskId);
                }
            }
        }

        var tasks = (await _tasks.GetByIdsAsync(ids.ToList(), includeDeleted: false, cancellationToken))
            .ToDictionary(t => t.Id);

        // Граф без циклов (их не пускаем при добавлении), поэтому рекурсия конечна.
        // Одна и та же задача может встретиться в дереве несколько раз: это "ромб", он допустим.
        DependencyNode Build(TaskItem task) => new(
            task,
            edges.Where(e => e.TaskId == task.Id && tasks.ContainsKey(e.DependsOnTaskId))
                .Select(e => Build(tasks[e.DependsOnTaskId]))
                .OrderBy(n => n.Task.Id)
                .ToList());

        return Build(root);
    }

    public Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(int id, CancellationToken cancellationToken = default) =>
        _tasks.GetHistoryAsync(id, cancellationToken);

    private Task SaveWithHistoryAsync(TaskItem task, TaskSnapshot before, string command, CancellationToken cancellationToken) =>
        CommitAsync(BuildEntries(task, before, command), cancellationToken);

    /// <summary>Записи истории по тому, что изменилось в задаче с момента слепка.</summary>
    private List<TaskHistoryEntry> BuildEntries(TaskItem task, TaskSnapshot before, string command) =>
        TaskSnapshot.Diff(before, TaskSnapshot.From(task))
            .Select(c => NewEntry(task.Id, command, c.Field, c.OldValue, c.NewValue))
            .ToList();

    /// <summary>Сохраняет все накопленные изменения вместе с записями истории одним сохранением.</summary>
    private async Task CommitAsync(List<TaskHistoryEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return;
        }

        await _tasks.AddHistoryAsync(entries, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string Describe(TaskItem task) => $"#{task.Id} {task.Title}";

    private async Task EnsureDependenciesDoneAsync(TaskItem task, TaskStatus target, CancellationToken cancellationToken)
    {
        var unmet = await GetUnmetDependenciesAsync(task.Id, cancellationToken);

        if (unmet.Count > 0)
        {
            throw new DomainValidationException(
                $"Нельзя перевести задачу в {target}: не выполнены зависимости: {string.Join(", ", unmet.Select(Describe))}.");
        }
    }

    private async Task<List<int>> GetDependentIdsAsync(List<int> taskIds, CancellationToken cancellationToken)
    {
        var edges = await _tasks.GetDependencyGraphAsync(cancellationToken);
        return edges.Where(e => taskIds.Contains(e.DependsOnTaskId)).Select(e => e.TaskId).Distinct().ToList();
    }

    /// <summary>
    /// Если задача перешла в Done или вышла из Done, зависимые от неё задачи могут разблокироваться или заблокироваться.
    /// Переоткрытая отменённая задача сама может оказаться заблокированной.
    /// </summary>
    private async Task ReconcileAfterStatusChangeAsync(
        TaskItem task,
        TaskStatus previousStatus,
        List<TaskHistoryEntry> entries,
        CancellationToken cancellationToken)
    {
        var affected = new List<int> { task.Id };

        if ((previousStatus == TaskStatus.Done) != (task.Status == TaskStatus.Done))
        {
            affected.AddRange(await GetDependentIdsAsync([task.Id], cancellationToken));
        }

        await ReconcileAsync(affected, entries, cancellationToken);
    }

    /// <summary>
    /// Приводит статус к зависимостям: Todo с невыполненной зависимостью становится Blocked,
    /// Blocked без невыполненных зависимостей возвращается в Todo. InProgress не трогаем: работа уже идёт.
    /// Блокировку, поставленную вручную, пересчёт тоже снимет, если у задачи нет невыполненных зависимостей.
    /// </summary>
    private async Task ReconcileAsync(List<int> taskIds, List<TaskHistoryEntry> entries, CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return;
        }

        var edges = await _tasks.GetDependencyGraphAsync(cancellationToken);
        var targets = await _tasks.GetByIdsAsync(taskIds, includeDeleted: false, cancellationToken);
        var dependencyIds = edges.Where(e => taskIds.Contains(e.TaskId)).Select(e => e.DependsOnTaskId).Distinct().ToList();
        var dependencies = (await _tasks.GetByIdsAsync(dependencyIds, includeDeleted: false, cancellationToken))
            .ToDictionary(t => t.Id);

        foreach (var target in targets.Where(t => !t.IsDeleted))
        {
            // Удалённые задачи в dependencies могли попасть из БД до сохранения, поэтому проверяем флаг в памяти.
            var hasUnmet = edges.Any(e =>
                e.TaskId == target.Id
                && dependencies.TryGetValue(e.DependsOnTaskId, out var dependency)
                && !dependency.IsDeleted
                && dependency.Status != TaskStatus.Done);

            if (target.Status == TaskStatus.Todo && hasUnmet)
            {
                target.ChangeStatus(TaskStatus.Blocked, _clock);
                entries.Add(NewEntry(target.Id, AutoBlockCommand, "Status", nameof(TaskStatus.Todo), nameof(TaskStatus.Blocked)));
            }
            else if (target.Status == TaskStatus.Blocked && !hasUnmet)
            {
                target.ChangeStatus(TaskStatus.Todo, _clock);
                entries.Add(NewEntry(target.Id, AutoUnblockCommand, "Status", nameof(TaskStatus.Blocked), nameof(TaskStatus.Todo)));
            }
        }
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
