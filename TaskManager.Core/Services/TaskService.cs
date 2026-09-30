using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Querying;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Services;

/// <summary>
/// Scenarios of working with a task. Each operation saves both the task itself and the history record with one
/// <see cref="IUnitOfWork.SaveChangesAsync"/>, that is in one transaction.
/// </summary>
public class TaskService
{
    // The names of the operations for the "which command" field in the history.
    public const string CreateCommand = "create";
    public const string EditCommand = "edit";
    public const string ChangeStatusCommand = "status";
    public const string ReopenCommand = "reopen";
    public const string DeleteCommand = "delete";
    public const string RestoreCommand = "restore";
    public const string MoveCommand = "move";
    public const string RecurrenceCommand = "recurrence";

    /// <summary>How many missed copies of one series are created in one catch-up call. The rest will be created by the next call.</summary>
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

    /// <summary>A page of tasks by filter and sorting.</summary>
    public Task<PagedResult<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default) =>
        _tasks.ListAsync(query, cancellationToken);

    /// <summary>
    /// Search by the user's filters: the filters are combined with AND, "overdue", "for today" and "for the week"
    /// are counted from the current time of the service.
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

    /// <summary>How many tasks match the filter. Cheaper than <see cref="SearchAsync"/> when only the number is needed.</summary>
    public Task<int> CountAsync(TaskFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return _tasks.CountAsync(filter.ToSpecification(_clock), cancellationToken);
    }

    public Task<TaskItem> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Two records in one transaction: the task Id appears only after the first save,
        // and a history record cannot be created without the task Id.
        return _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var task = TaskItem.Create(request.Title, request.Priority, _clock, request.Description, request.Deadline);

                if (request.ParentId is { } parentId)
                {
                    var parent = await _tasks.GetByIdAsync(parentId, includeDeleted: false, ct)
                        ?? throw new TaskNotFoundException(parentId);

                    // A new subtask: one level deeper than the parent.
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

        // The deadline and the recurrence rule are linked (by calendar a deadline is needed), so the order matters:
        // we remove the rule before removing the deadline, and we set the new rule after setting the deadline.
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

    /// <param name="expectedRowVersion">The task version the user saw. If the task was changed, the operation is rejected.</param>
    public async Task<TaskItem> ChangeStatusAsync(
        int id,
        TaskStatus newStatus,
        long? expectedRowVersion = null,
        CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        EnsureNotStale(task, expectedRowVersion);
        var before = TaskSnapshot.From(task);

        // A task can move to Todo, InProgress and Done only when all dependencies are done.
        // Blocked and Cancelled are always available.
        if (newStatus is TaskStatus.Todo or TaskStatus.InProgress or TaskStatus.Done)
        {
            await EnsureDependenciesDoneAsync(task, newStatus, cancellationToken);
        }

        task.ChangeStatus(newStatus, _clock);

        var entries = BuildEntries(task, before, ChangeStatusCommand);
        await ReconcileAfterStatusChangeAsync(task, before.Status, entries, cancellationToken);

        // Completing a recurring task creates the next copy. Writing it to the history needs an Id,
        // that is several saves in a row, so everything runs in one transaction.
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

    /// <summary>Reopens a completed or cancelled task.</summary>
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
    /// All subtasks of a task as a flat list in tree traversal order (a subtask first, then its subtasks),
    /// with the nesting level and the progress of each node.
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
        // The depth is limited by the nesting rule; a margin of one level protects against infinite recursion with corrupt data.
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
    /// The user acts on what was read earlier. If the task was changed in the meantime, other people's edits must not be
    /// overwritten silently (optimistic locking).
    /// </summary>
    private static void EnsureNotStale(TaskItem task, long? expectedRowVersion)
    {
        if (expectedRowVersion is { } expected && expected != task.RowVersion)
        {
            throw new ConcurrencyConflictException();
        }
    }

    /// <summary>The subtask progress of a task, for example 3/5.</summary>
    public async Task<TaskProgress> GetProgressAsync(int id, CancellationToken cancellationToken = default) =>
        (await GetAsync(id, cancellationToken)).GetProgress();

    /// <summary>Makes a task a subtask of another (or a top-level task when <paramref name="newParentId"/> = null).</summary>
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

            // The resulting depth: the level of the parent plus the height of the subtree of the moved task.
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

    /// <summary>Deletes a task together with all its subtasks, so that subtasks do not stay without a parent.</summary>
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

        // A deleted task no longer blocks those who depended on it.
        await ReconcileAsync(await GetDependentIdsAsync(deletedIds, cancellationToken), entries, cancellationToken);

        await CommitAsync(entries, cancellationToken);
    }

    /// <summary>Restores one task. Its subtasks stay in the trash and are restored separately.</summary>
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

        // A returned task may block its dependents again, and may itself end up blocked.
        var affected = (await GetDependentIdsAsync([task.Id], cancellationToken)).Append(task.Id).ToList();
        await ReconcileAsync(affected, entries, cancellationToken);

        await CommitAsync(entries, cancellationToken);
    }

    // ---- Dependencies ----

    /// <summary>Adds the dependency "<paramref name="taskId"/> cannot be started until <paramref name="dependsOnTaskId"/> is Done".</summary>
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

                // The dependency is already in the database, now the blocking can be recalculated and the history written with one save.
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

    /// <summary>The unfinished dependencies of a task (deleted tasks do not count).</summary>
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

    /// <summary>The history entries for what changed in the task since the snapshot.</summary>
    private List<TaskHistoryEntry> BuildEntries(TaskItem task, TaskSnapshot before, string command) =>
        TaskSnapshot.Diff(before, TaskSnapshot.From(task))
            .Select(c => NewEntry(task.Id, command, c.Field, c.OldValue, c.NewValue))
            .ToList();

    /// <summary>Saves all accumulated changes together with the history entries in one save.</summary>
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
    /// If a task became Done or left Done, the tasks that depend on it may become unblocked or blocked.
    /// A reopened cancelled task may itself end up blocked.
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
    /// Brings the status in line with the dependencies: Todo with an unfinished dependency becomes Blocked,
    /// Blocked without unfinished dependencies returns to Todo. InProgress is not touched: the work is already going on.
    /// A block set manually is also lifted by the recalculation if the task has no unfinished dependencies.
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
            // Deleted tasks in dependencies could have come from the database before saving, so we check the flag in memory.
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
    /// Calendar catch-up: for every task recurring "by calendar" whose next occurrence is due,
    /// creates a copy. If several periods were missed, it creates a chain until the nearest due date is in the future
    /// (but no more than <see cref="MaxCatchUpOccurrences"/> per series per call). The missed copies get deadlines in the past.
    /// Series of cancelled and deleted tasks do not continue. Safe to call again: what is already created is not duplicated.
    /// </summary>
    /// <returns>How many copies were created (subtasks are not counted).</returns>
    public async Task<int> GenerateDueOccurrencesAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var zone = _clock.LocalTimeZone;
        var total = 0;

        foreach (var source in await _tasks.GetPendingCalendarRecurrencesAsync(cancellationToken))
        {
            // Each series in its own transaction: a failure of one does not roll back those already processed.
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

    /// <summary>Brings the set of tags of a task to a list of names: removes the extra ones, takes the missing ones from the database or creates them.</summary>
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
    /// The rule anchor is taken from the deadline (the local day and time of the first occurrence). If the deadline did not change
    /// and the rule already has an anchor, it is kept: otherwise after a shift to 28 February we would forget about the 31st.
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
    /// Creates the next copy of a recurring task together with its subtasks. Saves everything to get the Ids,
    /// and appends to <paramref name="entries"/> the history records about the created tasks.
    /// </summary>
    private async Task<TaskItem?> SpawnSuccessorAsync(TaskItem source, List<TaskHistoryEntry> entries, CancellationToken cancellationToken)
    {
        var rule = source.Recurrence!;
        var zone = _clock.LocalTimeZone;

        // By calendar the next due date is counted from the previous due date, after completion: from the moment of completion.
        // Without a deadline (possible only "after completion") the copy has no deadline either.
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

    /// <summary>Copies the subtasks (of all levels) into the new copy: status Todo, the deadline is shifted by the same amount.</summary>
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

    /// <summary>The level of a task in the tree: a top-level task has 1. We go up through the parents.</summary>
    private async Task<int> GetDepthAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var depth = 1;
        TaskItem? current = task;

        // A limit on the number of steps protects against an infinite loop if a corrupt tree ends up in the database.
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

    /// <summary>The height of a subtree: 1 for a task without subtasks, 2 if there are children, and so on.</summary>
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

    /// <summary>Whether <paramref name="candidate"/> is the task <paramref name="taskId"/> or lies in its subtree (we go up from the candidate).</summary>
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

    /// <summary>A task and all its non-deleted descendants.</summary>
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
