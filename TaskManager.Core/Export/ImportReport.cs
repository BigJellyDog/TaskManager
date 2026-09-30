namespace TaskManager.Core.Export;

/// <summary>One record of the import report: which task from the file and what is wrong with it.</summary>
/// <param name="FileTaskId">The task Id in the file.</param>
/// <param name="Title">The task title (to show to the user).</param>
/// <param name="Reason">The reason.</param>
public sealed record ImportIssue(int? FileTaskId, string? Title, string Reason);

/// <summary>The result of the import.</summary>
/// <param name="Added">How many tasks were added.</param>
/// <param name="Skipped">Skipped on purpose: duplicates, deleted ones, subtasks of a skipped parent.</param>
/// <param name="Errors">Not imported because of errors in the data.</param>
/// <param name="Warnings">The task was added, but some links were not created (for example, a dependency would create a cycle).</param>
public sealed record ImportReport(
    int Added,
    IReadOnlyList<ImportIssue> Skipped,
    IReadOnlyList<ImportIssue> Errors,
    IReadOnlyList<ImportIssue> Warnings)
{
    /// <summary>Whether there is anything other than successfully added tasks.</summary>
    public bool HasProblems => Skipped.Count > 0 || Errors.Count > 0 || Warnings.Count > 0;
}
