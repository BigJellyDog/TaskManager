using DomainPriority = TaskManager.Core.Domain.Priority;
using DomainTaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.Services;

/// <summary>Russian names of enumerations for showing to the user.</summary>
public static class DisplayNames
{
    public static string StatusName(DomainTaskStatus status) => status switch
    {
        DomainTaskStatus.Todo => Loc.T("К выполнению"),
        DomainTaskStatus.InProgress => Loc.T("В работе"),
        DomainTaskStatus.Blocked => Loc.T("Заблокирована"),
        DomainTaskStatus.Done => Loc.T("Выполнена"),
        DomainTaskStatus.Cancelled => Loc.T("Отменена"),
        _ => status.ToString(),
    };

    public static string PriorityName(DomainPriority priority) => priority switch
    {
        DomainPriority.Low => Loc.T("Низкий"),
        DomainPriority.Medium => Loc.T("Средний"),
        DomainPriority.High => Loc.T("Высокий"),
        DomainPriority.Critical => Loc.T("Критический"),
        _ => priority.ToString(),
    };
}
