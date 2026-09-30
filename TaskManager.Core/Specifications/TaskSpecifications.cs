using System.Linq.Expressions;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Specifications;

/// <summary>Tasks with one of the listed statuses.</summary>
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

/// <summary>Tasks with the given priority.</summary>
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

/// <summary>Tasks with a tag. The name is compared ignoring case (Cyrillic included).</summary>
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
/// Tasks with a deadline in a range. The bounds are inclusive, either may be omitted.
/// Tasks without a deadline do not match.
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
/// Full-text search by title and description ignoring case. A query of several words:
/// all the words must occur in the text (in any order). An empty query matches any task.
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
