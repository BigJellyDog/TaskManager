using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.Services;

/// <summary>Цвета приоритетов и статусов. Значения лежат в Resources/Styles/DarkTheme.xaml и LightTheme.xaml, здесь только выбор по перечислению.</summary>
public static class AppColors
{
    public static Color For(Priority priority) => Get(priority switch
    {
        Priority.Low => "PLow",
        Priority.Medium => "PMed",
        Priority.High => "PHigh",
        Priority.Critical => "PCrit",
        _ => "PLow",
    });

    public static Color For(TaskStatus status) => Get(status switch
    {
        TaskStatus.Todo => "SToDo",
        TaskStatus.InProgress => "SProg",
        TaskStatus.Blocked => "SBlock",
        TaskStatus.Done => "SDone",
        TaskStatus.Cancelled => "SCancel",
        _ => "SToDo",
    });

    /// <summary>Ошибка, предупреждение и приглушённый текст в отчётах.</summary>
    public static Color Error => Get("SBlock");

    public static Color Warning => Get("PHigh");

    public static Color Muted => Get("SToDo");

    private static Color Get(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;
}
