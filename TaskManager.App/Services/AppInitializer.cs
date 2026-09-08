using Microsoft.EntityFrameworkCore;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Reminders;

namespace TaskManager.App.Services;

/// <summary>Запуск и остановка приложения: схема базы данных и фоновый сервис напоминаний.</summary>
public sealed class AppInitializer
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ReminderService _reminders;
    private readonly ILogger<AppInitializer> _logger;

    public AppInitializer(IServiceScopeFactory scopes, ReminderService reminders, ILogger<AppInitializer> logger)
    {
        _scopes = scopes;
        _reminders = reminders;
        _logger = logger;
    }

    /// <summary>Применяет миграции (создаёт базу при первом запуске) и запускает напоминания.</summary>
    public async Task InitializeAsync()
    {
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>().Database;
            await database.MigrateAsync();
        }

        // Первая проверка напоминаний выполняется сразу при запуске сервиса.
        await _reminders.StartAsync(CancellationToken.None);
        _logger.LogInformation("Приложение запущено");
    }

    /// <summary>Останавливает фоновый сервис. Вызывается при закрытии окна, поэтому синхронно и с ограничением по времени.</summary>
    public void Shutdown()
    {
        try
        {
            using var timeout = new CancellationTokenSource(StopTimeout);
            _reminders.StopAsync(timeout.Token).GetAwaiter().GetResult();
            _logger.LogInformation("Приложение остановлено");
        }
#pragma warning disable CA1031 // При закрытии приложения любая ошибка остановки не должна мешать выходу.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Не удалось корректно остановить сервис напоминаний");
        }
    }
}
