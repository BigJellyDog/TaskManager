﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>
/// The reminders block in the side panel, the "Main one now" variant: one most urgent reminder, large, under it the buttons
/// "Complete" and "Snooze" and the "1 of 3" switch. It lives as a single instance: the background service adds
/// reminders while the application is open, and the block shows them on any screen.
/// </summary>
public sealed partial class ReminderBannerViewModel : ViewModelBase
{
    private readonly TimeProvider _clock;
    private readonly INavigationService _navigation;
    private readonly ServiceRunner _runner;
    private readonly UserSettings _settings;
    private readonly ILogger _log;

    // Active reminders: one per task (a new, more urgent one replaces the previous).
    private readonly List<Reminder> _active = [];
    private int _index;

    public ReminderBannerViewModel(
        TimeProvider clock,
        INavigationService navigation,
        ServiceRunner runner,
        UserSettings settings,
        IDialogService dialogs,
        ILogger<ReminderBannerViewModel> logger)
        : base(dialogs, logger)
    {
        _log = logger;
        _clock = clock;
        _navigation = navigation;
        _runner = runner;
        _settings = settings;

        settings.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        Loc.Changed += () => MainThread.BeginInvokeOnMainThread(Refresh);
        ThemeManager.Changed += OnThemeChanged;
    }

    [ObservableProperty]
    private bool isVisible;

    /// <summary>How many reminders are active: shown as a badge at the bell in the narrow panel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    private int count;

    public bool HasCount => Count > 0 && _settings.ShowReminderBanner;

    [ObservableProperty]
    private string titleText = string.Empty;

    [ObservableProperty]
    private string tagText = string.Empty;

    [ObservableProperty]
    private Color tagColor = Colors.Gray;

    [ObservableProperty]
    private Brush tagStroke = Brush.Transparent;

    [ObservableProperty]
    private Color tagBackground = Colors.Transparent;

    [ObservableProperty]
    private string dateText = string.Empty;

    [ObservableProperty]
    private string leftText = string.Empty;

    [ObservableProperty]
    private Color leftColor = Colors.Gray;

    [ObservableProperty]
    private string pagerText = string.Empty;

    /// <summary>
    /// Adds reminders. Called from a background thread, so the change of the list is moved to the interface thread.
    /// </summary>
    public Task ShowAsync(IReadOnlyList<Reminder> reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var reminder in reminders)
            {
                _active.RemoveAll(r => r.TaskId == reminder.TaskId);
                _active.Add(reminder);
            }

            Refresh();
        });
    }

    /// <summary>Completes the task of a reminder: if needed it first takes the task into work.</summary>
    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (Current is not { } reminder)
        {
            return;
        }

        var done = await RunAsync(
            () => _runner.RunAsync(async sp =>
            {
                var service = sp.GetRequiredService<TaskService>();
                var task = await service.GetAsync(reminder.TaskId);
                if (task.Status == TaskStatus.Todo)
                {
                    task = await service.ChangeStatusAsync(task.Id, TaskStatus.InProgress, task.RowVersion);
                }

                await service.ChangeStatusAsync(task.Id, TaskStatus.Done, task.RowVersion);
            }),
            Loc.T("Не удалось изменить статус"));

        if (done)
        {
            _active.RemoveAll(r => r.TaskId == reminder.TaskId);
            Refresh();

            // Lists and counters on other screens learn about the change.
            WeakReferenceMessenger.Default.Send(new TasksChangedMessage());
        }
    }

    /// <summary>Snoozes: shows the next reminder, this one stays in the list.</summary>
    [RelayCommand]
    private void Snooze()
    {
        _index = Math.Min(_index + 1, Math.Max(_active.Count - 1, 0));
        Refresh();
    }

    [RelayCommand]
    private void Next()
    {
        if (_active.Count > 0)
        {
            _index = (_index + 1) % _active.Count;
            Refresh();
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (_active.Count > 0)
        {
            _index = (_index - 1 + _active.Count) % _active.Count;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (Current is not { } reminder)
        {
            return;
        }

        try
        {
            await _navigation.GoToAsync($"taskdetails?id={reminder.TaskId}");
        }
#pragma warning disable CA1031 // A navigation error must not bring the application down: we write it to the log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.LogError(ex, "Не удалось открыть задачу #{TaskId} из блока напоминаний", reminder.TaskId);
        }
    }

    // Overdue first, then by the nearest deadline.
    private List<Reminder> Ordered() => _active.OrderByDescending(r => r.Kind).ThenBy(r => r.Deadline).ToList();

    private Reminder? Current => Ordered() is { Count: > 0 } list ? list[Math.Min(_index, list.Count - 1)] : null;

    private void OnThemeChanged(object? sender, EventArgs e) => Refresh();

    private static Color Token(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Colors.Gray;

    private void Refresh()
    {
        var list = Ordered();
        _index = Math.Clamp(_index, 0, Math.Max(list.Count - 1, 0));

        IsVisible = list.Count > 0 && _settings.ShowReminderBanner;
        Count = list.Count;
        OnPropertyChanged(nameof(HasCount));

        if (list.Count == 0)
        {
            return;
        }

        var reminder = list[_index];
        var zone = _clock.LocalTimeZone;
        var now = _clock.GetUtcNow();
        var deadline = TimeZoneInfo.ConvertTime(reminder.Deadline, zone);
        var overdue = reminder.Kind == ReminderKind.Overdue;
        var today = deadline.Date == TimeZoneInfo.ConvertTime(now, zone).Date;

        // Colour by urgency: overdue in red, today in orange, later in blue (like the priorities).
        var key = overdue ? "PCrit" : today || reminder.Kind == ReminderKind.DueIn1Hour ? "PHigh" : "PMed";
        var color = Token(key);

        TitleText = reminder.Title;
        TagText = overdue ? Loc.F("Просрочено {0}", Elapsed(now - reminder.Deadline)) : Loc.T(today ? "Сегодня" : "Завтра");
        TagColor = color;
        TagStroke = new SolidColorBrush(color);
        TagBackground = overdue ? Token("PCritBg") : Token("SurfaceRaised");
        DateText = deadline.ToString("dd.MM HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        LeftText = Short(reminder.Deadline - now);
        LeftColor = color;
        PagerText = Loc.F("{0} из {1}", _index + 1, list.Count);
    }

    // "2 days", "5 hours": words, once the deadline has passed.
    private static string Elapsed(TimeSpan span)
    {
        span = span.Duration();
        if (span.TotalDays >= 1)
        {
            return Loc.Count((int)span.TotalDays, "день|дня|дней");
        }

        return span.TotalHours >= 1
            ? Loc.Count((int)span.TotalHours, "час|часа|часов")
            : Loc.Count(Math.Max((int)span.TotalMinutes, 1), "минута|минуты|минут");
    }

    // "-2 d", "5 h", "40 min": short, next to the date.
    private static string Short(TimeSpan span)
    {
        var sign = span < TimeSpan.Zero ? "−" : string.Empty;
        span = span.Duration();
        var text = span.TotalDays >= 1
            ? Loc.F("{0} д", (int)span.TotalDays)
            : span.TotalHours >= 1
                ? Loc.F("{0} ч", (int)span.TotalHours)
                : Loc.F("{0} мин", Math.Max((int)span.TotalMinutes, 1));
        return sign + text;
    }
}

/// <summary>Tasks changed on a screen that is not their own (for example completed from the reminders block): lists and counters are updated.</summary>
public sealed class TasksChangedMessage;
