using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Узел дерева зависимостей: задача и то, от чего она зависит.</summary>
/// <param name="Task">Задача.</param>
/// <param name="DependsOn">Задачи, от которых она зависит (рекурсивно).</param>
public sealed record DependencyNode(TaskItem Task, IReadOnlyList<DependencyNode> DependsOn);
