using System.Linq.Expressions;

namespace TaskManager.Core.Specifications;

/// <summary>Фабрики спецификаций (отдельным классом, потому что статические члены в generic-типах не рекомендуются).</summary>
public static class Specification
{
    /// <summary>Условие, которому удовлетворяет любой объект. Нужно, когда фильтров нет вообще.</summary>
    public static Specification<T> All<T>() => new ExpressionSpecification<T>(_ => true);

    /// <summary>Создаёт спецификацию из готового выражения, без отдельного класса.</summary>
    public static Specification<T> From<T>(Expression<Func<T, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new ExpressionSpecification<T>(expression);
    }
}

/// <summary>
/// Базовый класс спецификаций. Конкретные условия наследуются от него и реализуют <see cref="ToExpression"/>,
/// а комбинаторы <see cref="And"/>, <see cref="Or"/>, <see cref="Not"/> склеивают условия в новые.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private Func<T, bool>? _compiled;

    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
    {
        // Компиляция выражения дорогая, поэтому делаем её один раз.
        _compiled ??= ToExpression().Compile();
        return _compiled(entity);
    }

    /// <summary>Оба условия сразу (логическое И).</summary>
    public Specification<T> And(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AndSpecification<T>(this, other);
    }

    /// <summary>Хотя бы одно из условий (логическое ИЛИ).</summary>
    public Specification<T> Or(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new OrSpecification<T>(this, other);
    }

    /// <summary>Противоположное условие (логическое НЕ).</summary>
    public Specification<T> Not() => new NotSpecification<T>(this);
}

internal sealed class ExpressionSpecification<T> : Specification<T>
{
    private readonly Expression<Func<T, bool>> _expression;

    public ExpressionSpecification(Expression<Func<T, bool>> expression)
    {
        _expression = expression;
    }

    public override Expression<Func<T, bool>> ToExpression() => _expression;
}

internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression() =>
        ExpressionCombiner.Combine(_left.ToExpression(), _right.ToExpression(), Expression.AndAlso);
}

internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public OrSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression() =>
        ExpressionCombiner.Combine(_left.ToExpression(), _right.ToExpression(), Expression.OrElse);
}

internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _inner;

    public NotSpecification(ISpecification<T> inner)
    {
        _inner = inner;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var inner = _inner.ToExpression();
        return Expression.Lambda<Func<T, bool>>(Expression.Not(inner.Body), inner.Parameters);
    }
}

/// <summary>
/// Склеивает два выражения в одно. У каждого выражения свой параметр (t => ..., x => ...),
/// поэтому перед склейкой их надо привести к одному общему параметру.
/// </summary>
internal static class ExpressionCombiner
{
    public static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = Expression.Parameter(typeof(T), "t");
        var leftBody = new ParameterReplacer(left.Parameters[0], parameter).Visit(left.Body);
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
