using TaskManager.Core.Domain;
using TaskManager.Core.Specifications;

namespace TaskManager.Core.Querying;

/// <summary>Запрос списка задач: что отобрать, как отсортировать и какую страницу вернуть.</summary>
public sealed class TaskQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    /// <summary>Сортировка по умолчанию: сначала важные, среди равных сначала срочные.</summary>
    public static IReadOnlyList<TaskSort> DefaultSort { get; } =
    [
        new TaskSort(TaskSortField.Priority, Descending: true),
        new TaskSort(TaskSortField.Deadline),
    ];

    public TaskQuery(
        ISpecification<TaskItem>? filter = null,
        IReadOnlyList<TaskSort>? sort = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        if (page < 1)
        {
            throw new DomainValidationException("Номер страницы должен быть не меньше 1.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new DomainValidationException($"Размер страницы должен быть от 1 до {MaxPageSize}.");
        }

        Filter = filter ?? Specification.All<TaskItem>();
        Sort = sort is { Count: > 0 } ? sort : DefaultSort;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Условие отбора. Если фильтров нет, подходят все задачи.</summary>
    public ISpecification<TaskItem> Filter { get; }

    /// <summary>Правила сортировки по порядку важности. Если не заданы, действует <see cref="DefaultSort"/>.</summary>
    public IReadOnlyList<TaskSort> Sort { get; }

    /// <summary>Номер страницы, с единицы.</summary>
    public int Page { get; }

    public int PageSize { get; }
}
