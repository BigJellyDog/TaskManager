using TaskManager.Core.Domain;
using TaskManager.Core.Specifications;

namespace TaskManager.Core.Querying;

/// <summary>A task list query: what to select, how to sort and which page to return.</summary>
public sealed class TaskQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    /// <summary>The default sorting: important first, among equals urgent first.</summary>
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

    /// <summary>The selection condition. If there are no filters, all tasks match.</summary>
    public ISpecification<TaskItem> Filter { get; }

    /// <summary>The sorting rules in order of importance. If not set, <see cref="DefaultSort"/> applies.</summary>
    public IReadOnlyList<TaskSort> Sort { get; }

    /// <summary>The page number, starting from one.</summary>
    public int Page { get; }

    public int PageSize { get; }
}
