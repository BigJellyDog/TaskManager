using System.Linq.Expressions;

namespace TaskManager.Core.Specifications;

/// <summary>Specification factories (a separate class, because static members in generic types are discouraged).</summary>
public static class Specification
{
    /// <summary>A condition that any object satisfies. Needed when there are no filters at all.</summary>
    public static Specification<T> All<T>() => new ExpressionSpecification<T>(_ => true);

    /// <summary>Creates a specification from a ready expression, without a separate class.</summary>
    public static Specification<T> From<T>(Expression<Func<T, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new ExpressionSpecification<T>(expression);
    }
}

/// <summary>
/// The base class of specifications. Concrete conditions inherit from it and implement <see cref="ToExpression"/>,
/// and the combinators <see cref="And"/>, <see cref="Or"/>, <see cref="Not"/> glue conditions into new ones.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private Func<T, bool>? _compiled;

    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
    {
        // Compiling an expression is expensive, so we do it once.
        _compiled ??= ToExpression().Compile();
        return _compiled(entity);
    }

    /// <summary>Both conditions at once (logical AND).</summary>
    public Specification<T> And(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AndSpecification<T>(this, other);
    }

    /// <summary>At least one of the conditions (logical OR).</summary>
    public Specification<T> Or(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new OrSpecification<T>(this, other);
    }

    /// <summary>The opposite condition (logical NOT).</summary>
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
/// Glues two expressions into one. Each expression has its own parameter (t => ..., x => ...),
/// so before gluing they must be brought to one common parameter.
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
