using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>Одна строка баннера: текст напоминания, просрочена ли задача (красная жирная строка) и открытие карточки.</summary>
public sealed record ReminderRowViewModel(string Text, bool IsOverdue, ICommand OpenCommand);

/// <summary>
/// Баннер напоминаний в боковой панели. Живёт в единственном экземпляре: фоновый сервис добавляет напоминания,
/// пока приложение открыто, а баннер показывает их на любом экране.
/// </summary>
public sealed partial class ReminderBannerViewModel : ObservableObject
{
    // Столько строк показываем сразу, остальные сворачиваем в "и ещё N", чтобы баннер не занял всю панель.
    private const int MaxVisible = 5;

    private readonly TimeProvider _clock;
    private readonly INavigationService _navigation;
    private readonly ILogger<ReminderBannerViewModel> _logger;

    // Активные напоминания: по одному на задачу (новое, более срочное, заменяет прежнее).
    private readonly List<Reminder> _active = [];

    private readonly UserSettings _settings;

    public ReminderBannerViewModel(TimeProvider clock, INavigationService navigation, UserSettings settings, ILogger<ReminderBannerViewModel> logger)
    {
        _settings = settings;
        settings.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        _clock = clock;
        _navigation = navigation;
        _logger = logger;
    }

    public ObservableCollection<ReminderRowViewModel> VisibleItems { get; } = [];

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private string title = string.Empty;

    /// <summary>Сколько напоминаний активно: показывается значком у колокольчика в узкой панели.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    private int count;

    public bool HasCount => Count > 0 && _settings.ShowReminderBanner;

    [ObservableProperty]
    private string moreText = string.Empty;

    [ObservableProperty]
    private bool hasMore;

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

    [RelayCommand]
    private void Dismiss()
    {
        _active.Clear();
        Refresh();
    }

    private void Refresh()
    {
        var zone = _clock.LocalTimeZone;

        // Сначала просроченные, дальше по ближайшему дедлайну.
        var ordered = _active
            .OrderByDescending(r => r.Kind)
            .ThenBy(r => r.Deadline)
            .ToList();

        VisibleItems.Clear();
        foreach (var reminder in ordered.Take(MaxVisible))
        {
            var current = reminder;
            VisibleItems.Add(new ReminderRowViewModel(
                Describe(current, zone),
                current.Kind == ReminderKind.Overdue,
                new AsyncRelayCommand(() => OpenAsync(current))));
        }

        IsVisible = ordered.Count > 0 && _settings.ShowReminderBanner;
        Title = $"Напоминания ({ordered.Count})";
        Count = ordered.Count;
        OnPropertyChanged(nameof(HasCount));
        HasMore = ordered.Count > MaxVisible;
        MoreText = HasMore ? $"… и ещё {ordered.Count - MaxVisible}" : string.Empty;
    }

    private async Task OpenAsync(Reminder reminder)
    {
        // Открытое напоминание считается прочитанным: убираем его из баннера и показываем задачу.
        _active.RemoveAll(r => r.TaskId == reminder.TaskId);
        Refresh();

        try
        {
            await _navigation.GoToAsync($"taskdetails?id={reminder.TaskId}");
        }
#pragma warning disable CA1031 // Ошибка перехода не должна ронять приложение: пишем в журнал.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Не удалось открыть задачу #{TaskId} из баннера напоминаний", reminder.TaskId);
        }
    }

    private static string Describe(Reminder reminder, TimeZoneInfo zone)
    {
        var when = TimeZoneInfo.ConvertTime(reminder.Deadline, zone).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);

        var state = reminder.Kind switch
        {
            ReminderKind.Overdue => $"просрочена (дедлайн {when})",
            ReminderKind.DueIn1Hour => $"меньше часа до дедлайна ({when})",
            _ => $"меньше суток до дедлайна ({when})",
        };

        return $"«{reminder.Title}»: {state}";
    }
}
