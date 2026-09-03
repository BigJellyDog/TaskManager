using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Core.Abstractions;
using TaskManager.Infrastructure.Persistence;
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

        return services;
    }
}
