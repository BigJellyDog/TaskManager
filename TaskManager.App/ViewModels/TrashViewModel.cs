using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>Строка корзины: удалённая задача, флажок выбора и подписи колонок.</summary>
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

    private static string Plural(int n, string one, string few, string many)
    {
        var form = (n % 100 is >= 11 and <= 14) ? many : (n % 10) switch { 1 => one, >= 2 and <= 4 => few, _ => many };
        return $"{n} {form}";
    }
}

/// <summary>Корзина: удалённые задачи, восстановление (с подзадачами) и удаление навсегда с подтверждением.</summary>
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
    private int count;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private int selectedCount;

    public bool HasRows => Count > 0;

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

        // Числа в боковой панели (задачи и корзина) меняются от восстановления и удаления.
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

    private static string Plural(int n, string one, string few, string many)
    {
        var form = (n % 100 is >= 11 and <= 14) ? many : (n % 10) switch { 1 => one, >= 2 and <= 4 => few, _ => many };
        return $"{n} {form}";
    }
}
