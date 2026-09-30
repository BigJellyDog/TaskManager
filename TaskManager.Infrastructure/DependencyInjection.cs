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
    /// Registers the DbContext, the repositories and IUnitOfWork. All of them are scoped: one DbContext per scope (operation),
    /// so the repositories and UnitOfWork work with the same tracked objects.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.AddDbContext<TaskManagerDbContext>(options => options.UseSqlite($"Data Source={databasePath}").AddInterceptors(new SqliteTuningInterceptor()));
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<ITrashRepository, TrashRepository>();

        // Application services. TimeProvider and IReminderNotifier are registered with TryAdd:
        // the application can substitute its own in advance (for example, showing notifications in the interface).
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IReminderNotifier, LoggingReminderNotifier>();
        services.AddScoped<TaskService>();
        services.AddScoped<ReminderChecker>();
        services.AddScoped<StatisticsService>();
        services.AddScoped<TrashService>();
        services.AddScoped<TagService>();
        services.AddScoped<ExportService>();
        services.AddScoped<ImportService>();
        services.AddScoped<DemoDataService>();
        services.AddSingleton<IDatabaseBackup>(sp => new SqliteDatabaseBackup(
            databasePath,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<SqliteDatabaseBackup>>()));
        services.AddOptions<ReminderOptions>();
        services.AddSingleton<ReminderService>();

        return services;
    }
}
