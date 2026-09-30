using System.Linq.Expressions;

namespace TaskManager.Core.Specifications;

/// <summary>
/// A specification: a named selection condition. The condition is stored as an expression tree, not as a ready function,
/// so that EF Core can translate it to SQL (WHERE) instead of filtering in memory.
/// </summary>
public interface ISpecification<T>
{
    /// <summary>The condition as an expression. It can also be compiled and applied to an object in memory.</summary>
    Expression<Func<T, bool>> ToExpression();

    /// <summary>A check of a single object in memory (the same condition as in <see cref="ToExpression"/>).</summary>
    bool IsSatisfiedBy(T entity);
}
