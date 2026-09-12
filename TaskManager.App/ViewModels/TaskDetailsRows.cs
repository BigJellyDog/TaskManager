using System.Globalization;
using System.Windows.Input;
using TaskManager.App.Controls;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>Кнопка действия на карточке: статус, правка, подзадача. Недоступная остаётся на месте серой.</summary>
public sealed record ActionButtonViewModel(string Title, string Icon, ICommand Command, ButtonVariant Variant = ButtonVariant.Secondary, bool IsEnabled = true);

/// <summary>Иконка статуса в наборе Signal.</summary>
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

/// <summary>Подзадача в дереве карточки. Отступ показывает уровень вложенности.</summary>
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

/// <summary>Зависимость: задача, от которой зависит эта, или задача, которая зависит от этой.</summary>
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

    /// <summary>Зависимость ещё не выполнена: именно она блокирует задачу.</summary>
    public bool IsUnmet { get; }

    public ICommand OpenCommand { get; }

    /// <summary>Убрать зависимость. Только у тех, от кого зависит текущая задача.</summary>
    public ICommand? RemoveCommand { get; }

    public bool CanRemove => RemoveCommand is not null;
}

/// <summary>Одна запись истории: время, название поля и значение (значение выделяется жирным).</summary>
public sealed class HistoryRowViewModel
{
    public HistoryRowViewModel(string when, string text)
    {
        // "04.11.2026 10:40" -> "04.11 10:40": год в ленте не нужен.
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
