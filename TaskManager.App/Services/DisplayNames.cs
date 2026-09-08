using DomainPriority = TaskManager.Core.Domain.Priority;
using DomainTaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.Services;

/// <summary>Русские названия перечислений для показа пользователю.</summary>
public static class DisplayNames
{
    public static string StatusName(DomainTaskStatus status) => status switch
    {
        DomainTaskStatus.Todo => "К выполнению",
        DomainTaskStatus.InProgress => "В работе",
        DomainTaskStatus.Blocked => "Заблокирована",
        DomainTaskStatus.Done => "Выполнена",
        DomainTaskStatus.Cancelled => "Отменена",
        _ => status.ToString(),
    };

    public static string PriorityName(DomainPriority priority) => priority switch
    {
        DomainPriority.Low => "Низкий",
        DomainPriority.Medium => "Средний",
        DomainPriority.High => "Высокий",
        DomainPriority.Critical => "Критический",
        _ => priority.ToString(),
    };
}
