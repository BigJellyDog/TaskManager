using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Export;

/// <summary>
/// The export document: what is written to JSON and read on import. Tasks come as a flat list,
/// the hierarchy is set by <see cref="ExportTask.ParentId"/>. The change history is not exported.
/// </summary>
/// <param name="Version">The format version (<see cref="ExportFormat.CurrentVersion"/>), the import checks compatibility by it.</param>
/// <param name="ExportedAt">When the export was made.</param>
/// <param name="Tasks">The tasks.</param>
public sealed record ExportDocument(int Version, DateTimeOffset ExportedAt, IReadOnlyList<ExportTask> Tasks);

/// <summary>One task in the export document.</summary>
/// <param name="Id">The task number in the database at the time of export. Needed to refer to it in <c>ParentId</c> and <c>DependsOn</c>.</param>
/// <param name="ParentId">The Id of the parent task or null.</param>
/// <param name="DependsOn">The Ids of the tasks this one depends on (only among the exported ones).</param>
public sealed record ExportTask(
    int Id,
    int? ParentId,
    string Title,
    string? Description,
    Priority Priority,
    TaskStatus Status,
    DateTimeOffset? Deadline,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool IsDeleted,
    bool SuccessorCreated,
    IReadOnlyList<string> Tags,
    IReadOnlyList<int> DependsOn,
    ExportRecurrence? Recurrence);

/// <summary>The recurrence rule in the export document.</summary>
public sealed record ExportRecurrence(
    RecurrenceFrequency Frequency,
    int Interval,
    RecurrenceMode Mode,
    WeekDays WeekDays,
    DateTime? Anchor);
