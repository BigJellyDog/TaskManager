namespace TaskManager.Core.Domain;

/// <summary>
/// Задача. Главная сущность домена. Состояние меняется только методами ниже:
/// каждый метод сам проверяет бизнес-правила и бросает <see cref="DomainValidationException"/>
/// или <see cref="InvalidStatusTransitionException"/>.
/// </summary>
public class TaskItem
{
    public const int MaxTitleLength = 200;

    /// <summary>Максимальная вложенность: задача верхнего уровня это уровень 1.</summary>
    public const int MaxDepth = 3;

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

    /// <summary>
    /// Название и описание в верхнем регистре, по ним работает поиск. SQLite не умеет менять регистр кириллицы
    /// (UPPER и LIKE работают только с латиницей), поэтому нормализуем в C# и сохраняем рядом с данными.
    /// </summary>
    public string SearchText { get; private set; } = string.Empty;

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

        if (to == TaskStatus.Done)
        {
            EnsureSubtasksCompleted();
        }

        Status = to;
        CompletedAt = to == TaskStatus.Done ? clock.GetUtcNow() : null;
    }

    /// <summary>Переоткрывает выполненную (Done) или отменённую (Cancelled) задачу.</summary>
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
    /// Прогресс по подзадачам: считаются прямые дети без отменённых и удалённых.
    /// Дети должны быть загружены (<see cref="Children"/>).
    /// </summary>
    public TaskProgress GetProgress()
    {
        var counted = Children.Where(c => !c.IsDeleted && c.Status != TaskStatus.Cancelled).ToList();
        return new TaskProgress(counted.Count(c => c.Status == TaskStatus.Done), counted.Count);
    }

    /// <summary>
    /// Привязывает задачу к родителю (или делает её задачей верхнего уровня при null).
    /// Здесь проверяются правила, которые видны из самих объектов. Глубину вложенности и циклы
    /// проверяет <c>TaskService</c>, потому что для этого нужно ходить по дереву в БД.
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
        ParentId = parent?.Id;
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

    /// <summary>Приводит текст к виду, в котором он хранится в <see cref="SearchText"/>. Запрос поиска нормализуется так же.</summary>
    public static string NormalizeSearchText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Trim().ToUpperInvariant();
    }

    private void RefreshSearchText() =>
        SearchText = NormalizeSearchText(Description is null ? Title : $"{Title}\n{Description}");

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
