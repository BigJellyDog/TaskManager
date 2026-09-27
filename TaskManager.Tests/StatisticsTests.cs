using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Repositories;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Tests;

/// <summary>Статистика: окна «7 дней», «30 дней», «всё время» и график «выполнено по дням».</summary>
public sealed class StatisticsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now.AddDays(-20));

    public StatisticsTests()
    {
        // Время у FakeTimeProvider идёт только вперёд: 20 дней назад, 8 дней назад, вчера, час назад и сейчас.
        FinishAt(Now.AddDays(-20), "20 дней назад").GetAwaiter().GetResult();
        FinishAt(Now.AddDays(-8), "8 дней назад").GetAwaiter().GetResult();
        FinishAt(Now.AddDays(-1), "вчера").GetAwaiter().GetResult();
        FinishAt(Now.AddHours(-1), "сегодня 1").GetAwaiter().GetResult();
        FinishAt(Now, "сегодня 2").GetAwaiter().GetResult();
        _clock.SetUtcNow(Now);
    }

    public void Dispose() => _db.Dispose();

    private TaskService Tasks(TaskManagerDbContext context) =>
        new(new TaskRepository(context), new TagRepository(context), new UnitOfWork(context), _clock);

    private async Task FinishAt(DateTimeOffset moment, string title)
    {
        _clock.SetUtcNow(moment);
        await using var context = _db.CreateContext();
        var tasks = Tasks(context);
        var id = (await tasks.CreateAsync(new CreateTaskRequest(title))).Id;
        await tasks.ChangeStatusAsync(id, TaskStatus.InProgress);
        await tasks.ChangeStatusAsync(id, TaskStatus.Done);
    }

    private async Task<TaskStatistics> GetAsync(StatisticsPeriod period)
    {
        await using var context = _db.CreateContext();
        return await new StatisticsService(new StatisticsRepository(context), _clock).GetAsync(period);
    }

    [Fact]
    public async Task Week_HasSevenColumns_LastIsToday()
    {
        var stats = await GetAsync(StatisticsPeriod.Week);

        Assert.Equal(7, stats.CompletedByDay.Count);
        Assert.Equal(DateOnly.FromDateTime(Now.UtcDateTime), stats.CompletedByDay[^1].Day);
        Assert.Equal(2, stats.CompletedByDay[^1].Count);
        Assert.Equal(1, stats.CompletedByDay[^2].Count);
        Assert.Equal(3, stats.CompletedLastWeek);
        Assert.Equal(5, stats.CompletedLastMonth);
    }

    [Theory]
    [InlineData(StatisticsPeriod.Month)]
    [InlineData(StatisticsPeriod.AllTime)]
    public async Task MonthAndAllTime_ChartKeepsThirtyDays(StatisticsPeriod period)
    {
        var stats = await GetAsync(period);

        Assert.Equal(30, stats.CompletedByDay.Count);
        Assert.Equal(5, stats.CompletedByDay.Sum(d => d.Count));
        Assert.All(stats.CompletedByDay.Zip(stats.CompletedByDay.Skip(1)), p => Assert.Equal(p.First.Day.AddDays(1), p.Second.Day));
    }

    [Fact]
    public async Task Deadlines_CoverSevenDays()
    {
        var stats = await GetAsync(StatisticsPeriod.Week);

        Assert.Equal(7, stats.DeadlinesByDay.Count);
    }
}
