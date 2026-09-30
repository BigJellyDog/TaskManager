using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using TaskManager.App.Configuration;
using TaskManager.App.Services;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Querying;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>
/// The task list: filters, search, sorting and paging. Any change of a filter returns to the first
/// page and reloads the list. The text search starts on Enter, not on every letter.
/// </summary>
public sealed partial class TasksViewModel : ViewModelBase
{
    private readonly ServiceRunner _runner;
    private readonly TimeProvider _clock;
    private readonly INavigationService _navigation;
    private readonly UserSettings _settings;

    private int _pageSize => Math.Clamp(_settings.PageSize, 1, TaskQuery.MaxPageSize);

    // While the filter values are changed by the program (reset, loading tags), we do not start a reload on every change.
    private bool _suppressReload;

    private bool _isReloading;
    private bool _reloadRequested;

    // A task was completed from the reminders block: the list is read again.
    private void OnTasksChanged() => MainThread.BeginInvokeOnMainThread(() => _ = ReloadAsync());

    public TasksViewModel(
        ServiceRunner runner,
        TimeProvider clock,
        UserSettings settings,
        IDialogService dialogs,
        INavigationService navigation,
        ILogger<TasksViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
        _clock = clock;
        _navigation = navigation;
        _settings = settings;
        WeakReferenceMessenger.Default.Register<TasksViewModel, TasksChangedMessage>(this, (r, _) => r.OnTasksChanged());
        hideClosed = settings.HideClosed;
        isBoard = settings.DefaultBoard;

        StatusOptions =
        [
            new FilterOption<TaskStatus?>(Loc.T("Все статусы"), null),
            .. Enum.GetValues<TaskStatus>().Select(s => new FilterOption<TaskStatus?>(DisplayNames.StatusName(s), s)),
        ];
        PriorityOptions =
        [
            new FilterOption<Priority?>(Loc.T("Все приоритеты"), null),
            .. Enum.GetValues<Priority>().Reverse().Select(p => new FilterOption<Priority?>(DisplayNames.PriorityName(p), p)),
        ];
        SortOptions =
        [
            new FilterOption<IReadOnlyList<TaskSort>?>(Loc.T("Сначала важные и срочные"), null),
            new FilterOption<IReadOnlyList<TaskSort>?>(Loc.T("Ближайший дедлайн"), [new TaskSort(TaskSortField.Deadline)]),
            new FilterOption<IReadOnlyList<TaskSort>?>(Loc.T("Название А–Я"), [new TaskSort(TaskSortField.Title)]),
            new FilterOption<IReadOnlyList<TaskSort>?>(Loc.T("Недавно созданные"), [new TaskSort(TaskSortField.CreatedAt, Descending: true)]),
            new FilterOption<IReadOnlyList<TaskSort>?>(
                Loc.T("По статусу"),
                [new TaskSort(TaskSortField.Status), new TaskSort(TaskSortField.Priority, Descending: true)]),
        ];
        TagOptions = [AllTags];

        ResetFilterValues();
    }

    private static FilterOption<string?> AllTags => new(Loc.T("Все теги"), null);

    public IReadOnlyList<FilterOption<TaskStatus?>> StatusOptions { get; }

    public IReadOnlyList<FilterOption<Priority?>> PriorityOptions { get; }

    public IReadOnlyList<FilterOption<IReadOnlyList<TaskSort>?>> SortOptions { get; }

    public ObservableCollection<FilterOption<string?>> TagOptions { get; }

    public ObservableCollection<TaskRowViewModel> Tasks { get; } = [];

    /// <summary>The list was replaced with new data. The page scrolls it to the top in response.</summary>
    public event EventHandler? TasksReplaced;

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private FilterOption<TaskStatus?>? selectedStatus;

    [ObservableProperty]
    private FilterOption<Priority?>? selectedPriority;

    [ObservableProperty]
    private FilterOption<string?>? selectedTag;

    [ObservableProperty]
    private FilterOption<IReadOnlyList<TaskSort>?>? selectedSort;

    [ObservableProperty]
    private bool onlyOverdue;

    [ObservableProperty]
    private bool dueToday;

    [ObservableProperty]
    private bool dueThisWeek;

    [ObservableProperty]
    private bool hideClosed;

    /// <summary>The board is shown (columns by status), otherwise the table.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsList))]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private bool isBoard = true;

    public ObservableCollection<TaskRowViewModel> TodoColumn { get; } = [];

    public ObservableCollection<TaskRowViewModel> InProgressColumn { get; } = [];

    public ObservableCollection<TaskRowViewModel> BlockedColumn { get; } = [];

    public ObservableCollection<TaskRowViewModel> DoneColumn { get; } = [];

    [ObservableProperty]
    private int todoCount;

    [ObservableProperty]
    private int inProgressCount;

    [ObservableProperty]
    private int blockedColumnCount;

    [ObservableProperty]
    private int doneCount;

    public bool IsList => !IsBoard;

    public bool HasOverdue => OverdueCount > 0;

    [ObservableProperty]
    private int overdueCount;

    [ObservableProperty]
    private int todayCount;

    [ObservableProperty]
    private int weekCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private int allCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private int blockedCount;

    [ObservableProperty]
    private bool useDeadlineFrom;

    [ObservableProperty]
    private DateTime deadlineFrom;

    [ObservableProperty]
    private bool useDeadlineTo;

    [ObservableProperty]
    private DateTime deadlineTo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int page = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int totalPages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int totalCount;

    /// <summary>The page subtitle: "26 TASKS · 2 OVERDUE · 2 BLOCKED".</summary>
    public string SubtitleText
    {
        get
        {
            var parts = new List<string> { Loc.Count(AllCount, Loc.T("задача|задачи|задач")) };
            if (OverdueCount > 0)
            {
                parts.Add(Loc.F("{0} просрочено", OverdueCount));
            }

            if (BlockedCount > 0)
            {
                parts.Add($"{BlockedCount} {Loc.Plural(BlockedCount, "заблокирована|заблокировано|заблокировано")}");
            }

            if (!IsBoard)
            {
                parts.Clear();
                parts.Add(Loc.Count(AllCount, Loc.T("задача|задачи|задач")));
                parts.Add(Loc.T("сортировка: ") + (SelectedSort?.Title ?? string.Empty).ToLower(Loc.Culture));
            }

            return string.Join(" · ", parts);
        }
    }

    public string OverdueChipText => Loc.F("Просрочено · {0}", OverdueCount);

    public string TodayChipText => Loc.F("Сегодня · {0}", TodayCount);

    public string WeekChipText => Loc.F("Неделя · {0}", WeekCount);

    public string SummaryText => TotalCount == 0 ? Loc.T("Задач не найдено") : Loc.F("Найдено задач: {0}", TotalCount);

    public string PageText => Loc.F("Страница {0} из {1}", Page, Math.Max(TotalPages, 1));

    /// <summary>Called when the page is shown: loads the tags and the list (the data may have changed on other screens).</summary>
    public async Task LoadAsync()
    {
        ApplyListIntent();
        await RunAsync(LoadTagsAsync, Loc.T("Не удалось загрузить теги"));
        await ReloadAsync();
    }

    /// <summary>Opened from the statistics: we show the list with the needed filter instead of the previous one.</summary>
    private void ApplyListIntent()
    {
        if (ListIntent.Take() is not { } intent)
        {
            return;
        }

        IsBoard = false;
        ResetFilterValues();

        _suppressReload = true;
        try
        {
            if (intent == ListIntent.Overdue)
            {
                OnlyOverdue = true;
            }
            else if (intent == ListIntent.Blocked)
            {
                SelectedStatus = StatusOptions.First(o => o.Value == TaskStatus.Blocked);
            }
        }
        finally
        {
            _suppressReload = false;
        }
    }

    // A change of any filter reloads the list from the first page.
    partial void OnSelectedStatusChanged(FilterOption<TaskStatus?>? value) => FilterChanged();

    partial void OnSelectedPriorityChanged(FilterOption<Priority?>? value) => FilterChanged();

    partial void OnSelectedTagChanged(FilterOption<string?>? value) => FilterChanged();

    partial void OnSelectedSortChanged(FilterOption<IReadOnlyList<TaskSort>?>? value)
    {
        OnPropertyChanged(nameof(TitleHeader));
        OnPropertyChanged(nameof(PriorityHeader));
        OnPropertyChanged(nameof(DeadlineHeader));
        FilterChanged();
    }

    // Table column headers: the arrow stands at the column by which the list is currently sorted.
    public string TitleHeader => Loc.T("Название") + Arrow(2);

    public string PriorityHeader => Loc.T("Приоритет") + Arrow(0);

    public string DeadlineHeader => Loc.T("Дедлайн") + Arrow(1);

    private string Arrow(int sortOptionIndex) => ReferenceEquals(SelectedSort, SortOptions[sortOptionIndex]) ? " ↓" : string.Empty;

    /// <summary>A click on a column header: sort by title, priority (taking urgency into account) or deadline.</summary>
    [RelayCommand]
    private void SortBy(string column) =>
        SelectedSort = column switch
        {
            "title" => SortOptions[2],
            "deadline" => SortOptions[1],
            _ => SortOptions[0],
        };

    partial void OnOnlyOverdueChanged(bool value) => FilterChanged();

    partial void OnHideClosedChanged(bool value) => FilterChanged();

    partial void OnIsBoardChanged(bool value) => FilterChanged();

    partial void OnOverdueCountChanged(int value)
    {
        OnPropertyChanged(nameof(OverdueChipText));
        OnPropertyChanged(nameof(HasOverdue));
    }

    partial void OnTodayCountChanged(int value) => OnPropertyChanged(nameof(TodayChipText));

    partial void OnWeekCountChanged(int value) => OnPropertyChanged(nameof(WeekChipText));

    partial void OnDueTodayChanged(bool value) => FilterChanged();

    partial void OnDueThisWeekChanged(bool value) => FilterChanged();

    partial void OnUseDeadlineFromChanged(bool value) => FilterChanged();

    partial void OnUseDeadlineToChanged(bool value) => FilterChanged();

    // We reload on a date only if it takes part in the filter, otherwise choosing a date in a switched-off field does nothing.
    partial void OnDeadlineFromChanged(DateTime value)
    {
        if (UseDeadlineFrom)
        {
            FilterChanged();
        }
    }

    partial void OnDeadlineToChanged(DateTime value)
    {
        if (UseDeadlineTo)
        {
            FilterChanged();
        }
    }

    [RelayCommand]
    private Task NewTaskAsync() => _navigation.GoToAsync("taskedit?id=0");

    /// <summary>Opens the task card.</summary>
    public Task OpenTaskAsync(int id) => _navigation.GoToAsync($"taskdetails?id={id}");

    [RelayCommand]
    private void ShowBoard() => IsBoard = true;

    [RelayCommand]
    private void ShowList() => IsBoard = false;

    [RelayCommand]
    private Task OpenTaskAsync(TaskRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return OpenTaskAsync(row.Id);
    }

    [RelayCommand]
    private void ToggleOverdue() => OnlyOverdue = !OnlyOverdue;

    [RelayCommand]
    private void ToggleToday() => DueToday = !DueToday;

    [RelayCommand]
    private void ToggleWeek() => DueThisWeek = !DueThisWeek;

    [RelayCommand]
    private void ToggleHideClosed() => HideClosed = !HideClosed;

    /// <summary>The check box in a row: complete the task or return it to work. Errors (blocking, subtasks) are shown by RunAsync.</summary>
    [RelayCommand]
    private async Task ToggleDoneAsync(TaskRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var changed = await RunAsync(
            () => _runner.RunAsync(sp => row.IsClosed
                ? sp.GetRequiredService<TaskService>().ReopenAsync(row.Id, row.RowVersion)
                : sp.GetRequiredService<TaskService>().ChangeStatusAsync(row.Id, TaskStatus.Done, expectedRowVersion: row.RowVersion)),
            Loc.T("Не удалось изменить статус"));

        // On an error the row stayed as it was, but we refresh the list anyway: the task may have been changed elsewhere.
        _ = changed;
        await ReloadAsync();
    }

    [RelayCommand]
    private Task SearchAsync()
    {
        Page = 1;
        return ReloadAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    [RelayCommand]
    private Task ResetFiltersAsync()
    {
        ResetFilterValues();
        Page = 1;
        return ReloadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousPageAsync()
    {
        Page--;
        return ReloadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextPageAsync()
    {
        Page++;
        return ReloadAsync();
    }

    private bool CanGoPrevious() => Page > 1;

    private bool CanGoNext() => Page < TotalPages;

    private void FilterChanged()
    {
        if (_suppressReload)
        {
            return;
        }

        Page = 1;
        _ = ReloadAsync();
    }

    private void ResetFilterValues()
    {
        _suppressReload = true;
        try
        {
            SearchText = null;
            SelectedStatus = StatusOptions[0];
            SelectedPriority = PriorityOptions[0];
            SelectedTag = TagOptions.Count > 0 ? TagOptions[0] : AllTags;
            SelectedSort = SortOptions[0];
            OnlyOverdue = false;
            HideClosed = _settings.HideClosed;
            DueToday = false;
            DueThisWeek = false;
            UseDeadlineFrom = false;
            UseDeadlineTo = false;
            DeadlineFrom = _clock.GetLocalNow().Date;
            DeadlineTo = _clock.GetLocalNow().Date.AddDays(7);
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _runner.RunAsync(sp => sp.GetRequiredService<ITagRepository>().ListAsync());
        var selectedName = SelectedTag?.Value;

        // The tag list did not change: we do not touch the drop-down list, otherwise it is rebuilt on every visit to the tab.
        if (TagOptions.Count == tags.Count + 1 && tags.Select(t => t.Name).SequenceEqual(TagOptions.Skip(1).Select(o => o.Title)))
        {
            return;
        }

        _suppressReload = true;
        try
        {
            TagOptions.Clear();
            TagOptions.Add(AllTags);
            foreach (var tag in tags)
            {
                TagOptions.Add(new FilterOption<string?>(tag.Name, tag.Name));
            }

            // The selected tag stays selected if it still exists (for example it was not deleted).
            SelectedTag = TagOptions.FirstOrDefault(o => o.Value == selectedName) ?? AllTags;
        }
        finally
        {
            _suppressReload = false;
        }
    }

    /// <summary>
    /// Reloading the list. If the filter changed once more during loading, we load again afterwards:
    /// without this the last change of the filter would be lost.
    /// </summary>
    private async Task ReloadAsync()
    {
        if (_isReloading)
        {
            _reloadRequested = true;
            return;
        }

        _isReloading = true;
        try
        {
            do
            {
                _reloadRequested = false;
                await RunAsync(LoadPageAsync, Loc.T("Не удалось загрузить задачи"));
            }
            while (_reloadRequested);
        }
        finally
        {
            _isReloading = false;
        }
    }

    private async Task LoadPageAsync()
    {
        var now = _clock.GetUtcNow();
        var zone = _clock.LocalTimeZone;
        var changed = false;

        // We load only what is visible: the columns on the board, one page in the list.
        if (IsBoard)
        {
            changed |= await LoadBoardAsync(now, zone);
        }
        else
        {
            var result = await FetchAsync(Page);

            // There are fewer pages now (for example tasks were deleted on another screen): we go to the last one.
            if (result.Items.Count == 0 && result.TotalCount > 0 && Page > result.TotalPages)
            {
                Page = result.TotalPages;
                result = await FetchAsync(Page);
            }

            changed |= Sync(Tasks, result.Items.Select(t => new TaskRowViewModel(t, now, zone)).ToList());
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
        }

        await LoadCountsAsync();

        if (changed)
        {
            TasksReplaced?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Replaces the content of the list only if it changed. Returns whether it changed.</summary>
    private static bool Sync(ObservableCollection<TaskRowViewModel> target, List<TaskRowViewModel> fresh)
    {
        if (target.Count == fresh.Count && target.Zip(fresh).All(pair => pair.First.Signature == pair.Second.Signature))
        {
            return false;
        }

        target.Clear();
        foreach (var row in fresh)
        {
            target.Add(row);
        }

        return true;
    }

    /// <summary>
    /// The board: four columns by status. The search, tags and quick filters work here too, but the status filter does not:
    /// the status is set by the column itself. Cancelled tasks are not shown on the board. The columns are read at the same time.
    /// </summary>
    private async Task<bool> LoadBoardAsync(DateTimeOffset now, TimeZoneInfo zone)
    {
        Task<PagedResult<TaskItem>> Fetch(TaskStatus status)
        {
            var filter = BuildFilter() with { Statuses = [status] };
            var sort = SelectedSort?.Value;
            return _runner.RunAsync(sp =>
                sp.GetRequiredService<TaskService>().SearchAsync(filter, sort, 1, TaskQuery.MaxPageSize));
        }

        var todo = Fetch(TaskStatus.Todo);
        var progress = Fetch(TaskStatus.InProgress);
        var blocked = Fetch(TaskStatus.Blocked);
        var done = Fetch(TaskStatus.Done);
        await Task.WhenAll(todo, progress, blocked, done);

        List<TaskRowViewModel> Rows(PagedResult<TaskItem> result) => result.Items.Select(t => new TaskRowViewModel(t, now, zone)).ToList();

        var changed = Sync(TodoColumn, Rows(todo.Result));
        changed |= Sync(InProgressColumn, Rows(progress.Result));
        changed |= Sync(BlockedColumn, Rows(blocked.Result));
        changed |= Sync(DoneColumn, Rows(done.Result));

        TodoCount = todo.Result.TotalCount;
        InProgressCount = progress.Result.TotalCount;
        BlockedColumnCount = blocked.Result.TotalCount;
        DoneCount = done.Result.TotalCount;
        return changed;
    }

    /// <summary>The counters of the chips and the subtitle. Counted without the other filters: this is the size of the selection, not of its visible part.</summary>
    private async Task LoadCountsAsync()
    {
        // All five counters in one dependency scope: one database, one context, without five re-creations.
        var counts = await _runner.RunAsync(async sp =>
        {
            var service = sp.GetRequiredService<TaskService>();

            Task<int> CountAsync(TaskFilter filter) => service.CountAsync(filter);

            return (
                Overdue: await CountAsync(new TaskFilter(OnlyOverdue: true)),
                Today: await CountAsync(new TaskFilter(DueToday: true)),
                Week: await CountAsync(new TaskFilter(DueThisWeek: true)),
                Blocked: await CountAsync(new TaskFilter(Statuses: [TaskStatus.Blocked])),
                All: await CountAsync(new TaskFilter()));
        });

        OverdueCount = counts.Overdue;
        TodayCount = counts.Today;
        WeekCount = counts.Week;
        BlockedCount = counts.Blocked;
        AllCount = counts.All;
    }

    private async Task<PagedResult<TaskItem>> FetchAsync(int pageNumber)
    {
        var filter = BuildFilter();
        var sort = SelectedSort?.Value;

        return await _runner.RunAsync(sp =>
            sp.GetRequiredService<TaskService>().SearchAsync(filter, sort, pageNumber, _pageSize));
    }

    private TaskFilter BuildFilter()
    {
        var zone = _clock.LocalTimeZone;

        DateTimeOffset StartOfDay(DateTime date) => new(date.Date, zone.GetUtcOffset(date.Date));

        // The range is inclusive: the "to" date covers the whole day up to the last tick.
        DateTimeOffset? from = UseDeadlineFrom ? StartOfDay(DeadlineFrom) : null;
        DateTimeOffset? to = UseDeadlineTo ? StartOfDay(DeadlineTo.AddDays(1)).AddTicks(-1) : null;

        return new TaskFilter(
            Statuses: SelectedStatus?.Value is { } status
                ? [status]
                : HideClosed ? [TaskStatus.Todo, TaskStatus.InProgress, TaskStatus.Blocked] : null,
            Priority: SelectedPriority?.Value,
            Tag: SelectedTag?.Value,
            DeadlineFrom: from,
            DeadlineTo: to,
            Text: SearchText,
            OnlyOverdue: OnlyOverdue,
            DueToday: DueToday,
            DueThisWeek: DueThisWeek);
    }
}
