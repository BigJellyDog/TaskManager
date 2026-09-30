using TaskManager.Core.Domain;

namespace TaskManager.Core.Services;

/// <summary>A subtask in the flat list of the tree: the task itself, the nesting level and the progress of its own subtasks.</summary>
/// <param name="Task">The subtask.</param>
/// <param name="Level">The level relative to the task shown: 1 for direct subtasks, 2 for their subtasks and so on.</param>
/// <param name="Progress">The progress of the subtasks of this subtask (without cancelled and deleted ones).</param>
public sealed record SubtaskEntry(TaskItem Task, int Level, TaskProgress Progress);
