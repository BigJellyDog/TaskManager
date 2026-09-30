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
    public const string RecurrenceCommand = "recurrence";

    /// <summary>Сколько пропущенных копий одной серии создаётся за один вызов догона. Остаток создаст следующий вызов.</summary>
    public const int MaxCatchUpOccurrences = 50;
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

    /// <summary>
    /// Поиск по фильтрам пользователя: фильтры комбинируются через И, "просроченные", "на сегодня" и "на неделю"
    /// считаются от текущего времени сервиса.
    /// </summary>
    public Task<PagedResult<TaskItem>> SearchAsync(
        TaskFilter filter,
        IReadOnlyList<TaskSort>? sort = null,
        int page = 1,
        int pageSize = TaskQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return _tasks.ListAsync(new TaskQuery(filter.ToSpecification(_clock), sort, page, pageSize), cancellationToken);
    }

    /// <summary>Сколько задач подходит под фильтр. Дешевле <see cref="SearchAsync"/>, когда нужно только число.</summary>
    public Task<int> CountAsync(TaskFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return _tasks.CountAsync(filter.ToSpecification(_clock), cancellationToken);
    }

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
                    task.SetRecurrence(PrepareRule(request.Recurrence, request.Deadline, deadlineChanged: true));
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

        EnsureNotStale(task, request.ExpectedRowVersion);

        var before = TaskSnapshot.From(task);

        task.SetTitle(request.Title);
        task.SetDescription(request.Description);
        task.SetPriority(request.Priority);

        // Дедлайн и правило повторения связаны (по календарю нужен дедлайн), поэтому порядок важен:
        // правило снимаем до удаления дедлайна, а новое правило ставим после установки дедлайна.
        var rule = PrepareRule(request.Recurrence, request.Deadline, deadlineChanged: request.Deadline != task.Deadline);
        if (rule is null)
        {
            task.SetRecurrence(null);
            task.SetDeadline(request.Deadline);
        }
        else
        {
            task.SetDeadline(request.Deadline);
            task.SetRecurrence(rule);
        }

        await SyncTagsAsync(task, request.Tags, cancellationToken);

        await SaveWithHistoryAsync(task, before, EditCommand, cancellationToken);
        return task;
    }

    /// <param name="expectedRowVersion">Версия задачи, которую видел пользователь. Если задачу изменили, операция отклоняется.</param>
    public async Task<TaskItem> ChangeStatusAsync(
        int id,
        TaskStatus newStatus,
        long? expectedRowVersion = null,
        CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        EnsureNotStale(task, expectedRowVersion);
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

        // Завершение повторяющейся задачи создаёт следующую копию. Для её записи в историю нужен Id,
        // то есть несколько сохранений подряд, поэтому всё выполняется одной транзакцией.
        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var justCompleted = before.Status != TaskStatus.Done && task.Status == TaskStatus.Done;
                if (justCompleted && task.Recurrence is not null && !task.SuccessorCreated)
                {
                    await SpawnSuccessorAsync(task, entries, ct);
                }

                await CommitAsync(entries, ct);
            },
            cancellationToken);

        return task;
    }

    /// <summary>Переоткрывает выполненную или отменённую задачу.</summary>
    public async Task<TaskItem> ReopenAsync(int id, long? expectedRowVersion = null, CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        EnsureNotStale(task, expectedRowVersion);
        var before = TaskSnapshot.From(task);

        task.Reopen();

        var entries = BuildEntries(task, before, ReopenCommand);
        await ReconcileAfterStatusChangeAsync(task, before.Status, entries, cancellationToken);
        await CommitAsync(entries, cancellationToken);
        return task;
    }

    /// <summary>
    /// Все подзадачи задачи плоским списком в порядке обхода дерева (сначала подзадача, потом её подзадачи),
    /// с уровнем вложенности и прогрессом каждого узла.
    /// </summary>
    public async Task<IReadOnlyList<SubtaskEntry>> GetSubtaskTreeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entries = new List<SubtaskEntry>();
        await AddSubtaskLevelAsync(await _tasks.GetChildrenAsync(id, cancellationToken), 1, entries, cancellationToken);
        return entries;
    }

    private async Task AddSubtaskLevelAsync(
        IReadOnlyList<TaskItem> siblings,
        int level,
        List<SubtaskEntry> entries,
        CancellationToken cancellationToken)
    {
        // Глубина ограничена правилом вложенности; запас в один уровень защищает от бесконечной рекурсии при битых данных.
        if (level > TaskItem.MaxDepth + 1)
        {
            return;
        }

        foreach (var node in siblings)
        {
            var children = await _tasks.GetChildrenAsync(node.Id, cancellationToken);
            var counted = children.Where(c => c.Status != TaskStatus.Cancelled).ToList();

            entries.Add(new SubtaskEntry(node, level, new TaskProgress(counted.Count(c => c.Status == TaskStatus.Done), counted.Count)));
            await AddSubtaskLevelAsync(children, level + 1, entries, cancellationToken);
        }
    }

    /// <summary>
    /// Пользователь действует над тем, что прочитал раньше. Если задачу успели изменить, молча перезаписывать
    /// чужие правки нельзя (оптимистичная блокировка).
    /// </summary>
    private static void EnsureNotStale(TaskItem task, long? expectedRowVersion)
    {
        if (expectedRowVersion is { } expected && expected != task.RowVersion)
        {
            throw new ConcurrencyConflictException();
        }
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
    public async Task DeleteAsync(int id, long? expectedRowVersion = null, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(id, includeDeleted: false, cancellationToken)
            ?? throw new TaskNotFoundException(id);
        EnsureNotStale(task, expectedRowVersion);

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

    /// <summary>
    /// Догон по календарю: для каждой повторяющейся "по календарю" задачи, у которой наступил срок следующего вхождения,
    /// создаёт копию. Если пропущено несколько периодов, создаёт цепочку, пока ближайший срок не окажется в будущем
    /// (но не больше <see cref="MaxCatchUpOccurrences"/> на серию за вызов). Пропущенные копии получают дедлайны в прошлом.
    /// Серии отменённых и удалённых задач не продолжаются. Безопасно вызывать повторно: уже созданное не дублируется.
    /// </summary>
    /// <returns>Сколько копий создано (подзадачи не считаются).</returns>
    public async Task<int> GenerateDueOccurrencesAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var zone = _clock.LocalTimeZone;
        var total = 0;

        foreach (var source in await _tasks.GetPendingCalendarRecurrencesAsync(cancellationToken))
        {
            // Каждая серия в своей транзакции: сбой одной не откатывает уже обработанные.
            total += await _unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    var entries = new List<TaskHistoryEntry>();
                    var created = 0;
                    var current = source;

                    while (created < MaxCatchUpOccurrences
                        && current.Deadline is { } deadline
                        && current.Recurrence is { Mode: RecurrenceMode.ByCalendar } rule
                        && RecurrenceCalculator.GetNext(rule, deadline, zone) <= now)
                    {
                        var next = await SpawnSuccessorAsync(current, entries, ct);
                        if (next is null)
                        {
                            break;
                        }

                        current = next;
                        created++;
                    }

                    await CommitAsync(entries, ct);
                    return created;
                },
                cancellationToken);
        }

        return total;
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

    /// <summary>
    /// Якорь правила берётся из дедлайна (локальные день и время первого срабатывания). Если дедлайн не менялся
    /// и у правила уже есть якорь, он сохраняется: иначе после сдвига на 28 февраля мы забыли бы про 31-е.
    /// </summary>
    private RecurrenceRule? PrepareRule(RecurrenceRule? rule, DateTimeOffset? deadline, bool deadlineChanged)
    {
        if (rule is null || deadline is not { } d)
        {
            return rule;
        }

        if (rule.Anchor is not null && !deadlineChanged)
        {
            return rule;
        }

        return rule.WithAnchor(TimeZoneInfo.ConvertTime(d, _clock.LocalTimeZone).DateTime);
    }

    /// <summary>
    /// Создаёт следующую копию повторяющейся задачи вместе с подзадачами. Сохраняет всё, чтобы получить Id,
    /// и дописывает в <paramref name="entries"/> записи истории о созданных задачах.
    /// </summary>
    private async Task<TaskItem?> SpawnSuccessorAsync(TaskItem source, List<TaskHistoryEntry> entries, CancellationToken cancellationToken)
    {
        var rule = source.Recurrence!;
        var zone = _clock.LocalTimeZone;

        // По календарю следующий срок считается от прошлого срока, после выполнения: от момента выполнения.
        // Без дедлайна (возможно только "после выполнения") у копии тоже нет дедлайна.
        DateTimeOffset? nextDeadline = null;
        if (rule.Mode == RecurrenceMode.ByCalendar)
        {
            if (source.Deadline is not { } previous)
            {
                return null;
            }

            nextDeadline = RecurrenceCalculator.GetNext(rule, previous, zone);
        }
        else if (source.Deadline is not null)
        {
            nextDeadline = RecurrenceCalculator.GetNext(rule, source.CompletedAt ?? _clock.GetUtcNow(), zone);
        }

        var anchor = rule.Anchor ?? (source.Deadline is { } d ? TimeZoneInfo.ConvertTime(d, zone).DateTime : (DateTime?)null);
        var nextRule = anchor is { } a
            ? rule.WithAnchor(a)
            : new RecurrenceRule(rule.Frequency, rule.Interval, rule.Mode, rule.WeekDays);

        var shift = nextDeadline is { } next && source.Deadline is { } old ? next - old : (TimeSpan?)null;

        var copy = TaskItem.CreateSuccessor(source, nextDeadline, nextRule, _clock);
        if (source.Parent is { IsDeleted: false, Status: not TaskStatus.Done } parent)
        {
            copy.SetParent(parent);
        }

        var created = new List<TaskItem> { copy };
        await _tasks.AddAsync(copy, cancellationToken);
        await CopySubtasksAsync(source, copy, shift, created, cancellationToken);

        source.MarkSuccessorCreated();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var item in created)
        {
            entries.Add(NewEntry(item.Id, RecurrenceCommand, "Created", null, item.Title));
        }

        entries.Add(NewEntry(source.Id, RecurrenceCommand, "NextOccurrence", null, Describe(copy)));
        return copy;
    }

    /// <summary>Копирует подзадачи (всех уровней) в новую копию: статус Todo, дедлайн сдвигается на ту же величину.</summary>
    private async Task CopySubtasksAsync(
        TaskItem sourceParent,
        TaskItem copyParent,
        TimeSpan? shift,
        List<TaskItem> created,
        CancellationToken cancellationToken)
    {
        foreach (var child in await _tasks.GetChildrenAsync(sourceParent.Id, cancellationToken))
        {
            DateTimeOffset? deadline = child.Deadline is { } d && shift is { } s ? d + s : null;

            var childCopy = TaskItem.CreateSuccessor(child, deadline, null, _clock);
            childCopy.SetParent(copyParent);
            await _tasks.AddAsync(childCopy, cancellationToken);
            created.Add(childCopy);

            await CopySubtasksAsync(child, childCopy, shift, created, cancellationToken);
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
