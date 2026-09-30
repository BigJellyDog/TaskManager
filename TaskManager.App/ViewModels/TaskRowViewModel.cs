using System.Globalization;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>One row of the task list. Read-only: everything is prepared in advance, the template only shows it.</summary>
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

        // The children are loaded by the list query, so the progress is calculated without additional database calls.
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

    /// <summary>The name of the status icon in the Signal set.</summary>
    public string StatusIcon { get; }

    public string PriorityText { get; }

    public Color PriorityColor { get; }

    /// <summary>How many bars of the priority icon are filled: 1 (low) ... 4 (critical).</summary>
    public int PriorityLevel { get; }

    public bool IsClosed { get; }

    public bool IsDone { get; }

    public bool IsOverdue { get; }

    public string DeadlineText { get; }

    public bool HasDeadline => DeadlineText.Length > 0;

    /// <summary>The deadline on a board card: for overdue ones with the note "overdue".</summary>
    public string BoardDeadlineText => IsOverdue ? Loc.T("просрочено · ") + DeadlineText : DeadlineText;

    public string TagsText { get; }

    public bool HasTags => TagsText.Length > 0;

    /// <summary>The name of the parent for a subtask (empty for root tasks).</summary>
    public string ParentTitle { get; }

    public bool HasParent => ParentTitle.Length > 0;

    /// <summary>The subtask progress like "1/2" (empty if there are no subtasks).</summary>
    public string ProgressText { get; }

    /// <summary>
    /// Everything that is visible in the row. If the labels of two selections are equal, the list on the screen is not rebuilt:
    /// switching tabs does not recreate dozens of cards when the data has not changed.
    /// </summary>
    public string Signature =>
        $"{Id}|{RowVersion}|{Title}|{StatusText}|{PriorityText}|{TagsText}|{ParentTitle}|{ProgressText}|{DeadlineText}|{IsOverdue}";

    public bool HasProgress => ProgressText.Length > 0;
}
