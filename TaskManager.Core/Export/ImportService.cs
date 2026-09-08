using System.Text.Json;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;

namespace TaskManager.Core.Export;

/// <summary>
/// Импорт задач из JSON, созданного <see cref="ExportService"/>. Ошибка в файле целиком отклоняет импорт,
/// ошибка в одной задаче только её саму (и её подзадачи): остальные добавляются, а причины попадают в отчёт.
/// Всё выполняется одной транзакцией.
/// </summary>
public class ImportService
{
    public const string ImportCommand = "import";

    private readonly ITaskRepository _tasks;
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ImportService(ITaskRepository tasks, ITagRepository tags, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _tasks = tasks;
        _tags = tags;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <exception cref="DomainValidationException">Файл не читается, версия не поддерживается или нет списка задач.</exception>
    public Task<ImportReport> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        var document = Parse(json);
        return _unitOfWork.ExecuteInTransactionAsync(ct => ImportAsync(document, ct), cancellationToken);
    }

    private static ExportDocument Parse(string json)
    {
        ExportDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<ExportDocument>(json, ExportFormat.JsonOptions);
        }
        catch (JsonException ex)
        {
            // В сообщении исключения есть путь к проблемному месту, например $.tasks[2].priority.
            throw new DomainValidationException($"Файл не похож на экспорт задач: {ex.Message}", ex);
        }

        if (document?.Tasks is null)
        {
            throw new DomainValidationException("В файле нет списка задач.");
        }

        if (document.Version < 1 || document.Version > ExportFormat.CurrentVersion)
        {
            throw new DomainValidationException(
                $"Версия формата {document.Version} не поддерживается (поддерживается до {ExportFormat.CurrentVersion}).");
        }

        return document;
    }

    private async Task<ImportReport> ImportAsync(ExportDocument document, CancellationToken cancellationToken)
    {
        var skipped = new List<ImportIssue>();
        var errors = new List<ImportIssue>();
        var warnings = new List<ImportIssue>();

        var results = new Dictionary<int, Outcome>();
        var byId = new Dictionary<int, ExportTask>();

        foreach (var task in document.Tasks)
        {
            if (!byId.TryAdd(task.Id, task))
            {
                errors.Add(new ImportIssue(task.Id, task.Title, $"Id {task.Id} повторяется в файле."));
            }
        }

        // Родители обрабатываются раньше детей: сортируем по глубине. Задачи с битой иерархией отсеиваем сразу.
        var ordered = new List<(ExportTask Task, int Depth)>();
        foreach (var task in byId.Values)
        {
            if (TryGetDepth(task, byId, out var depth, out var problem))
            {
                ordered.Add((task, depth));
            }
            else
            {
                errors.Add(new ImportIssue(task.Id, task.Title, problem));
                results[task.Id] = Outcome.Failed;
            }
        }

        // Дубликат: то же название (без учёта регистра) и тот же дедлайн. Сначала то, что уже есть в базе.
        var known = new Dictionary<(string Title, DateTimeOffset? Deadline), Outcome>();
        foreach (var existing in await _tasks.GetAllAsync(includeDeleted: false, cancellationToken))
        {
            known.TryAdd((TaskItem.NormalizeSearchText(existing.Title), existing.Deadline), Outcome.Existing(existing.Id));
        }

        var tagCache = new Dictionary<string, Tag>();
        var created = new List<(ExportTask File, TaskItem Entity)>();

        foreach (var (file, depth) in ordered.OrderBy(o => o.Depth).ThenBy(o => o.Task.Id))
        {
            if (file.IsDeleted)
            {
                skipped.Add(new ImportIssue(file.Id, file.Title, "Удалённая задача не импортируется."));
                results[file.Id] = Outcome.Skipped;
                continue;
            }

            if (depth > TaskItem.MaxDepth)
            {
                errors.Add(new ImportIssue(file.Id, file.Title, $"Вложенность больше {TaskItem.MaxDepth} уровней."));
                results[file.Id] = Outcome.Failed;
                continue;
            }

            TaskItem? parent = null;
            if (file.ParentId is { } parentId)
            {
                var parentOutcome = results[parentId];

                if (parentOutcome.Kind != OutcomeKind.Created)
                {
                    var (list, reason) = parentOutcome.Kind switch
                    {
                        OutcomeKind.Failed => (errors, "Родительская задача не импортирована из-за ошибки."),
                        OutcomeKind.Duplicate => (skipped, "Родительская задача уже есть, подзадачи пропущены."),
                        _ => (skipped, "Родительская задача пропущена."),
                    };

                    list.Add(new ImportIssue(file.Id, file.Title, reason));
                    results[file.Id] = parentOutcome.Kind == OutcomeKind.Failed ? Outcome.Failed : Outcome.Skipped;
                    continue;
                }

                parent = parentOutcome.Entity;
            }

            var key = (TaskItem.NormalizeSearchText(file.Title ?? string.Empty), file.Deadline);
            if (known.TryGetValue(key, out var match))
            {
                skipped.Add(new ImportIssue(file.Id, file.Title, "Такая задача уже есть (то же название и дедлайн)."));
                results[file.Id] = match;
                continue;
            }

            try
            {
                var entity = await BuildTaskAsync(file, parent, tagCache, cancellationToken);
                await _tasks.AddAsync(entity, cancellationToken);

                var outcome = Outcome.Created(entity);
                results[file.Id] = outcome;
                known[key] = outcome;
                created.Add((file, entity));
            }
            catch (DomainValidationException ex)
            {
                errors.Add(new ImportIssue(file.Id, file.Title, ex.Message));
                results[file.Id] = Outcome.Failed;
            }
        }

        if (created.Count > 0)
        {
            // Первое сохранение: база выдаёт новые Id, они нужны для зависимостей и записей истории.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await AddDependenciesAsync(created, results, warnings, cancellationToken);

            var now = _clock.GetUtcNow();
            await _tasks.AddHistoryAsync(
                created.Select(c => new TaskHistoryEntry(c.Entity.Id, now, ImportCommand, "Created", null, c.Entity.Title)),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ImportReport(created.Count, skipped, errors, warnings);
    }

    private async Task<TaskItem> BuildTaskAsync(
        ExportTask file,
        TaskItem? parent,
        Dictionary<string, Tag> tagCache,
        CancellationToken cancellationToken)
    {
        var rule = file.Recurrence is { } r
            ? new RecurrenceRule(r.Frequency, r.Interval, r.Mode, r.WeekDays, r.Anchor)
            : null;

        var entity = TaskItem.CreateImported(
            file.Title,
            file.Priority,
            file.CreatedAt,
            file.Description,
            file.Deadline,
            file.Status,
            file.CompletedAt,
            file.SuccessorCreated,
            rule);

        // Новые теги в репозиторий добавляем только когда задача целиком собрана: иначе при ошибке остался бы лишний тег.
        var newTags = new List<Tag>();
        foreach (var name in file.Tags ?? [])
        {
            var normalized = Tag.Normalize(name ?? string.Empty);
            if (!tagCache.TryGetValue(normalized, out var tag))
            {
                tag = await _tags.GetByNameAsync(name ?? string.Empty, cancellationToken) ?? new Tag(name ?? string.Empty);
                newTags.Add(tag);
            }

            entity.AddTag(tag);
        }

        if (parent is not null)
        {
            entity.SetParent(parent);
        }

        foreach (var tag in newTags)
        {
            if (tag.Id == 0)
            {
                await _tags.AddAsync(tag, cancellationToken);
            }

            tagCache[tag.NormalizedName] = tag;
        }

        return entity;
    }

    /// <summary>
    /// Зависимости переносятся по новым Id. Те, которые создать нельзя (задача не импортирована, цикл),
    /// не отменяют задачу, а попадают в предупреждения.
    /// </summary>
    private async Task AddDependenciesAsync(
        List<(ExportTask File, TaskItem Entity)> created,
        Dictionary<int, Outcome> results,
        List<ImportIssue> warnings,
        CancellationToken cancellationToken)
    {
        var edges = (await _tasks.GetDependencyGraphAsync(cancellationToken)).ToList();
        var edgeSet = edges.ToHashSet();

        foreach (var (file, entity) in created)
        {
            foreach (var dependencyFileId in (file.DependsOn ?? []).Distinct())
            {
                var dependencyId = results.TryGetValue(dependencyFileId, out var outcome) ? outcome.TaskId : null;

                if (dependencyId is null)
                {
                    warnings.Add(new ImportIssue(file.Id, file.Title, $"Зависимость от задачи #{dependencyFileId} пропущена: её нет среди импортированных."));
                    continue;
                }

                // Путь цикла здесь не показываем: в нём были бы Id из базы, а в отчёте все номера из файла.
                if (DependencyGraph.FindCyclePath(edges, entity.Id, dependencyId.Value) is not null)
                {
                    warnings.Add(new ImportIssue(file.Id, file.Title,
                        $"Зависимость от задачи #{dependencyFileId} пропущена: она создаёт цикл."));
                    continue;
                }

                if (edgeSet.Add((entity.Id, dependencyId.Value)))
                {
                    edges.Add((entity.Id, dependencyId.Value));
                    await _tasks.AddDependencyAsync(new TaskDependency(entity.Id, dependencyId.Value), cancellationToken);
                }
            }
        }
    }

    /// <summary>Уровень задачи в иерархии файла. Ложь, если родитель не найден или иерархия зациклена.</summary>
    private static bool TryGetDepth(ExportTask task, Dictionary<int, ExportTask> byId, out int depth, out string problem)
    {
        depth = 1;
        problem = string.Empty;

        var visited = new HashSet<int> { task.Id };
        var current = task;

        while (current.ParentId is { } parentId)
        {
            if (!byId.TryGetValue(parentId, out var parent))
            {
                problem = $"Родительская задача #{parentId} не найдена в файле.";
                return false;
            }

            if (!visited.Add(parentId))
            {
                problem = "Иерархия подзадач зациклена.";
                return false;
            }

            depth++;
            current = parent;
        }

        return true;
    }

    private enum OutcomeKind
    {
        Created,
        Duplicate,
        Skipped,
        Failed,
    }

    /// <summary>Что стало с задачей из файла: нужно детям (подзадачам) и зависимостям.</summary>
    private sealed class Outcome
    {
        public static Outcome Skipped { get; } = new(OutcomeKind.Skipped, null, null);

        public static Outcome Failed { get; } = new(OutcomeKind.Failed, null, null);

        private Outcome(OutcomeKind kind, TaskItem? entity, int? existingId)
        {
            Kind = kind;
            Entity = entity;
            ExistingId = existingId;
        }

        public OutcomeKind Kind { get; }

        /// <summary>Созданная задача (для Created и для дубликата внутри самого файла).</summary>
        public TaskItem? Entity { get; }

        /// <summary>Id уже существующей задачи (для дубликата).</summary>
        public int? ExistingId { get; }

        /// <summary>Id задачи в базе, если она есть (создана или уже существовала).</summary>
        public int? TaskId => Entity is { Id: not 0 } e ? e.Id : ExistingId;

        public static Outcome Created(TaskItem entity) => new(OutcomeKind.Created, entity, null);

        public static Outcome Existing(int id) => new(OutcomeKind.Duplicate, null, id);
    }
}
