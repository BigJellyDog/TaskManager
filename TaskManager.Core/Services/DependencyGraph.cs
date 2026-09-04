namespace TaskManager.Core.Services;

/// <summary>
/// Граф зависимостей: ребро (A, B) означает "A зависит от B". Чистая логика без базы данных.
/// </summary>
public static class DependencyGraph
{
    /// <summary>
    /// Ищет цикл, который появится, если добавить зависимость "<paramref name="taskId"/> зависит от
    /// <paramref name="dependsOnTaskId"/>". Цикл возникает, когда от <paramref name="dependsOnTaskId"/>
    /// по уже существующим зависимостям можно дойти до <paramref name="taskId"/>.
    /// Поиск в глубину (DFS) без рекурсии, у каждой вершины заходим один раз, поэтому "ромб" без цикла проходит.
    /// </summary>
    /// <returns>Цепочка Id, замыкающая цикл (например 1, 2, 3, 1), или null, если цикла не будет.</returns>
    public static IReadOnlyList<int>? FindCyclePath(
        IEnumerable<(int TaskId, int DependsOnTaskId)> existingEdges,
        int taskId,
        int dependsOnTaskId)
    {
        ArgumentNullException.ThrowIfNull(existingEdges);

        // Самозависимость: A зависит от A.
        if (taskId == dependsOnTaskId)
        {
            return [taskId, taskId];
        }

        var dependenciesOf = existingEdges
            .GroupBy(e => e.TaskId, e => e.DependsOnTaskId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Стек хранит вершину и путь до неё. Идём от dependsOnTaskId вглубь и ищем taskId.
        var visited = new HashSet<int> { dependsOnTaskId };
        var stack = new Stack<(int Node, List<int> Path)>();
        stack.Push((dependsOnTaskId, [dependsOnTaskId]));

        while (stack.Count > 0)
        {
            var (node, path) = stack.Pop();

            if (!dependenciesOf.TryGetValue(node, out var next))
            {
                continue;
            }

            foreach (var dependency in next)
            {
                if (dependency == taskId)
                {
                    // Новая зависимость замкнёт цепочку: taskId -> dependsOnTaskId -> ... -> taskId.
                    return [taskId, .. path, taskId];
                }

                if (visited.Add(dependency))
                {
                    stack.Push((dependency, [.. path, dependency]));
                }
            }
        }

        return null;
    }
}
