using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Reminders;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Tests;

/// <summary>Фоновая проверка напоминаний: показ, повтор и режим «никогда» (<see cref="ReminderOptions.Enabled"/> выключен).</summary>
public sealed class ReminderServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly RecordingNotifier _notifier = new();
    private readonly ServiceProvider _provider;

    public ReminderServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IReminderNotifier>(_notifier);
        services.AddScoped(_ => _db.CreateContext());
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<TaskService>();
        services.AddScoped<ReminderChecker>();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    private ReminderService NewService(bool enabled) =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), _clock, Options.Create(new ReminderOptions { Enabled = enabled }), NullLogger<ReminderService>.Instance);

    private async Task CreateTaskDueInAsync(TimeSpan left)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TaskService>().CreateAsync(new CreateTaskRequest("срочная", Deadline: Now + left));
    }

    [Fact]
    public async Task RunOnce_ShowsUpcomingDeadline_AndDoesNotRepeatIt()
    {
        await CreateTaskDueInAsync(TimeSpan.FromMinutes(30));
        var service = NewService(enabled: true);

        await service.RunOnceAsync(CancellationToken.None);
        await service.RunOnceAsync(CancellationToken.None);

        var shown = Assert.Single(_notifier.Batches);
        Assert.Equal("срочная", Assert.Single(shown).Title);
    }

    [Fact]
    public async Task RunOnce_WhenReminderCheckIsOff_ShowsNothingAndRemembersNothing()
    {
        await CreateTaskDueInAsync(TimeSpan.FromMinutes(30));
        var off = NewService(enabled: false);

        await off.RunOnceAsync(CancellationToken.None);

        Assert.Empty(_notifier.Batches);

        // Включили обратно: напоминание не потеряно, оно показывается сейчас.
        await NewService(enabled: true).RunOnceAsync(CancellationToken.None);
        Assert.Single(_notifier.Batches);
    }

    private sealed class RecordingNotifier : IReminderNotifier
    {
        public List<IReadOnlyList<Reminder>> Batches { get; } = [];

        public Task NotifyAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default)
        {
            Batches.Add(reminders);
            return Task.CompletedTask;
        }
    }
}
