using System.Windows.Input;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>Кнопка действия над статусом (начать, выполнить, переоткрыть...).</summary>
public sealed record StatusActionViewModel(string Title, ICommand Command);

/// <summary>Подзадача в дереве карточки. Отступ показывает уровень вложенности.</summary>
public sealed class SubtaskRowViewModel
{
    private const double IndentPerLevel = 24;

    public SubtaskRowViewModel(SubtaskEntry entry, ICommand openCommand)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Title = entry.Task.Title;
        StatusText = DisplayNames.StatusName(entry.Task.Status);
        StatusColor = AppColors.For(entry.Task.Status);
        Indent = new Thickness((entry.Level - 1) * IndentPerLevel, 0, 0, 0);
        ProgressText = entry.Progress.HasSubtasks ? $"{entry.Progress} подзадач" : string.Empty;
        TitleDecorations = entry.Task.Status is TaskStatus.Done or TaskStatus.Cancelled
            ? TextDecorations.Strikethrough
            : TextDecorations.None;
        OpenCommand = openCommand;
    }

    public string Title { get; }

    public string StatusText { get; }

    public Color StatusColor { get; }

    public Thickness Indent { get; }

    public string ProgressText { get; }

    public bool HasProgress => ProgressText.Length > 0;

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
        IsUnmet = isUnmet;
        OpenCommand = openCommand;
        RemoveCommand = removeCommand;
    }

    public string Title { get; }

    public string StatusText { get; }

    public Color StatusColor { get; }

    /// <summary>Зависимость ещё не выполнена: именно она блокирует задачу.</summary>
    public bool IsUnmet { get; }

    public ICommand OpenCommand { get; }

    /// <summary>Убрать зависимость. Только у тех, от кого зависит текущая задача.</summary>
    public ICommand? RemoveCommand { get; }

    public bool CanRemove => RemoveCommand is not null;
}

/// <summary>Одна строка истории изменений.</summary>
public sealed record HistoryRowViewModel(string When, string Text);
