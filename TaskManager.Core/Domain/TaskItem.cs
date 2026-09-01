namespace TaskManager.Core.Domain;

/// <summary>Задача. Главная сущность домена.</summary>
public class TaskItem
{
    public TaskItem(string title, Priority priority, DateTimeOffset createdAt)
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
}
