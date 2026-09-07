using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Export;

/// <summary>
/// Документ экспорта: то, что записывается в JSON и читается при импорте. Задачи идут плоским списком,
/// иерархию задаёт <see cref="ExportTask.ParentId"/>. История изменений не экспортируется.
/// </summary>
/// <param name="Version">Версия формата (<see cref="ExportFormat.CurrentVersion"/>), по ней импорт проверяет совместимость.</param>
/// <param name="ExportedAt">Когда сделан экспорт.</param>
/// <param name="Tasks">Задачи.</param>
public sealed record ExportDocument(int Version, DateTimeOffset ExportedAt, IReadOnlyList<ExportTask> Tasks);

/// <summary>Одна задача в документе экспорта.</summary>
/// <param name="Id">Номер задачи в базе на момент экспорта. Нужен, чтобы сослаться на неё в <c>ParentId</c> и <c>DependsOn</c>.</param>
/// <param name="ParentId">Id родительской задачи или null.</param>
/// <param name="DependsOn">Id задач, от которых зависит эта (только среди экспортированных).</param>
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

/// <summary>Правило повторения в документе экспорта.</summary>
public sealed record ExportRecurrence(
    RecurrenceFrequency Frequency,
    int Interval,
    RecurrenceMode Mode,
    WeekDays WeekDays,
    DateTime? Anchor);
