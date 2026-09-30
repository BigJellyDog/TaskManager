namespace TaskManager.Core.Services;

/// <summary>
/// The dependency graph: the edge (A, B) means "A depends on B". Pure logic without a database.
/// </summary>
public static class DependencyGraph
{
    /// <summary>
    /// Looks for the cycle that would appear if the dependency "<paramref name="taskId"/> depends on
    /// <paramref name="dependsOnTaskId"/>" is added. A cycle appears when from <paramref name="dependsOnTaskId"/>
    /// one can reach <paramref name="taskId"/> through the existing dependencies.
    /// Depth-first search (DFS) without recursion, we enter each vertex once, so a "diamond" without a cycle passes.
    /// </summary>
    /// <returns>The chain of Ids closing the cycle (for example 1, 2, 3, 1), or null if there will be no cycle.</returns>
    public static IReadOnlyList<int>? FindCyclePath(
        IEnumerable<(int TaskId, int DependsOnTaskId)> existingEdges,
        int taskId,
        int dependsOnTaskId)
    {
        ArgumentNullException.ThrowIfNull(existingEdges);

        // Self-dependency: A depends on A.
        if (taskId == dependsOnTaskId)
        {
            return [taskId, taskId];
        }

        var dependenciesOf = existingEdges
            .GroupBy(e => e.TaskId, e => e.DependsOnTaskId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // The stack holds a vertex and the path to it. We go from dependsOnTaskId in depth and look for taskId.
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
                    // The new dependency will close the chain: taskId -> dependsOnTaskId -> ... -> taskId.
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
