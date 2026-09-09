using System.Globalization;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using DomainPriority = TaskManager.Core.Domain.Priority;
using DomainTaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.Services;

/// <summary>Превращает запись истории (поле, старое и новое значение, команда) в читаемую строку для пользователя.</summary>
public static class HistoryFormatter
{
    private const string Dash = "—";

    public static string When(TaskHistoryEntry entry, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(zone);

        return TimeZoneInfo.ConvertTime(entry.ChangedAt, zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    public static string Text(TaskHistoryEntry entry, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(zone);

        var text = entry.FieldName switch
        {
            "Created" => "Задача создана",
            "IsDeleted" => entry.NewValue == "True" ? "Задача удалена" : "Задача восстановлена",
            "NextOccurrence" => $"Создана следующая копия: {entry.NewValue}",
            _ => $"{FieldName(entry.FieldName)}: {Value(entry.FieldName, entry.OldValue, zone)} → {Value(entry.FieldName, entry.NewValue, zone)}",
        };

        // Пометка, если изменение сделано не пользователем напрямую.
        var origin = entry.Command switch
        {
            TaskService.AutoBlockCommand or TaskService.AutoUnblockCommand => " (автоматически из-за зависимостей)",
            TaskService.RecurrenceCommand => " (повторение)",
            "import" => " (импорт)",
            _ => string.Empty,
        };

        return text + origin;
    }

    private static string FieldName(string field) => field switch
    {
        "Title" => "Название",
        "Description" => "Описание",
        "Priority" => "Приоритет",
        "Status" => "Статус",
        "Deadline" => "Дедлайн",
        "Tags" => "Теги",
        "Recurrence" => "Повторение",
        "ParentId" => "Родительская задача",
        "DependsOn" => "Зависимость",
        _ => field,
    };

    private static string Value(string field, string? value, TimeZoneInfo zone)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Dash;
        }

        return field switch
        {
            "Priority" when Enum.TryParse<DomainPriority>(value, out var priority) => DisplayNames.PriorityName(priority),
            "Status" when Enum.TryParse<DomainTaskStatus>(value, out var status) => DisplayNames.StatusName(status),
            "Deadline" when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var deadline)
                => TimeZoneInfo.ConvertTime(deadline, zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
            _ => value,
        };
    }
}
