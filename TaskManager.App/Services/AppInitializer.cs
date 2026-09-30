using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Reminders;

namespace TaskManager.App.Services;

/// <summary>Start-up and shutdown of the application: the database schema and the background reminder service.</summary>
public sealed class AppInitializer(
    IServiceScopeFactory scopes,
    ReminderService reminders,
    IDatabaseBackup backup,
    ILogger<AppInitializer> logger)
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    /// <summary>A message for the user about what happened at start-up (for example, the database was restored from a backup).</summary>
    public string? StartupNotice { get; private set; }

    /// <summary>Applies a staged restore and the migrations (creates the database on the first start) and starts the reminders.</summary>
    public async Task InitializeAsync()
    {
        // A restore from a backup replaces the database file, so it runs first, while nobody has opened the database yet.
        StartupNotice = await backup.ApplyPendingRestoreAsync();

        await using (var scope = scopes.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>().Database;
            await database.MigrateAsync();
        }

        // The first reminder check runs right when the service starts.
        await reminders.StartAsync(CancellationToken.None);
        logger.LogInformation("Приложение запущено");

        // The first query of each kind is slow (EF builds and compiles the query). We warm them up in the background while the user
        // looks at the first screen, so that the first visit to every tab does not lag.
        _ = Task.Run(WarmUpAsync);
    }

    private async Task WarmUpAsync()
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var tasks = sp.GetRequiredService<TaskManager.Core.Services.TaskService>();

            await tasks.SearchAsync(new TaskManager.Core.Querying.TaskFilter(), pageSize: 1);
            await tasks.CountAsync(new TaskManager.Core.Querying.TaskFilter(OnlyOverdue: true));
            await sp.GetRequiredService<TaskManager.Core.Services.StatisticsService>().GetAsync();
            await sp.GetRequiredService<TaskManager.Core.Services.TrashService>().ListAsync();
            await sp.GetRequiredService<TaskManager.Core.Services.TagService>().ListAsync();
        }
#pragma warning disable CA1031 // The warm-up is optional: no error here must get in the way.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogDebug(ex, "Прогрев запросов не удался");
        }
    }

    /// <summary>Stops the background service. Called when the window closes, so synchronous and with a time limit.</summary>
    public void Shutdown()
    {
        try
        {
            using var timeout = new CancellationTokenSource(StopTimeout);
            reminders.StopAsync(timeout.Token).GetAwaiter().GetResult();
            logger.LogInformation("Приложение остановлено");
        }
#pragma warning disable CA1031 // When the application closes, no error while stopping must get in the way of exiting.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Не удалось корректно остановить сервис напоминаний");
        }
    }
}
