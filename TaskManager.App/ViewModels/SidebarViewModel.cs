using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Querying;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>A side panel item: a section, an icon, a shortcut and a counter on the right.</summary>
public sealed partial class NavItemViewModel : ObservableObject
{
    public NavItemViewModel(string title, string icon, string route, string hint, bool separatorAbove = false)
    {
        _titleKey = title;
        Icon = icon;
        Route = route;
        Hint = hint;
        SeparatorAbove = separatorAbove;
    }

    private readonly string _titleKey;

    /// <summary>The section name in the current language.</summary>
    public string Title => Loc.T(_titleKey);

    /// <summary>The shortcut hint: in German "Strg".</summary>
    public string HintText => Loc.T(Hint);

    /// <summary>Recalculate the labels after a language change.</summary>
    public void Relocalize()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HintText));
    }

    public string Icon { get; }

    public string Route { get; }

    public string Hint { get; }

    public bool SeparatorAbove { get; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    private string count = string.Empty;

    public bool HasCount => Count.Length > 0;
}

/// <summary>The side panel: sections, the selected section and counters (all tasks, in the trash).</summary>
public sealed partial class SidebarViewModel : ObservableObject
{
    private readonly ServiceRunner _runner;
    private readonly INavigationService _navigation;
    private readonly ILogger<SidebarViewModel> _logger;

    public SidebarViewModel(ServiceRunner runner, INavigationService navigation, ReminderBannerViewModel reminders, ILogger<SidebarViewModel> logger)
    {
        Reminders = reminders;
        _runner = runner;
        _navigation = navigation;
        _logger = logger;

        Items =
        [
            new NavItemViewModel("Задачи", "list", "tasks", "Ctrl 1"),
            new NavItemViewModel("Статистика", "chart", "statistics", "Ctrl 2"),
            new NavItemViewModel("Данные", "database", "data", "Ctrl 3"),
            new NavItemViewModel("Корзина", "trash", "trash", "Ctrl 4", separatorAbove: true),
            new NavItemViewModel("Теги", "tag", "tags", "Ctrl 5"),
            new NavItemViewModel("Настройки", "settings", "settings", "Ctrl 6"),
        ];
        Items[0].IsSelected = true;

        // A task was completed from the reminders block: the counters in the panel change.
        WeakReferenceMessenger.Default.Register<SidebarViewModel, TasksChangedMessage>(
            this, (r, _) => MainThread.BeginInvokeOnMainThread(async () => await r.RefreshCountsAsync()));
    }

    public ObservableCollection<NavItemViewModel> Items { get; }

    /// <summary>The reminders banner: in the narrow panel a bell with a badge is shown instead.</summary>
    public ReminderBannerViewModel Reminders { get; }

    /// <summary>The window is narrow (about 1024): the panel folds into icons.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpanded))]
    private bool isCompact;

    public bool IsExpanded => !IsCompact;

    /// <summary>Highlights the section by the current Shell address (for example "//tasks/taskdetails?id=3" is "Tasks").</summary>
    public void SelectRoute(string? location)
    {
        var segment = (location ?? string.Empty).TrimStart('/').Split('/', '?')[0];
        foreach (var item in Items)
        {
            item.IsSelected = item.Route == segment;
        }
    }

    /// <summary>The language changed: the section names are read again.</summary>
    public void Relocalize()
    {
        foreach (var item in Items)
        {
            item.Relocalize();
        }
    }

    [RelayCommand]
    private Task OpenAsync(NavItemViewModel item) => _navigation.GoToAsync("//" + item.Route);

    /// <summary>Refreshes the task counter. An error does not interfere: the counter simply stays as it was.</summary>
    public async Task RefreshCountsAsync()
    {
        try
        {
            // Both numbers in one dependency scope and with cheap COUNT queries.
            var (tasks, trashed) = await _runner.RunAsync(async sp =>
                (await sp.GetRequiredService<TaskService>().CountAsync(new TaskFilter()),
                    await sp.GetRequiredService<TrashService>().CountAsync()));

            Items[0].Count = tasks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Items[3].Count = trashed == 0 ? string.Empty : trashed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
#pragma warning disable CA1031 // The counter in the panel must not bring the application down.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Не удалось обновить счётчики боковой панели");
        }
    }
}
