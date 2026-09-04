using System.Globalization;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Services;

/// <summary>
/// Слепок отслеживаемых полей задачи в виде строк. Сравнение двух слепков (до и после изменения)
/// даёт список записей для истории: поле, старое значение, новое значение.
/// </summary>
internal sealed record TaskSnapshot(
    string Title,
    string? Description,
    Priority Priority,
    TaskStatus Status,
    DateTimeOffset? Deadline,
    string Tags,
    string? Recurrence)
{
    public static TaskSnapshot From(TaskItem task)
    {
        var tags = string.Join(", ", task.Tags.Select(t => t.Name).Order(StringComparer.CurrentCultureIgnoreCase));

        return new TaskSnapshot(
            task.Title,
            task.Description,
            task.Priority,
            task.Status,
            task.Deadline,
            tags,
            Describe(task.Recurrence));
    }

    /// <summary>Поля, значение которых отличается. Пустой список значит, что менять нечего.</summary>
    public static List<(string Field, string? OldValue, string? NewValue)> Diff(TaskSnapshot before, TaskSnapshot after)
    {
        var changes = new List<(string, string?, string?)>();

        Add(changes, nameof(Title), before.Title, after.Title);
        Add(changes, nameof(Description), before.Description, after.Description);
        Add(changes, nameof(Priority), before.Priority.ToString(), after.Priority.ToString());
        Add(changes, nameof(Status), before.Status.ToString(), after.Status.ToString());
        Add(changes, nameof(Deadline), Format(before.Deadline), Format(after.Deadline));
        Add(changes, nameof(Tags), NullIfEmpty(before.Tags), NullIfEmpty(after.Tags));
        Add(changes, nameof(Recurrence), before.Recurrence, after.Recurrence);

        return changes;
    }

    private static void Add(List<(string, string?, string?)> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add((field, oldValue, newValue));
        }
    }

    // Дата в ISO 8601: однозначная строка, интерфейс сам отформатирует её для пользователя.
    private static string? Format(DateTimeOffset? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture);

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string? Describe(RecurrenceRule? rule) =>
        rule is null
            ? null
            : $"{rule.Frequency}, every {rule.Interval}, {rule.Mode}" + (rule.WeekDays == WeekDays.None ? string.Empty : $", {rule.WeekDays}");
}
