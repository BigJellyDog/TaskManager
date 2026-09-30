namespace TaskManager.Core.Domain;

/// <summary>
/// The task. The main entity of the domain. The state is changed only by the methods below:
/// each method checks the business rules itself and throws <see cref="DomainValidationException"/>
/// or <see cref="InvalidStatusTransitionException"/>.
/// </summary>
public class TaskItem
{
    public const int MaxTitleLength = 200;

    /// <summary>The maximum nesting: a top-level task is level 1.</summary>
    public const int MaxDepth = 3;

    private TaskItem(string title, Priority priority, DateTimeOffset createdAt)
    {
        Title = title;
        Priority = priority;
        CreatedAt = createdAt;
    }

    // Needed by EF Core.
    private TaskItem()
    {
        Title = string.Empty;
    }

    public int Id { get; private set; }

    /// <summary>
    /// The row version for optimistic locking. Increased by the infrastructure on every save.
    /// If the task was changed between reading and saving, the versions will not match and the save will be rejected.
    /// </summary>
    public long RowVersion { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// The title and description in upper case, search works on them. SQLite cannot change the case of Cyrillic
    /// (UPPER and LIKE work only with Latin), so we normalise in C# and store it next to the data.
    /// </summary>
    public string SearchText { get; private set; } = string.Empty;

    public Priority Priority { get; private set; }

    public TaskStatus Status { get; private set; } = TaskStatus.Todo;

    public DateTimeOffset? Deadline { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the task became Done. Needed for statistics (whether on time, how long it took).</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Soft delete: the record stays in the database but is hidden by a filter.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>The parent task (null for top-level tasks).</summary>
    public int? ParentId { get; private set; }

    public TaskItem? Parent { get; private set; }

    public ICollection<TaskItem> Children { get; } = new List<TaskItem>();

    public ICollection<Tag> Tags { get; } = new List<Tag>();

    /// <summary>Which tasks this one depends on.</summary>
    public ICollection<TaskDependency> Dependencies { get; } = new List<TaskDependency>();

    /// <summary>Which tasks depend on this one.</summary>
    public ICollection<TaskDependency> Dependents { get; } = new List<TaskDependency>();

    /// <summary>The recurrence rule (null if the task is not recurring).</summary>
    public RecurrenceRule? Recurrence { get; private set; }

    /// <summary>
    /// The next copy of a recurring task has already been created (on completion or by schedule).
    /// Needed so as not to get two copies of one due date.
    /// </summary>
    public bool SuccessorCreated { get; private set; }

    /// <summary>
    /// Creates a task. A deadline in the past is checked only here, at creation:
    /// for an existing task the deadline is allowed to "go stale".
    /// </summary>
    public static TaskItem Create(
        string title,
        Priority priority,
        TimeProvider clock,
        string? description = null,
        DateTimeOffset? deadline = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var now = clock.GetUtcNow();

        if (deadline is { } d && d < now)
        {
            throw new DomainValidationException("Дедлайн не может быть в прошлом.");
        }

        var task = new TaskItem(NormalizeTitle(title), EnsurePriority(priority), now)
        {
            Description = NormalizeDescription(description),
            Deadline = deadline,
        };
        task.RefreshSearchText();

        return task;
    }

    public void SetTitle(string title)
    {
        EnsureNotDeleted();
        Title = NormalizeTitle(title);
        RefreshSearchText();
    }

    public void SetDescription(string? description)
    {
        EnsureNotDeleted();
        Description = NormalizeDescription(description);
        RefreshSearchText();
    }

    public void SetPriority(Priority priority)
    {
        EnsureNotDeleted();
        Priority = EnsurePriority(priority);
    }

    /// <summary>Changes the deadline. A past date is acceptable here, the "not in the past" rule applies only at creation.</summary>
    public void SetDeadline(DateTimeOffset? deadline)
    {
        EnsureNotDeleted();
        EnsureDeadlineForCalendarRecurrence(Recurrence, deadline);
        Deadline = deadline;
    }

    public void SetRecurrence(RecurrenceRule? rule)
    {
        EnsureNotDeleted();
        EnsureDeadlineForCalendarRecurrence(rule, Deadline);
        Recurrence = rule;
    }

    public void AddTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        EnsureNotDeleted();

        if (!Tags.Any(t => string.Equals(t.Name, tag.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Tags.Add(tag);
        }
    }

    public void RemoveTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        EnsureNotDeleted();
        Tags.Remove(tag);
    }

    /// <summary>Changes the status by an ordinary transition. Forbidden transitions are rejected by <see cref="TaskStateMachine"/>.</summary>
    public void ChangeStatus(TaskStatus to, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureNotDeleted();
        TaskStateMachine.EnsureTransition(Status, to);

        if (to == TaskStatus.Done)
        {
            EnsureSubtasksCompleted();
        }

        Status = to;
        CompletedAt = to == TaskStatus.Done ? clock.GetUtcNow() : null;
    }

    /// <summary>Reopens a completed (Done) or cancelled (Cancelled) task.</summary>
    public void Reopen()
    {
        EnsureNotDeleted();
        var target = TaskStateMachine.EnsureReopen(Status);

        if (Parent is { IsDeleted: false, Status: TaskStatus.Done })
        {
            throw new DomainValidationException(
                "Нельзя переоткрыть подзадачу, пока выполнена родительская задача. Сначала переоткройте родителя.");
        }

        Status = target;
        CompletedAt = null;
    }

    /// <summary>
    /// Progress by subtasks: direct children are counted, without cancelled and deleted ones.
    /// The children must be loaded (<see cref="Children"/>).
    /// </summary>
    public TaskProgress GetProgress()
    {
        var counted = Children.Where(c => !c.IsDeleted && c.Status != TaskStatus.Cancelled).ToList();
        return new TaskProgress(counted.Count(c => c.Status == TaskStatus.Done), counted.Count);
    }

    /// <summary>
    /// Attaches the task to a parent (or makes it a top-level task when null).
    /// Here the rules visible from the objects themselves are checked. The nesting depth and cycles
    /// are checked by <c>TaskService</c>, because that requires walking the tree in the database.
    /// </summary>
    public void SetParent(TaskItem? parent)
    {
        EnsureNotDeleted();

        if (parent is not null)
        {
            if (ReferenceEquals(parent, this))
            {
                throw new DomainValidationException("Задача не может быть подзадачей самой себя.");
            }

            if (parent.IsDeleted)
            {
                throw new DomainValidationException("Нельзя добавить подзадачу к удалённой задаче.");
            }

            if (parent.Status == TaskStatus.Done && Status is not (TaskStatus.Done or TaskStatus.Cancelled))
            {
                throw new DomainValidationException(
                    "Нельзя добавить невыполненную подзадачу к выполненной задаче. Сначала переоткройте родителя.");
            }
        }

        Parent = parent;
        // A parent that is not saved yet has Id 0: then EF will set the link by the Parent reference on save.
        ParentId = parent is { Id: not 0 } ? parent.Id : null;
    }

    /// <summary>
    /// Recreates a task from external data (import): the status and dates are carried over as they are, so the status machine
    /// and the "deadline not in the past" rule do not apply. The other field checks work as usual.
    /// </summary>
    public static TaskItem CreateImported(
        string title,
        Priority priority,
        DateTimeOffset createdAt,
        string? description,
        DateTimeOffset? deadline,
        TaskStatus status,
        DateTimeOffset? completedAt,
        bool successorCreated,
        RecurrenceRule? recurrence)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainValidationException("Неизвестный статус задачи.");
        }

        if (status == TaskStatus.Done && completedAt is null)
        {
            throw new DomainValidationException("У выполненной задачи нет даты выполнения.");
        }

        var task = new TaskItem(NormalizeTitle(title), EnsurePriority(priority), createdAt)
        {
            Description = NormalizeDescription(description),
            Deadline = deadline,
            Status = status,
            CompletedAt = status == TaskStatus.Done ? completedAt : null,
            SuccessorCreated = successorCreated,
        };
        task.RefreshSearchText();

        if (recurrence is not null)
        {
            task.SetRecurrence(recurrence);
        }

        return task;
    }

    /// <summary>Marks that the next copy of a recurring task has been created.</summary>
    public void MarkSuccessorCreated() => SuccessorCreated = true;

    /// <summary>
    /// Creates the next copy of a recurring task: the title, description, priority and tags are taken from
    /// <paramref name="source"/>, the status is Todo. The deadline may be in the past (the task was closed late),
    /// so the "not in the past" rule does not apply here. Subtasks are copied by <c>TaskService</c>.
    /// </summary>
    public static TaskItem CreateSuccessor(TaskItem source, DateTimeOffset? deadline, RecurrenceRule? recurrence, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(clock);

        var task = new TaskItem(source.Title, source.Priority, clock.GetUtcNow())
        {
            Description = source.Description,
            Deadline = deadline,
        };
        task.RefreshSearchText();

        foreach (var tag in source.Tags)
        {
            task.Tags.Add(tag);
        }

        if (recurrence is not null)
        {
            task.SetRecurrence(recurrence);
        }

        return task;
    }

    public void Delete()
    {
        if (IsDeleted)
        {
            throw new DomainValidationException("Задача уже удалена.");
        }

        IsDeleted = true;
    }

    public void Restore()
    {
        if (!IsDeleted)
        {
            throw new DomainValidationException("Задача не удалена, восстанавливать нечего.");
        }

        IsDeleted = false;
    }

    /// <summary>Brings text to the form in which it is stored in <see cref="SearchText"/>. The search query is normalised the same way.</summary>
    public static string NormalizeSearchText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Trim().ToUpperInvariant();
    }

    private void RefreshSearchText() =>
        SearchText = NormalizeSearchText(Description is null ? Title : $"{Title}\n{Description}");

    private static void EnsureDeadlineForCalendarRecurrence(RecurrenceRule? rule, DateTimeOffset? deadline)
    {
        if (rule is { Mode: RecurrenceMode.ByCalendar } && deadline is null)
        {
            throw new DomainValidationException("Для повторения по календарю нужен дедлайн.");
        }
    }

    private void EnsureSubtasksCompleted()
    {
        var progress = GetProgress();

        if (!progress.IsComplete)
        {
            throw new DomainValidationException(
                $"Нельзя завершить задачу: не завершены подзадачи ({progress}).");
        }
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new DomainValidationException("Удалённую задачу нельзя изменять. Сначала восстановите её.");
        }
    }

    private static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException("Название задачи не может быть пустым.");
        }

        if (trimmed.Length > MaxTitleLength)
        {
            throw new DomainValidationException($"Название задачи не может быть длиннее {MaxTitleLength} символов.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static Priority EnsurePriority(Priority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new DomainValidationException("Неизвестный приоритет.");
        }

        return priority;
    }
}
