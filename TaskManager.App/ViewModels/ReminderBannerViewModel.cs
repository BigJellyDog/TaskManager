﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>
/// Блок напоминаний в боковой панели, вариант «Главное сейчас»: одно самое срочное напоминание крупно, под ним кнопки
/// «Выполнить» и «Отложить» и переключатель «1 из 3». Живёт в единственном экземпляре: фоновый сервис добавляет
/// напоминания, пока приложение открыто, а блок показывает их на любом экране.
/// </summary>
public sealed partial class ReminderBannerViewModel : ViewModelBase
{
    private readonly TimeProvider _clock;
    private readonly INavigationService _navigation;
    private readonly ServiceRunner _runner;
    private readonly UserSettings _settings;
    private readonly ILogger _log;

    // Активные напоминания: по одному на задачу (новое, более срочное, заменяет прежнее).
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

    /// <summary>Сколько напоминаний активно: показывается значком у колокольчика в узкой панели.</summary>
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
    /// Добавляет напоминания. Вызывается из фонового потока, поэтому изменение списка переносится в поток интерфейса.
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

    /// <summary>Выполняет задачу напоминания: при необходимости сначала берёт её в работу.</summary>
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

            // Списки и счётчики на других экранах узнают об изменении.
            WeakReferenceMessenger.Default.Send(new TasksChangedMessage());
        }
    }

    /// <summary>Откладывает: показывает следующее напоминание, это остаётся в списке.</summary>
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
#pragma warning disable CA1031 // Ошибка перехода не должна ронять приложение: пишем в журнал.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.LogError(ex, "Не удалось открыть задачу #{TaskId} из блока напоминаний", reminder.TaskId);
        }
    }

    // Сначала просроченные, дальше по ближайшему дедлайну.
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

        // Цвет по срочности: просрочено красным, сегодня оранжевым, позже синим (как приоритеты).
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

    // «2 дня», «5 часов»: слова, когда срок прошёл.
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

    // «−2 д», «5 ч», «40 мин»: коротко рядом с датой.
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

/// <summary>Задачи изменились не на своём экране (например, выполнена из блока напоминаний): списки и счётчики обновляются.</summary>
public sealed class TasksChangedMessage;
