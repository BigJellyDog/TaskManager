using System.Text.Json;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;

namespace TaskManager.Core.Export;

/// <summary>
/// Экспорт задач в JSON (для обмена и импорта) и Markdown (для чтения). Возвращает текст:
/// куда его записать, решает интерфейс.
/// </summary>
public class ExportService
{
    private readonly ITaskRepository _tasks;
    private readonly TimeProvider _clock;

    public ExportService(ITaskRepository tasks, TimeProvider clock)
    {
        _tasks = tasks;
        _clock = clock;
    }

    /// <summary>Все задачи в формате JSON. Удалённые по умолчанию не включаются.</summary>
    public async Task<string> ExportJsonAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var document = await BuildDocumentAsync(includeDeleted, cancellationToken);
        return JsonSerializer.Serialize(document, ExportFormat.JsonOptions);
    }

    /// <summary>Чек-лист в формате Markdown с вложенными подзадачами.</summary>
    public async Task<string> ExportMarkdownAsync(bool includeDeleted = false, Func<string, string>? translate = null, CancellationToken cancellationToken = default)
    {
        var document = await BuildDocumentAsync(includeDeleted, cancellationToken);
        return MarkdownRenderer.Render(document, _clock.LocalTimeZone, translate);
    }

    private async Task<ExportDocument> BuildDocumentAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        var tasks = await _tasks.GetAllAsync(includeDeleted, cancellationToken);
        var ids = tasks.Select(t => t.Id).ToHashSet();

        // В графе есть и рёбра удалённых задач, оставляем только те, где обе задачи попали в экспорт.
        var dependsOn = (await _tasks.GetDependencyGraphAsync(cancellationToken))
            .Where(e => ids.Contains(e.TaskId) && ids.Contains(e.DependsOnTaskId))
            .ToLookup(e => e.TaskId, e => e.DependsOnTaskId);

        var exported = tasks.Select(t => new ExportTask(
            t.Id,
            t.ParentId,
            t.Title,
            t.Description,
            t.Priority,
            t.Status,
            t.Deadline,
            t.CreatedAt,
            t.CompletedAt,
            t.IsDeleted,
            t.SuccessorCreated,
            t.Tags.Select(tag => tag.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList(),
            dependsOn[t.Id].Order().ToList(),
            t.Recurrence is { } r ? new ExportRecurrence(r.Frequency, r.Interval, r.Mode, r.WeekDays, r.Anchor) : null))
            .ToList();

        return new ExportDocument(ExportFormat.CurrentVersion, _clock.GetUtcNow(), exported);
    }
}
