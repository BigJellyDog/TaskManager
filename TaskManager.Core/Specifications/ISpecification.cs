using System.Linq.Expressions;

namespace TaskManager.Core.Specifications;

/// <summary>
/// Спецификация: именованное условие отбора. Условие хранится как дерево выражения, а не как готовая функция,
/// чтобы EF Core мог перевести его в SQL (WHERE), а не фильтровать в памяти.
/// </summary>
public interface ISpecification<T>
{
    /// <summary>Условие в виде выражения. Его же можно скомпилировать и применить к объекту в памяти.</summary>
    Expression<Func<T, bool>> ToExpression();

    /// <summary>Проверка одного объекта в памяти (то же условие, что и в <see cref="ToExpression"/>).</summary>
    bool IsSatisfiedBy(T entity);
}
