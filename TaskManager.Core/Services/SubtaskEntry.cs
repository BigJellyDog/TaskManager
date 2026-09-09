using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>Подзадача в плоском списке дерева: сама задача, уровень вложенности и прогресс её собственных подзадач.</summary>
/// <param name="Task">Подзадача.</param>
/// <param name="Level">Уровень относительно показанной задачи: у прямых подзадач 1, у их подзадач 2 и так далее.</param>
/// <param name="Progress">Прогресс подзадач этой подзадачи (без отменённых и удалённых).</param>
public sealed record SubtaskEntry(TaskItem Task, int Level, TaskProgress Progress);
