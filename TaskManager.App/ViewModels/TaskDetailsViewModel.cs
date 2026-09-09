using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Querying;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>
/// Карточка задачи: сведения, действия над статусом, подзадачи деревом, зависимости, теги и история.
/// Параметр страницы: <c>id</c>.
/// </summary>
public sealed partial class TaskDetailsViewModel : ViewModelBase, IQueryAttributable
{
    private readonly ServiceRunner _runner;
    private readonly TimeProvider _clock;
    private readonly INavigationService _navigation;

    private int _taskId;
    private int? _parentId;

    // Версия задачи на момент чтения: уходит в сервис вместе с действием, чтобы тот заметил чужие правки.
    private long _rowVersion;
    private bool _hasSubtasksForDelete;
    private bool _loadedOnce;

    public TaskDetailsViewModel(
        ServiceRunner runner,
        TimeProvider clock,
        IDialogService dialogs,
        INavigationService navigation,
        ILogger<TaskDetailsViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
        _clock = clock;
        _navigation = navigation;
    }

    public ObservableCollection<StatusActionViewModel> StatusActions { get; } = [];

    public ObservableCollection<SubtaskRowViewModel> Subtasks { get; } = [];

    public ObservableCollection<DependencyRowViewModel> DependsOn { get; } = [];

    public ObservableCollection<DependencyRowViewModel> Blocks { get; } = [];

    public ObservableCollection<HistoryRowViewModel> History { get; } = [];

    public ObservableCollection<FilterOption<int>> DependencyCandidates { get; } = [];

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private Color statusColor = Colors.Gray;

    [ObservableProperty]
    private string priorityText = string.Empty;

    [ObservableProperty]
    private Color priorityColor = Colors.Gray;

    [ObservableProperty]
    private string deadlineText = string.Empty;

    [ObservableProperty]
    private bool isOverdue;

    [ObservableProperty]
    private string tagsText = string.Empty;

    [ObservableProperty]
    private string recurrenceText = string.Empty;

    [ObservableProperty]
    private string progressText = string.Empty;

    [ObservableProperty]
    private string parentText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddDependencyCommand))]
    private FilterOption<int>? selectedCandidate;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasTags => TagsText.Length > 0;

    public bool HasRecurrence => RecurrenceText.Length > 0;

    public bool HasProgress => ProgressText.Length > 0;

    public bool HasParent => ParentText.Length > 0;

    public bool HasSubtasks => Subtasks.Count > 0;

    public bool HasDependsOn => DependsOn.Count > 0;

    public bool HasNoDependsOn => DependsOn.Count == 0;

    public bool HasBlocks => Blocks.Count > 0;

    public bool HasHistory => History.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _taskId = query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id) ? id : 0;
    }

    /// <summary>Загружает карточку. Вызывается при каждом показе страницы: после правки в форме данные нужно обновить.</summary>
    public async Task LoadAsync()
    {
        var ok = await RunAsync(LoadCoreAsync, "Не удалось загрузить задачу");

        // Задачи нет (удалена, неверный номер): показывать пустую карточку бессмысленно.
        if (!ok && !_loadedOnce)
        {
            await _navigation.GoBackAsync();
        }
    }

    [RelayCommand]
    private Task EditAsync() => _navigation.GoToAsync($"taskedit?id={_taskId}");

    [RelayCommand]
    private Task AddSubtaskAsync() => _navigation.GoToAsync($"taskedit?id=0&parentId={_taskId}");

    [RelayCommand]
    private async Task OpenParentAsync()
    {
        if (_parentId is { } parentId)
        {
            await _navigation.GoToAsync($"taskdetails?id={parentId}");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var message = _hasSubtasksForDelete
            ? $"Удалить задачу «{Title}» вместе со всеми её подзадачами?"
            : $"Удалить задачу «{Title}»?";

        if (!await Dialogs.ConfirmAsync("Удаление", message, "Удалить", "Отмена"))
        {
            return;
        }

        var deleted = await RunAsync(
            async () =>
            {
                try
                {
                    await _runner.RunAsync(sp => sp.GetRequiredService<TaskService>().DeleteAsync(_taskId, _rowVersion));
                }
                catch (ConcurrencyConflictException)
                {
                    await HandleConflictAsync();
                    throw new OperationCanceledException();
                }
            },
            "Не удалось удалить задачу");

        if (deleted)
        {
            await _navigation.GoBackAsync();
        }
    }

    private bool CanAddDependency() => SelectedCandidate is not null;

    [RelayCommand(CanExecute = nameof(CanAddDependency))]
    private async Task AddDependencyAsync()
    {
        if (SelectedCandidate is not { } candidate)
        {
            return;
        }

        await MutateAsync(
            service => service.AddDependencyAsync(_taskId, candidate.Value),
            "Не удалось добавить зависимость");
    }

    /// <summary>Выполняет изменение задачи и перезагружает карточку. Конфликт версий объясняется и обновляет данные.</summary>
    private Task<bool> MutateAsync(Func<TaskService, Task> operation, string errorTitle) =>
        RunAsync(
            async () =>
            {
                try
                {
                    await _runner.RunAsync(sp => operation(sp.GetRequiredService<TaskService>()));
                }
                catch (ConcurrencyConflictException)
                {
                    await HandleConflictAsync();
                    return;
                }

                await LoadCoreAsync();
            },
            errorTitle);

    private async Task HandleConflictAsync()
    {
        await Dialogs.AlertAsync(
            "Задача изменена",
            "Эту задачу уже изменили в другом месте. Показаны актуальные данные, повторите действие.");
        await LoadCoreAsync();
    }

    private async Task LoadCoreAsync()
    {
        var data = await _runner.RunAsync(async sp =>
        {
            var service = sp.GetRequiredService<TaskService>();
            var task = await service.GetAsync(_taskId);
            var subtasks = await service.GetSubtaskTreeAsync(_taskId);
            var history = await service.GetHistoryAsync(_taskId);

            // Кандидаты в зависимости: открытые задачи по алфавиту (до 200).
            var candidates = (await service.SearchAsync(
                new TaskFilter(Statuses: [TaskStatus.Todo, TaskStatus.InProgress, TaskStatus.Blocked]),
                [new TaskSort(TaskSortField.Title)],
                1,
                TaskQuery.MaxPageSize)).Items;

            return (task, subtasks, history, candidates);
        });

        Apply(data.task, data.subtasks, data.history, data.candidates);
        _loadedOnce = true;
    }

    private void Apply(
        TaskItem task,
        IReadOnlyList<SubtaskEntry> subtasks,
        IReadOnlyList<TaskHistoryEntry> history,
        IReadOnlyList<TaskItem> candidates)
    {
        var zone = _clock.LocalTimeZone;
        var now = _clock.GetUtcNow();
        var finished = task.Status is TaskStatus.Done or TaskStatus.Cancelled;

        _rowVersion = task.RowVersion;
        _parentId = task.ParentId;
        _hasSubtasksForDelete = subtasks.Count > 0;

        Title = task.Title;
        Description = task.Description;
        StatusText = DisplayNames.StatusName(task.Status);
        StatusColor = AppColors.For(task.Status);
        PriorityText = DisplayNames.PriorityName(task.Priority);
        PriorityColor = AppColors.For(task.Priority);

        IsOverdue = !finished && task.Deadline is { } deadline && deadline < now;
        DeadlineText = task.Deadline is { } d
            ? "Дедлайн: " + TimeZoneInfo.ConvertTime(d, zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
            : "Дедлайна нет";

        TagsText = string.Join("  ", task.Tags.Select(t => "#" + t.Name).Order(StringComparer.CurrentCultureIgnoreCase));
        RecurrenceText = task.Recurrence is { } rule ? Describe(rule) : string.Empty;

        var progress = task.GetProgress();
        ProgressText = progress.HasSubtasks ? $"Подзадачи: {progress} ({progress.Percent}%)" : string.Empty;
        ParentText = task.Parent is { } parent ? $"Подзадача задачи: {parent.Title}" : string.Empty;

        ReplaceStatusActions(task.Status);

        Replace(Subtasks, subtasks.Select(e => new SubtaskRowViewModel(e, OpenCommand(e.Task.Id))));

        var dependsOnRows = task.Dependencies
            .Where(x => x.DependsOnTask is not null)
            .Select(x => new DependencyRowViewModel(
                x.DependsOnTask!,
                x.DependsOnTask!.Status != TaskStatus.Done,
                OpenCommand(x.DependsOnTaskId),
                new AsyncRelayCommand(() => MutateAsync(
                    service => service.RemoveDependencyAsync(_taskId, x.DependsOnTaskId),
                    "Не удалось убрать зависимость"))));
        Replace(DependsOn, dependsOnRows);

        var blocksRows = task.Dependents
            .Where(x => x.Task is not null)
            .Select(x => new DependencyRowViewModel(x.Task!, isUnmet: false, OpenCommand(x.TaskId), removeCommand: null));
        Replace(Blocks, blocksRows);

        // История: новые изменения сверху.
        Replace(History, history.Reverse().Select(h => new HistoryRowViewModel(HistoryFormatter.When(h, zone), HistoryFormatter.Text(h, zone))));

        // В кандидаты не попадают сама задача и те, от кого она уже зависит.
        var already = task.Dependencies.Select(x => x.DependsOnTaskId).ToHashSet();
        Replace(
            DependencyCandidates,
            candidates.Where(c => c.Id != task.Id && !already.Contains(c.Id)).Select(c => new FilterOption<int>($"#{c.Id} {c.Title}", c.Id)));
        SelectedCandidate = null;

        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(HasRecurrence));
        OnPropertyChanged(nameof(HasProgress));
        OnPropertyChanged(nameof(HasParent));
        OnPropertyChanged(nameof(HasSubtasks));
        OnPropertyChanged(nameof(HasDependsOn));
        OnPropertyChanged(nameof(HasNoDependsOn));
        OnPropertyChanged(nameof(HasBlocks));
        OnPropertyChanged(nameof(HasHistory));
    }

    /// <summary>Кнопки по таблице переходов автомата: показываем только то, что разрешено из текущего статуса.</summary>
    private void ReplaceStatusActions(TaskStatus status)
    {
        var actions = new List<StatusActionViewModel>();

        foreach (var target in TaskStateMachine.GetAllowedTransitions(status))
        {
            var title = target switch
            {
                TaskStatus.InProgress => "▶ В работу",
                TaskStatus.Done => "✔ Выполнить",
                TaskStatus.Blocked => "⏸ Заблокировать",
                TaskStatus.Todo => "↩ К выполнению",
                TaskStatus.Cancelled => "✖ Отменить",
                _ => DisplayNames.StatusName(target),
            };

            var to = target;
            actions.Add(new StatusActionViewModel(
                title,
                new AsyncRelayCommand(() => MutateAsync(
                    service => service.ChangeStatusAsync(_taskId, to, _rowVersion),
                    "Не удалось сменить статус"))));
        }

        // Выполненную и отменённую задачу можно только переоткрыть.
        if (TaskStateMachine.TryGetReopenTarget(status, out _))
        {
            actions.Add(new StatusActionViewModel(
                "⟲ Переоткрыть",
                new AsyncRelayCommand(() => MutateAsync(
                    service => service.ReopenAsync(_taskId, _rowVersion),
                    "Не удалось переоткрыть задачу"))));
        }

        Replace(StatusActions, actions);
    }

    private AsyncRelayCommand OpenCommand(int taskId) =>
        new(() => RunAsync(() => _navigation.GoToAsync($"taskdetails?id={taskId}")));

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private static string Describe(RecurrenceRule rule)
    {
        var unit = rule.Frequency switch
        {
            RecurrenceFrequency.Daily => "дн.",
            RecurrenceFrequency.Weekly => "нед.",
            RecurrenceFrequency.Monthly => "мес.",
            _ => "г.",
        };

        var text = $"Повторяется раз в {rule.Interval} {unit}";

        if (rule.WeekDays != WeekDays.None)
        {
            var days = new (WeekDays Flag, string Name)[]
            {
                (WeekDays.Monday, "пн"), (WeekDays.Tuesday, "вт"), (WeekDays.Wednesday, "ср"), (WeekDays.Thursday, "чт"),
                (WeekDays.Friday, "пт"), (WeekDays.Saturday, "сб"), (WeekDays.Sunday, "вс"),
            };
            text += " (" + string.Join(", ", days.Where(x => rule.WeekDays.HasFlag(x.Flag)).Select(x => x.Name)) + ")";
        }

        return text + (rule.Mode == RecurrenceMode.ByCalendar ? ", по календарю" : ", после выполнения");
    }
}
