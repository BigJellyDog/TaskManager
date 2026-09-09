using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>
/// Форма создания и редактирования задачи. Параметры страницы: <c>id</c> (0 или нет: новая задача)
/// и <c>parentId</c> (создание подзадачи).
/// </summary>
public sealed partial class TaskEditViewModel : ViewModelBase, IQueryAttributable
{
    private readonly ServiceRunner _runner;
    private readonly TimeProvider _clock;
    private readonly INavigationService _navigation;

    private int _taskId;
    private int? _parentId;

    // Версия задачи на момент чтения: уходит в сервис, чтобы тот заметил чужие правки.
    private long _loadedRowVersion;

    // Правило, прочитанное из базы: если пользователь его не менял, сохраняем его же (вместе с якорем).
    private RecurrenceRule? _originalRule;

    public TaskEditViewModel(
        ServiceRunner runner,
        TimeProvider clock,
        IDialogService dialogs,
        INavigationService navigation,
        ILogger<TaskEditViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
        _clock = clock;
        _navigation = navigation;

        PriorityOptions =
        [
            .. Enum.GetValues<Priority>().Reverse().Select(p => new FilterOption<Priority>(DisplayNames.PriorityName(p), p)),
        ];
        RecurrenceOptions =
        [
            new FilterOption<RecurrenceFrequency?>("Не повторять", null),
            new FilterOption<RecurrenceFrequency?>("Каждый день", RecurrenceFrequency.Daily),
            new FilterOption<RecurrenceFrequency?>("Каждую неделю", RecurrenceFrequency.Weekly),
            new FilterOption<RecurrenceFrequency?>("Каждый месяц", RecurrenceFrequency.Monthly),
            new FilterOption<RecurrenceFrequency?>("Каждый год", RecurrenceFrequency.Yearly),
        ];
        ModeOptions =
        [
            new FilterOption<RecurrenceMode>("Следующая после выполнения", RecurrenceMode.AfterCompletion),
            new FilterOption<RecurrenceMode>("По календарю (даже если не закрыта)", RecurrenceMode.ByCalendar),
        ];

        SelectedPriority = PriorityOptions.Single(o => o.Value == Priority.Medium);
        SelectedRecurrence = RecurrenceOptions[0];
        SelectedMode = ModeOptions[0];
        DeadlineDate = _clock.GetLocalNow().Date.AddDays(1);
        DeadlineTime = new TimeSpan(18, 0, 0);
    }

    public IReadOnlyList<FilterOption<Priority>> PriorityOptions { get; }

    public IReadOnlyList<FilterOption<RecurrenceFrequency?>> RecurrenceOptions { get; }

    public IReadOnlyList<FilterOption<RecurrenceMode>> ModeOptions { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? title;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private FilterOption<Priority>? selectedPriority;

    [ObservableProperty]
    private bool hasDeadline;

    [ObservableProperty]
    private DateTime deadlineDate;

    [ObservableProperty]
    private TimeSpan deadlineTime;

    [ObservableProperty]
    private string? tagsText;

    [ObservableProperty]
    private string existingTagsHint = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecurrence))]
    [NotifyPropertyChangedFor(nameof(IsWeekly))]
    [NotifyPropertyChangedFor(nameof(IntervalText))]
    private FilterOption<RecurrenceFrequency?>? selectedRecurrence;

    [ObservableProperty]
    private FilterOption<RecurrenceMode>? selectedMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IntervalText))]
    private double intervalValue = 1;

    [ObservableProperty]
    private bool monday;

    [ObservableProperty]
    private bool tuesday;

    [ObservableProperty]
    private bool wednesday;

    [ObservableProperty]
    private bool thursday;

    [ObservableProperty]
    private bool friday;

    [ObservableProperty]
    private bool saturday;

    [ObservableProperty]
    private bool sunday;

    [ObservableProperty]
    private string pageTitle = "Новая задача";

    [ObservableProperty]
    private string parentText = string.Empty;

    public bool HasRecurrence => SelectedRecurrence?.Value is not null;

    public bool IsWeekly => SelectedRecurrence?.Value == RecurrenceFrequency.Weekly;

    public bool HasParent => ParentText.Length > 0;

    public string IntervalText
    {
        get
        {
            var unit = SelectedRecurrence?.Value switch
            {
                RecurrenceFrequency.Daily => "дн.",
                RecurrenceFrequency.Weekly => "нед.",
                RecurrenceFrequency.Monthly => "мес.",
                RecurrenceFrequency.Yearly => "г.",
                _ => string.Empty,
            };

            return $"Повторять раз в {(int)IntervalValue} {unit}";
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _taskId = ReadInt(query, "id") ?? 0;
        _parentId = ReadInt(query, "parentId");
    }

    /// <summary>Загружает данные формы: список существующих тегов, а при редактировании ещё и саму задачу.</summary>
    public Task LoadAsync() => RunAsync(LoadCoreAsync, "Не удалось загрузить задачу");

    partial void OnParentTextChanged(string value) => OnPropertyChanged(nameof(HasParent));

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await RunAsync(SaveCoreAsync, "Не удалось сохранить задачу");

    [RelayCommand]
    private Task CancelAsync() => _navigation.GoBackAsync();

    private static int? ReadInt(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var number) && number > 0
            ? number
            : null;

    private async Task LoadCoreAsync()
    {
        var tags = await _runner.RunAsync(sp => sp.GetRequiredService<ITagRepository>().ListAsync());
        ExistingTagsHint = tags.Count == 0 ? string.Empty : "Уже есть: " + string.Join(", ", tags.Select(t => t.Name));

        if (_taskId > 0)
        {
            var task = await _runner.RunAsync(sp => sp.GetRequiredService<TaskService>().GetAsync(_taskId));
            Fill(task);
            return;
        }

        PageTitle = "Новая задача";

        if (_parentId is { } parentId)
        {
            var parent = await _runner.RunAsync(sp => sp.GetRequiredService<TaskService>().GetAsync(parentId));
            ParentText = $"Подзадача задачи: {parent.Title}";
            PageTitle = "Новая подзадача";
        }
    }

    private void Fill(TaskItem task)
    {
        PageTitle = $"Редактирование задачи #{task.Id}";
        _loadedRowVersion = task.RowVersion;
        _parentId = task.ParentId;
        ParentText = task.Parent is { } parent ? $"Подзадача задачи: {parent.Title}" : string.Empty;

        Title = task.Title;
        Description = task.Description;
        SelectedPriority = PriorityOptions.Single(o => o.Value == task.Priority);
        TagsText = string.Join(", ", task.Tags.Select(t => t.Name).Order(StringComparer.CurrentCultureIgnoreCase));

        HasDeadline = task.Deadline is not null;
        if (task.Deadline is { } deadline)
        {
            var local = TimeZoneInfo.ConvertTime(deadline, _clock.LocalTimeZone);
            DeadlineDate = local.Date;
            DeadlineTime = local.TimeOfDay;
        }

        _originalRule = task.Recurrence;
        if (task.Recurrence is { } rule)
        {
            SelectedRecurrence = RecurrenceOptions.Single(o => o.Value == rule.Frequency);
            SelectedMode = ModeOptions.Single(o => o.Value == rule.Mode);
            IntervalValue = rule.Interval;
            Monday = rule.WeekDays.HasFlag(WeekDays.Monday);
            Tuesday = rule.WeekDays.HasFlag(WeekDays.Tuesday);
            Wednesday = rule.WeekDays.HasFlag(WeekDays.Wednesday);
            Thursday = rule.WeekDays.HasFlag(WeekDays.Thursday);
            Friday = rule.WeekDays.HasFlag(WeekDays.Friday);
            Saturday = rule.WeekDays.HasFlag(WeekDays.Saturday);
            Sunday = rule.WeekDays.HasFlag(WeekDays.Sunday);
        }
        else
        {
            SelectedRecurrence = RecurrenceOptions[0];
        }
    }

    private async Task SaveCoreAsync()
    {
        var priority = SelectedPriority?.Value ?? Priority.Medium;
        var deadline = BuildDeadline();
        var tags = ParseTags();
        var rule = BuildRule();

        try
        {
            if (_taskId > 0)
            {
                var request = new EditTaskRequest(
                    _taskId, Title ?? string.Empty, priority, Description, deadline, tags, rule, _loadedRowVersion);
                await _runner.RunAsync(sp => sp.GetRequiredService<TaskService>().EditAsync(request));
            }
            else
            {
                var request = new CreateTaskRequest(Title ?? string.Empty, priority, Description, deadline, tags, rule, _parentId);
                await _runner.RunAsync(sp => sp.GetRequiredService<TaskService>().CreateAsync(request));
            }
        }
        catch (ConcurrencyConflictException)
        {
            // Задачу изменили, пока форма была открыта. Правки в форме потеряются, поэтому спрашиваем.
            var reload = await Dialogs.ConfirmAsync(
                "Задача изменена",
                "Эту задачу уже изменили в другом месте. Загрузить актуальные данные? Несохранённые правки в форме будут потеряны.",
                "Загрузить",
                "Оставить как есть");

            if (reload)
            {
                await LoadCoreAsync();
            }

            return;
        }

        await _navigation.GoBackAsync();
    }

    private DateTimeOffset? BuildDeadline()
    {
        if (!HasDeadline)
        {
            return null;
        }

        var zone = _clock.LocalTimeZone;
        var local = DeadlineDate.Date + DeadlineTime;

        // Весеннего перевода часов такого времени не существует: сдвигаем на час вперёд.
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private List<string> ParseTags() =>
        (TagsText ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private RecurrenceRule? BuildRule()
    {
        if (SelectedRecurrence?.Value is not { } frequency)
        {
            return null;
        }

        var weekDays = WeekDays.None;
        if (frequency == RecurrenceFrequency.Weekly)
        {
            weekDays = (Monday ? WeekDays.Monday : 0)
                | (Tuesday ? WeekDays.Tuesday : 0)
                | (Wednesday ? WeekDays.Wednesday : 0)
                | (Thursday ? WeekDays.Thursday : 0)
                | (Friday ? WeekDays.Friday : 0)
                | (Saturday ? WeekDays.Saturday : 0)
                | (Sunday ? WeekDays.Sunday : 0);
        }

        var mode = SelectedMode?.Value ?? RecurrenceMode.AfterCompletion;
        var interval = (int)IntervalValue;

        // Не менялось: возвращаем прежнее правило вместе с его якорем (иначе после сдвига на 28 февраля забылось бы 31-е).
        if (_originalRule is { } original
            && original.Frequency == frequency
            && original.Interval == interval
            && original.Mode == mode
            && original.WeekDays == weekDays)
        {
            return original;
        }

        return new RecurrenceRule(frequency, interval, mode, weekDays);
    }
}
