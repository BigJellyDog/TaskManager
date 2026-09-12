using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Controls;
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

    /// <summary>Остальные действия: прочие переходы статуса, «Изменить», «Подзадача».</summary>
    public ObservableCollection<ActionButtonViewModel> SecondaryActions { get; } = [];

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
    private string statusIcon = "circle";

    [ObservableProperty]
    private int priorityLevel = 1;

    /// <summary>Дедлайн для строки сведений: «до 05.11.2026 12:00 · осталось 20 ч» (пусто, если дедлайна нет).</summary>
    [ObservableProperty]
    private string deadlineChipText = string.Empty;

    [ObservableProperty]
    private string deadlineValueText = string.Empty;

    [ObservableProperty]
    private string recurrenceValueText = "Нет";

    [ObservableProperty]
    private string createdText = string.Empty;

    [ObservableProperty]
    private string parentTitle = string.Empty;

    [ObservableProperty]
    private string subtasksCountText = string.Empty;

    [ObservableProperty]
    private double subtasksFraction;

    /// <summary>Главное действие над статусом (одно на экран) и причина, по которой оно сейчас недоступно.</summary>
    [ObservableProperty]
    private ActionButtonViewModel? primaryAction;

    [ObservableProperty]
    private string primaryReason = string.Empty;

    [ObservableProperty]
    private ActionButtonViewModel? cancelAction;

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

    public bool HasPrimaryReason => PrimaryReason.Length > 0;

    public bool HasPrimaryAction => PrimaryAction is not null;

    /// <summary>Ширина заполненной части полосы прогресса подзадач (вся полоса 90).</summary>
    public double SubtasksBarWidth => 90 * SubtasksFraction;

    public bool HasCancelAction => CancelAction is not null;

    /// <summary>Цвет статуса и приоритета с 10% прозрачностью: подложка «пилюли» в сведениях.</summary>
    public Color StatusTint => StatusColor.WithAlpha(0.1f);

    public Color PriorityTint => PriorityColor.WithAlpha(0.1f);

    /// <summary>Подпись выпадающего списка зависимостей: выбранная задача или подсказка.</summary>
    public string CandidateText => SelectedCandidate?.Title ?? "Добавить зависимость…";

    public bool HasDeadlineChip => DeadlineChipText.Length > 0;

    public bool HasSubtasksCount => SubtasksCountText.Length > 0;

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

    /// <summary>Флажок подзадачи: выполнить или вернуть в работу.</summary>
    [RelayCommand]
    private async Task ToggleSubtaskAsync(SubtaskRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        await MutateAsync(
            service => row.IsClosed
                ? service.ReopenAsync(row.TaskId, row.RowVersion)
                : service.ChangeStatusAsync(row.TaskId, TaskStatus.Done, row.RowVersion),
            "Не удалось изменить статус подзадачи");
    }

    [RelayCommand]
    private Task BackToTasksAsync() => _navigation.GoToAsync("//tasks");

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

    partial void OnSelectedCandidateChanged(FilterOption<int>? value) => OnPropertyChanged(nameof(CandidateText));

    partial void OnCancelActionChanged(ActionButtonViewModel? value) => OnPropertyChanged(nameof(HasCancelAction));

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

        StatusIcon = StatusIcons.For(task.Status);
        PriorityLevel = task.Priority switch
        {
            Priority.Critical => 4,
            Priority.High => 3,
            Priority.Medium => 2,
            _ => 1,
        };

        var deadlineLocal = task.Deadline is { } due ? TimeZoneInfo.ConvertTime(due, zone) : (DateTimeOffset?)null;
        DeadlineValueText = deadlineLocal?.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? "Нет";
        DeadlineChipText = deadlineLocal is { } local
            ? "до " + local.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + RemainingText(task, finished, now)
            : string.Empty;
        RecurrenceValueText = task.Recurrence is { } recurrence ? Describe(recurrence).Replace("Повторяется ", string.Empty, StringComparison.Ordinal) : "Нет";
        CreatedText = TimeZoneInfo.ConvertTime(task.CreatedAt, zone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        ParentTitle = task.Parent?.Title ?? string.Empty;
        OnPropertyChanged(nameof(HasDeadlineChip));

        var directChildren = subtasks.Where(e => e.Level == 1).ToList();
        var doneChildren = directChildren.Count(e => e.Task.Status == TaskStatus.Done);
        SubtasksCountText = directChildren.Count > 0 ? $"{doneChildren} из {directChildren.Count}" : string.Empty;
        SubtasksFraction = directChildren.Count > 0 ? (double)doneChildren / directChildren.Count : 0;
        OnPropertyChanged(nameof(HasSubtasksCount));
        OnPropertyChanged(nameof(SubtasksBarWidth));

        BuildActions(task, subtasks);

        Replace(Subtasks, subtasks.Select(e => new SubtaskRowViewModel(e, zone, OpenCommand(e.Task.Id))));

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

    private static string RemainingText(TaskItem task, bool finished, DateTimeOffset now)
    {
        if (finished || task.Deadline is not { } deadline)
        {
            return string.Empty;
        }

        var left = deadline - now;
        if (left <= TimeSpan.Zero)
        {
            return " · просрочено";
        }

        return left.TotalHours < 48
            ? $" · осталось {Math.Max(1, (int)Math.Round(left.TotalHours))} ч"
            : $" · осталось {(int)left.TotalDays} д";
    }

    /// <summary>
    /// Кнопки по таблице переходов автомата. Главное действие одно: из «К выполнению» это «В работу», из «В работе»
    /// «Выполнить», из завершённых «Переоткрыть». Если оно сейчас недоступно (не завершены подзадачи или зависимости),
    /// кнопка остаётся серой, а причина написана под ней.
    /// </summary>
    private void BuildActions(TaskItem task, IReadOnlyList<SubtaskEntry> subtasks)
    {
        var status = task.Status;
        var allowed = TaskStateMachine.GetAllowedTransitions(status);
        var reopen = TaskStateMachine.TryGetReopenTarget(status, out _);

        ActionButtonViewModel Move(string title, string icon, TaskStatus to, ButtonVariant variant = ButtonVariant.Secondary, bool enabled = true) =>
            new(title, icon, new AsyncRelayCommand(() => MutateAsync(
                service => service.ChangeStatusAsync(_taskId, to, _rowVersion),
                "Не удалось сменить статус")), variant, enabled);

        // Причина, по которой нельзя выполнить.
        var directChildren = subtasks.Where(e => e.Level == 1).ToList();
        var openChildren = directChildren.Count(e => e.Task.Status is not (TaskStatus.Done or TaskStatus.Cancelled));
        var unmet = task.Dependencies.Count(x => x.DependsOnTask is { Status: not TaskStatus.Done });
        var reason = openChildren > 0
            ? $"Не завершены подзадачи ({directChildren.Count(e => e.Task.Status == TaskStatus.Done)}/{directChildren.Count})"
            : unmet > 0 ? "Есть невыполненные зависимости" : string.Empty;

        ActionButtonViewModel? primary = null;
        if (allowed.Contains(TaskStatus.Done))
        {
            primary = Move("Выполнить", "check", TaskStatus.Done, ButtonVariant.Primary, reason.Length == 0);
        }
        else if (allowed.Contains(TaskStatus.InProgress))
        {
            primary = Move("В работу", "half", TaskStatus.InProgress, ButtonVariant.Primary);
            reason = string.Empty;
        }
        else if (reopen)
        {
            primary = new ActionButtonViewModel(
                "Переоткрыть",
                "undo",
                new AsyncRelayCommand(() => MutateAsync(service => service.ReopenAsync(_taskId, _rowVersion), "Не удалось переоткрыть задачу")),
                ButtonVariant.Primary);
            reason = string.Empty;
        }
        else
        {
            reason = string.Empty;
        }

        PrimaryAction = primary;
        PrimaryReason = reason;
        OnPropertyChanged(nameof(HasPrimaryReason));
        OnPropertyChanged(nameof(HasPrimaryAction));
        OnPropertyChanged(nameof(StatusTint));
        OnPropertyChanged(nameof(PriorityTint));

        var secondary = new List<ActionButtonViewModel>();
        if (allowed.Contains(TaskStatus.Blocked))
        {
            secondary.Add(Move("Заблокировать", "blocked", TaskStatus.Blocked));
        }

        if (allowed.Contains(TaskStatus.Todo))
        {
            secondary.Add(Move("К выполнению", "undo", TaskStatus.Todo));
        }

        if (allowed.Contains(TaskStatus.InProgress) && primary?.Title != "В работу")
        {
            secondary.Add(Move("В работу", "half", TaskStatus.InProgress));
        }

        secondary.Add(new ActionButtonViewModel("Изменить", "edit", EditCommand));
        secondary.Add(new ActionButtonViewModel("Подзадача", "subtask", AddSubtaskCommand));
        Replace(SecondaryActions, secondary);

        OnPropertyChanged(nameof(HasCancelAction));
        CancelAction = allowed.Contains(TaskStatus.Cancelled) ? Move("Отменить задачу", "x", TaskStatus.Cancelled, ButtonVariant.Ghost) : null;
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
