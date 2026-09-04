using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Services;

/// <summary>
/// Сценарии работы с задачей. Каждая операция сохраняет и саму задачу, и запись в истории одним
/// <see cref="IUnitOfWork.SaveChangesAsync"/>, то есть в одной транзакции.
/// </summary>
public class TaskService
{
    // Названия операций для поля "какая команда" в истории.
    public const string CreateCommand = "create";
    public const string EditCommand = "edit";
    public const string ChangeStatusCommand = "status";
    public const string ReopenCommand = "reopen";
    public const string DeleteCommand = "delete";
    public const string RestoreCommand = "restore";

    private readonly ITaskRepository _tasks;
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public TaskService(ITaskRepository tasks, ITagRepository tags, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _tasks = tasks;
        _tags = tags;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<TaskItem> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await _tasks.GetWithDetailsAsync(id, cancellationToken) ?? throw new TaskNotFoundException(id);

    public Task<TaskItem> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Две записи в одной транзакции: Id задачи появляется только после первого сохранения,
        // а запись истории без Id задачи создать нельзя.
        return _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var task = TaskItem.Create(request.Title, request.Priority, _clock, request.Description, request.Deadline);

                if (request.Recurrence is not null)
                {
                    task.SetRecurrence(request.Recurrence);
                }

                await SyncTagsAsync(task, request.Tags ?? [], ct);
                await _tasks.AddAsync(task, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                await _tasks.AddHistoryAsync([NewEntry(task.Id, CreateCommand, "Created", null, task.Title)], ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return task;
            },
            cancellationToken);
    }

    public async Task<TaskItem> EditAsync(EditTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var task = await GetAsync(request.Id, cancellationToken);
        var before = TaskSnapshot.From(task);

        task.SetTitle(request.Title);
        task.SetDescription(request.Description);
        task.SetPriority(request.Priority);
        task.SetDeadline(request.Deadline);
        task.SetRecurrence(request.Recurrence);
        await SyncTagsAsync(task, request.Tags, cancellationToken);

        await SaveWithHistoryAsync(task, before, EditCommand, cancellationToken);
        return task;
    }

    public async Task<TaskItem> ChangeStatusAsync(int id, TaskStatus newStatus, CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        var before = TaskSnapshot.From(task);

        task.ChangeStatus(newStatus, _clock);

        await SaveWithHistoryAsync(task, before, ChangeStatusCommand, cancellationToken);
        return task;
    }

    /// <summary>Переоткрывает выполненную или отменённую задачу.</summary>
    public async Task<TaskItem> ReopenAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await GetAsync(id, cancellationToken);
        var before = TaskSnapshot.From(task);

        task.Reopen();

        await SaveWithHistoryAsync(task, before, ReopenCommand, cancellationToken);
        return task;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(id, includeDeleted: false, cancellationToken)
            ?? throw new TaskNotFoundException(id);

        task.Delete();

        await _tasks.AddHistoryAsync([NewEntry(task.Id, DeleteCommand, "IsDeleted", "False", "True")], cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(id, includeDeleted: true, cancellationToken)
            ?? throw new TaskNotFoundException(id);

        task.Restore();

        await _tasks.AddHistoryAsync([NewEntry(task.Id, RestoreCommand, "IsDeleted", "True", "False")], cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(int id, CancellationToken cancellationToken = default) =>
        _tasks.GetHistoryAsync(id, cancellationToken);

    private async Task SaveWithHistoryAsync(TaskItem task, TaskSnapshot before, string command, CancellationToken cancellationToken)
    {
        var changes = TaskSnapshot.Diff(before, TaskSnapshot.From(task));

        if (changes.Count == 0)
        {
            return;
        }

        var entries = changes.Select(c => NewEntry(task.Id, command, c.Field, c.OldValue, c.NewValue));
        await _tasks.AddHistoryAsync(entries, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Приводит набор тегов задачи к списку имён: лишние снимает, недостающие берёт из БД или создаёт.</summary>
    private async Task SyncTagsAsync(TaskItem task, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var wanted = new Dictionary<string, string>();
        foreach (var name in names)
        {
            wanted.TryAdd(Tag.Normalize(name), name);
        }

        foreach (var tag in task.Tags.Where(t => !wanted.ContainsKey(t.NormalizedName)).ToList())
        {
            task.RemoveTag(tag);
        }

        var present = task.Tags.Select(t => t.NormalizedName).ToHashSet();
        foreach (var (normalized, name) in wanted)
        {
            if (present.Contains(normalized))
            {
                continue;
            }

            var tag = await _tags.GetByNameAsync(name, cancellationToken);
            if (tag is null)
            {
                tag = new Tag(name);
                await _tags.AddAsync(tag, cancellationToken);
            }

            task.AddTag(tag);
        }
    }

    private TaskHistoryEntry NewEntry(int taskId, string command, string field, string? oldValue, string? newValue) =>
        new(taskId, _clock.GetUtcNow(), command, field, oldValue, newValue);
}
