using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>A row of the tag list; in edit mode an input field is shown instead of the name.</summary>
public sealed partial class TagRowViewModel : ObservableObject
{
    public TagRowViewModel(TagUsage usage, double fraction)
    {
        Id = usage.Id;
        Name = usage.Name;
        TaskCount = usage.TaskCount;
        CanDelete = usage.CanDelete;
        Fraction = fraction;
        EditName = usage.Name;
    }

    public int Id { get; }

    public string Name { get; }

    public string DisplayName => "#" + Name;

    public int TaskCount { get; }

    public bool CanDelete { get; }

    /// <summary>The length of the bar relative to the most frequent tag (0-1).</summary>
    public double Fraction { get; }

    public string EditHint => TaskCount == 0 ? Loc.T("Не используется") : Loc.F("Изменит {0} {1}", TaskCount, Loc.Plural(TaskCount, "задачу|задачи|задач"));

    /// <summary>Selected for a bulk delete. Only an unused tag can be selected.</summary>
    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private bool isEditing;

    /// <summary>The check box is shown: the tag is unused and its name is not being edited.</summary>
    public bool CanSelect => CanDelete && !IsEditing;

    public bool IsNotEditing => !IsEditing;

    [ObservableProperty]
    private string editName;
}

/// <summary>Tags: a list with the number of tasks, renaming in all tasks and deleting the unused ones.</summary>
public sealed partial class TagsViewModel : ViewModelBase
{
    private readonly ServiceRunner _runner;
    private IReadOnlyList<TagUsage> _all = [];

    public TagsViewModel(ServiceRunner runner, IDialogService dialogs, ILogger<TagsViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
    }

    private TagRowViewModel? _anchor;

    public ObservableCollection<TagRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(AllSelected))]
    private int selectedCount;

    /// <summary>The shown rows that can be selected at all (unused tags).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeletable))]
    [NotifyPropertyChangedFor(nameof(AllSelected))]
    private int deletableCount;

    public bool HasSelection => SelectedCount > 0;

    public bool HasDeletable => DeletableCount > 0;

    /// <summary>Every selectable row is selected (the header check mark is on).</summary>
    public bool AllSelected => DeletableCount > 0 && SelectedCount == DeletableCount;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string subtitleText = string.Empty;

    public Task LoadAsync() => RunAsync(LoadCoreAsync, Loc.T("Не удалось загрузить теги"));

    partial void OnSearchTextChanged(string value) => Rebuild();

    private async Task LoadCoreAsync()
    {
        _all = await _runner.RunAsync(sp => sp.GetRequiredService<TagService>().ListAsync());
        var unused = _all.Count(t => t.TaskCount == 0);
        SubtitleText = _all.Count == 0
            ? Loc.T("ТЕГОВ ПОКА НЕТ")
            : Loc.F("{0} · {1} НЕ ИСПОЛЬЗУЮТСЯ", Loc.Count(_all.Count, "ТЕГ|ТЕГА|ТЕГОВ"), unused);
        Rebuild();
    }

    private void Rebuild()
    {
        var filter = SearchText.Trim().TrimStart('#');
        var shown = _all
            .Where(t => filter.Length == 0 || t.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(t => t.TaskCount)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var max = _all.Select(t => t.TaskCount).DefaultIfEmpty(0).Max();

        foreach (var old in Rows)
        {
            old.PropertyChanged -= OnRowChanged;
        }

        Rows.Clear();
        _anchor = null;
        foreach (var tag in shown)
        {
            var row = new TagRowViewModel(tag, max == 0 ? 0 : (double)tag.TaskCount / max);
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        DeletableCount = Rows.Count(r => r.CanDelete);
        SelectedCount = 0;
    }

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TagRowViewModel.IsSelected))
        {
            SelectedCount = Rows.Count(r => r.IsSelected);
        }
    }

    /// <summary>
    /// A click on a row's check box. With Shift held, everything between the previous click and this one
    /// takes the state of the previous click (as in a mail client); otherwise just this row toggles.
    /// </summary>
    [RelayCommand]
    private void ToggleSelect(TagRowViewModel row)
    {
        if (!row.CanDelete)
        {
            return;
        }

        if (KeyboardState.IsShiftDown && _anchor is not null && Rows.Contains(_anchor))
        {
            var from = Rows.IndexOf(_anchor);
            var to = Rows.IndexOf(row);
            var state = _anchor.IsSelected;
            for (var i = Math.Min(from, to); i <= Math.Max(from, to); i++)
            {
                if (Rows[i].CanDelete)
                {
                    Rows[i].IsSelected = state;
                }
            }
        }
        else
        {
            row.IsSelected = !row.IsSelected;
        }

        _anchor = row;
    }

    /// <summary>The check mark in the header: clears the selection if everything is selected, otherwise selects every unused tag shown.</summary>
    [RelayCommand]
    private void ToggleSelectAll()
    {
        var select = !AllSelected;
        foreach (var row in Rows.Where(r => r.CanDelete))
        {
            row.IsSelected = select;
        }

        _anchor = null;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var ids = Rows.Where(r => r.IsSelected && r.CanDelete).Select(r => r.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var message = Loc.F("Удалить {0}? Они нигде не используются.", Loc.Count(ids.Count, "тег|тега|тегов"));
        if (!await Dialogs.ConfirmAsync(Loc.T("Удалить теги"), message, Loc.T("Удалить"), Loc.T("Отмена")))
        {
            return;
        }

        var done = await RunAsync(
            () => _runner.RunAsync(sp => sp.GetRequiredService<TagService>().DeleteManyAsync(ids)),
            Loc.T("Не удалось удалить теги"));

        if (done)
        {
            await LoadCoreAsync();
        }
    }

    [RelayCommand]
    private void StartEdit(TagRowViewModel row)
    {
        foreach (var other in Rows.Where(r => r.IsEditing))
        {
            other.IsEditing = false;
        }

        row.EditName = row.Name;
        row.IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit(TagRowViewModel row) => row.IsEditing = false;

    [RelayCommand]
    private async Task SaveEditAsync(TagRowViewModel row)
    {
        var name = row.EditName.Trim().TrimStart('#');
        if (string.Equals(name, row.Name, StringComparison.Ordinal))
        {
            row.IsEditing = false;
            return;
        }

        var done = await RunAsync(
            () => _runner.RunAsync(sp => sp.GetRequiredService<TagService>().RenameAsync(row.Id, name)),
            Loc.T("Не удалось переименовать тег"));

        if (done)
        {
            await LoadCoreAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(TagRowViewModel row)
    {
        if (!row.CanDelete)
        {
            return;
        }

        if (!await Dialogs.ConfirmAsync(Loc.T("Удалить тег"), Loc.F("Тег «#{0}» нигде не используется. Удалить его?", row.Name), Loc.T("Удалить"), Loc.T("Отмена")))
        {
            return;
        }

        var done = await RunAsync(
            () => _runner.RunAsync(sp => sp.GetRequiredService<TagService>().DeleteAsync(row.Id)),
            Loc.T("Не удалось удалить тег"));

        if (done)
        {
            await LoadCoreAsync();
        }
    }
}
