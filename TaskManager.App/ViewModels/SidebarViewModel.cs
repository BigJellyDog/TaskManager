using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Querying;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>Пункт боковой панели: раздел, иконка, горячая клавиша и счётчик справа.</summary>
public sealed partial class NavItemViewModel : ObservableObject
{
    public NavItemViewModel(string title, string icon, string route, string hint, bool separatorAbove = false)
    {
        Title = title;
        Icon = icon;
        Route = route;
        Hint = hint;
        SeparatorAbove = separatorAbove;
    }

    public string Title { get; }

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

/// <summary>Боковая панель: разделы, выбранный раздел и счётчики (всего задач, в корзине).</summary>
public sealed partial class SidebarViewModel : ObservableObject
{
    private readonly ServiceRunner _runner;
    private readonly INavigationService _navigation;
    private readonly ILogger<SidebarViewModel> _logger;

    public SidebarViewModel(ServiceRunner runner, INavigationService navigation, ILogger<SidebarViewModel> logger)
    {
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
    }

    public ObservableCollection<NavItemViewModel> Items { get; }

    /// <summary>Подсвечивает раздел по текущему адресу Shell (например, "//tasks/taskdetails?id=3" это «Задачи»).</summary>
    public void SelectRoute(string? location)
    {
        var segment = (location ?? string.Empty).TrimStart('/').Split('/', '?')[0];
        foreach (var item in Items)
        {
            item.IsSelected = item.Route == segment;
        }
    }

    [RelayCommand]
    private Task OpenAsync(NavItemViewModel item) => _navigation.GoToAsync("//" + item.Route);

    /// <summary>Обновляет счётчик задач. Ошибка не мешает работе: счётчик просто остаётся прежним.</summary>
    public async Task RefreshCountsAsync()
    {
        try
        {
            var page = await _runner.RunAsync(sp => sp.GetRequiredService<TaskService>().SearchAsync(new TaskFilter(), pageSize: 1));
            Items[0].Count = page.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var trashed = (await _runner.RunAsync(sp => sp.GetRequiredService<TrashService>().ListAsync())).Count;
            Items[3].Count = trashed == 0 ? string.Empty : trashed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
#pragma warning disable CA1031 // Счётчик в панели не должен ронять приложение.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Не удалось обновить счётчики боковой панели");
        }
    }
}
