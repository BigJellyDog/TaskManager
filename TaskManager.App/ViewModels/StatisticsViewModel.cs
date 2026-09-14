using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.App.ViewModels;

/// <summary>Строка горизонтальной гистограммы приоритетов: значок, полоса, число и доля.</summary>
public sealed record PriorityBarViewModel(string Title, int Level, int Count, string Percent, double Fraction, Color Color);

/// <summary>Столбец вертикального графика. <see cref="Height"/> уже в пикселях, <see cref="Color"/> — цвет заливки.</summary>
public sealed record ChartColumnViewModel(string Value, string Label, double Height, Color Color, Color ValueColor);

/// <summary>Сегмент полосы статусов; <see cref="Weight"/> — его доля по ширине.</summary>
public sealed record StatusSegmentViewModel(string Title, string Icon, int Count, double Weight, Color Color);

/// <summary>Строка списка тегов: подпись, длина полосы относительно самого частого тега и число.</summary>
public sealed record TagBarViewModel(string Name, int Count, double Fraction);

/// <summary>Статистика по задачам (FR-11): шесть показателей, переключатель периода и четыре панели.</summary>
public sealed partial class StatisticsViewModel : ViewModelBase
{
    // Высота дорожки столбца на графиках, пиксели. Ненулевое значение всегда видно хотя бы тонкой полоской.
    private const double TrackHeight = 78;
    private const double MinBarHeight = 3;

    // Названия дней и десятичный разделитель как в макете, независимо от языка системы.
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private readonly ServiceRunner _runner;
    private readonly INavigationService _navigation;

    public StatisticsViewModel(ServiceRunner runner, INavigationService navigation, IDialogService dialogs, ILogger<StatisticsViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
        _navigation = navigation;
    }

    public ObservableCollection<PriorityBarViewModel> Bars { get; } = [];

    public ObservableCollection<ChartColumnViewModel> CompletedColumns { get; } = [];

    public ObservableCollection<ChartColumnViewModel> DeadlineColumns { get; } = [];

    public ObservableCollection<StatusSegmentViewModel> StatusSegments { get; } = [];

    public ObservableCollection<TagBarViewModel> Tags { get; } = [];

    [ObservableProperty]
    private StatisticsPeriod period = StatisticsPeriod.Week;

    [ObservableProperty]
    private int completedLastWeek;

    [ObservableProperty]
    private int completedLastMonth;

    [ObservableProperty]
    private string onTimeText = "—";

    [ObservableProperty]
    private string onTimeNote = string.Empty;

    /// <summary>Сколько из пяти делений индикатора «Вовремя» залито.</summary>
    [ObservableProperty]
    private int onTimeBlocks;

    [ObservableProperty]
    private string averageText = "—";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverdue))]
    private int overdueCount;

    [ObservableProperty]
    private int blockedCount;

    [ObservableProperty]
    private int openCount;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string completedHint = string.Empty;

    [ObservableProperty]
    private string deadlinesHint = string.Empty;

    [ObservableProperty]
    private string subtitle = string.Empty;

    public bool HasOverdue => OverdueCount > 0;

    public bool IsWeek => Period == StatisticsPeriod.Week;

    public bool IsMonth => Period == StatisticsPeriod.Month;

    public bool IsAllTime => Period == StatisticsPeriod.AllTime;

    public Task LoadAsync() => RunAsync(LoadCoreAsync, "Не удалось загрузить статистику");

    [RelayCommand]
    private Task ShowOverdueAsync()
    {
        ListIntent.Request(ListIntent.Overdue);
        return _navigation.GoToAsync("//tasks");
    }

    [RelayCommand]
    private Task ShowBlockedAsync()
    {
        ListIntent.Request(ListIntent.Blocked);
        return _navigation.GoToAsync("//tasks");
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void SelectPeriod(string value) =>
        Period = Enum.TryParse<StatisticsPeriod>(value, out var parsed) ? parsed : StatisticsPeriod.Week;

    partial void OnPeriodChanged(StatisticsPeriod value)
    {
        OnPropertyChanged(nameof(IsWeek));
        OnPropertyChanged(nameof(IsMonth));
        OnPropertyChanged(nameof(IsAllTime));
        _ = LoadAsync();
    }

    private async Task LoadCoreAsync()
    {
        var selected = Period;
        var stats = await _runner.RunAsync(sp => sp.GetRequiredService<StatisticsService>().GetAsync(selected));

        CompletedLastWeek = stats.CompletedLastWeek;
        CompletedLastMonth = stats.CompletedLastMonth;
        OverdueCount = stats.OverdueCount;
        BlockedCount = stats.BlockedCount;
        OpenCount = stats.OpenCount;

        // Без выполненных задач с дедлайном процент не определён: показываем прочерк, а не 0% или 100%.
        OnTimeText = stats.OnTimePercent is { } percent ? percent.ToString("0.#", Ru) + "%" : "—";
        OnTimeBlocks = stats.OnTimePercent is { } share ? (int)Math.Round(share / 20) : 0;
        OnTimeNote = stats.CompletedWithDeadline == 0
            ? "нет выполненных с дедлайном"
            : $"{stats.CompletedOnTime} из {stats.CompletedWithDeadline} с дедлайном";
        AverageText = stats.AverageCompletionTime is { } average ? FormatDuration(average) : "—";

        FillPriorities(stats);
        FillStatuses(stats);
        FillCompleted(stats);
        FillDeadlines(stats);
        FillTags(stats);

        Subtitle = $"СВОДКА НА {DateTime.Now:dd.MM.yyyy} · {Plural(stats.OpenCount, "ОТКРЫТАЯ ЗАДАЧА", "ОТКРЫТЫЕ ЗАДАЧИ", "ОТКРЫТЫХ ЗАДАЧ")}";
        IsEmpty = stats.OpenCount == 0 && stats.CompletedLastMonth == 0 && stats.AverageCompletionTime is null;
    }

    private void FillPriorities(TaskStatistics stats)
    {
        var max = stats.OpenByPriority.Values.DefaultIfEmpty(0).Max();
        Bars.Clear();
        foreach (var priority in Enum.GetValues<Priority>().Reverse())
        {
            var count = stats.OpenByPriority.GetValueOrDefault(priority);
            var share = stats.OpenCount == 0 ? 0 : Math.Round(count * 100.0 / stats.OpenCount);
            Bars.Add(new PriorityBarViewModel(
                DisplayNames.PriorityName(priority),
                (int)priority + 1,
                count,
                share + "%",
                max == 0 ? 0 : (double)count / max,
                AppColors.For(priority)));
        }
    }

    private void FillStatuses(TaskStatistics stats)
    {
        StatusSegments.Clear();
        foreach (var (status, icon) in new[] { (TaskStatus.Todo, "circle"), (TaskStatus.InProgress, "half"), (TaskStatus.Blocked, "blocked") })
        {
            var count = stats.OpenByStatus.GetValueOrDefault(status);
            StatusSegments.Add(new StatusSegmentViewModel(DisplayNames.StatusName(status), icon, count, count, AppColors.For(status)));
        }
    }

    private void FillCompleted(TaskStatistics stats)
    {
        var days = stats.CompletedByDay;
        var max = days.Max(d => d.Count);
        var accent = ThemeColor("Accent");
        var line = ThemeColor("Line");
        var muted = ThemeColor("InkMuted");
        var weekly = days.Count <= 7;

        CompletedColumns.Clear();
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var label = weekly
                ? ShortWeekday(day.Day)
                : (i % 6 == 0 || i == days.Count - 1 ? day.Day.ToString("dd.MM", Ru) : string.Empty);
            CompletedColumns.Add(new ChartColumnViewModel(
                day.Count.ToString(Ru),
                label,
                BarHeight(day.Count, max),
                day.Count == 0 ? line : accent,
                muted));
        }

        var total = days.Sum(d => d.Count);
        if (total == 0)
        {
            CompletedHint = "За этот период нет выполненных задач";
            return;
        }

        var peak = days.MaxBy(d => d.Count)!;
        var peakText = weekly
            ? peak.Day.ToDateTime(TimeOnly.MinValue).ToString("dddd", Ru)
            : peak.Day.ToString("dd.MM", Ru);
        CompletedHint = $"Среднее: {((double)total / days.Count).ToString("0.0", Ru)} в день · пик: {peakText}";
    }

    private void FillDeadlines(TaskStatistics stats)
    {
        var days = stats.DeadlinesByDay;
        var max = Math.Max(stats.OverdueCount, days.Max(d => d.Count));
        var accent = ThemeColor("Accent");
        var line = ThemeColor("Line");
        var crit = ThemeColor("PCrit");
        var muted = ThemeColor("InkMuted");

        DeadlineColumns.Clear();
        DeadlineColumns.Add(new ChartColumnViewModel(
            stats.OverdueCount.ToString(Ru),
            "Проср.",
            BarHeight(stats.OverdueCount, max),
            stats.OverdueCount == 0 ? line : crit,
            stats.OverdueCount == 0 ? muted : crit));
        foreach (var day in days)
        {
            var date = day.Day.ToDateTime(TimeOnly.MinValue);
            DeadlineColumns.Add(new ChartColumnViewModel(
                day.Count.ToString(Ru),
                $"{ShortWeekday(day.Day)} {date:dd}",
                BarHeight(day.Count, max),
                day.Count == 0 ? line : accent,
                muted));
        }

        var upcoming = days.Sum(d => d.Count);
        DeadlinesHint = $"Всего: {upcoming + stats.OverdueCount} · из них просрочено {stats.OverdueCount}";
    }

    private void FillTags(TaskStatistics stats)
    {
        var max = stats.TopTags.Select(t => t.Count).DefaultIfEmpty(0).Max();
        Tags.Clear();
        foreach (var tag in stats.TopTags)
        {
            Tags.Add(new TagBarViewModel("#" + tag.Name, tag.Count, max == 0 ? 0 : (double)tag.Count / max));
        }
    }

    private static double BarHeight(int count, int max) =>
        max == 0 || count == 0 ? MinBarHeight : Math.Max(MinBarHeight, count * TrackHeight / max);

    private static string ShortWeekday(DateOnly day)
    {
        var name = Ru.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek).TrimEnd('.');
        return name.Length == 0 ? name : char.ToUpper(name[0], Ru) + name[1..];
    }

    private static Color ThemeColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Colors.Gray;

    private static string Plural(int n, string one, string few, string many)
    {
        var form = (n % 100 is >= 11 and <= 14) ? many : (n % 10) switch { 1 => one, >= 2 and <= 4 => few, _ => many };
        return $"{n} {form}";
    }

    /// <summary>Длительность в виде "2д 12ч", "5ч 30мин" или "15мин": две самые крупные единицы.</summary>
    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalDays >= 1)
        {
            return $"{(int)value.TotalDays}д {value.Hours}ч";
        }

        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours}ч {value.Minutes}мин";
        }

        return $"{Math.Max(1, (int)value.TotalMinutes)}мин";
    }
}
