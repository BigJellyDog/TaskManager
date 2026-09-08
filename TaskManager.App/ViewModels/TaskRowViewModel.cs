using System.Globalization;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>Одна строка списка задач. Только для чтения: всё подготовлено заранее, шаблон лишь показывает.</summary>
public sealed class TaskRowViewModel
{
    public TaskRowViewModel(TaskItem task, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(zone);

        Id = task.Id;
        Title = task.Title;

        StatusText = DisplayNames.StatusName(task.Status);
        StatusColor = AppColors.For(task.Status);
        PriorityText = DisplayNames.PriorityName(task.Priority);
        PriorityColor = AppColors.For(task.Priority);

        var finished = task.Status is TaskStatus.Done or TaskStatus.Cancelled;
        TitleDecorations = finished ? TextDecorations.Strikethrough : TextDecorations.None;

        IsOverdue = !finished && task.Deadline is { } deadline && deadline < now;
        DeadlineText = task.Deadline is { } d
            ? "до " + TimeZoneInfo.ConvertTime(d, zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;

        TagsText = string.Join("  ", task.Tags.Select(t => "#" + t.Name).Order(StringComparer.CurrentCultureIgnoreCase));

        // Дети загружает запрос списка, поэтому прогресс считается без дополнительных обращений к базе.
        var progress = task.GetProgress();
        ProgressText = progress.HasSubtasks ? $"{progress} подзадач" : string.Empty;
    }

    public int Id { get; }

    public string Title { get; }

    public TextDecorations TitleDecorations { get; }

    public string StatusText { get; }

    public Color StatusColor { get; }

    public string PriorityText { get; }

    public Color PriorityColor { get; }

    public bool IsOverdue { get; }

    public string DeadlineText { get; }

    public bool HasDeadline => DeadlineText.Length > 0;

    public string TagsText { get; }

    public bool HasTags => TagsText.Length > 0;

    public string ProgressText { get; }

    public bool HasProgress => ProgressText.Length > 0;
}
