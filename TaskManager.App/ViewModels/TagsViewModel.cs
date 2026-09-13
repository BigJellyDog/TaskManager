using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>Строка списка тегов; в режиме правки вместо названия показано поле ввода.</summary>
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

    /// <summary>Длина полосы относительно самого частого тега (0–1).</summary>
    public double Fraction { get; }

    public string EditHint => TaskCount == 0 ? "Не используется" : $"Изменит {TaskCount} {PluralTask(TaskCount)}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    private bool isEditing;

    public bool IsNotEditing => !IsEditing;

    [ObservableProperty]
    private string editName;

    private static string PluralTask(int n) =>
        (n % 100 is >= 11 and <= 14) ? "задач" : (n % 10) switch { 1 => "задачу", >= 2 and <= 4 => "задачи", _ => "задач" };
}

/// <summary>Теги: список с числом задач, переименование во всех задачах и удаление неиспользуемых.</summary>
public sealed partial class TagsViewModel : ViewModelBase
{
    private readonly ServiceRunner _runner;
    private IReadOnlyList<TagUsage> _all = [];

    public TagsViewModel(ServiceRunner runner, IDialogService dialogs, ILogger<TagsViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
    }

    public ObservableCollection<TagRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string subtitleText = string.Empty;

    public Task LoadAsync() => RunAsync(LoadCoreAsync, "Не удалось загрузить теги");

    partial void OnSearchTextChanged(string value) => Rebuild();

    private async Task LoadCoreAsync()
    {
        _all = await _runner.RunAsync(sp => sp.GetRequiredService<TagService>().ListAsync());
        var unused = _all.Count(t => t.TaskCount == 0);
        SubtitleText = _all.Count == 0
            ? "ТЕГОВ ПОКА НЕТ"
            : $"{Plural(_all.Count, "ТЕГ", "ТЕГА", "ТЕГОВ")} · {unused} НЕ ИСПОЛЬЗУЮТСЯ";
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

        Rows.Clear();
        foreach (var tag in shown)
        {
            Rows.Add(new TagRowViewModel(tag, max == 0 ? 0 : (double)tag.TaskCount / max));
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
            "Не удалось переименовать тег");

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

        if (!await Dialogs.ConfirmAsync("Удалить тег", $"Тег «#{row.Name}» нигде не используется. Удалить его?", "Удалить", "Отмена"))
        {
            return;
        }

        var done = await RunAsync(
            () => _runner.RunAsync(sp => sp.GetRequiredService<TagService>().DeleteAsync(row.Id)),
            "Не удалось удалить тег");

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
