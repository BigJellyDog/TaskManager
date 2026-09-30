using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>A trash row: a deleted task, a selection check box and the column labels.</summary>
public sealed partial class TrashRowViewModel : ObservableObject
{
    public TrashRowViewModel(TrashEntry entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        StatusText = DisplayNames.StatusName(entry.Status);
        StatusColor = AppColors.For(entry.Status);
        StatusIcon = entry.Status switch
        {
            TaskStatus.InProgress => "half",
            TaskStatus.Blocked => "blocked",
            TaskStatus.Done => "done",
            TaskStatus.Cancelled => "cancelled",
            _ => "circle",
        };
        DeletedText = entry.DeletedAt is { } at ? at.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.InvariantCulture) : "—";
        SubtasksText = entry.DeletedSubtasks == 0 ? Loc.T("нет") : Loc.Count(entry.DeletedSubtasks, "подзадача|подзадачи|подзадач");
    }

    public int Id { get; }

    public string Title { get; }

    public string StatusText { get; }

    public Color StatusColor { get; }

    public string StatusIcon { get; }

    public string DeletedText { get; }

    public string SubtasksText { get; }

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>The trash: deleted tasks, restore (with subtasks) and delete forever with a confirmation.</summary>
public sealed partial class TrashViewModel : ViewModelBase
{
    private readonly ServiceRunner _runner;
    private readonly SidebarViewModel _sidebar;

    public TrashViewModel(ServiceRunner runner, SidebarViewModel sidebar, IDialogService dialogs, ILogger<TrashViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
        _sidebar = sidebar;
    }

    public ObservableCollection<TrashRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    [NotifyPropertyChangedFor(nameof(AllSelected))]
    private int count;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(AllSelected))]
    private int selectedCount;

    public bool HasRows => Count > 0;

    /// <summary>All rows are selected: the check mark in the table header.</summary>
    public bool AllSelected => Count > 0 && SelectedCount == Count;

    public bool HasSelection => SelectedCount > 0;

    public string SubtitleText => Count == 0 ? Loc.T("КОРЗИНА ПУСТА") : Loc.Count(Count, "УДАЛЁННАЯ ЗАДАЧА|УДАЛЁННЫЕ ЗАДАЧИ|УДАЛЁННЫХ ЗАДАЧ");

    public Task LoadAsync() => RunAsync(LoadCoreAsync, Loc.T("Не удалось загрузить корзину"));

    private async Task LoadCoreAsync()
    {
        var entries = await _runner.RunAsync(sp => sp.GetRequiredService<TrashService>().ListAsync());

        foreach (var row in Rows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        Rows.Clear();
        foreach (var entry in entries)
        {
            var row = new TrashRowViewModel(entry);
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        Count = Rows.Count;
        SelectedCount = 0;

        // The numbers in the side panel (tasks and trash) change on restore and delete.
        await _sidebar.RefreshCountsAsync();
    }

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrashRowViewModel.IsSelected))
        {
            SelectedCount = Rows.Count(r => r.IsSelected);
        }
    }

    [RelayCommand]
    private void ToggleSelect(TrashRowViewModel row) => row.IsSelected = !row.IsSelected;

    /// <summary>The check mark in the header: if all are selected it clears the selection, otherwise it selects all rows.</summary>
    [RelayCommand]
    private void ToggleSelectAll()
    {
        var select = !AllSelected;
        foreach (var row in Rows)
        {
            row.IsSelected = select;
        }
    }

    [RelayCommand]
    private async Task RestoreSelectedAsync()
    {
        var ids = Rows.Where(r => r.IsSelected).Select(r => r.Id).ToList();
        if (ids.Count > 0)
        {
            await RestoreAsync(ids);
        }
    }

    [RelayCommand]
    private Task RestoreOneAsync(TrashRowViewModel row) => RestoreAsync([row.Id]);

    private async Task RestoreAsync(List<int> ids)
    {
        var done = await RunAsync(
            () => _runner.RunAsync(sp => sp.GetRequiredService<TrashService>().RestoreAsync(ids)),
            Loc.T("Не удалось восстановить задачи"));

        if (done)
        {
            await LoadCoreAsync();
        }
    }

    [RelayCommand]
    private async Task PurgeOneAsync(TrashRowViewModel row)
    {
        var extra = row.SubtasksText == Loc.T("нет") ? string.Empty : Loc.F(" вместе с подзадачами ({0})", row.SubtasksText);
        if (!await Dialogs.ConfirmAsync(Loc.T("Удалить навсегда"), Loc.F("Задача «{0}»{1} будет удалена безвозвратно. Продолжить?", row.Title, extra), Loc.T("Удалить"), Loc.T("Отмена")))
        {
            return;
        }

        await PurgeAsync([row.Id]);
    }

    [RelayCommand]
    private async Task EmptyAsync()
    {
        if (Rows.Count == 0)
        {
            return;
        }

        var total = Rows.Count;
        if (!await Dialogs.ConfirmAsync(
                Loc.T("Очистить корзину"),
                Loc.F("Будет удалено навсегда: {0} (и их подзадачи). Это нельзя отменить. Продолжить?", Loc.Count(total, "задача|задачи|задач")),
                Loc.T("Очистить"),
                Loc.T("Отмена")))
        {
            return;
        }

        await PurgeAsync(Rows.Select(r => r.Id).ToList());
    }

    private async Task PurgeAsync(List<int> ids)
    {
        var done = await RunAsync(
            () => _runner.RunAsync(sp => sp.GetRequiredService<TrashService>().PurgeAsync(ids)),
            Loc.T("Не удалось удалить задачи"));

        if (done)
        {
            await LoadCoreAsync();
        }
    }
}
