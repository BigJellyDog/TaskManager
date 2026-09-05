using System.Linq.Expressions;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Specifications;

/// <summary>Задачи с одним из перечисленных статусов.</summary>
public sealed class StatusSpecification : Specification<TaskItem>
{
    private readonly TaskStatus[] _statuses;

    public StatusSpecification(params TaskStatus[] statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        _statuses = statuses;
    }

    public override Expression<Func<TaskItem, bool>> ToExpression()
    {
        var statuses = _statuses;
        return t => statuses.Contains(t.Status);
    }
}

/// <summary>Задачи с заданным приоритетом.</summary>
public sealed class PrioritySpecification : Specification<TaskItem>
{
    private readonly Priority _priority;

    public PrioritySpecification(Priority priority)
    {
        _priority = priority;
    }

    public override Expression<Func<TaskItem, bool>> ToExpression()
    {
        var priority = _priority;
        return t => t.Priority == priority;
    }
}

/// <summary>Задачи с тегом. Имя сравнивается без учёта регистра (в том числе для кириллицы).</summary>
public sealed class TagSpecification : Specification<TaskItem>
{
    private readonly string _normalizedName;

    public TagSpecification(string tagName)
    {
        _normalizedName = Tag.Normalize(tagName);
    }

    public override Expression<Func<TaskItem, bool>> ToExpression()
    {
        var name = _normalizedName;
        return t => t.Tags.Any(tag => tag.NormalizedName == name);
    }
}

/// <summary>
/// Задачи с дедлайном в диапазоне. Границы включительно, любую можно не указывать.
/// Задачи без дедлайна под условие не попадают.
/// </summary>
public sealed class DeadlineRangeSpecification : Specification<TaskItem>
{
    private readonly DateTimeOffset? _from;
    private readonly DateTimeOffset? _to;

    public DeadlineRangeSpecification(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from is { } f && to is { } t && f > t)
        {
            throw new DomainValidationException("Начало диапазона дедлайнов не может быть позже конца.");
        }

        _from = from;
        _to = to;
    }

    public override Expression<Func<TaskItem, bool>> ToExpression()
    {
        var from = _from;
        var to = _to;

        return t => t.Deadline != null
            && (from == null || t.Deadline >= from)
            && (to == null || t.Deadline <= to);
    }
}

/// <summary>
/// Полнотекстовый поиск по названию и описанию без учёта регистра. Запрос из нескольких слов:
/// в тексте должны встретиться все слова (в любом порядке). Пустой запрос подходит любой задаче.
/// </summary>
public sealed class TextSpecification : Specification<TaskItem>
{
    private readonly string[] _words;

    public TextSpecification(string? query)
    {
        _words = (query ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(TaskItem.NormalizeSearchText)
            .Distinct()
            .ToArray();
    }

    public override Expression<Func<TaskItem, bool>> ToExpression()
    {
        var result = Specification.All<TaskItem>();

        foreach (var word in _words)
        {
            var w = word;
            result = result.And(Specification.From<TaskItem>(t => t.SearchText.Contains(w)));
        }

        return result.ToExpression();
    }
}
