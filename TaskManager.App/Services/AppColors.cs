using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.Services;

/// <summary>Цвета приоритетов и статусов. Значения лежат в Resources/Styles/Colors.xaml, здесь только выбор по перечислению.</summary>
public static class AppColors
{
    public static Color For(Priority priority) => Get(priority switch
    {
        Priority.Low => "PriorityLow",
        Priority.Medium => "PriorityMedium",
        Priority.High => "PriorityHigh",
        Priority.Critical => "PriorityCritical",
        _ => "PriorityLow",
    });

    public static Color For(TaskStatus status) => Get(status switch
    {
        TaskStatus.Todo => "StatusTodo",
        TaskStatus.InProgress => "StatusInProgress",
        TaskStatus.Blocked => "StatusBlocked",
        TaskStatus.Done => "StatusDone",
        TaskStatus.Cancelled => "StatusCancelled",
        _ => "StatusTodo",
    });

    private static Color Get(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;
}
