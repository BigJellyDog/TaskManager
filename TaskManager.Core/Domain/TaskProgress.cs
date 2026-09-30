namespace TaskManager.Core.Domain;

/// <summary>Progress by subtasks: how many are done out of how many ("3/5").</summary>
public readonly record struct TaskProgress(int Completed, int Total)
{
    /// <summary>There are no subtasks at all.</summary>
    public bool HasSubtasks => Total > 0;

    /// <summary>All counted subtasks are done (or there are none), the parent can be closed.</summary>
    public bool IsComplete => Completed == Total;

    /// <summary>The completion percentage, 0-100. Returns 0 without subtasks.</summary>
    public int Percent => Total == 0 ? 0 : Completed * 100 / Total;

    public override string ToString() => $"{Completed}/{Total}";
}
