namespace TaskManager.Core.Domain;

/// <summary>
/// Задача. Главная сущность домена. Состояние меняется только методами ниже:
/// каждый метод сам проверяет бизнес-правила и бросает <see cref="DomainValidationException"/>
/// или <see cref="InvalidStatusTransitionException"/>.
/// </summary>
public class TaskItem
{
    public const int MaxTitleLength = 200;

    private TaskItem(string title, Priority priority, DateTimeOffset createdAt)
    {
        Title = title;
        Priority = priority;
        CreatedAt = createdAt;
    }

    // Нужен EF Core.
    private TaskItem()
    {
        Title = string.Empty;
    }

    public int Id { get; private set; }

    /// <summary>
    /// Версия строки для оптимистичной блокировки. Увеличивается инфраструктурой при каждом сохранении.
    /// Если задачу успели изменить между чтением и сохранением, версии не совпадут и сохранение будет отклонено.
    /// </summary>
    public long RowVersion { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public Priority Priority { get; private set; }

    public TaskStatus Status { get; private set; } = TaskStatus.Todo;

    public DateTimeOffset? Deadline { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Когда задача стала Done. Нужно для статистики (вовремя ли, сколько заняла).</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Мягкое удаление: запись остаётся в БД, но скрыта фильтром.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Родительская задача (null для задач верхнего уровня).</summary>
    public int? ParentId { get; private set; }

    public TaskItem? Parent { get; private set; }

    public ICollection<TaskItem> Children { get; } = new List<TaskItem>();

    public ICollection<Tag> Tags { get; } = new List<Tag>();

    /// <summary>От каких задач зависит эта.</summary>
    public ICollection<TaskDependency> Dependencies { get; } = new List<TaskDependency>();

    /// <summary>Какие задачи зависят от этой.</summary>
    public ICollection<TaskDependency> Dependents { get; } = new List<TaskDependency>();

    /// <summary>Правило повторения (null, если задача не повторяющаяся).</summary>
    public RecurrenceRule? Recurrence { get; private set; }

    /// <summary>
    /// Создаёт задачу. Дедлайн в прошлом проверяется только здесь, при создании:
    /// у уже существующей задачи дедлайн имеет право "протухнуть".
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

        return task;
    }

    public void SetTitle(string title)
    {
        EnsureNotDeleted();
        Title = NormalizeTitle(title);
    }

    public void SetDescription(string? description)
    {
        EnsureNotDeleted();
        Description = NormalizeDescription(description);
    }

    public void SetPriority(Priority priority)
    {
        EnsureNotDeleted();
        Priority = EnsurePriority(priority);
    }

    /// <summary>Меняет дедлайн. Прошедшая дата здесь допустима, правило "не в прошлом" действует только при создании.</summary>
    public void SetDeadline(DateTimeOffset? deadline)
    {
        EnsureNotDeleted();
        Deadline = deadline;
    }

    public void SetRecurrence(RecurrenceRule? rule)
    {
        EnsureNotDeleted();
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

    /// <summary>Меняет статус обычным переходом. Запрещённые переходы отклоняет <see cref="TaskStateMachine"/>.</summary>
    public void ChangeStatus(TaskStatus to, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureNotDeleted();
        TaskStateMachine.EnsureTransition(Status, to);

        Status = to;
        CompletedAt = to == TaskStatus.Done ? clock.GetUtcNow() : null;
    }

    /// <summary>Переоткрывает выполненную (Done) или отменённую (Cancelled) задачу.</summary>
    public void Reopen()
    {
        EnsureNotDeleted();
        Status = TaskStateMachine.EnsureReopen(Status);
        CompletedAt = null;
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
