namespace TaskManager.Core.Querying;

/// <summary>Поле, по которому можно сортировать список задач.</summary>
public enum TaskSortField
{
    Priority = 0,

    /// <summary>Задачи без дедлайна всегда в конце списка, при любом направлении сортировки.</summary>
    Deadline = 1,

    CreatedAt = 2,

    /// <summary>Алфавитный порядок без учёта регистра (в том числе для кириллицы).</summary>
    Title = 3,

    Status = 4,
}

/// <summary>Одно правило сортировки: поле и направление.</summary>
public sealed record TaskSort(TaskSortField Field, bool Descending = false);
