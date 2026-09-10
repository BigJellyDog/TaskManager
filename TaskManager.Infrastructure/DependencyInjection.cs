using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Export;
using TaskManager.Core.Services;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Reminders;
using TaskManager.Infrastructure.Repositories;

namespace TaskManager.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует DbContext, репозитории и IUnitOfWork. Все они scoped: на один scope (операцию) один DbContext,
    /// поэтому репозитории и UnitOfWork работают с одними и теми же отслеживаемыми объектами.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.AddDbContext<TaskManagerDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();

        // Сервисы приложения. TimeProvider и IReminderNotifier регистрируются через TryAdd:
        // приложение может заранее подставить свои (например, показ уведомлений в интерфейсе).
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IReminderNotifier, LoggingReminderNotifier>();
        services.AddScoped<TaskService>();
        services.AddScoped<ReminderChecker>();
        services.AddScoped<StatisticsService>();
        services.AddScoped<ExportService>();
        services.AddScoped<ImportService>();
        services.AddSingleton<IDatabaseBackup>(sp => new SqliteDatabaseBackup(
            databasePath,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<SqliteDatabaseBackup>>()));
        services.AddOptions<ReminderOptions>();
        services.AddSingleton<ReminderService>();

        return services;
    }
}
