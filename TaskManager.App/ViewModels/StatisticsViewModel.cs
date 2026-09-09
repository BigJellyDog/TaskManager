using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.App.Services;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;

namespace TaskManager.App.ViewModels;

/// <summary>Одна полоса гистограммы: приоритет, сколько открытых задач и ширина полосы.</summary>
public sealed record PriorityBarViewModel(string Title, int Count, double Width, Color Color);

/// <summary>Статистика по задачам (FR-11): карточки с цифрами и гистограмма открытых задач по приоритетам.</summary>
public sealed partial class StatisticsViewModel : ViewModelBase
{
    // Ширина самой длинной полосы гистограммы, остальные пропорциональны ей.
    private const double MaxBarWidth = 460;

    private readonly ServiceRunner _runner;

    public StatisticsViewModel(ServiceRunner runner, IDialogService dialogs, ILogger<StatisticsViewModel> logger)
        : base(dialogs, logger)
    {
        _runner = runner;
    }

    public ObservableCollection<PriorityBarViewModel> Bars { get; } = [];

    [ObservableProperty]
    private int completedLastWeek;

    [ObservableProperty]
    private int completedLastMonth;

    [ObservableProperty]
    private string onTimeText = "—";

    [ObservableProperty]
    private string onTimeNote = string.Empty;

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

    public bool HasOverdue => OverdueCount > 0;

    public Task LoadAsync() => RunAsync(LoadCoreAsync, "Не удалось загрузить статистику");

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadCoreAsync()
    {
        var stats = await _runner.RunAsync(sp => sp.GetRequiredService<StatisticsService>().GetAsync());

        CompletedLastWeek = stats.CompletedLastWeek;
        CompletedLastMonth = stats.CompletedLastMonth;
        OverdueCount = stats.OverdueCount;
        BlockedCount = stats.BlockedCount;
        OpenCount = stats.OpenCount;

        // Без выполненных задач с дедлайном процент не определён: показываем прочерк, а не 0% или 100%.
        OnTimeText = stats.OnTimePercent is { } percent ? percent.ToString("0.#", CultureInfo.CurrentCulture) + "%" : "—";
        OnTimeNote = stats.CompletedWithDeadline == 0
            ? "нет выполненных задач с дедлайном"
            : $"{stats.CompletedOnTime} из {stats.CompletedWithDeadline} выполненных с дедлайном";
        AverageText = stats.AverageCompletionTime is { } average ? FormatDuration(average) : "—";

        var max = stats.OpenByPriority.Values.DefaultIfEmpty(0).Max();
        Bars.Clear();
        foreach (var priority in Enum.GetValues<Priority>().Reverse())
        {
            var count = stats.OpenByPriority.GetValueOrDefault(priority);

            // Ненулевое значение всегда видно хотя бы тонкой полоской.
            var width = max == 0 || count == 0 ? 0 : Math.Max(6, count * MaxBarWidth / max);
            Bars.Add(new PriorityBarViewModel(DisplayNames.PriorityName(priority), count, width, AppColors.For(priority)));
        }

        IsEmpty = stats.OpenCount == 0 && stats.CompletedLastMonth == 0 && stats.AverageCompletionTime is null;
    }

    /// <summary>Длительность в виде "2 д 12 ч", "5 ч 30 мин" или "15 мин": две самые крупные единицы.</summary>
    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalDays >= 1)
        {
            return $"{(int)value.TotalDays} д {value.Hours} ч";
        }

        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours} ч {value.Minutes} мин";
        }

        return $"{Math.Max(1, (int)value.TotalMinutes)} мин";
    }
}
