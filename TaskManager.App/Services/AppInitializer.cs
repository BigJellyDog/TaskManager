using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Abstractions;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Reminders;

namespace TaskManager.App.Services;

/// <summary>Запуск и остановка приложения: схема базы данных и фоновый сервис напоминаний.</summary>
public sealed class AppInitializer(
    IServiceScopeFactory scopes,
    ReminderService reminders,
    IDatabaseBackup backup,
    ILogger<AppInitializer> logger)
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Сообщение для пользователя о том, что произошло при запуске (например, база восстановлена из копии).</summary>
    public string? StartupNotice { get; private set; }

    /// <summary>Применяет подготовленное восстановление, миграции (создаёт базу при первом запуске) и запускает напоминания.</summary>
    public async Task InitializeAsync()
    {
        // Восстановление из копии подменяет файл базы, поэтому оно выполняется первым, пока база ещё никем не открыта.
        StartupNotice = await backup.ApplyPendingRestoreAsync();

        await using (var scope = scopes.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>().Database;
            await database.MigrateAsync();
        }

        // Первая проверка напоминаний выполняется сразу при запуске сервиса.
        await reminders.StartAsync(CancellationToken.None);
        logger.LogInformation("Приложение запущено");

        // Первый запрос каждого вида медленный (EF строит и компилирует запрос). Прогреваем их в фоне, пока пользователь
        // смотрит на первый экран, чтобы первый заход на каждую вкладку не подтормаживал.
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
#pragma warning disable CA1031 // Прогрев необязателен: любая ошибка здесь не должна мешать работе.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogDebug(ex, "Прогрев запросов не удался");
        }
    }

    /// <summary>Останавливает фоновый сервис. Вызывается при закрытии окна, поэтому синхронно и с ограничением по времени.</summary>
    public void Shutdown()
    {
        try
        {
            using var timeout = new CancellationTokenSource(StopTimeout);
            reminders.StopAsync(timeout.Token).GetAwaiter().GetResult();
            logger.LogInformation("Приложение остановлено");
        }
#pragma warning disable CA1031 // При закрытии приложения любая ошибка остановки не должна мешать выходу.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Не удалось корректно остановить сервис напоминаний");
        }
    }
}
