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
        RowVersion = task.RowVersion;
        Title = task.Title;

        Status = task.Status;
        StatusText = DisplayNames.StatusName(task.Status);
        StatusColor = AppColors.For(task.Status);
        StatusIcon = task.Status switch
        {
            TaskStatus.InProgress => "half",
            TaskStatus.Blocked => "blocked",
            TaskStatus.Done => "done",
            TaskStatus.Cancelled => "cancelled",
            _ => "circle",
        };

        PriorityText = DisplayNames.PriorityName(task.Priority);
        PriorityColor = AppColors.For(task.Priority);
        PriorityLevel = task.Priority switch
        {
            Priority.Critical => 4,
            Priority.High => 3,
            Priority.Medium => 2,
            _ => 1,
        };

        IsClosed = task.Status is TaskStatus.Done or TaskStatus.Cancelled;
        IsDone = task.Status == TaskStatus.Done;
        TitleDecorations = IsClosed ? TextDecorations.Strikethrough : TextDecorations.None;

        IsOverdue = !IsClosed && task.Deadline is { } deadline && deadline < now;
        DeadlineText = task.Deadline is { } d
            ? TimeZoneInfo.ConvertTime(d, zone).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;

        TagsText = string.Join("  ", task.Tags.Select(t => "#" + t.Name).Order(StringComparer.CurrentCultureIgnoreCase));

        ParentTitle = task.Parent?.Title ?? string.Empty;

        // Дети загружает запрос списка, поэтому прогресс считается без дополнительных обращений к базе.
        var progress = task.GetProgress();
        ProgressText = progress.HasSubtasks ? progress.ToString() : string.Empty;
    }

    public int Id { get; }

    public long RowVersion { get; }

    public string Title { get; }

    public TextDecorations TitleDecorations { get; }

    public TaskStatus Status { get; }

    public string StatusText { get; }

    public Color StatusColor { get; }

    /// <summary>Имя иконки статуса в наборе Signal.</summary>
    public string StatusIcon { get; }

    public string PriorityText { get; }

    public Color PriorityColor { get; }

    /// <summary>Сколько столбиков значка приоритета залито: 1 (низкий) … 4 (критический).</summary>
    public int PriorityLevel { get; }

    public bool IsClosed { get; }

    public bool IsDone { get; }

    public bool IsOverdue { get; }

    public string DeadlineText { get; }

    public bool HasDeadline => DeadlineText.Length > 0;

    /// <summary>Дедлайн на карточке доски: у просроченных с пометкой «просрочено».</summary>
    public string BoardDeadlineText => IsOverdue ? "просрочено · " + DeadlineText : DeadlineText;

    public string TagsText { get; }

    public bool HasTags => TagsText.Length > 0;

    /// <summary>Название родителя для подзадачи (пусто у корневых задач).</summary>
    public string ParentTitle { get; }

    public bool HasParent => ParentTitle.Length > 0;

    /// <summary>Прогресс подзадач вида "1/2" (пусто, если подзадач нет).</summary>
    public string ProgressText { get; }

    public bool HasProgress => ProgressText.Length > 0;
}
