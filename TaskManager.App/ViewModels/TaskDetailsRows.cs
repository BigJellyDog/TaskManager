using System.Globalization;
using System.Windows.Input;
using TaskManager.App.Controls;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>An action button on the card: status, edit, subtask. An unavailable one stays in place, grey.</summary>
public sealed record ActionButtonViewModel(string Title, string Icon, ICommand Command, ButtonVariant Variant = ButtonVariant.Secondary, bool IsEnabled = true);

/// <summary>A status icon in the Signal set.</summary>
public static class StatusIcons
{
    public static string For(TaskStatus status) => status switch
    {
        TaskStatus.InProgress => "half",
        TaskStatus.Blocked => "blocked",
        TaskStatus.Done => "done",
        TaskStatus.Cancelled => "cancelled",
        _ => "circle",
    };
}

/// <summary>A subtask in the card tree. The indent shows the nesting level.</summary>
public sealed class SubtaskRowViewModel
{
    private const double IndentPerLevel = 24;

    public SubtaskRowViewModel(SubtaskEntry entry, TimeZoneInfo zone, ICommand openCommand)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(zone);

        var task = entry.Task;
        TaskId = task.Id;
        RowVersion = task.RowVersion;
        Title = task.Title;
        StatusText = DisplayNames.StatusName(task.Status);
        StatusColor = AppColors.For(task.Status);
        StatusIcon = StatusIcons.For(task.Status);
        Indent = new Thickness((entry.Level - 1) * IndentPerLevel, 0, 0, 0);
        ProgressText = entry.Progress.HasSubtasks ? entry.Progress.ToString() : string.Empty;
        IsClosed = task.Status is TaskStatus.Done or TaskStatus.Cancelled;
        IsDone = task.Status == TaskStatus.Done;
        TitleDecorations = IsClosed ? TextDecorations.Strikethrough : TextDecorations.None;
        DeadlineText = task.Deadline is { } d && !IsClosed
            ? TimeZoneInfo.ConvertTime(d, zone).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
        OpenCommand = openCommand;
    }

    public int TaskId { get; }

    public long RowVersion { get; }

    public string Title { get; }

    public string StatusText { get; }

    public Color StatusColor { get; }

    public string StatusIcon { get; }

    public Thickness Indent { get; }

    public string ProgressText { get; }

    public bool HasProgress => ProgressText.Length > 0;

    public bool IsClosed { get; }

    public bool IsDone { get; }

    public string DeadlineText { get; }

    public bool HasDeadline => DeadlineText.Length > 0;

    public TextDecorations TitleDecorations { get; }

    public ICommand OpenCommand { get; }
}

/// <summary>A dependency: the task this one depends on, or a task that depends on this one.</summary>
public sealed class DependencyRowViewModel
{
    public DependencyRowViewModel(TaskItem task, bool isUnmet, ICommand openCommand, ICommand? removeCommand)
    {
        ArgumentNullException.ThrowIfNull(task);

        Title = $"#{task.Id} {task.Title}";
        StatusText = DisplayNames.StatusName(task.Status);
        StatusColor = AppColors.For(task.Status);
        StatusIcon = StatusIcons.For(task.Status);
        IsUnmet = isUnmet;
        OpenCommand = openCommand;
        RemoveCommand = removeCommand;
    }

    public string Title { get; }

    public string StatusText { get; }

    public Color StatusColor { get; }

    public string StatusIcon { get; }

    /// <summary>The dependency is not done yet: it is the one blocking the task.</summary>
    public bool IsUnmet { get; }

    public ICommand OpenCommand { get; }

    /// <summary>Remove the dependency. Only for those the current task depends on.</summary>
    public ICommand? RemoveCommand { get; }

    public bool CanRemove => RemoveCommand is not null;
}

/// <summary>One history entry: the time, the field name and the value (the value is shown in bold).</summary>
public sealed class HistoryRowViewModel
{
    public HistoryRowViewModel(string when, string text)
    {
        // "04.11.2026 10:40" -> "04.11 10:40": the year is not needed in the feed.
        When = when.Length >= 16 ? when.Remove(5, 5) : when;

        var split = text.IndexOf(": ", StringComparison.Ordinal);
        if (split > 0)
        {
            Prefix = text[..(split + 2)];
            Value = text[(split + 2)..];
        }
        else
        {
            Prefix = string.Empty;
            Value = text;
        }
    }

    public string When { get; }

    public string Prefix { get; }

    public string Value { get; }
}
